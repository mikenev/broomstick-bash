using UnityEngine;
namespace BroomstickBash {
  /// <summary>Spell bolt. Destroys Enemy layer, passes through Kid layer with a sparkle, is blocked by Obstacle layer.</summary>
  public class Spell : MonoBehaviour {
    float speed, life = 1.6f;
    public static Spell Fire(Vector3 at, float speed){
      var cfg = GameConfig.I;
      var go = cfg.MakeBlock("Spell", cfg.sphere, cfg.spellMat, at, new Vector3(0.6f, 0.35f, 0.35f));
      go.layer = Layers.Spell;
      var c = go.AddComponent<SphereCollider>(); c.isTrigger = true;
      var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
      var s = go.AddComponent<Spell>(); s.speed = speed; GameEvents.SpellCast?.Invoke(); return s;
    }
    void Update(){
      if (GameManager.I && !GameManager.I.IsActive) return;
      transform.position += Vector3.right * speed * Time.deltaTime;
      life -= Time.deltaTime; if (life <= 0) Destroy(gameObject);
    }
    void OnTriggerEnter(Collider o){
      int l = o.gameObject.layer;
      if (l == Layers.Kid){ Puff.Burst(o.transform.position + Vector3.up * 0.5f, GameConfig.I.sparkleMat, 10, 0.12f, 3f); GameEvents.SpellSparkle?.Invoke(); return; }
      if (l == Layers.Hazard){ Puff.Burst(o.transform.position, GameConfig.I.sparkleMat, 4); Destroy(o.gameObject); Destroy(gameObject); GameEvents.FireballZapped?.Invoke(); return; }
      var ob = o.GetComponentInParent<ObstacleBase>();
      if (ob && ob.OnSpellHit()){ Destroy(gameObject); if (ob.gameObject.layer != Layers.Enemy) GameEvents.SpellBlocked?.Invoke(); else if (ob.hp > 0) GameEvents.SpellHit?.Invoke(); return; }   // lethal hits: EnemyKilled plays the pop
      var boss = o.GetComponentInParent<Werewolf>();
      if (boss){ boss.Damage(1); Destroy(gameObject); }   // audio comes from BossHurt / BossImmune
    }
  }
}
