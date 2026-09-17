# FIRED. — you are the bullet

**▶ Play in your browser: [sohampawar.itch.io/fired](https://sohampawar.itch.io/fired)** — one click, nothing to install.

Fired from a gun in the opening frame, you never stop flying. Mouse steers, walls
deflect you instead of killing you, and **speed is your health**. An endless runner
whose fail state is interesting: every mistake leaves you alive, pointed somewhere
you didn't choose, bleeding speed.

Unity **6000.3** · URP · New Input System · WebGL-first (Windows builds too).
By **Soham Pawar**.

---

## ⚠ Cloning this repo

All binary assets (models, textures, audio, video — ~1,300 files) are stored with
**Git LFS**. GitHub's **"Download ZIP" button does not resolve LFS**: you would get
~1,300 text pointer files and the project would open with no art, no audio and empty
meshes.

```bash
# Install Git LFS once, then:
git lfs install
git clone https://github.com/SohamPawar246/Fired.git
```

Sanity check after cloning — this file should be ~94 MB, not ~130 bytes:

```bash
ls -l Assets/Gunplay.fbx
```

Open with Unity **6000.3** or newer. First import takes a few minutes.

---

## Controls

| Input | Action |
|---|---|
| **Mouse** | Steer |
| **Left click** | Fire (opening shot only) |
| **Right mouse** | Deadeye — slow time to line up a shot, drains a meter |
| **R** | Restart instantly from the results screen |
| **Esc / P** | Pause |

Gamepad is supported throughout, including menu navigation.

---

## The loop

Fly fast, thread targets to refuel, survive your own mistakes. Speed drains
continuously, targets refund it, walls tax it — a head-on slam costs 16% of your
speed, a graze 7%. Run out and you fall out of the air.

The run escalates through three environments as you get deeper:

| Zone | From | Look |
|---|---|---|
| **STATION** | 0 m | Clean neon station — the onboarding zone |
| **THE CAVES** | 150 m | Warm rock, twistier layouts |
| **THE DEPTHS** | 400 m | Cold violet stone, turn-heavy |

---

## The day-1 hook — what to look for

Best distance, best score, furthest zone and total runs persist across sessions
(PlayerPrefs, so they survive a browser refresh).

- `BEST 240 m` sits under the score the whole time you fly.
- **NEW BEST fires the instant you pass your record, mid-flight** — not on the
  results screen. The record is snapshotted at run start so it fires once, at the
  right metre, while you are still live and can extend it.
- The results card always frames the gap: *"60 m short of your best (180 m)"*.
- **R restarts in ~1.25 s** from a single keypress. The opening cinematic plays once
  per session; retries drop straight into flight.

---

## Where to look in the code

| Concern | File |
|---|---|
| Run orchestration, HUD, scoring, Deadeye, records | `Scripts/Game/FiredGameController.cs` |
| Flight, steering, ricochet, stall and fall | `Scripts/Game/BulletController.cs` |
| Endless level streaming, zones, wrong-way handling | `Scripts/Game/ModuleStreamer.cs` |
| Persistence (settings **and** records) | `Scripts/Core/SaveSystem.cs` |
| Speed gauge states, danger redline, leading cap | `Scripts/UI/SpeedGauge.cs` |
| Scoring targets placed by the designer | `Scripts/Game/FiredTarget.cs`, `LevelInfo.cs` |
| Palette and colour-meaning rules | `Scripts/Core/Theme.cs` |

---

## Architecture notes

**Scene flow** (also the Build Settings order):

```
Boot → StudioIntro → EpilepsyWarning → MainMenu → Game → (Credits → MainMenu)
```

Pause and the results screen are overlays inside `Game`, not separate scenes. Every
scene is independently playable — a persistent `GameManager` auto-spawns from
`Resources/GameManager.prefab` before the first scene loads, so nothing depends on
Boot having run.

**Never call `SceneManager.LoadScene` directly.** Always:

```csharp
GameManager.Instance.SceneFlow.LoadScene("SceneName");
```

That gives you the neon wipe transition and resets `timeScale` for free.

**UI lives in scenes, not in code.** The HUD used to be constructed at runtime with
`new GameObject(...)`, which made it impossible to edit in the Inspector. It is now
authored as real scene objects under `Canvas`, and the runtime scripts only *read*
`[SerializeField]` references. `Tools > FIRED > Bake Runtime UI` regenerates them
idempotently. Add new UI by authoring it in the scene and exposing a field — never
by building it in code.

**Levels are streamed from modules.** `ModuleStreamer` chains prefabs from
`Prefabs/Levels/` using entry/exit sockets declared in `ModuleInfo`. All three Kenney
kits share one socket grid (`corridor-wide` is 8×8 in every kit), so a cave module
chains onto a station module with no seam. Unused exits are sealed with a full-width
blocker so the player cannot leave the map.

---

## Editor tools — `Tools > FIRED`

| Menu item | What it does |
|---|---|
| Bake Runtime UI into Scenes | Rebuilds the HUD scene objects and rewires them. Safe to re-run. |
| Build Level Module Prefabs (all kits) | Regenerates level modules for all three kits |
| Build Example Level (into Game scene) | Rebuilds `Level_Example` and the zone wiring |
| Regenerate UI Sprites (safe) | Re-creates the generated UI sprites. Touches no scene. |
| Build Windows (playtest zip) | Standalone build to `../FIRED_Builds/Windows` |
| Build WebGL (itch.io) | Web build, itch-safe compression settings |
| **DANGER —** Rebuild Game Scene / Regenerate Menu Scenes | Destructive. Overwrites hand edits. Both confirm first. |

Builds are written **outside** the repo (`../FIRED_Builds/`) — there is no ignore
rule for build output.

---

## Building

For itch.io, zip the **contents** of the WebGL output so `index.html` sits at the zip
root, then tick *"This file will be played in the browser."* WebGL uses Brotli with
**decompression fallback enabled**, which matters: without it the build depends on the
host sending the right `Content-Encoding` header and otherwise hangs on the loading bar.

Bump **Project Settings > Player > Version** before each upload — it is stamped on the
main menu, bottom-left.

---

## Dev shortcuts

| Key | What | Where |
|---|---|---|
| F1 | FPS overlay | All builds |
| F12 | Screenshot to `persistentDataPath/Screenshots` | All builds except WebGL |
| Ctrl+R | Reload the current scene | Editor + development builds |
| K / L | Force Win / Game Over | Editor + development builds (`DevHotkeys.cs`) |

---

## Credits & licences

**Art** — Kenney ([kenney.nl](https://kenney.nl), **CC0**): Modular Space Kit, Modular
Cave Kit, Modular Dungeon Kit, Modular Buildings, Blaster Kit.
Quaternius ([quaternius.com](https://quaternius.com), **CC0**): Modular SciFi MegaKit.

**Audio** — Kenney (**CC0**). `ProceduralAudio.cs` generates fallback clips in code for
any empty slot, so the game is never silent even with no audio assets present.

**Fonts** — Bungee, Chakra Petch, Bangers (**SIL OFL**, licences included in
`Assets/Art/Fonts/`).

**UI sprites** — generated procedurally by the project's own editor tooling; no
third-party source.

Third-party asset licences are included in their respective folders under
`Assets/Level/` and `Assets/Gun/`.
