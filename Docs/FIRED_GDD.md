# FIRED. — Game Design Document v2

*Indie Connect Game Jam · Theme: INFINITE RUNNER · 48 hours · Unity 6 / URP / WebGL*

> **You are the bullet.** Fired from a gun in the opening frame, you never stop flying.
> Ride it in third person through a neon-frozen battlefield, ricochet to turn,
> X-ray through skeletons for points, and when you finally drop — watch your whole
> flight rewind back into the barrel.

**v2 changes:** third-person "backseat" camera · comic-burst style popups with
count-up scoring · skeletons + per-bone scoring replace skulls · rewind-to-gun
ending · art direction pivoted to cartoon-cyberpunk maximum-color.

---

## 1. Overview

| | |
|---|---|
| **Title** | FIRED. |
| **Pitch** | An infinite runner where you are the projectile. |
| **Genre** | Third-person infinite runner / flight-action |
| **Camera** | Chase cam "sitting on the back of the bullet" — bullet visible bottom-center, world impacts readable |
| **Platform** | WebGL (itch.io), mouse+keyboard primary, gamepad supported |
| **Build budget** | ≤ 75 MB compressed (jam cap 300 MB — stay far under) |
| **Session** | 60–120 s per run, restart ≤ 2 s |
| **Look** | Cartoon-cyberpunk, maximum color: neon pink/cyan/yellow on deep violet, comic-book impact language |

### Design pillars

1. **Never stop moving.** No mid-run interruptions; death flows into the rewind, the rewind flows into restart.
2. **The world is a frozen photograph.** Only the bullet (and its consequences) move.
3. **Speed is health.** One resource feeds everything.
4. **Every impact is a comic panel.** Shatters, bursts, BANG!-text, count-ups — impact legibility is the art direction.

---

## 2. Core loop

```
FIRED from the gun (2 s cinematic, skippable)
   ↓
FLY (chase cam) — steer, thread gaps, graze for style
   ↓
RICOCHET — shallow angle bounces; head-on = embed
   ↓
X-RAY — pass through a body: skeleton reveal, bone shatters, comic burst + count-up
   ↓
ESCALATE — tighter gaps, rarer bodies, denser glass
   ↓
STALL — speed hits zero: bullet drops, bounces, rolls, flips… stops on the ground
   ↓
REWIND — the entire flight path rewinds at high speed back into the gun barrel
   ↓
SCORE CARD — receipt of bones, panes, threads; Enter = FIRED again
```

---

## 3. Mechanics

### 3.1 Camera & flight (v2)

- **Third-person chase cam:** camera sits ~1.2 m behind and ~0.35 m above the
  bullet, loose-follow (position lag ~0.08 s, rotation lag ~0.12 s) so turns read.
  The spinning brass bullet is always visible lower-center — impacts (glass blooms,
  skeleton reveals) happen *around and past* it, fully readable.
- Camera pulls **closer at low speed** (danger intimacy) and **back + FOV up at high
  speed**. Slight orbital tilt during Deadeye so you see the bullet against the
  guide line.
- Mouse steers (pitch/yaw, capped ~90°/s); subtle roll into turns; bullet mesh spins
  on its long axis constantly (cheap life).
- **Speed economy** (tuning start points): base decay −4 m/s²; soft clip −8;
  hard graze −20 + screen crack; glass −2; ricochet −10%; **bone pass-through +25
  and up**; start 80, soft cap 120, **stall at 20 → drop**.

### 3.2 Ricochet

- Shallow (<35° to surface) auto-reflects: spark burst, 60 ms hit-stop, *ping*.
- Head-on: embedded in the wall = run ends (skips tumble, goes straight to rewind
  after a beat — camera holds on the embedded bullet, dust settles, then rewind).
- **Deadeye (hold RMB):** 15% time, neon reflection guide, drains meter; bodies
  refill it. The skill ceiling is chaining deliberate banks.

### 3.3 Skeletons & the X-ray reveal (v2 — replaces skulls)

Frozen humans populate the dioramas. Fly **through** one:

1. **X-ray flash:** the body's material swaps to a translucent neon-blue silhouette
   ("holo-flesh") and the **full skeleton child mesh is revealed** inside it.
2. **The bone you hit** tints hot red and swaps to its **cracked variant** (same
   bone mesh with a baked crack overlay + slight split) — *no fracture simulation*.
   Decision rationale: at 200 km/h a color-pop + crack read is 100% legible; shard
   physics would be invisible and cost a day.
3. **Comic burst popup** at the impact point (see 3.4) names the bone and counts up
   its score.
