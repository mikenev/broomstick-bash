using UnityEngine;
using BroomstickBash.Art;
namespace BroomstickBash {
  /// <summary>4-layer parallax backdrop from the art prefabs (Assets/Art/README.md):
  /// BG_HousesFar z=28, BG_HousesNear z=12, BG_Pumpkins z=3 each tile every BroomArt.LayerWidth (40 m) with 3 copies that wrap
  /// around the camera; BG_Moon z=60 is placed once and follows the camera at 0.95 (never tiles). Layer x = cam.x * follow,
  /// so follow 1 = glued to the camera (infinitely far), 0 = fixed to the street. If the art is missing, the M1 gray-box
  /// houses/moon stay. When art is present, the gray-box "Houses" scenery and the camera's gray-box moon are disabled.</summary>
  public class ParallaxBackground : MonoBehaviour {
    public Transform cam;
    public GameObject grayHouses, grayMoon;
    struct Layer { public Transform[] tiles; public float follow; }
    Layer[] layers; Transform moon; Vector3 moonOffset = new(14f, 8f, 60f);
    const float MoonFollow = 0.95f;
    void Start(){
      if (!cam && Camera.main) cam = Camera.main.transform;
      if (!BroomArt.Load("BG_HousesFar")) return; // keep gray-box
      if (grayHouses) grayHouses.SetActive(false);
      if (grayMoon) grayMoon.SetActive(false);
      layers = new[]{ Make("BG_HousesFar", 28f, 0.6f), Make("BG_HousesNear", 12f, 0.35f), Make("BG_Pumpkins", 3f, 0f) };
      var m = BroomArt.Load("BG_Moon");
      if (m){ moon = Instantiate(m, transform, false).transform; moon.name = "Art_BG_Moon"; }
      LateUpdate();
    }
    Layer Make(string name, float z, float follow){
      var p = BroomArt.Load(name); var l = new Layer{ follow = follow, tiles = new Transform[p ? 3 : 0] };
      for (int i = 0; i < l.tiles.Length; i++){
        var t = Instantiate(p, transform, false).transform; t.name = $"Art_{name}_{i}";
        t.position = new Vector3(i * BroomArt.LayerWidth, 0, z); l.tiles[i] = t;
      }
      return l;
    }
    void LateUpdate(){
      if (layers == null || !cam) return;
      float cx = cam.position.x, W = BroomArt.LayerWidth;
      foreach (var l in layers){
        float origin = cx * l.follow;            // where the layer's "zero" sits this frame
        float rel = cx - origin;                  // camera position in layer space
        float baseTile = Mathf.Floor(rel / W) - 1; // 3 tiles: one behind, one under, one ahead of the camera
        for (int i = 0; i < l.tiles.Length; i++){
          var p = l.tiles[i].position; p.x = origin + (baseTile + i) * W; l.tiles[i].position = p;
        }
      }
      if (moon) moon.position = new Vector3(cx * MoonFollow + moonOffset.x, moonOffset.y, moonOffset.z);
    }
  }
}
