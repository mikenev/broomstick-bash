using UnityEngine;
namespace BroomstickBash {
  /// <summary>Side camera: follows the player's x with a lead, fixed height; slight vertical easing for 2.5D feel.</summary>
  public class SideCamera : MonoBehaviour {
    public Transform target; public float lead = 5f, height = 5f, distance = 17f, yFollow = 0.15f;
    void LateUpdate(){
      if (!target) return;
      float y = height + (target.position.y - height) * yFollow;
      transform.position = new Vector3(target.position.x + lead, y, -distance);
      transform.rotation = Quaternion.Euler(2f, 0, 0);
    }
  }
  /// <summary>Recycles background houses / street tiles around the camera.</summary>
  public class Scenery : MonoBehaviour {
    public Transform cam; public float span = 60f;
    void LateUpdate(){
      foreach (Transform c in transform){
        var p = c.position;
        if (p.x < cam.position.x - span * 0.5f) { p.x += span; c.position = p; }
        else if (p.x > cam.position.x + span * 0.5f) { p.x -= span; c.position = p; }
      }
    }
  }
}