4. Speed refund + slow-mo meter refill + multiplier tick. The body stays in
   X-ray state behind you — a trail of glowing anatomy marks your flight line.

**Bone score table** (refund scales with score):

| Bone | Points | Speed refund | Note |
|---|---|---|---|
| **SKULL** | 500 | +30 m/s | The jackpot — heads sit on the hardest lines |
| SPINE | 300 | +25 | Vertical shot rewards |
| PELVIS | 200 | +20 | |
| RIBCAGE | 150 | +18 | Widest target, the bread-and-butter |
| ARM / LEG | 100 | +14 | |
| **PINKY** | 50 | +8 | Comedy bone — “PINKY! +50” is a marketing GIF |

- Multi-bone lines (through a wrist *and* the skull of the guy behind) chain in one
  slow-mo — the "X-RAY COMBO" moment.
- Placement rule: skull lines always require a setup (a ricochet or a glass pane
  first). Ribs are forgiving. Difficulty = anatomy placement.

### 3.4 Comic-burst style popups (v2)

Every scoring event spawns a **comic-book starburst** (jagged sparkle polygon) at
the impact point, containing:

- the event name in the comic font — **SKULL!**, **THREAD!**, **PANE!**, **BANK!**
- a number that **counts up from 0 to the awarded score** over ~0.4 s (tick audio
  rises in pitch as it counts — slot-machine feel)

Rules: bursts are world-anchored billboards that pop in at 0→1.15→1.0 scale,
tilt −8°…+8° random, live ~0.9 s, then pop out. During any **style event the game
drops to ~40% slow-mo for 0.25 s** so the burst and the count-up are readable at
speed. Bursts stack in a fan if events chain. Colors: yellow burst/pink text for
bones, cyan for style, green for combos.

### 3.5 Stall, tumble & THE REWIND (v2 — replaces plain death)

1. **Stall:** below 20 m/s the nose drops; control fades out; the bullet becomes a
   physics object for its final moment — it **bounces, rolls, flips** off whatever
   it hits (Rigidbody for ≤2.5 s max).
2. **Rest:** the instant the bullet is grounded and still (velocity < 0.1 for
   0.4 s), hold one quiet beat — dust, distant neon hum, tiny "clink."
3. **REWIND:** the entire run **rewinds along the exact flight path back into the
   gun barrel** at ramping speed (~6–10× with ease-in, long runs compressed to a
   max of ~4 s). VHS treatment: chromatic aberration spike, scanline overlay,
   reversed whoosh/squeal audio, popups un-popping, glass un-shattering
   (swap fractured → intact as the camera passes), skeletons un-revealing.
4. Rewind terminates inside the barrel → **muzzle flash** → score card slams in.
   Any key skips straight to the card.

Implementation: ring buffer records position/rotation (+ event indices) every
0.05 s during flight — the rewind is just playing the buffer backwards through the
camera; world un-swaps fire off the recorded event indices. Cheap, deterministic,
enormous wow.

### 3.6 Scoring

- Score = meters × multiplier + style/bone points (counted live via bursts).
- Score card = comic receipt: “**1,847 m** · 6 SKULLS · 2 PINKIES · 9 PANES ·
  stopped by: THE FLOOR”. Fires `GameEvents.RaiseGameOver` into the existing shell
  overlay; best run saved via `SaveSystem`.

---

## 4. World & level generation

Unchanged from v1 in structure (50 m hand-composed chunks, 4-ahead streaming,
pooled, 3 difficulty tiers interleaved with breathers, 2–3 aligned gates per chunk)
— but the setting pivots:

**Neon megacity block-war, frozen mid-chaos.** Standing set-pieces re-skinned:

1. **Alley market** — noodle stands, hanging holo-signs, civilians mid-duck
2. **Shattering storefront** — plate glass bursting around a drone crash
3. **The volley** — police mechs' frozen tracer wall to thread
4. **Glass tower atrium** — five parallel panes, pure style room
5. **Highway ribbon** — hover-car pileup frozen mid-flip, thread the gaps
6. **Neon sign canyon** — ricochet chain across giant kanji/glyph signs
7. **Steam vent street** — blind flight through frozen neon-lit steam
8. **The kingpin's convoy** — triple-skull line behind armored glass

Onboarding unchanged: the opening barrel cinematic is the tutorial; first chunk
telegraphs one gap, one pane, one ribcage line with a neon guide trail.

---

## 5. Art direction (v2 — cartoon cyberpunk, maximum color)

**"A Saturday-morning cartoon shot inside a neon war photo."**

