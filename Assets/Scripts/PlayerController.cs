using UnityEngine;
using UnityEngine.InputSystem;
namespace BroomstickBash {
  /// <summary>Witch: flap physics, hearts, invulnerability flash, spell casting. Moves forward at the scroll speed.</summary>
  public class PlayerController : MonoBehaviour {
    public float gravity = 24f, flapVelocity = 8.2f, maxFall = -12f;
    public int maxHearts = 3;
    public float invulnTime = 1.5f, castCooldown = 0.45f;
    public int Hearts { get; private set; }
    public float VelocityY => vy;
    public bool Invulnerable => invuln > 0;
    public float CooldownLeft => cool;
    float vy, invuln, cool; Renderer[] rends;
    public int flapsThisRun;

    GameObject art;
    /// <summary>Seconds after start/restart during which bouncing off the street is free (no heart lost).</summary>
    public float streetGrace = 1.25f; float grace;
    void Awake(){
      // M2 art: BA_Witch (origin = broom handle; gray-box broom sits at y -0.3). The hurt flash toggles only the art renderers
      // when art is present, so the hidden gray-box never reappears; falls back to the gray-box renderers otherwise.
      art = BroomstickBash.Art.BroomArt.Attach("Witch", transform);
      if (art){ art.transform.localPosition = new Vector3(0, -0.3f, 0); rends = art.GetComponentsInChildren<Renderer>(true); }
      else rends = GetComponentsInChildren<Renderer>();
    }
    public void StartGrace(){ grace = streetGrace; }
    public void ResetPlayer(){
      Hearts = maxHearts; vy = 0; invuln = 0; cool = 0; flapsThisRun = 0; grace = streetGrace;
      transform.position = new Vector3(0, 5f, 0); transform.rotation = Quaternion.identity; SetVisible(true);
    }
    public static bool FlapPressed(){
      return (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
    }
    static bool CastPressed(){
      return (Keyboard.current != null && (Keyboard.current.xKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame))
        || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        || (Touchscreen.current != null && Touchscreen.current.touches.Count > 1 && Touchscreen.current.touches[1].press.wasPressedThisFrame);
    }
    public void Flap(){ vy = flapVelocity; flapsThisRun++; BroomstickBash.Art.BroomArt.Trigger(art, "Flap"); }

    public void Tick(float dt, float scroll){
      var cfg = GameConfig.I;
      if (FlapPressed()) Flap();
      if (cool > 0) cool -= dt;
      if (grace > 0) grace -= dt;
      if (CastPressed() && cool <= 0) CastSpell();
      vy = Mathf.Max(vy - gravity * dt, maxFall);
      var p = transform.position; p.x += scroll * dt; p.y += vy * dt;
      float top = cfg.ceilingY - 0.4f, bottom = cfg.groundY + 0.45f;
      if (p.y > top){ p.y = top; vy = Mathf.Min(vy, 0); }
      if (p.y < bottom){ p.y = bottom; if (grace <= 0) Hit(); vy = flapVelocity * 0.9f; } // street bounce costs a heart (if not invulnerable)
      transform.position = p;
      transform.rotation = Quaternion.Euler(0, 0, Mathf.Clamp(vy * 3f, -30, 25));
      if (invuln > 0){ invuln -= dt; SetVisible(invuln <= 0 || Mathf.FloorToInt(invuln * 12f) % 2 == 0); }
    }
    public void CastSpell(){
      cool = castCooldown; BroomstickBash.Art.BroomArt.Trigger(art, "Cast");
      Spell.Fire(transform.position + new Vector3(0.9f, 0.1f, 0), GameManager.I.ScrollSpeed + 14f);
    }
    public void Hit(){
      if (invuln > 0 || Hearts <= 0 || !GameManager.I.IsActive) return;
      Hearts--; invuln = invulnTime; BroomstickBash.Art.BroomArt.Trigger(art, "Hurt");
      Puff.Burst(transform.position, GameConfig.I.fireMat, 8);
      GameEvents.PlayerHit?.Invoke(Hearts);
      if (Hearts <= 0){ SetVisible(true); GameManager.I.GameOver(); }
    }
    void OnTriggerEnter(Collider o){ Touch(o); }
    void OnTriggerStay(Collider o){ Touch(o); }
    void Touch(Collider o){
      int l = o.gameObject.layer;
      if (l == Layers.Enemy || l == Layers.Kid || l == Layers.Obstacle || l == Layers.Hazard){
        if (invuln > 0) return;
        Hit();
        if (l == Layers.Hazard) Destroy(o.gameObject);
      }
    }
    void SetVisible(bool v){ foreach (var r in rends) if (r) r.enabled = v; }
  }
}
