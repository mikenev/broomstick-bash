#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;
namespace BroomstickBash {
  /// <summary>Dev-build-only QA harness (compiled out of release): P toggles a hover autopilot, K (in Werewolf) deals
  /// boss damage, and key events are logged with a [QA] prefix for headless smoke tests.</summary>
  public class DevQA : MonoBehaviour {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){ DontDestroyOnLoad(new GameObject("DevQA").AddComponent<DevQA>().gameObject); }
    bool auto; float minFireballDist = 999f; readonly System.Collections.Generic.HashSet<int> seen = new();
    void OnEnable(){
      GameEvents.PlayerHit += h => Debug.Log($"[QA] hit hearts={h} t={Time.time:0.00}");
      GameEvents.BossStarted += () => { minFireballDist = 999f; Debug.Log($"[QA] boss start t={Time.time:0.00} hazards={FindObjectsByType<Fireball>(FindObjectsSortMode.None).Length} obstacles={FindObjectsByType<ObstacleBase>(FindObjectsSortMode.None).Length}"); };
      GameEvents.BossPhaseChanged += p => Debug.Log($"[QA] boss phase {p}");
      GameEvents.BossDefeated += () => Debug.Log("[QA] boss defeated");
      GameEvents.Win += () => Debug.Log($"[QA] WIN minFireballSpawnDist={minFireballDist:0.00} fired={Werewolf.FiredPatterns} rejected={Werewolf.RejectedPatterns}");
      GameEvents.GameOver += () => Debug.Log($"[QA] GAME OVER minFireballSpawnDist={minFireballDist:0.00}");
    }
    void Update(){
      var gm = GameManager.I; if (!gm) return;
      if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame){ auto = !auto; Debug.Log($"[QA] autopilot {auto}"); }
      foreach (var f in FindObjectsByType<Fireball>(FindObjectsSortMode.None)){
        if (!seen.Add(f.GetInstanceID())) continue;
        minFireballDist = Mathf.Min(minFireballDist, Vector2.Distance(f.transform.position, gm.player.transform.position));
      }
      if (auto && gm.IsActive && gm.player.transform.position.y < 4.5f && gm.player.VelocityY < 0) gm.player.Flap();
    }
  }
}
#endif
