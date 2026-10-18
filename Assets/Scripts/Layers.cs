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
    public static System.Action BossStarted, BossDefeated, GameOver, RunReset, BossFired;
    /// <summary>M2: fired once when the run is won (boss defeated), with the final score. GameOver stays loss-only.</summary>
    public static System.Action<int> Won;
    /// <summary>M2: fired once at the end of every run: (won, finalScore). Use this for end-of-run audio/UI (M6).</summary>
    public static System.Action<bool, int> RunEnded;
    public static System.Action<AttackKind> BossTelegraph;   // M3: wind-up began (audio/VFX hook)
    public static System.Action<int> BossHurt, BossPhaseChanged;   // M3
    public static System.Action BossImmune;   // spell hit the boss during its entry grace (fizzle)
    /// <summary>Audio hooks: spell cast / spell damaged an enemy or the boss / spell passed through a kid (sparkle) /
    /// spell blocked by an obstacle / spell destroyed a fireball. ObstacleSpawned fires from ObstacleBase.OnSpawned.</summary>
    public static System.Action SpellCast, SpellHit, SpellSparkle, SpellBlocked, FireballZapped;
    public static System.Action<ObstacleBase> ObstacleSpawned;
    public static void Clear(){ PlayerHit = null; EnemyKilled = null; BossStarted = BossDefeated = GameOver = RunReset = BossFired = null; Won = null; RunEnded = null; BossTelegraph = null; BossHurt = BossPhaseChanged = null; BossImmune = null; SpellCast = SpellHit = SpellSparkle = SpellBlocked = FireballZapped = null; ObstacleSpawned = null; }
  }
}
