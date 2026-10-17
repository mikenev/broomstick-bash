using System.Collections.Generic;
using UnityEngine;
namespace BroomstickBash {
  /// <summary>Spawns entries from the SpawnTable ahead of the camera and despawns them behind it.</summary>
  public class Spawner : MonoBehaviour, IDifficultySource {
    public SpawnTable table;
    public Transform cam, player;
    public float spawnAhead = 15f, despawnBehind = 14f;
    public System.Func<float> difficultyOverride;   // hook: custom curve / QA
    public bool spawning = true;
    readonly List<ObstacleBase> live = new();
    float timer;
    public float Difficulty => difficultyOverride != null ? difficultyOverride() :
      Mathf.Clamp01((GameManager.I ? GameManager.I.Distance : 0) / Mathf.Max(1, table.rampDistance));
    public float SpeedMultiplier => table.speedByDifficulty.Evaluate(Difficulty);

    public void ResetSpawner(){ ClearAll(); timer = 1.5f; spawning = true; }
    public void ClearAll(bool puff = false){
      foreach (var o in live) if (o){ if (puff) Puff.Burst(o.transform.position, GameConfig.I.sparkleMat, 4); o.Despawn(); }
      live.Clear();
    }
    void Update(){
      var gm = GameManager.I; if (!gm || !gm.IsActive) return;
      live.RemoveAll(o => !o);
      for (int i = live.Count - 1; i >= 0; i--) if (live[i].transform.position.x < cam.position.x - despawnBehind){ live[i].Despawn(); live.RemoveAt(i); }
      if (!spawning || gm.State != GameState.Playing) return;
      timer -= Time.deltaTime;
      if (timer <= 0){ SpawnOne(); timer = table.intervalByDifficulty.Evaluate(Difficulty) * Random.Range(0.85f, 1.15f); }
    }
    public ObstacleBase SpawnOne(){
      var e = table.Pick(Difficulty); if (e == null) return null;
      float x = cam.position.x + spawnAhead;
      var o = ObstacleFactory.Create(e, x, GameConfig.I);
      o.OnSpawned(new SpawnContext{ difficulty = Difficulty, spawnX = x, y = o.transform.position.y, player = player });
      live.Add(o); return o;
    }
  }
}