- **Palette — neon on deep violet:** background #0D0221 → #2A0E4F; **hot pink
  #FF2A6D**, **cyan #00F0FF**, **electric yellow #FFE600**, **purple #7122FA**,
  **acid green #39FF14** (reserved for combos). Color-meaning rules: cyan =
  interactive/UI, pink+yellow = celebration (bones, title), red-pink = damage/death,
  green = combo. The world is dark violet so the five neons scream.
- **Shapes: chunky low-poly with fat black-ish outlines** (inverted-hull outline
  pass or rim-dark shader) → the "cartoon" read. Flat cel shading, no PBR.
- **Comic language everywhere:** starburst popups, screen-edge speedlines during
  slow-mo, halftone-dot pattern in dim overlays, onomatopoeia on big events
  (BANG!, KRSSH! on glass, OH NO! on stall).
- **Fonts:** **Bungee** (display — chunky urban neon, titles/menus), **Bangers**
  (comic — bursts/onomatopoeia only), **Chakra Petch** (UI/HUD numbers — squared
  techno). All Google Fonts OFL, downloaded into the repo with licenses.
- **Frozen VFX sculptures** re-tinted: explosion shells in pink/orange with yellow
  cores, steam in violet, tracers as cyan rods.
- **Post stack:** bloom UP (neons feed it), vignette violet, film grain swapped for
  a faint **halftone/scanline** feel, chromatic aberration pulsing with speed and
  spiking in the rewind.
- **Shell/menu unified:** cyberpunk city backdrop (synthwave sun, parallax neon
  skylines, drifting traffic streaks), Bungee wordmark in yellow→pink gradient,
  cyan text menu, and a **gunshot flourish on PLAY** (muzzle flash + BANG! burst +
  bang SFX) — the menu is the holster, the run is the shot.

---

## 6. Audio

| Layer | Content |
|---|---|
| Wind/flight | Whoosh pitch ∝ speed (the speedometer) |
| Deadeye | Ducked + heartbeat + shimmer |
| Ricochet | Ping + 60 ms hit-stop |
| Bone/X-ray | Wet *crunch* + rising count-up ticks + choir/synth stab; SKULL gets an extra sub-drop |
| Glass | Layered shatter, random pitch |
| Comic bursts | Count-up tick-tick-tick rising in pitch (slot machine) |
| Stall/tumble | Clink-clink-roll… silence… distant neon hum |
| **Rewind** | Reversed whoosh + tape squeal + all events re-triggering reversed |
| Music | Synthwave loop, 3 intensity stems keyed to multiplier (BeepBox/CC0; procedural pad fallback) |
| Menu | Gunshot bang on PLAY (procedural noise-burst + sub thump) |

---

## 7. Controls, HUD, shell integration

- **Mouse** steer · **RMB hold** Deadeye · **LMB** commit/skip · **Shift** barrel-roll
  dodge · **Esc/P** pause. Gamepad: RS steer, LT Deadeye, A commit, B roll.
- **HUD:** cyan speed arc around the bullet (empties toward stall), distance
  top-right (Chakra Petch), multiplier as big Bungee stamp that slams on change,
  comic bursts carry all other feedback. Nothing else.
- Shell hooks unchanged: `SceneFlowController`, `PauseMenu`, `GameOverScreen`
  (+stats lines), `SaveSystem`, `AudioManager` (+`SetMusicIntensity`, +bang SFX).

---

## 8. Asset production plan (v2)

**Golden rule:** flat cel material + shared neon gradient atlas + outline pass over
everything; any source model becomes "ours" instantly.

### 8.1 Sources by asset type

