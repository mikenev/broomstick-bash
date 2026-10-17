# Credits

## Audio
All music and sound effects in `Assets/Audio` are **original and procedurally generated** by
`tools/audio/generate_audio.py` (Python, numpy/scipy, OGG Vorbis via ffmpeg/libvorbis).
No samples, recordings, or third-party/CC0 assets are used. Vocal-like sounds (hit_ouch, ghost_wail,
werewolf_roar, boss_hurt) are synthesized from oscillators and filters and are intentionally non-human.

### LOOPS
These clips are authored as seamless loops. Set `AudioSource.loop = true` when playing them
(Unity's AudioImporter has no loop setting; looping is set per AudioSource):
- `Assets/Audio/Music/main_theme_loop.ogg`
- `Assets/Audio/Music/boss_theme_loop.ogg`

Every other clip (stings and all SFX) is a one-shot (`loop = false`, or use `PlayOneShot`).
