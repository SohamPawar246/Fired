# FIRED. — Level Design Guide (handoff)

*You build the levels and pick the assets. The code finds your work through three
small components and does everything else. This doc is the whole contract.*

---

## Who does what

| You (designer) | The systems (already built) |
|---|---|
| Build level geometry in the **Game scene** from any imported kit | Opening gun-shot sequence, camera direction, slow-mo beat |
| Place **LevelInfo** (one per level) and aim the muzzle | Bullet flight, steering, ricochet vs. head-on death, stall |
| Add **FiredTarget** + trigger colliders to things worth points | Scoring, multiplier, comic popups, BAM on big hits, speed refunds |
| Drop colliders on anything that should block/ricochet | HUD, pause, game over with run summary, restart loop |

**Your levels are safe:** the scaffold builder never regenerates `Game.unity`
anymore. (Only the explicit menu item *Tools > Jam Scaffold > Rebuild Game Scene
(WIPES your level!)* does, and it says so.)

---

## The infinite city (CityStreamer)

The theme is INFINITE RUNNER, so the world is now streamed: **CityStreamer**
generates street blocks endlessly ahead of the bullet from prefab slots you wire
in its inspector (towers, sign, cable, pipe, antenna, AC, light square, target
rings, enemy body, skeleton). Every slot is optional — empty slots are skipped.
It measures real bounds for grounding, parents signs/pipes/roof props TO their
tower and snaps them flush, and drops 2–3 enemies + 3–4 target rings + neon-framed
obstacle panels per 120 m block, with one enemy per block near the center lane.

To restyle the city you can: swap any prefab slot, change `blockLength`/`halfWidth`,
or edit the recipes in `CityStreamer.cs` (each prop type is one small method).
Hand-built set pieces still work — anything you place in the scene simply exists
alongside the streamed blocks (put them off the street axis or far ahead).

## Building a hand-authored level — the 5 steps

1. **Open `Assets/Scenes/Game.unity`.** Build your geometry under one root object
   (e.g. `Level_01`). Use any imported kit. Scale/rotate freely.

2. **Colliders = walls.** Anything with a (non-trigger) collider is a surface:
   shallow-angle hits ricochet the bullet, head-on hits kill it. For kit meshes,
   add a `MeshCollider` (no settings needed). No collider = decoration the bullet
   flies through harmlessly.

3. **Add `LevelInfo`** to your level root (delete/disable my `Level_Example` first —
   only ONE LevelInfo per scene):
   - `muzzle` → create an empty child at the gun barrel tip. **Its blue (+Z) arrow
     is the flight direction.** This is the only required field.
   - `shooterAnimator` → your character's Animator (its **default state plays**
     during the intro — the existing `Gunplay.controller` already works like this).
   - `fireDelaySeconds` → when in that animation the gun "fires" (BAM + bang +
     bullet launch). Watch the anim and pick the moment.
   - `bamVfxPrefab` → `Assets/Prefabs/Bam VFX.prefab` (also reused on 300+ point hits).
   - `bulletStartSpeed` → per-level difficulty knob (default 80).

4. **Add `FiredTarget`** to everything worth points:
   - Add a collider, tick **Is Trigger** ✔ (trigger = scoring, non-trigger = wall!).
   - Pick a **Preset** — Skull 500 / Spine 300 / Pelvis 200 / Ribs 150 / Limb 100 /
     Pinky 50 / Core 100 / MegaCore 500 — or Custom and type your own
     points/refund/label/color. `spin` makes pickups rotate.
   - Speed refunds are the fuel economy: a lane without targets starves the player.
     Rule of thumb: one reachable target every ~40 m.

5. **Press Play in the scene.** Intro → shot → flight. Iterate.

### Placement tips (learned building the example)
- The bullet launches at the muzzle height — put early targets near that height,
  raise them later so the player learns to climb.
- Colliders on scaled FBX rigs: collider fields are in LOCAL units. If a model
  needed scaling (skeleton!), size colliders by eye in the Scene view, not by
  typing metres. (This bug gave a straight-line flight 9900 free points.)
- Fog hides everything past ~380 m — a level is a chain of 300–400 m sightlines.
- Mixamo characters: if they render grey, the fix is a URP Lit material with the
  matching `Assets/Textures/*_Diffuse.png` in Base Map (see
  `ExampleLevelBuilder.FixShooterMaterials` for the pattern).

---

## The example level (`Level_Example` in Game.unity)

Built from your assets as a living reference — **replace it with your own work**.
It demonstrates: Kenney-tower street canyon, cyberpunk-kit obstacle panels and
pickups-as-cores, your Skelton.fbx floating with skull/ribs targets, your Ch44 +
Gunplay shooter with a kit gun clipped into the right hand, muzzle + fill light,
and the wired LevelInfo. Regenerate it anytime with
*Tools > Jam Scaffold > Build Example Level* (replaces only `Level_Example`).

## The opening shot (already directed, data-driven)

Camera frames the shooter → your animation plays → at `fireDelaySeconds`: BAM
sprite + gunshot + bullet leaves the barrel in slow motion → chase cam snaps on.
Any click skips to the shot. You control all of it from LevelInfo; no code.

## Asset intake checklist (when you add new stuff)

- **Kits/props**: import anywhere under `Assets/Level/` — no registration needed;
  reference them straight from the scene.
- **Videos**: name contains `menu`/`loop`/`bg` → menu background; `intro`/`studio`/
  `logo` → studio intro. Then run *Tools > Jam Scaffold > Build All Scenes*.
- **Characters (Mixamo)**: download WITH skin, drop the FBX + a controller whose
  default state is the animation. Assign textures per above.
- **Watch the size**: `Gunplay.fbx` + `Ch44_nonPBR.fbx` are ~98 MB each in source.
  Meshes compress heavily in builds, but if the WebGL build creeps past ~150 MB,
  the fix is deleting the duplicate character FBX (only `Gunplay.fbx` is used).

## Known rough edges (honest list)

- The muzzle/hand-gun alignment on the Mixamo pose is approximate — nudge the
  `Muzzle` empty and `HandGun` transform in the scene to taste.
- The Kenney buildings read pastel, not cyberpunk; they're lit dark violet + neon
  points, but swapping facades to the Cyberpunk/SciFi kits will look better.
- No Deadeye (RMB slow-mo aim), no rewind ending yet — next systems on my list.
- Mouse cursor isn't locked during flight (kept free so menus stay testable).
