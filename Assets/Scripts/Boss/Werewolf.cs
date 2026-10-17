using System.Collections.Generic;
using UnityEngine;
using BroomstickBash.Art;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif
namespace BroomstickBash {
  /// <summary>Werewolf boss: hovers/leaps at the right of the screen, throws telegraphed fireball patterns that ramp
  /// through 3 phases as HP drops. Every pattern is checked by FairnessValidator before it is committed.
  /// Art swap point: assign <see cref="visualPrefab"/> (Art Dev's werewolf) and the gray-box child "GrayBox" is hidden.</summary>
  public class Werewolf : MonoBehaviour {
    enum Mode { Entering, Idle, Telegraph, Defeated }
    [Header("Art")] public GameObject visualPrefab; public Transform grayBox;
    public Vector3 artOffset = new Vector3(-0.2f, -1.75f, 0); public float artScale = 1.25f;
    GameObject art; Animator anim; PhaseTuning committed;
    [Header("Refs")] public Transform cam, player;
    [Header("Stats")] public int maxHp = 12;
    public float entryGrace = 2.5f;               // witch invulnerable + no attacks after boss entry
    public float minSpawnDistance = 6f;           // fireballs never spawn closer than this to the witch
    public float phase2Hp = 8, phase3Hp = 4;      // HP thresholds (<=)
    public PhaseTuning[] phases = DefaultPhases();
    public int Hp { get; private set; }
    public int Phase => Hp > phase2Hp ? 0 : Hp > phase3Hp ? 1 : 2;
    public bool Defeated => mode == Mode.Defeated;
    public static int RejectedPatterns, FiredPatterns;   // telemetry for QA

    Mode mode; float t, timer, flash, defeatT; bool poofed; AttackKind pending; List<Shot> planned; int attackIdx;
    readonly List<GameObject> tells = new(); Renderer[] rends; MaterialPropertyBlock mpb; System.Random rng = new();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    PlayerController pc;

    public static PhaseTuning[] DefaultPhases() => new[]{
      new PhaseTuning{ fireballSpeed = 3.6f, telegraph = 1.5f, rest = 1.8f, wallGap = 3.8f, aimedCount = 1, arcCount = 2, attacks = new[]{ AttackKind.Aimed, AttackKind.Wall } },
      new PhaseTuning{ fireballSpeed = 4.2f, telegraph = 1.3f, rest = 1.5f, wallGap = 3.4f, aimedCount = 2, aimedSpreadDeg = 18f, arcCount = 2, attacks = new[]{ AttackKind.Aimed, AttackKind.Arc, AttackKind.Wall } },
      new PhaseTuning{ fireballSpeed = 4.8f, telegraph = 1.1f, rest = 1.2f, wallGap = 3.0f, aimedCount = 3, aimedSpreadDeg = 22f, arcCount = 3, attacks = new[]{ AttackKind.Wall, AttackKind.Aimed, AttackKind.Arc } },
    };

    void Awake(){
      mpb = new MaterialPropertyBlock();
      if (!visualPrefab) visualPrefab = BroomArt.Load("Werewolf");   // fallback: Art Dev's Resources prefab
      if (visualPrefab){
        art = Instantiate(visualPrefab, transform, false); art.name = "Art_Werewolf";
        foreach (var c in art.GetComponentsInChildren<Collider>()) Destroy(c);
        // art faces +X with its front to the camera; mirror on X so it faces the witch (-X) and still shows its front
        art.transform.localPosition = artOffset; art.transform.localScale = new Vector3(-artScale, artScale, artScale);
        if (grayBox) grayBox.gameObject.SetActive(false);
        anim = art.GetComponentInChildren<Animator>();
      }
      rends = GetComponentsInChildren<Renderer>(false);
      pc = player ? player.GetComponent<PlayerController>() : null;
    }
    void OnDisable(){ ClearTells(); }

    public void Begin(){
      gameObject.SetActive(true);
      Hp = maxHp; t = 0; mode = Mode.Entering; timer = entryGrace; attackIdx = 0; flash = 0; defeatT = 0;
      transform.position = new Vector3(cam.position.x + 16f, 5f, 0); transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
      SetTint(null); foreach (var r in rends) if (r) r.enabled = true;
      if (anim){ anim.Rebind(); anim.Update(0f); }
      GameEvents.BossStarted?.Invoke();
    }

