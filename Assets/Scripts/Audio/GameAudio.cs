using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace BroomstickBash {
  /// <summary>Central audio manager (music + SFX), driven by GameEvents and GameManager state.
  /// Music: "Midnight Hop" (main_theme_loop) on the title and during the run, crossfades to "Moonfang" (boss_theme_loop)
  /// when the boss starts, and back to Midnight Hop after a win / game over / restart.
  /// WebGL autoplay: nothing plays until the first user input (key, mouse button or touch); then audio is unlocked and the
  /// current track starts. SFX are rate-limited per clip so frequent sounds (flap, honk) don't stack.</summary>
  public class GameAudio : MonoBehaviour {
    public static GameAudio I;
    [Header("Music")] public AudioClip mainTheme, bossTheme, titleSting, gameOverSting, victorySting;
    [Header("SFX")] public AudioClip flap, cast, spellHit, sparkle, hurt, honk, bat, ghost, enemyPop, fireballImpact, uiClick;
    [Header("Boss SFX")] public AudioClip roar, bossHurt, fireballThrow, bossDefeat;
    [Header("Mix")] [Range(0, 1)] public float musicVolume = 0.35f, bossMusicVolume = 0.42f, sfxVolume = 0.85f;
    public float crossfade = 1.2f;

    public bool Unlocked { get; private set; }
    public string CurrentTrack => target ? target.name : "none";

    AudioSource musA, musB, sfx, active; AudioClip target; float duck = 1f, duckUntil;
    readonly Dictionary<AudioClip, float> lastPlay = new();
    GameState lastState = (GameState)(-1);
    int lastFlaps;
    GameManager gm;

    struct Rule { public float vol, minGap; public Rule(float v, float g){ vol = v; minGap = g; } }
    Dictionary<AudioClip, Rule> rules;

    void Awake(){
      I = this;
      musA = NewSource(true); musB = NewSource(true); sfx = NewSource(false); active = musA;
      rules = new Dictionary<AudioClip, Rule>();
      void R(AudioClip c, float v, float g){ if (c) rules[c] = new Rule(v, g); }
      R(flap, 0.30f, 0.12f); R(cast, 0.45f, 0.10f); R(spellHit, 0.60f, 0.06f); R(sparkle, 0.45f, 0.20f);
      R(hurt, 0.90f, 0.30f); R(honk, 0.40f, 2.50f); R(bat, 0.35f, 1.50f); R(ghost, 0.40f, 2.50f);
      R(enemyPop, 0.55f, 0.06f); R(fireballImpact, 0.55f, 0.10f); R(uiClick, 0.35f, 0.15f);
      R(roar, 0.90f, 1.0f); R(bossHurt, 0.55f, 0.12f); R(fireballThrow, 0.40f, 0.25f); R(bossDefeat, 0.90f, 1f);
      R(titleSting, 0.60f, 1f); R(gameOverSting, 0.75f, 1f); R(victorySting, 0.80f, 1f);
    }
    AudioSource NewSource(bool loop){ var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.volume = 0; return s; }

    void OnEnable(){
      GameEvents.PlayerHit += OnPlayerHit; GameEvents.EnemyKilled += OnKilled; GameEvents.ObstacleSpawned += OnSpawned;
      GameEvents.SpellCast += OnCast; GameEvents.SpellHit += OnSpellHit; GameEvents.SpellSparkle += OnSparkle;
      GameEvents.SpellBlocked += OnBlocked; GameEvents.FireballZapped += OnZapped;
      GameEvents.BossStarted += OnBossStart; GameEvents.BossPhaseChanged += OnPhase; GameEvents.BossHurt += OnBossHurt;
      GameEvents.BossFired += OnBossFired; GameEvents.BossDefeated += OnBossDefeated;
      GameEvents.GameOver += OnGameOver; GameEvents.Won += OnWon; GameEvents.RunReset += OnReset;
    }
    void OnDisable(){
      GameEvents.PlayerHit -= OnPlayerHit; GameEvents.EnemyKilled -= OnKilled; GameEvents.ObstacleSpawned -= OnSpawned;
      GameEvents.SpellCast -= OnCast; GameEvents.SpellHit -= OnSpellHit; GameEvents.SpellSparkle -= OnSparkle;
      GameEvents.SpellBlocked -= OnBlocked; GameEvents.FireballZapped -= OnZapped;
      GameEvents.BossStarted -= OnBossStart; GameEvents.BossPhaseChanged -= OnPhase; GameEvents.BossHurt -= OnBossHurt;
      GameEvents.BossFired -= OnBossFired; GameEvents.BossDefeated -= OnBossDefeated;
      GameEvents.GameOver -= OnGameOver; GameEvents.Won -= OnWon; GameEvents.RunReset -= OnReset;
    }

    void Start(){ gm = GameManager.I; Music(mainTheme); }

    // ---------- autoplay unlock ----------
    static bool AnyInput(){
      var kb = Keyboard.current; if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
      var m = Mouse.current; if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame)) return true;
      var t = Touchscreen.current; return t != null && t.primaryTouch.press.wasPressedThisFrame;
    }
    void Unlock(){
      if (Unlocked) return; Unlocked = true;
      AudioListener.pause = false;
      Debug.Log("[Audio] unlocked on first input; music=" + CurrentTrack);
      if (target){ active.clip = target; active.volume = 0; active.Play(); }
      One(titleSting);
    }

    void Update(){
      if (!Unlocked){ if (AnyInput()) Unlock(); else return; }
      if (!gm) gm = GameManager.I;
      if (gm){
        if (gm.State != lastState){
          if (gm.State == GameState.Playing && (lastState == GameState.Ready || (int)lastState == -1)) One(uiClick);
          lastState = gm.State;
        }
        int f = gm.player ? gm.player.flapsThisRun : 0;   // flap SFX without touching PlayerController
        if (f > lastFlaps && gm.IsActive) One(flap);
        lastFlaps = f;
      }
      // music crossfade toward `target`
      if (Time.unscaledTime > duckUntil) duck = Mathf.MoveTowards(duck, 1f, Time.unscaledDeltaTime * 1.5f);
      float want = (target == bossTheme ? bossMusicVolume : musicVolume) * duck;
      float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, crossfade);
      var other = active == musA ? musB : musA;
      active.volume = Mathf.MoveTowards(active.volume, active.clip == target && target ? want : 0f, step * Mathf.Max(want, 0.3f) * 2f);
      other.volume = Mathf.MoveTowards(other.volume, 0f, step * 0.6f);
      if (other.isPlaying && other.volume <= 0.001f) other.Stop();
    }

    /// <summary>Switch music with a crossfade. Same track = no-op (keeps playing, no restart).</summary>
    public void Music(AudioClip clip){
      if (target == clip && (active.isPlaying || !Unlocked)) return;
      target = clip;
      if (!Unlocked || !clip) return;
      if (active.clip == clip && active.isPlaying) return;
      var other = active == musA ? musB : musA;
      active = other; active.clip = clip; active.volume = 0; active.Play();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
      Debug.Log("[Audio] music -> " + clip.name);
#endif
    }
    void Duck(float level, float seconds){ duck = Mathf.Min(duck, level); duckUntil = Time.unscaledTime + seconds; }

    /// <summary>Rate-limited one-shot. Returns false if suppressed.</summary>
    public bool One(AudioClip c, float extra = 1f){
      if (!c || !Unlocked) return false;
      var r = rules.TryGetValue(c, out var rr) ? rr : new Rule(0.6f, 0.08f);
      if (lastPlay.TryGetValue(c, out var t) && Time.unscaledTime - t < r.minGap) return false;
      lastPlay[c] = Time.unscaledTime;
      sfx.PlayOneShot(c, r.vol * extra * sfxVolume);
      return true;
    }

    // ---------- event handlers ----------
    void OnPlayerHit(int hearts){ One(hurt); }
    void OnKilled(ObstacleBase o){ One(enemyPop); }
    void OnSpawned(ObstacleBase o){
      if (!o) return;
      switch (o.kind){ case ObstacleKind.Car: One(honk); break; case ObstacleKind.Bat: One(bat); break; case ObstacleKind.Ghost: One(ghost); break; }
    }
    void OnCast(){ One(cast); }
    void OnSpellHit(){ One(spellHit); }
    void OnSparkle(){ One(sparkle); }
    void OnBlocked(){ One(fireballImpact, 0.6f); }
    void OnZapped(){ One(fireballImpact); }
    void OnBossStart(){ One(roar); Music(bossTheme); }
    void OnPhase(int p){ One(roar, 0.8f); }
    void OnBossHurt(int hp){ if (hp > 0) One(bossHurt); }
    void OnBossFired(){ One(fireballThrow); }
    void OnBossDefeated(){ One(bossDefeat); Music(mainTheme); Duck(0.25f, 4f); }
    void OnGameOver(){ One(gameOverSting); Music(mainTheme); Duck(0.3f, 3f); }
    void OnWon(int score){ One(victorySting); Duck(0.25f, 4f); }
    void OnReset(){ Music(mainTheme); lastFlaps = 0; }
  }
}
