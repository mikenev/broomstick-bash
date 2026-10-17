using System.Collections.Generic;
using UnityEngine;
namespace BroomstickBash {
  /// <summary>One obstacle in a planned column. All geometry is decided here (not randomised in the factory) so the
  /// fairness validator checks exactly what gets spawned.</summary>
  public struct PlannedItem {
    public ObstacleKind kind;
    public float y;        // ground kinds: base (groundY); ceiling: top; flyers: centre/base of motion
    public float size;     // kid height, lamp-post height, branch length (unused for others)
    public float amp;      // flyer motion amplitude (bat swoop depth, ghost sine amplitude)
    public float speed;    // extra leftward speed (m/s)
    public float dx;       // x offset inside the column (kid groups)
    /// <summary>Vertical band this item can ever occupy (world y), incl. motion, rotation and bob.</summary>
    public Vector2 Envelope(float groundY, float ceilingY){
      switch (kind){
        case ObstacleKind.Kid: return new Vector2(groundY, groundY + size + 0.15f);
        case ObstacleKind.Car: return new Vector2(groundY, groundY + LevelPlanner.CarHeight);
        case ObstacleKind.LampPost: return new Vector2(groundY, groundY + size + LevelPlanner.LampHeadR);
        case ObstacleKind.Branch: return new Vector2(ceilingY - size - 0.3f, ceilingY + 0.1f); // tilted top pokes above the ceiling (irrelevant to the witch)
        case ObstacleKind.Bat: return new Vector2(y - amp - LevelPlanner.BatHalf, y + LevelPlanner.BatHalf);
        case ObstacleKind.Ghost: return new Vector2(y - amp - LevelPlanner.GhostHalf, y + amp + LevelPlanner.GhostHalf);
      }
      return new Vector2(y - 1, y + 1);
    }
  }
  public class PlannedColumn {
    public List<PlannedItem> items = new();
    public float difficulty, scroll, interval;
    public Vector2 safe;          // chosen flappable window for the witch's centre (min, max)
    public string template;
  }

  /// <summary>Multi-spawn pattern planner with a fairness guarantee. Each column (a group spawned at one x) must leave a
  /// flappable window for the witch's centre of at least MinWindow(d), and that window must be reachable from the
  /// previous column's window in the time between them (climb/fall rates are conservative). Patterns that fail are re-rolled;
  /// after MaxTries a single always-safe item is used. BuildWeb.ValidatePatterns re-checks every column independently.</summary>
  public class LevelPlanner {
    // geometry shared with ObstacleFactory (keep in sync)
    public const float CarHeight = 1.95f, LampHeadR = 0.4f, BatHalf = 0.42f, GhostHalf = 0.62f;
    // witch: sphere r 0.42 centred +0.1 -> occupies [y-0.32, y+0.52]; centre clamped to [ground+0.45, ceiling-0.4]
    public const float PlayerBelow = 0.32f, PlayerAbove = 0.52f, PlayerMinY = 0.45f, PlayerMaxOff = 0.4f;
    public const float ClimbRate = 4f, FallRate = 6f;   // m/s the witch can surely move (flap spam ~8, free fall up to 12)
    public const float SpawnAhead = 15f, PassMargin = 3f;
    public const int MaxTries = 24;
    public static float MinWindow(float d) => Mathf.Lerp(2.6f, 1.8f, d); // a single flap arc is ~1.4 m tall

    public float groundY = 0, ceilingY = 10;
    public System.Random rng;
    Vector2 prevSafe; float prevMinSpeed; bool hasPrev;
    public LevelPlanner(System.Random r, float ground, float ceiling){ rng = r; groundY = ground; ceilingY = ceiling; Reset(); }
    public void Reset(){ hasPrev = false; prevSafe = new Vector2(PlayerMinY, ceilingY - PlayerMaxOff); prevMinSpeed = 0; }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    /// <summary>Plan the next column. <paramref name="dx"/> = metres since the previous column; <paramref name="scroll"/> = current scroll speed.</summary>
    public PlannedColumn Next(float d, float scroll, float dx, bool allowGhost = true){
      PlannedColumn best = null;
      for (int i = 0; i < MaxTries && best == null; i++){
        var c = Roll(d, allowGhost);
        if (Check(c, d, scroll, dx, out var w)){ c.safe = w; best = c; }
      }
      if (best == null){ // always-safe fallback: one small kid (leaves most of the sky)
        best = new PlannedColumn{ template = "fallback" };
        best.items.Add(new PlannedItem{ kind = ObstacleKind.Kid, y = groundY, size = 1.0f, speed = 0.5f });
        Check(best, d, scroll, dx, out var w); best.safe = w;
      }
      best.difficulty = d; best.scroll = scroll;
      prevSafe = best.safe; hasPrev = true; prevMinSpeed = MinSpeed(best);
      return best;
    }

    /// <summary>Computes the largest flappable window that is also reachable from the previous one. Public for the validator.</summary>
    public bool Check(PlannedColumn c, float d, float scroll, float dx, out Vector2 window){
      window = Vector2.zero;
      var wins = Windows(c);
      // drift: a faster column closes on the slower previous one before both pass the witch
      float tPass = (SpawnAhead + PassMargin) / Mathf.Max(1f, scroll);
      // conservative: assume the previous column may be standing still relative to the street
      float dxEff = Mathf.Max(0.5f, dx - MaxSpeed(c) * tPass);
      float dt = dxEff / Mathf.Max(1f, scroll + MaxSpeed(c));
      Vector2 reach = hasPrev ? new Vector2(prevSafe.x - FallRate * dt, prevSafe.y + ClimbRate * dt) : new Vector2(-99, 99);
      float bestLen = -1;
      foreach (var w in wins){
        if (w.y - w.x < MinWindow(d) + 0.03f) continue;     // small margin over the validator's sampling
        if (w.y < reach.x || w.x > reach.y) continue;                 // not reachable in time
        if (w.y - w.x > bestLen){ bestLen = w.y - w.x; window = w; }
      }
      return bestLen >= 0;
    }

