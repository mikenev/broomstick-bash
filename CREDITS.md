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

## Art
All models, rigs, animations and materials in `Assets/Art` are **original work created for Broomstick Bash**, generated
procedurally by `ArtSource/build_assets.py` (Blender 4.2.3, headless). No third-party models, textures, packs or references
to existing characters are used.

| Asset | Source | License |
|---|---|---|
| Witch, Kid_Pumpkin, Kid_SheetGhost, Kid_Robot, Bat, Ghost, Werewolf, Car_Red, Car_Teal, Branch, LampPost, BG_Moon, BG_HousesFar, BG_HousesNear, BG_Pumpkins, FX_Spell, FX_Sparkle, FX_Fireball | Original, `ArtSource/build_assets.py` | Same as this repository |

Tools (not redistributed): Blender 4.2.3 (GPL; generated output is not covered by the GPL), Unity 6000.3.26f1.