| Asset | Primary source | Notes |
|---|---|---|
| **Humans (frozen poses)** | **Mixamo**: 2 characters × 6 dramatic anims (run/dive/yell/fall/cover/throw), **frozen at one frame**, decimated ~1.5 k tris | Cyberpunk-ify with palette + neon trim strips (emissive quads glued to shoulders/visors — 5 min each) |
| **Skeletons (inside humans)** | Best: rig a CC0 low-poly skeleton in **Mixamo** (upload → auto-rig) and apply the **same animation frozen at the same frame** as its body → perfect nesting. Fallback: Quaternius skeleton character (CC0) posed manually | One skeleton per body pose; hidden child, revealed on hit |
| **Cracked bone variants** | Blender: duplicate bone mesh → knife-cut a jagged split + darken crack line via vertex color; slight 2 mm separation | Swap-in on hit; no physics |
| **Holo-flesh X-ray shader** | Unity: simple fresnel-rim transparent shader (URP Shader Graph, one node chain) in cyan | Body swaps to this material on reveal |
| **Gun (menu + barrel cinematic)** | Kenney Blaster Kit (CC0 — already sci-fi!) or 30 min Blender chunky revolver | Chunky cartoon proportions: oversize barrel |
| **Bullet (player, always visible now)** | Blender, 15 min: brass cone + rim grooves + emissive pink tip; spins constantly | It's the protagonist — give it a face? (sticker eyes = instant personality, test it) |
| **City kit** | **Kenney** city/roads + Quaternius buildings + neon signs as emissive quads with glyph textures (generate glyph texture procedurally or hand-paint 1 atlas) | |
| **Hover cars / mechs** | Poly Pizza CC0 "cyberpunk car", Quaternius mech/vehicle packs | |
| **Frozen explosion/steam sculptures** | Blender recipe from v1 (icosphere+displace, metaballs), re-tinted neon | |
| **Glass panes (intact + fractured)** | Blender **Cell Fracture**, unchanged from v1 — fractured swap + 10 cm push, frozen | Un-shatter during rewind (swap back) |
| **Comic burst sprites** | Procedural starburst polygon (already generatable in our builder) + Bangers text | |
| **SFX** | freesound CC0 + jsfxr; count-up ticks & bang procedural (our `ProceduralAudio`) | |
| **Music** | BeepBox synthwave stems / CC0; procedural pad fallback | |

AI text-to-3D (Meshy/Tripo) remains a hero-prop-only option — check the jam's
AI-disclosure rule first; CC0 + cel shading is usually faster than AI cleanup.

### 8.2 The body+skeleton alignment pipeline (the key v2 asset trick)

1. Mixamo: auto-rig the human AND the skeleton model (upload skeleton FBX).
2. Apply the **identical animation** to both; export both.
3. Blender (Blender MCP can batch this): scrub both to the **same frame**, freeze,
   decimate, export as one paired FBX (`Body_Dive.fbx` containing Body + Skeleton
   meshes, aligned).
4. Unity prefab per pose: Body (cel material) + Skeleton child (disabled) +
   per-bone trigger colliders (sphere/capsule per scoring zone: skull, spine,
   pelvis, ribs, limbs, pinky) tagged with bone type.
5. On pass-through: enable skeleton, swap body → holo-flesh, tint + swap hit bone
   to cracked variant, spawn burst. Whole effect is material/mesh swaps — WebGL-safe.

### 8.3 Rewind tech (asset-free)

Ring buffer (pos/rot @ 20 Hz + event log). Rewind = camera replays buffer reversed
at ramped speed; each passed event index un-fires its swap (fractured→intact,
X-ray→body). Post-FX spike + reversed audio. ~150 lines total.

---

## 9. 48-hour schedule (v2 deltas)

Same skeleton as v1 (playable-ugly by hour 8, feature freeze at 40) with swaps:

- Hours 2–6 (code): chase cam replaces FPS cam (lag-follow + FOV/pull-back curve).
- Hours 8–14: X-ray reveal system (material/mesh swaps + bone colliders) replaces
  skull swap; comic burst popup component + count-up.
- Hours 14–20 (art): body+skeleton Mixamo pipeline batch; neon re-tint pass.
- Hours 20–24: stall→tumble physics + **rewind system** (protect this — it's the
  ending judges remember).
- MoSCoW moves: rewind = **Must**; tumble physics = Should (fallback: bullet just
  drops flat); bullet face sticker = Could; onomatopoeia set beyond BANG/KRSSH = Could.

### Risks (v2 additions)

| Risk | Mitigation |
|---|---|
| Chase cam clips through walls | Camera collision spherecast + snap-in; chunks authored with 1 m camera clearance behind flight lines |
| Skeleton alignment drift | Same-frame freeze from identical Mixamo anim = aligned by construction; verify once per pose |
| Rewind desync (world un-swaps wrong) | Event log stores exact buffer indices; rewind is data playback, never re-simulation |
| Count-up popups tank WebGL perf | Pool 12 bursts; TMP text, no per-frame layout |

---

## 10. Judging criteria mapping (v2)

| Criterion | Our answer |
|---|---|
| Creativity | You are the projectile; the world is one frozen instant |
| Gameplay | Speed-is-health economy + ricochet/Deadeye skill ceiling + bone-hunting lines |
| Innovation | X-ray anatomy scoring; the full-run rewind-to-the-gun ending |
| Technical | Chunk streaming at 120 m/s in WebGL; path-record rewind; paired-mesh X-ray |
| Visual | Cartoon-cyberpunk neon dioramas, comic bursts, glass blooms |
| Experience | 2 s restart, receipt scoring, PINKY! +50 |
