using UnityEngine;
namespace BroomstickBash {
  /// <summary>Tiny gray-box particle burst (cubes that fly out and shrink). Used for the kid sparkle and kill puffs.</summary>
  public class Puff : MonoBehaviour {
    Vector3 vel; float life, max; Vector3 s0;
    public static void Burst(Vector3 at, Material mat, int count = 8, float size = 0.18f, float speed = 4f){
      var cfg = GameConfig.I; if (!cfg) return;
      for (int i = 0; i < count; i++){
        var go = cfg.MakeBlock("Puff", cfg.cube, mat, at, Vector3.one * size);
        var p = go.AddComponent<Puff>();
        p.vel = Random.insideUnitSphere * speed; p.vel.z *= 0.3f; p.max = p.life = Random.Range(0.35f, 0.6f); p.s0 = go.transform.localScale;
      }
    }
    void Update(){
      life -= Time.deltaTime; if (life <= 0){ Destroy(gameObject); return; }
      transform.position += vel * Time.deltaTime; vel *= 0.92f;
      transform.Rotate(300 * Time.deltaTime, 200 * Time.deltaTime, 0);
      transform.localScale = s0 * (life / max);
    }
  }
}
