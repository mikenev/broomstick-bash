using System.Collections.Generic;
using UnityEngine;
namespace BroomstickBash {
  /// <summary>Witch flap physics, mirrored from PlayerController.</summary>
  public struct FlapModel {
    public float gravity, flapVelocity, maxFall, groundY, ceilingY, playerRadius;
    public static FlapModel From(PlayerController p, GameConfig c) => new FlapModel{
      gravity = p.gravity, flapVelocity = p.flapVelocity, maxFall = p.maxFall, groundY = c.groundY, ceilingY = c.ceilingY, playerRadius = 0.42f };
    public static FlapModel Default => new FlapModel{ gravity = 24f, flapVelocity = 8.2f, maxFall = -12f, groundY = 0f, ceilingY = 10f, playerRadius = 0.42f };
  }

  /// <summary>Fairness check: is there ANY flap-only input sequence (one decision every decisionStep seconds, i.e. a
  /// human-ish flap rate) that keeps the witch off the street and clear of every fireball, with a safety margin?
  /// Dynamic programming over discretized (y, vy) states; the witch's x is fixed (fireballs are simulated in her frame).</summary>
  public static class FairnessValidator {
    public const float FireballRadius = 0.3f;
    public static float Margin = 0.35f;          // extra clearance for casual players
    public static float DecisionStep = 0.12f;    // at most one flap per 0.12 s
    const float Dt = 1f / 30f, YBin = 0.1f, VBin = 0.5f;

    public static bool IsFair(List<Shot> shots, FlapModel m, float startY, float startVy, float telegraph){
      if (shots == null || shots.Count == 0) return true;
      float top = m.ceilingY - 0.4f, bottom = m.groundY + 0.45f;
      float hitR = FireballRadius + m.playerRadius + Margin;
      var states = new Dictionary<int, Vector2>(); states[Key(startY, startVy)] = new Vector2(startY, startVy);
      float t = 0, maxT = telegraph + 12f;
      int sub = Mathf.Max(1, Mathf.RoundToInt(DecisionStep / Dt));
      var next = new Dictionary<int, Vector2>();
      while (t < maxT){
        if (AllPassed(shots, t - telegraph)) return true;
        next.Clear();
        foreach (var s in states.Values){
          for (int a = 0; a < 2; a++){
            float y = s.x, vy = a == 1 ? m.flapVelocity : s.y; bool dead = false;
            for (int k = 0; k < sub && !dead; k++){
              vy = Mathf.Max(vy - m.gravity * Dt, m.maxFall); y += vy * Dt;
              if (y > top){ y = top; vy = Mathf.Min(vy, 0); }
              if (y < bottom){ dead = true; break; }
              if (Collides(shots, t + (k + 1) * Dt - telegraph, y, hitR)) dead = true;
            }
            if (!dead) next[Key(y, vy)] = new Vector2(y, vy);
          }
        }
        if (next.Count == 0) return false;
        (states, next) = (next, states);
        t += sub * Dt;
      }
      return true;
    }
    static int Key(float y, float vy) => Mathf.RoundToInt(y / YBin) * 1000 + Mathf.RoundToInt(vy / VBin) + 500;
    static Vector2 PosAt(Shot s, float t){
      float tt = t - s.delay; if (tt < 0) return new Vector2(9999, 0);
      return s.pos + s.vel * tt + 0.5f * s.gravity * tt * tt * Vector2.down;
    }
    // witch-frame: the witch sits at x = 0; y here is absolute, shot y is relative to witch's start → stored absolute by caller
    static bool Collides(List<Shot> shots, float t, float y, float r){
      if (t < 0) return false;
      foreach (var s in shots){ var p = PosAt(s, t); float dx = p.x, dy = p.y - y; if (dx * dx + dy * dy < r * r) return true; }
      return false;
    }
    static bool AllPassed(List<Shot> shots, float t){
      if (t < 0) return false;
      foreach (var s in shots){ var p = PosAt(s, t); if (p.x > -1.5f && p.y > -1f) return false; }
      return true;
    }
    /// <summary>Converts shots built relative to the witch (pos.y relative) into absolute-y shots for IsFair.</summary>
    public static List<Shot> ToAbsolute(List<Shot> rel, float witchY){
      var o = new List<Shot>(rel.Count);
      foreach (var s in rel){ var c = s; c.pos.y += witchY; o.Add(c); }
      return o;
    }

    /// <summary>Test-time sweep used by the editor build (BossFairnessTest): every phase x attack x start height x seed.
    /// Returns (checked, unfair) and logs failures.</summary>
    public static (int total, int unfair) Sweep(PhaseTuning[] phases, FlapModel m, int seeds = 12){
      int total = 0, bad = 0;
      for (int p = 0; p < phases.Length; p++)
        foreach (AttackKind k in System.Enum.GetValues(typeof(AttackKind)))
          for (float y0 = 1.5f; y0 <= 9f; y0 += 1.5f)
            for (int s = 0; s < seeds; s++){
              var rng = new System.Random(s * 7919 + p * 31 + (int)k);
              float bossY = 2.5f + (float)rng.NextDouble() * 5f;
              var rel = FireballPatterns.Build(k, phases[p], new Vector2(12.5f - 1.6f, bossY + 0.4f - y0), y0, m.groundY, m.ceilingY, rng);
              total++;
              if (!IsFair(ToAbsolute(rel, y0), m, y0, 0f, phases[p].telegraph)){ bad++; Debug.LogWarning($"[Fairness] UNFAIR phase {p} {k} y0={y0} seed={s}"); }
            }
      return (total, bad);
    }
  }
}
