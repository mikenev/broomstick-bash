using UnityEngine;
namespace BroomstickBash.Art {
  /// <summary>Loader for the Broomstick Bash art prefabs in Assets/Art/Resources/Art (built by BroomArtSetup).
  /// Conventions: 1 unit = 1 m, characters face +X (flight direction) with their front toward a side camera at -Z, origin at
  /// the feet/ground (witch: broom handle; branch: top attachment point; FX: centre). Animated prefabs carry an Animator on the
  /// "Model" child with trigger names equal to state names:
  ///   Witch: Fly (loop) + Flap, Cast, Hurt    Werewolf: Idle (loop) + Throw, Hurt, Defeat (terminal)
  ///   Kid_*: Walk (loop) + Cheer              Bat: Flap (loop)    Ghost: Float (loop)
  /// Background layers (BG_Moon, BG_HousesFar, BG_HousesNear, BG_Pumpkins) each tile every <see cref="LayerWidth"/> m along X.</summary>
  public static class BroomArt {
    public const float LayerWidth = 40f;
    public static bool Enabled = true;
    public static GameObject Load(string name) => Enabled ? Resources.Load<GameObject>("Art/BA_" + name) : null;
    /// <summary>Instantiates art under <paramref name="parent"/>, strips any colliders (gameplay keeps its own), optionally hides
    /// the parent's existing gray-box renderers. Returns null if the prefab is missing so callers can keep the placeholder.</summary>
    public static GameObject Attach(string name, Transform parent, bool hidePlaceholder = true){
      var p = Load(name); if (!p || !parent) return null;
      if (hidePlaceholder) foreach (var r in parent.GetComponentsInChildren<Renderer>()) r.enabled = false;
      var g = Object.Instantiate(p, parent, false); g.name = "Art_" + name;
      foreach (var c in g.GetComponentsInChildren<Collider>()){ if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
      return g;
    }
    public static void Trigger(GameObject art, string state){ var a = art ? art.GetComponentInChildren<Animator>() : null; if (a) a.SetTrigger(state); }
  }
}
