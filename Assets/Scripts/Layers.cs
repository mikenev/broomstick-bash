using UnityEngine;
namespace BroomstickBash {
  /// <summary>Layer indices (must match ProjectSettings/TagManager.asset).</summary>
  public static class Layers {
    public const int Player = 8, Enemy = 9, Kid = 10, Obstacle = 11, Spell = 12, Hazard = 13;
    public static void Validate(){
      void Check(string n, int i){ if (LayerMask.NameToLayer(n) != i) Debug.LogError($"Layer '{n}' expected at {i}, found {LayerMask.NameToLayer(n)} (TagManager mismatch)"); }
      Check("Player", Player); Check("Enemy", Enemy); Check("Kid", Kid); Check("Obstacle", Obstacle); Check("Spell", Spell); Check("Hazard", Hazard);
    }
  }
  /// <summary>Global gameplay events, for M2 audio/VFX/UI to subscribe to.</summary>
  public static class GameEvents {
    public static System.Action<int> PlayerHit;        // hearts left
    public static System.Action<ObstacleBase> EnemyKilled;
    public static System.Action BossStarted, BossDefeated, GameOver, RunReset;
    public static void Clear(){ PlayerHit = null; EnemyKilled = null; BossStarted = BossDefeated = GameOver = RunReset = null; }
  }
}
