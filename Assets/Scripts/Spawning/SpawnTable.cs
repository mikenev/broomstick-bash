using System.Collections.Generic;
using UnityEngine;
namespace BroomstickBash {
  public enum ObstacleKind { Kid, Car, Branch, Bat, Ghost }
  [System.Serializable] public class SpawnEntry {
    public ObstacleKind kind;
    [Tooltip("Optional prefab (M2 art). If null, ObstacleFactory builds a gray-box version.")] public GameObject prefab;
    public float weight = 1f;
    [Range(0, 1)] public float minDifficulty = 0f;
    public float minY = 0f, maxY = 0f;   // used by flyers; ground/ceiling kinds ignore
  }
  /// <summary>Data-driven spawn table + difficulty ramp. M2: tune weights/curves or add entries in the asset.</summary>
  [CreateAssetMenu(menuName = "Broomstick Bash/Spawn Table")]
  public class SpawnTable : ScriptableObject {
    public List<SpawnEntry> entries = new();
    [Tooltip("Seconds between spawns vs difficulty 0..1")] public AnimationCurve intervalByDifficulty = AnimationCurve.Linear(0, 1.8f, 1, 0.9f);
    [Tooltip("Scroll speed multiplier vs difficulty 0..1")] public AnimationCurve speedByDifficulty = AnimationCurve.Linear(0, 1f, 1, 1.4f);
    [Tooltip("Distance (m) to reach difficulty 1")] public float rampDistance = 600f;
    [Tooltip("Distance (m) at which the boss appears")] public float bossDistance = 450f;

    public SpawnEntry Pick(float difficulty){
      float total = 0; foreach (var e in entries) if (difficulty >= e.minDifficulty) total += e.weight;
      if (total <= 0) return null;
      float r = Random.value * total;
      foreach (var e in entries){ if (difficulty < e.minDifficulty) continue; r -= e.weight; if (r <= 0) return e; }
      return entries[entries.Count - 1];
    }
    public static SpawnTable CreateDefault(){
      var t = CreateInstance<SpawnTable>();
      t.entries.Add(new SpawnEntry{ kind = ObstacleKind.Kid, weight = 3 });
      t.entries.Add(new SpawnEntry{ kind = ObstacleKind.Car, weight = 2 });
      t.entries.Add(new SpawnEntry{ kind = ObstacleKind.Branch, weight = 2 });
      t.entries.Add(new SpawnEntry{ kind = ObstacleKind.Bat, weight = 2.5f, minY = 3.5f, maxY = 7.5f });
      t.entries.Add(new SpawnEntry{ kind = ObstacleKind.Ghost, weight = 1.5f, minDifficulty = 0.15f, minY = 3f, maxY = 8f });
      return t;
    }
  }
  /// <summary>Difficulty hook: 0..1. Override via Spawner.difficultyOverride for testing or a custom curve.</summary>
  public interface IDifficultySource { float Difficulty { get; } }
}