    float TargetX => cam.position.x + 7.5f;
    void Update(){
      var gm = GameManager.I; if (!gm || !gm.IsActive) return;
      float dt = Time.deltaTime; t += dt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame) Damage(1);   // dev-only QA: auto-hit
#endif
      if (mode == Mode.Defeated){ DefeatTick(dt); return; }
      var p = transform.position;
      p.x = Mathf.Max(Mathf.MoveTowards(p.x, TargetX, (gm.ScrollSpeed + 6f) * dt), TargetX);
      float bobAmp = mode == Mode.Telegraph ? 0.3f : 2.6f;      // holds (mostly) still while winding up
      p.y = Mathf.Lerp(p.y, 5f + Mathf.Sin(t * 0.9f) * bobAmp, 3f * dt);
      transform.position = p;

      if (flash > 0){ flash -= dt; if (flash <= 0) SetTint(mode == Mode.Telegraph ? TellColor : (Color?)null); }
      timer -= dt;
      switch (mode){
        case Mode.Entering: if (timer <= 0){ mode = Mode.Idle; timer = 0.6f; } break;
        case Mode.Idle: if (timer <= 0) PlanAttack(); break;
        case Mode.Telegraph: TelegraphTick(); if (timer <= 0) Fire(); break;
      }
    }

    PhaseTuning Tuning => phases[Mathf.Clamp(Phase, 0, phases.Length - 1)];
    Color TellColor => pending == AttackKind.Wall ? new Color(1f, 0.85f, 0.2f) : pending == AttackKind.Arc ? new Color(1f, 0.4f, 0.9f) : new Color(1f, 0.3f, 0.2f);
    Vector2 HandRel => (Vector2)(transform.position + new Vector3(-1.6f, 0.4f, 0) - player.position);

    void PlanAttack(){
      var tun = Tuning; var m = FlapModel.From(pc, GameConfig.I);
      float y = player.position.y, vy = pc.VelocityY; planY = y;
      var order = tun.attacks; pending = order[attackIdx++ % order.Length];
      planned = null;
      // runtime fairness check: try a few random variants, then fall back to a single slow aimed shot, else skip
      for (int tries = 0; tries < 5 && planned == null; tries++){
        var rel = FireballPatterns.Build(pending, tun, HandRel, y, m.groundY, m.ceilingY, rng);
        EnforceMinDistance(rel);
        if (rel.Count > 0 && FairnessValidator.IsFair(FairnessValidator.ToAbsolute(rel, y), m, y, vy, tun.telegraph)) planned = rel;
        else RejectedPatterns++;
      }
      if (planned == null){
        pending = AttackKind.Aimed;
        var fb = new PhaseTuning{ fireballSpeed = 3.2f, aimedCount = 1, telegraph = tun.telegraph };
        var rel = FireballPatterns.Build(AttackKind.Aimed, fb, HandRel, y, m.groundY, m.ceilingY, rng);
        EnforceMinDistance(rel);
        planned = rel.Count > 0 && FairnessValidator.IsFair(FairnessValidator.ToAbsolute(rel, y), m, y, vy, tun.telegraph) ? rel : null;
      }
      if (planned == null){ timer = tun.rest; return; }   // nothing fair right now: skip a beat
      mode = Mode.Telegraph; timer = tun.telegraph; committed = tun;   // phase can't change a committed attack SetTint(TellColor);
      SpawnTells();
      GameEvents.BossTelegraph?.Invoke(pending);
    }
    void EnforceMinDistance(List<Shot> rel){ rel.RemoveAll(s => s.pos.magnitude < minSpawnDistance); }

