using System.Collections.Generic;
using UnityEngine;
namespace BroomstickBash {
  public enum AttackKind { Aimed, Arc, Wall }
  /// <summary>One planned fireball, in the witch's frame: offset from the witch at fire time and relative velocity.</summary>
  public struct Shot { public Vector2 pos; public Vector2 vel; public float gravity; public float delay;
    /// <summary>true = a fireball already in the air (moves from t=0, not after the telegraph). pos.y is absolute.</summary>
    public bool live; }

  /// <summary>Per-phase tuning (phase 0 = full HP ... 2 = low HP). Tuned for casual players.</summary>
  [System.Serializable] public class PhaseTuning {
    public float fireballSpeed = 4f;   // m/s relative to the witch
    public float telegraph = 1.4f;     // wind-up seconds before the attack fires
    public float rest = 1.6f;          // pause after an attack
    public float wallGap = 3.6f;       // vertical opening in a wall volley (m)
    public int aimedCount = 1; public float aimedSpreadDeg = 0f;
    public int arcCount = 2;
    public AttackKind[] attacks = { AttackKind.Aimed, AttackKind.Wall };
  }

  /// <summary>Builds attack patterns. Pure (no scene access) so the editor/test fairness check can use it.</summary>
  public static class FireballPatterns {
    public const float WallTop = 9.3f, WallBottom = 0.9f, WallSpacing = 1.05f;
    /// <param name="bossRel">boss hand position relative to the witch</param>
    public static List<Shot> Build(AttackKind k, PhaseTuning t, Vector2 bossRel, float witchY, float groundY, float ceilY, System.Random rng){
      var list = new List<Shot>();
      switch (k){
        case AttackKind.Aimed: {
          var dir = (-bossRel).normalized;
          for (int i = 0; i < t.aimedCount; i++){
            float a = (i - (t.aimedCount - 1) * 0.5f) * t.aimedSpreadDeg;
            list.Add(new Shot{ pos = bossRel, vel = (Vector2)(Quaternion.Euler(0, 0, a) * dir) * t.fireballSpeed, delay = i * 0.0f });
          }
          break;
        }
        case AttackKind.Arc: {
          for (int i = 0; i < t.arcCount; i++){
            float vy = 3.5f + (float)rng.NextDouble() * 3.5f;
            list.Add(new Shot{ pos = bossRel + Vector2.up * 0.5f, vel = new Vector2(-t.fireballSpeed * (0.9f + 0.25f * i), vy), gravity = 3.2f, delay = i * 0.45f });
          }
          break;
        }
        case AttackKind.Wall: {
          // gap centered somewhere reachable, biased a bit toward (but never forced onto) the witch's height
          float lo = groundY + 1.2f + t.wallGap * 0.5f, hi = ceilY - 0.8f - t.wallGap * 0.5f;
          float c = Mathf.Lerp(Mathf.Lerp(lo, hi, (float)rng.NextDouble()), Mathf.Clamp(witchY, lo, hi), 0.35f);
          for (float y = WallBottom; y <= WallTop + 0.01f; y += WallSpacing){
            if (Mathf.Abs(y - c) < t.wallGap * 0.5f) continue;
            list.Add(new Shot{ pos = new Vector2(bossRel.x, y - witchY), vel = new Vector2(-t.fireballSpeed, 0) });
          }
          break;
        }
      }
      return list;
    }
  }
}
