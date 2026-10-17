# Broomstick Bash art
- Source: `ArtSource/build_assets.py` → `blender -b -P ArtSource/build_assets.py -- <out>`, copy the `.fbx` files into `Assets/Art/Models/`.
  Previews: `blender -b -P ArtSource/render_previews.py -- <out> <preview_dir>`.
- **Broomstick Bash > Art > Rebuild Art Prefabs** (batch `-executeMethod BroomArtSetup.BatchRebuild`) does the import setup, flat URP
  palette materials (`Materials/`, `*Glow` emissive, `GhostBody` translucent), Animator controllers (`Animators/`), prefabs
  (`Resources/Art/BA_*.prefab`) and validation.
- Runtime: `BroomstickBash.Art.BroomArt.Load("Witch")`, `BroomArt.Attach(name, parent)`, `BroomArt.Trigger(art, "Cast")`.

Conventions match M1: metres, ground y=0, ceiling y=10, camera at -Z looking +Z, flight toward +X. Characters face +X with their
front toward the camera. Origins: feet/ground for kids, cars, lamp, werewolf; broom handle for the witch; top attachment point for
the branch (hangs down 3.05 m); centre for bat, ghost and FX.

| Prefab | Size (m) | Animator (trigger = state name) |
|---|---|---|
| BA_Witch | 1.9 long (broom) | Fly (loop), Flap, Cast, Hurt |
| BA_Kid_Pumpkin / _SheetGhost / _Robot | 1.25-1.3 tall | Walk (loop), Cheer |
| BA_Bat | 0.77 wingspan | Flap (loop) |
| BA_Ghost | 1.0 x 1.4 | Float (loop) |
| BA_Werewolf | 2.7 tall | Idle (loop), Throw, Hurt, Defeat (terminal) |
| BA_Car_Red / _Teal | 4.0 x 1.5 | - |
| BA_Branch, BA_LampPost | 3.05 / 4.0 | - |
| BA_FX_Spell / _Sparkle / _Fireball | 0.5-1.1 | BroomFxSpin (spin + pulse) |

Background layers tile every 40 m (`BroomArt.LayerWidth`); suggested depths/parallax: BG_Moon z=60 (place once, follow camera at
~0.95; do not tile), BG_HousesFar z=28, BG_HousesNear z=12, BG_Pumpkins z=3.

`BroomArtSkin` (runtime, no M1 edits) auto-skins spawned root objects named Kid, Car, Branch, Ghost, Bat, Fireball and Boss.
Still for M1 code: the player (PlayerController caches and toggles its renderers for the hurt flash - attach `BA_Witch` and
include its renderers, and fire Flap/Cast/Hurt), boss triggers (Throw on volley, Hurt on Damage, Defeat), parallax layers.
