using UnityEngine;
namespace BroomstickBash {
  /// <summary>Recycles background houses / street tiles around the camera.</summary>
  public class Scenery : MonoBehaviour {
    public Transform cam; public float span = 60f;
    void LateUpdate(){
      if (!cam && Camera.main) cam = Camera.main.transform; if (!cam) return;
      foreach (Transform c in transform){
        var p = c.position;
        if (p.x < cam.position.x - span * 0.5f) { p.x += span; c.position = p; }
        else if (p.x > cam.position.x + span * 0.5f) { p.x -= span; c.position = p; }
      }
    }
  }
}