    /// <summary>Free intervals for the witch's centre in this column.</summary>
    public List<Vector2> Windows(PlannedColumn c){
      var occ = new List<Vector2>();
      foreach (var it in c.items){ var e = it.Envelope(groundY, ceilingY); occ.Add(new Vector2(e.x - PlayerAbove, e.y + PlayerBelow)); }
      occ.Sort((a, b) => a.x.CompareTo(b.x));
      var res = new List<Vector2>(); float lo = groundY + PlayerMinY, hi = ceilingY - PlayerMaxOff, cur = lo;
      foreach (var o in occ){ if (o.x > cur) res.Add(new Vector2(cur, Mathf.Min(o.x, hi))); cur = Mathf.Max(cur, o.y); }
      if (cur < hi) res.Add(new Vector2(cur, hi));
      res.RemoveAll(w => w.y <= w.x);
      return res;
    }
    static float MaxSpeed(PlannedColumn c){ float m = 0; foreach (var i in c.items) m = Mathf.Max(m, i.speed); return m; }
    static float MinSpeed(PlannedColumn c){ float m = 99; foreach (var i in c.items) m = Mathf.Min(m, i.speed); return c.items.Count > 0 ? m : 0; }

    // ---- pattern templates ----
    PlannedItem Ground(float d){
      double r = rng.NextDouble();
      if (r < 0.45) return Kid(d, 0);
      if (r < 0.75) return new PlannedItem{ kind = ObstacleKind.Car, y = groundY, speed = R(1.2f, 1.5f + 1.5f * d) };
      return new PlannedItem{ kind = ObstacleKind.LampPost, y = groundY, size = R(3.2f, 4.2f + 1.6f * d) };
    }
    PlannedItem Kid(float d, float dx) => new PlannedItem{ kind = ObstacleKind.Kid, y = groundY, size = R(0.9f, 1.8f), speed = R(0.3f, 1.0f), dx = dx };
    PlannedItem Ceiling(float d) => new PlannedItem{ kind = ObstacleKind.Branch, y = ceilingY, size = R(2.2f, 3.0f + 1.4f * d) };
    PlannedItem Flyer(float d, bool allowGhost, float lo, float hi){
      if (allowGhost && d >= 0.15f && rng.NextDouble() < 0.4)
        return new PlannedItem{ kind = ObstacleKind.Ghost, y = R(lo + 1.2f, hi - 1.2f), amp = R(0.6f, 0.8f + 0.6f * d), speed = 1.2f };
      return new PlannedItem{ kind = ObstacleKind.Bat, y = R(lo + 1.6f, hi), amp = R(0.8f, 1.2f + 0.8f * d), speed = R(2.0f, 2.5f + 1.0f * d) };
    }
    PlannedColumn Roll(float d, bool allowGhost){
      var c = new PlannedColumn();
      // template weights shift with difficulty: singles early, doubles mid, triples/groups late
      float w1 = Mathf.Lerp(1f, 0.25f, d), w2 = d < 0.2f ? 0 : Mathf.Lerp(0.3f, 0.9f, d), w3 = d < 0.55f ? 0 : Mathf.Lerp(0.2f, 0.7f, d);
      float r = R(0, w1 + w2 + w3);
      float top = ceilingY - 0.5f, bot = groundY + 1f;
      if (r < w1){
        int k = rng.Next(3); c.template = "single";
        c.items.Add(k == 0 ? Ground(d) : k == 1 ? Ceiling(d) : Flyer(d, allowGhost, 3f, 8f));
      } else if (r < w1 + w2){
        int k = rng.Next(4);
        if (k == 0){ c.template = "ground+ceiling"; c.items.Add(Ground(d)); c.items.Add(Ceiling(d)); }
        else if (k == 1){ c.template = "ground+flyer"; c.items.Add(Ground(d)); c.items.Add(Flyer(d, allowGhost, 5.5f, top)); }
        else if (k == 2){ c.template = "ceiling+flyer"; c.items.Add(Ceiling(d)); c.items.Add(Flyer(d, allowGhost, bot + 0.5f, 4.5f)); }
        else { c.template = "kid-group"; c.items.Add(Kid(d, -1.3f)); c.items.Add(Kid(d, 0)); if (d > 0.5f) c.items.Add(Kid(d, 1.3f)); }
      } else {
        int k = rng.Next(2);
        if (k == 0){ c.template = "ground+flyer+ceiling"; c.items.Add(Ground(d)); c.items.Add(Ceiling(d)); c.items.Add(Flyer(d, allowGhost, 3.5f, 7f)); }
        else { c.template = "bat-flock"; float y0 = R(4f, 6f); c.items.Add(new PlannedItem{ kind = ObstacleKind.Bat, y = y0 + 3.2f, amp = 0.8f, speed = 2.6f });
               c.items.Add(new PlannedItem{ kind = ObstacleKind.Bat, y = y0 - 1.6f, amp = 0.8f, speed = 2.6f, dx = 1.2f }); }
      }
      return c;
    }
  }
}
