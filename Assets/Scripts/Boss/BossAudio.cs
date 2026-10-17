using UnityEngine;
namespace BroomstickBash {
  /// <summary>Minimal boss audio: boss_theme loop during the fight plus a few stings/SFX, driven by GameEvents.</summary>
  public class BossAudio : MonoBehaviour {
    public AudioClip bossTheme, roar, hurt, throwSfx, defeat, victory;
    [Range(0, 1)] public float musicVolume = 0.5f, sfxVolume = 0.8f;
    AudioSource music, sfx;
    void Awake(){
      music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.volume = musicVolume;
      sfx = gameObject.AddComponent<AudioSource>(); sfx.playOnAwake = false;
    }
    void OnEnable(){
      GameEvents.BossStarted += OnStart; GameEvents.BossPhaseChanged += OnPhase; GameEvents.BossHurt += OnHurt;
      GameEvents.BossFired += OnFire; GameEvents.BossDefeated += OnDefeat; GameEvents.Win += OnWin;
      GameEvents.GameOver += StopMusic; GameEvents.RunReset += StopMusic;
    }
    void OnDisable(){
      GameEvents.BossStarted -= OnStart; GameEvents.BossPhaseChanged -= OnPhase; GameEvents.BossHurt -= OnHurt;
      GameEvents.BossFired -= OnFire; GameEvents.BossDefeated -= OnDefeat; GameEvents.Win -= OnWin;
      GameEvents.GameOver -= StopMusic; GameEvents.RunReset -= StopMusic;
    }
    void One(AudioClip c, float v = 1f){ if (c) sfx.PlayOneShot(c, sfxVolume * v); }
    void OnStart(){ One(roar); if (bossTheme){ music.clip = bossTheme; music.volume = musicVolume; music.Play(); } }
    void OnPhase(int p){ One(roar, 0.8f); }
    void OnHurt(int hp){ if (hp > 0) One(hurt, 0.7f); }
    void OnFire(){ One(throwSfx, 0.6f); }
    void OnDefeat(){ StopMusic(); One(defeat); }
    void OnWin(){ One(victory); }
    void StopMusic(){ if (music) music.Stop(); }
  }
}
