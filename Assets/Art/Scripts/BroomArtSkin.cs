using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BroomstickBash.Art {
  /// <summary>Optional, code-free art hookup for the M1 gray-box objects. Each frame it looks at the scene's root objects
  /// (spawned obstacles are roots) and, for known names, hides the gray-box renderers and attaches the matching art prefab,
  /// fitted to the gray-box size. Colliders, layers, transforms and gameplay scripts are not touched.
  /// Matched names: Kid, Car, Branch, LampPost, Ghost, Bat, Fireball, Boss. This is the ONLY obstacle art path (SpawnEntry.prefab is unused). Disable with BroomArtSkin.Enabled = false before scene load.
  /// The player (Witch) and background are not auto-skinned: PlayerController re-enables its cached renderers while
  /// flashing, and the parallax layers need a scroller - both should be wired in M1 code (see Assets/Art/README.md).</summary>
  public class BroomArtSkin : MonoBehaviour {
    public static bool Enabled = true;
    static readonly string[] Kids = { "Kid_Pumpkin", "Kid_SheetGhost", "Kid_Robot" };
    readonly HashSet<int> done = new(); readonly List<GameObject> roots = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot(){ if (Enabled && BroomArt.Load("Witch")) { var g = new GameObject("BroomArtSkin"); g.AddComponent<BroomArtSkin>(); DontDestroyOnLoad(g); } }
    void LateUpdate(){
      roots.Clear(); SceneManager.GetActiveScene().GetRootGameObjects(roots);
      foreach (var r in roots){
        if (done.Contains(r.GetInstanceID())) continue;
        if (Skin(r)) done.Add(r.GetInstanceID());
      }
      if (done.Count > 4000) done.Clear();
    }
    /// <summary>Fits art to a gray-box: <paramref name="height"/> world metres tall, art origin placed at local offset.</summary>
    static GameObject Fit(GameObject box, string art, float artScale, Vector3 localOffset, bool keepRotation = true){
      var g = BroomArt.Attach(art, box.transform); if (!g) return null;
      var s = box.transform.lossyScale;
      g.transform.localScale = new Vector3(artScale / s.x, artScale / s.y, artScale / s.z);
      g.transform.localPosition = localOffset;
      if (!keepRotation) g.transform.rotation = Quaternion.identity;
      return g;
    }
    static bool Skin(GameObject r){
      switch (r.name){
        case "Kid": { float h = r.transform.lossyScale.y * 2f; // capsule mesh is 2 units tall
          Fit(r, Kids[Random.Range(0, Kids.Length)], h / 1.3f, new Vector3(0, -1f, 0), false); return true; }
        case "Car": { var g = Fit(r, Random.value < 0.5f ? "Car_Red" : "Car_Teal", 0.8f, new Vector3(0, -0.5f, 0)); return true; }
        case "Branch": { float len = r.transform.lossyScale.y; Fit(r, "Branch", len / 3.05f, new Vector3(0, 0.5f, 0)); return true; }
        case "LampPost": { var pole = r.transform.Find("Pole"); float h = pole ? pole.localScale.y * 2f : 4f; // pole height (root scale 1, origin at ground)
          Fit(r, "LampPost", h / 4f, Vector3.zero); return true; }
        case "Ghost": Fit(r, "Ghost", 0.9f, Vector3.zero, false); return true;
        case "Bat": Fit(r, "Bat", 1.6f, Vector3.zero, false); return true;
        case "Fireball": Fit(r, "FX_Fireball", 0.9f, Vector3.zero, false); return true;
        case "Boss": Fit(r, "Werewolf", 1.0f, new Vector3(0, -0.55f, 0), false); return true;
        case "BroomArtSkin": return true;
      }
      return r.name.Length > 0; // anything else: mark seen (re-checked only if renamed/re-created)
    }
  }
}