    // Tell: glowing "ghost" orbs where each fireball will appear, growing through the wind-up; boss tints + crouches.
    void SpawnTells(){
      ClearTells(); var cfg = GameConfig.I;
      foreach (var s in planned){
        var go = cfg.MakeBlock("Tell", cfg.sphere, cfg.sparkleMat, Vector3.zero, Vector3.one * 0.15f);
        go.transform.SetParent(transform.parent, true);
        tells.Add(go);
      }
    }
    void TelegraphTick(){
      float k = 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, committed.telegraph));
      transform.localScale = new Vector3(1f + 0.08f * k, 1f - 0.12f * k, 1f);
      // blink faster as it gets closer to firing
      bool on = Mathf.Repeat(t * (4f + 10f * k), 1f) < 0.6f;
      for (int i = 0; i < tells.Count && i < planned.Count; i++){
        if (!tells[i]) continue;
        // tells follow the witch's frame so they show exactly where the shot will start (shots are planned relative to her)
        var s = planned[i]; tells[i].transform.position = new Vector3(player.position.x + s.pos.x, Mathf.Clamp(PlannedAbsY(s), -1, 11), -0.6f);
        tells[i].transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.55f, k);
        tells[i].SetActive(on || k > 0.85f);
      }
    }
    float planY; float PlannedAbsY(Shot s) => planY + s.pos.y;
    void ClearTells(){ foreach (var g in tells) if (g) Destroy(g); tells.Clear(); }

    void Fire(){
      var cfg = GameConfig.I; var gm = GameManager.I;
      float baseY = planY;
      foreach (var s in planned){
        var at = new Vector3(player.position.x + s.pos.x, baseY + s.pos.y, 0);
        if (Vector2.Distance(at, player.position) < minSpawnDistance) continue;   // hard guarantee, even if the witch moved
        if (s.delay > 0){ StartCoroutine(Delayed(s, at)); continue; }
        Spawn(s, at);
      }
      ClearTells(); SetTint(null); transform.localScale = Vector3.one;
      if (art) BroomArt.Trigger(art, "Throw");
      mode = Mode.Idle; timer = committed.rest; FiredPatterns++;
      GameEvents.BossFired?.Invoke();
    }
    System.Collections.IEnumerator Delayed(Shot s, Vector3 at){
      float d = s.delay; while (d > 0){ if (GameManager.I.IsActive) d -= Time.deltaTime; yield return null; }
      if (mode == Mode.Defeated) yield break;
      at = new Vector3(player.position.x + s.pos.x, at.y, 0);
      if (Vector2.Distance(at, player.position) >= minSpawnDistance) Spawn(s, at);
    }
    void Spawn(Shot s, Vector3 at){
      var cfg = GameConfig.I;
      var go = cfg.MakeBlock("Fireball", cfg.sphere, cfg.fireMat, at, Vector3.one * 0.6f);
      go.layer = Layers.Hazard;
      go.AddComponent<SphereCollider>().isTrigger = true;
      var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
      var f = go.AddComponent<Fireball>(); f.vel = (Vector3)s.vel + Vector3.right * GameManager.I.ScrollSpeed; f.gravity = s.gravity;
    }

    public void Damage(int n){
      if (Hp <= 0 || mode == Mode.Defeated || mode == Mode.Entering) return;
      int before = Phase;
      Hp -= n; flash = 0.1f; SetTint(Color.white);
      Puff.Burst(transform.position + Vector3.left, GameConfig.I.sparkleMat, 6);
      if (art && Hp > 0) BroomArt.Trigger(art, "Hurt");
      GameEvents.BossHurt?.Invoke(Hp);
      if (Hp <= 0){ StartDefeat(); return; }
      if (Phase != before){ Puff.Burst(transform.position, GameConfig.I.fireMat, 14, 0.25f, 6f); GameEvents.BossPhaseChanged?.Invoke(Phase); }
    }

    // Defeat: stagger/shake 1.1 s → topple and fall 0.9 s → poof → 1 s pause → win screen.
    void StartDefeat(){
      mode = Mode.Defeated; defeatT = 0; poofed = false; ClearTells(); StopAllCoroutines(); SetTint(Color.white);
      foreach (var f in FindObjectsByType<Fireball>(FindObjectsSortMode.None)) Destroy(f.gameObject);
      if (pc) pc.GrantInvulnerability(5f);
      if (art) BroomArt.Trigger(art, "Defeat");
      GameEvents.BossDefeated?.Invoke();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
      Debug.Log("[Werewolf] DEFEATED");
#endif
    }
    void DefeatTick(float dt){
      defeatT += dt; var p = transform.position;
      p.x = Mathf.Max(p.x, TargetX);
      if (defeatT < 1.1f){
        transform.position = new Vector3(TargetX + Mathf.Sin(defeatT * 60f) * 0.15f, p.y, 0);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(defeatT * 25f) * 8f);
        SetTint(Mathf.Repeat(defeatT * 8f, 1f) < 0.5f ? Color.white : (Color?)null);
      } else if (defeatT < 2.0f){
        float k = (defeatT - 1.1f) / 0.9f;
        transform.position = new Vector3(TargetX, Mathf.Lerp(p.y, 0.8f, k), 0); transform.rotation = Quaternion.Euler(0, 0, -80f * k);
      } else if (!poofed){
        poofed = true; SetTint(null);
        Puff.Burst(transform.position, GameConfig.I.ghostMat, 30, 0.45f, 8f);
        foreach (var r in rends) if (r) r.enabled = false;
      } else if (defeatT > 3.0f){
        foreach (var r in rends) if (r) r.enabled = true;
        gameObject.SetActive(false); GameManager.I.Win();
      }
    }

    void SetTint(Color? c){
      foreach (var r in rends){
        if (!r) continue;
        if (c.HasValue){ r.GetPropertyBlock(mpb); mpb.SetColor(BaseColor, c.Value); r.SetPropertyBlock(mpb); }
        else r.SetPropertyBlock(null);
      }
    }
  }
}
