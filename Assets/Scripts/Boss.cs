using UnityEngine;
namespace BroomstickBash {
  /// <summary>Rough boss prototype: a block that hovers at the right of the screen and throws fireball volleys. ~12 spell hits.</summary>
  public class Boss : MonoBehaviour {
    public int maxHp = 12;
    public float volleyInterval = 1.9f, fireballSpeed = 7f;
    public int volleySize = 3; public float spreadDeg = 14f;
    public int Hp { get; private set; }
    public Transform cam, player;
    float t, nextVolley, flash; Renderer rend; Color baseCol;
    void Awake(){ rend = GetComponent<Renderer>(); }
    public void Begin(){
      Hp = maxHp; t = 0; nextVolley = 1.5f; gameObject.SetActive(true);
      transform.position = new Vector3(cam.position.x + 16f, 5f, 0);
      GameEvents.BossStarted?.Invoke();
    }
    void Update(){
      var gm = GameManager.I; if (!gm || !gm.IsActive) return;
      t += Time.deltaTime;
      float targetX = cam.position.x + 7.5f;
      var p = transform.position;
      p.x = Mathf.MoveTowards(p.x, targetX, (gm.ScrollSpeed + 6f) * Time.deltaTime);
      if (p.x < targetX) p.x = targetX;
      p.y = 5f + Mathf.Sin(t * 1.1f) * 3f;
      transform.position = p;
      if (t >= nextVolley && Mathf.Abs(p.x - targetX) < 0.5f){ Volley(); nextVolley = t + volleyInterval; }
      if (flash > 0){ flash -= Time.deltaTime; rend.material.SetColor("_BaseColor", flash > 0 ? Color.white : baseCol); }
    }
    void Volley(){
      var cfg = GameConfig.I;
      Vector3 from = transform.position + Vector3.left * 1.4f;
      Vector3 dir = (player.position - from); dir.z = 0; dir.Normalize();
      for (int i = 0; i < volleySize; i++){
        float a = (i - (volleySize - 1) * 0.5f) * spreadDeg;
        var d = Quaternion.Euler(0, 0, a) * dir;
        var go = cfg.MakeBlock("Fireball", cfg.sphere, cfg.fireMat, from, Vector3.one * 0.6f);
        go.layer = Layers.Hazard;
        go.AddComponent<SphereCollider>().isTrigger = true;
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var f = go.AddComponent<Fireball>(); f.vel = d * fireballSpeed + Vector3.right * GameManager.I.ScrollSpeed;
      }
    }
    public void Damage(int n){
      if (Hp <= 0) return;
      if (baseCol == default) baseCol = rend.material.GetColor("_BaseColor");
      Hp -= n; flash = 0.08f; Puff.Burst(transform.position + Vector3.left, GameConfig.I.sparkleMat, 6);
      if (Hp <= 0){
        Puff.Burst(transform.position, GameConfig.I.bossMat, 30, 0.4f, 8f);
        foreach (var f in FindObjectsByType<Fireball>(FindObjectsSortMode.None)) Destroy(f.gameObject);
        gameObject.SetActive(false);
        GameEvents.BossDefeated?.Invoke(); GameManager.I.Win();
      }
    }
  }
  public class Fireball : MonoBehaviour {
    public Vector3 vel; float life = 6f;
    void Update(){
      if (GameManager.I && !GameManager.I.IsActive) return;
      transform.position += vel * Time.deltaTime; transform.Rotate(0, 0, 400 * Time.deltaTime);
      life -= Time.deltaTime; if (life <= 0) Destroy(gameObject);
    }
  }
}
