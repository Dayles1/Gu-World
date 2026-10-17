# Character: the teenage base bodies

Builds the bodies the game dresses — a 16-year-old East Asian boy (1.70 m) and girl
(1.60 m) — with MakeHuman's Blender plugin (MPFB 2), as drop-in replacements for the
Quaternius models in `Assets/GuWorld/Models` (`human-male.glb`, `human-female.glb`).

Output (in `out/`):

| file | what |
|---|---|
| `human-teen-male.glb`, `human-teen-female.glb` | the bodies |
| `verify-male.txt`, `verify-female.txt` | each body re-imported and compared with the current model |
| `preview-*.png` | current and new side by side, same scale: body front/side, face front/three-quarter, arms down |
| `work/` | logs, the `.blend` of each body, texture intermediates (not kept in git) |

## Setup (once)

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Character\setup.ps1
```

Downloads (SHA-256 checked) and installs, outside the repo, in `D:\Dev\Tools`:
portable Blender 4.5.14 LTS, the MPFB 2.0.18 extension from extensions.blender.org,
and MakeHuman's CC0 system assets pack (unpacked into MPFB's user data). Every step is
skipped when it is already done.

## Build

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Character\build.ps1            # both, with previews
powershell -ExecutionPolicy Bypass -File Tools\Character\build.ps1 -Gender male -NoPreviews
```

About 10 s per body and a minute for the previews; Blender runs headless and stays
under 1 GB. The scripts can also be run on their own:

```
blender -b --python build_teen.py -- --gender male --out out/human-teen-male.glb [--params p.json] [--blend b.blend]
blender -b --factory-startup --python verify_glb.py -- --new out/human-teen-male.glb --old ../../Assets/GuWorld/Models/human-male.glb
blender -b --factory-startup --python render_previews.py -- --out out/preview --model "label=file.glb" ...
python glb_report.py out/human-teen-male.glb --compare ../../Assets/GuWorld/Models/human-male.glb
```

## The look

All in `PRESETS` at the top of `build_teen.py`; `--params` takes a JSON file with the
same keys (`{"male": {"phenotype": {"age": 0.32}, "targets": {...}}}`) to try changes
without editing it.

- MakeHuman macros: Asian 1.0, muscle 0.3, weight 0.40 (boy) / 0.42 (girl), proportions
  and height 0.5; girl cup size 0.5, firmness 0.6. Age 0.34 (boy) / 0.36 (girl): MakeHuman's
  own "16" (0.3125) blends 64 % of its 11-year-old target and reads as 13–14.
- Face targets: slightly bigger eyes with a small upward tilt, lower and bolder brows, a
  smaller nose, a smaller, thinner-lipped mouth, a slimmer jaw and neck (the girl's softer
  and more oval).
- The finished body is scaled to exactly 1.70 / 1.60 m.
- Skin `young_asian_male` / `young_asian_female`; eyes `brown` (the iris recoloured dark
  brown); brows `eyebrow004` (boy) / `eyebrow001` (girl), drawn 1.6× / 1.3× thicker and
  lifted 1.2 mm off the skin; lashes `eyelashes01` / `eyelashes02`. No hair, no clothes —
  the game makes them.

## What the .glb holds (matches the current models)

- Node `Armature` with the skinned meshes `SuperHero_Male` / `SuperHero_Female` (material
  `MI_Superhero_Male` / `MI_Superhero_Female`, 2048² JPEG), `Eyes` (`MI_Eyes`) and
  `Eyebrows` (two materials, `MI_Hair_Eyebrows` and `MI_Hair_Eyelashes`, PNG with alpha,
  alphaMode MASK), and the bone `root`.
- One skin of the current 65 joints, same names, order and parents (`root`, `pelvis`,
  `spine_01..03`, `neck_01`, `Head`, clavicle/upperarm/lowerarm/hand, five fingers ×
  `01..03` + `04_leaf`, thigh/calf/foot/`ball`/`ball_leaf`), at most 4 weights per vertex.
- T-pose (arms level, palms down, fingers straight; legs straight down), metres, Y-up,
  facing +Z, the figure's left on +X, the soles at y = 0, the root at the origin.
- MakeHuman's base mesh: 26 756 triangles for the body, 28 376 in all.

## Unity side

See the verification reports for every difference. What the game will notice:

- **Height.** `HumanBody.Fit` scales a model so its hip joints sit at the Mannequin's
  (0.96 above the sole), whatever the model's own size. A teenager's legs are shorter for
  their height than the superhero's, so the new bodies come out 1.846 (boy) / 1.890 (girl)
  figure units tall instead of ~1.80, i.e. 1.83 / 1.76 m with today's `HeroView.HeightOf`
  (1.78 / 1.68) and `PersonFigure.ModelHeight` (1.8). Either set `ModelHeight` to the
  model's fitted height (1.846 / 1.890: every person then stands at exactly the height they
  are given) or lower `HeightOf` to 1.658 / 1.523 for 1.70 / 1.60 m.
- **No hair meshes.** `Hair_SimpleParted` and `Hair_Buns` are gone; those two styles have
  nothing to borrow (keep the old models as hair donors, or map them to grown styles).
- **Already young.** `Youth.Reshape`, the bigger `Head` bone (`TeenHead`) and
  `Muscles.Smooth` were made for the grown-up superhero bodies; on these they will
  overdo it (youth 0 for these models, or skip them).
- The brows and lashes are alpha-tested cards (`MI_Hair_*`, MASK 0.15), not geometry.
- The skin texture has no normal or metal/roughness map (the old one had both).
