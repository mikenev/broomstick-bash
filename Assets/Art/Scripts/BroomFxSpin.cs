using UnityEngine;
/// <summary>Cheap procedural motion for FX prefabs (spin + pulse) so effects need no particle systems or textures.</summary>
public class BroomFxSpin : MonoBehaviour {
  public Vector3 spinDegPerSec = new(0, 0, 360f);
  public float pulse = 0.15f, pulseHz = 6f;
  Vector3 baseScale; float t0;
  void Awake(){ baseScale = transform.localScale; t0 = Random.value * 10f; }
  void Update(){
    transform.GetChild(0).Rotate(spinDegPerSec * Time.deltaTime, Space.World);
    transform.localScale = baseScale * (1f + pulse * Mathf.Sin((Time.time + t0) * pulseHz * 6.2832f));
  }
}
