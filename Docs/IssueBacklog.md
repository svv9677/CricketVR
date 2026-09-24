# CricketVR — Issue Backlog

**Opened 2026-09-22 against `d52e98a`. All five reported issues plus one found during the work
addressed the same day; working tree is uncommitted and awaiting Rao's Editor test and device build.**

Five gameplay/visual issues raised by Rao after the Unity 6 / OpenXR migration, plus a sixth
(missing boundary detection) surfaced by the issue-1 verification.

Companion documents: [RevivalPlan.md](RevivalPlan.md) · [SceneRealismPlan.md](SceneRealismPlan.md) ·
[TechnicalDesignDocument.md](TechnicalDesignDocument.md)

> **Evidence standard.** Every root cause below was first read out of the project files, then
> **confirmed in the live Unity Editor (6000.3.24f1) over MCP** before anything was changed.
> The "Verified" column says exactly how far each fix was proven. Nothing here has been run on a
> Quest yet — that is Rao's next step.

| # | Issue | Root cause | Verified | Status |
|---|---|---|---|---|
| 1 | Ball physics wacky | No object in `CricketVR.unity` carried the `Ground` tag, so two `Ball.cs` branches were dead | **Play mode, simulated bounce** | ✅ Fixed |
| 2 | Player scale ≠ fielders | Rig root at `y = 2.65` with no floor-referenced tracking origin | **Play mode: camera at 1.700 m** | ✅ Fixed |
| 3 | Bat orientation wrong | Euler degrees typed into a `Quaternion`'s raw x/y/z/w + hand anchors not tracked | Editor only — **needs a device build** | ✅ Fixed, needs device confirmation |
| 4 | Ground texture LOD | Aniso off, base map capped to 1024, no high-frequency detail | **Rendered comparison shots** | ✅ Fixed |
| 5 | Crowd slants / no animation | Crowd painted across a 27.4° raked cone | **Rendered comparison shots** | ✅ Fixed; animation needs motion to judge |
| 6 | Boundary detection absent (found during this work, pre-existing) | No boundary object existed; `Main.theBoundaryCollider` unassigned | **Play mode: fires at 56.98 m** | ✅ Fixed |

---

## 1. Ball physics — FIXED

### Root cause (confirmed live)

`Ball.cs` gated its two most important collision branches on conditions that could never be true in
`CricketVR.unity`:

- [Ball.cs:167](../Assets/Scripts/Ball.cs:167) required `tag == "Ground"` — **every one of the 32
  objects in the scene was `Untagged`**. The `Ground` tag existed in `TagManager.asset` but was only
  applied in `Nets.unity`.
- [Ball.cs:203](../Assets/Scripts/Ball.cs:203) required `name == "Plane"` — **no such object exists**.

Consequences, all of which reproduce identically in the Editor (which is why Rao saw it there too):

| Symptom | Mechanism |
|---|---|
| Ball curves sideways for the *whole* flight, including after pitching | `applySwing = false` lived inside the dead branch, so the lateral swing force at [Ball.cs:118](../Assets/Scripts/Ball.cs:118) never stopped |
| Leg-spin and off-spin did nothing off the surface | The entire pitch-turn block was in the same dead branch |
| Stopped balls never reset | `bounced` was only set in the dead `"Plane"` branch, and the dead-ball reset requires it |

### What changed

- Tagged `Pitch Base`, `Pitch Markings` and the outfield `NMD_stD_Ground00_0` as **`Ground`**.
- `Ball.cs`: both branches now use `CompareTag("Ground")`.
- `Bat.cs:283`: `trackerVelocity` was a per-**frame** displacement used as a velocity, making every
  shot frame-rate dependent. It is now divided by `Time.deltaTime`, with a documented
  `SwingTuningReferenceDeltaTime = 1/72` applied at the use site so **shot power is unchanged at
  72 Hz on device** while no longer varying with framerate. This is what makes Editor testing
  representative of the headset.
- Removed the dead `ballMomentum` / `batMomentum` lines — the bat rigidbody is teleported every
  `LateUpdate`, so both were always zero and neither was ever used.

### How it was verified

Driven in Play mode with `Physics.Simulate` rather than by eye:

```
BEFORE: fresh=True  bounced=False applySwing=True
  first-bounce branch fired on step 24 at y=0.070
AFTER : fresh=False bounced=False applySwing=False
PASS - the ball registered its pitch bounce and swing was switched off.

BEFORE: bounced=False
  bounce registered on step 35 at y=0.003
AFTER : bounced=True
PASS - outfield bounce now registers, so the dead-ball auto-reset can fire.
```

### Second root cause, found 2026-09-22 from a device capture — THE BIG ONE

The tag fix above was real but it was **not** what made the ball look wrong. A device video showed
the ball pitching almost at the bowler's feet and flying into the stands. Instrumenting a real
Editor delivery found this at release:

```
=== DELIVERY === cfg: InSwing, speedX=7.18, length=3.97
  ball kinematic=False pos=(-7.83, 1.43, 0.06) vel=(7.64, -11.66, 0.40) |v|=13.95
```

**The ball already had 13.95 m/s before the delivery impulse was applied.**

While it is parented to the bowler's hand the ball is kinematic, and a kinematic body carries its
motion across the switch to dynamic. `Main.cs` zeroed the velocity at line 1046 — **while the body
was still kinematic**, which is a no-op; `isKinematic = false` only happened six lines later. So the
bowler's hand-swing velocity (mostly *downward*, from the arm coming over) was added on top of the
intended impulse.

A controlled experiment, same impulse, only the inherited velocity differing:

| | release vy | pitches at | target |
|---|---|---|---|
| as shipped | −14.42 m/s | **x = −3.85** | 3.97 |
| velocity genuinely zeroed | −2.78 m/s | **x = 3.27** | 3.97 |

Every ball left the hand ~5× too steep, pitched **~8 m short** near the bowler's feet, and reached
the batsman chest-high at ~165 km/h. That is the "wacky ball physics".

**Fix:** *assign* the release velocity instead of `AddForce`-ing onto whatever the body carried:

```csharp
theBallRigidBody.linearVelocity = speed / theBallRigidBody.mass;
```

Assignment overwrites, so it is immune to the carry-over (zeroing after `isKinematic = false` was
tried first and did **not** reliably stick). It is exactly the intended impulse from rest. The
author had this very line in the file, commented out.

Verified on a real Editor delivery: release `vel=(34.88, -0.40, 2.26)` = exactly `speedX/mass`, and
the ball pitched at **x ≈ 7.3** against a target length of 8.58, arriving at 31 m/s instead of 52.

### Third pass, 2026-09-23 — the delivery set-up rebuilt

Patching the old release path kept failing on device, so it was replaced outright with
[BallDelivery.cs](../Assets/Scripts/BallDelivery.cs). The old path had three compounding faults:

1. **The release point was an animation bone.** Whatever the bowler's hand happened to be doing on
   the frame the animation event fired became the start of the trajectory. Measured once at
   `(1.56, 4.05, 6.08)` - mid-pitch and 4 m in the air.
2. **The solver divided by an unchecked flight time.** Once the release point was past the target
   pitching point, `time` went negative and the vertical velocity flipped sign - firing the ball
   upward at ~42 m/s. That is the "sky high into the stands".
3. **It mixed feet and metres**, and `height - startPos.y / time` was missing the parentheses to
   mean `(height - startPos.y) / time`.

`BallDelivery.Solve` replaces all of it with one exact projectile solve in metres:

```
t  = (pitchX - releaseX) / speed
vy = (bounceHeight - releaseY + 0.5*g*t*t) / t
```

plus validation of every input: the release point is clamped into a plausible box (falling back to
a nominal release point if it is wild or NaN), speed is clamped to 15-45 m/s, the pitching point is
clamped onto the strip, and the ball must still be far enough behind it to be a real delivery.
Anything corrected is logged with the offending value.

**Verified analytically across every config in `Constants.cs`** - error 0.0000 m on all of them:

| type | speed | release vy | pitches at | error |
|---|---|---|---|---|
| pace | 140-155 km/h | -6.06 .. -1.91 | exactly as asked | 0.0000 |
| in/out swing | 124-140 km/h | -7.20 .. -1.33 | exactly as asked | 0.0000 |
| leg/off spin | 68-85 km/h | +0.92 .. +1.75 (looped, correct) | exactly as asked | 0.0000 |

and the four release points that actually broke on device now all produce a correct delivery:

```
hand=(1.56, 4.05, 6.08)  ->  vy -7.94, pitches x=4.00   (was vy +42, into the stands)
hand=(16.00, 1.50, 0.00) ->  vy -3.50, pitches x=4.00
hand=(10.20, 1.00, 0.00) ->  vy -1.80, pitches x=4.00
hand=(NaN,  1.00, 0.00)  ->  falls back to nominal, pitches x=4.00
```

Confirmed end-to-end on real Editor deliveries: release `(35.789, -2.203, 2.588)` - vx exactly
`speedX/mass` - pitching on `Pitch Markings` and carrying to the keeper at ~30 m/s, repeatably.

**Also fixed:** `StopTheBall()` set `isKinematic = true` *before* zeroing the velocity, which Unity
warns about ("Setting linear velocity of a kinematic body is not supported") and which is the same
no-op that let motion leak between deliveries. Zeroing now happens first.

**Bat orientation corrected too:** the grab offset is now `(270, 180, 0)`, solved so the bat
reproduces its authored pose - `up = (-1, 0, 0)`, face pointing down the pitch. It was
`(21.18, -0.5, 9.98)`, which gave `up = (-0.354, 0.918, -0.176)`, i.e. **92% vertical**, so every
contact launched the ball skyward. Exact only for a level controller; still needs `BatOffsetTuner`
on device to match a real grip.

### Why the device looked worse than the Editor

The shot direction is driven entirely by the **bat's `transform.up`**
([Bat.cs:201](../Assets/Scripts/Bat.cs:201), [:211](../Assets/Scripts/Bat.cs:211)), then multiplied
by `BatAmplifier = 75`. In the Editor the hand anchors are untracked, so the bat never moves and
never touches the ball. On device it does. Measured: with a level controller the current grab offset
gives `bat.up = (-0.354, 0.918, -0.176)` — **92% vertical**, so any contact launches the ball nearly
straight up at ~40 m/s. That is the 158 m / 185 m / 321 m readouts in the capture, and it is
**issue 3, not a physics bug**. Dial the offset in with `BatOffsetTuner` and it goes away.

### Also noted, not changed (one fix at a time)

- **`GetYVel` operator precedence**, [Main.cs:1336](../Assets/Scripts/Main.cs:1336):
  `(height - startPos.y / time)` should be `((height - startPos.y) / time)`. Real, but worth only
  ~1 m of length error, so it is left for a separate change rather than bundled into this one.
- **Spurious boundary exits.** Teleporting the ball fires `OnTriggerExit` on the boundary capsule —
  seen at radius 7.5 m and 15.4 m in the logs. Harmless today because `BoundaryCollider` only acts
  in `InGame_BallHitLoop`, but keep that guard.
- **`Assets/Scripts/BallDiagnostics.cs`** is the temporary instrumentation used to find this
  (per-delivery config, release velocity, every contact with before/after velocity). Not attached to
  anything; attach it to `Main` and `BallContactLogger` to the `Ball` for the next device round,
  then delete.

### Fourth pass, 2026-09-24 — the real root cause, found from device logcat

The third pass made the *maths* of the delivery exact but still fed it a release point read off
the ball's own transform. On device that reading was garbage from the second delivery onwards.

**How it was finally caught.** The in-world diagnostics board is unreadable in a 1080p headset
capture (≈11 px per line), so decoding the video was a dead end. Instead, with the Quest attached
over USB:

```bash
adb logcat -c && adb logcat -v time -s Unity:I > dev.txt
```

That gives the full `Debug.Log` stream off the device, and it named the fault immediately:

```
runup: ballPos=(-11.48, 9.92, 3.73) ballLocal=(-8.382, 6.285, -3.876) parent=Offset
[Delivery] ... [corrected: release (-1.36, 7.11, 8.10) outside the plausible box
                -> (-5.00, 3.00, 2.50). pitch x -1.31 is only 3.69 m ahead of release]
```

The ball was parented to the bowling hand **12.3 m away from it**, orbiting on that arm through
the whole run-up. At release its transform read `(-1.36, 7.11, 8.10)` — mid-pitch, seven metres
up and eight metres to the leg side. `BallDelivery` then clamped that to the corner of its
plausible box, which is both the "snap to a different position" and the "bounces outside the
pitch and goes sky high" the videos showed. Delivery 1 was always fine; 2 onwards never were.

**Root cause: a `Transform` write on an interpolating kinematic `Rigidbody` is discarded.**

`AnimatedBowler` carried the ball by parenting it to the hand bone with
`SetParent(hand.transform)`, which keeps world position and therefore hands the ball a large
local offset — the bowler is teleported to the top of his run-up in the same LateUpdate, so the
offset was ~12 m. A line at the bottom of `LateUpdate` was supposed to erase it:

```csharp
if (ball.transform.parent != null && ball.transform.parent.gameObject.name == hand.name)
    ball.transform.localPosition = Vector3.zero;
```

The ball is kinematic while carried and its Rigidbody has interpolation enabled, so Unity
overwrites the Transform from the Rigidbody's own pose before rendering, and
`Physics.autoSyncTransforms` is `false` in this project, so nothing pushes the Transform back
into PhysX. The write was thrown away. Parenting hid the damage — the hierarchy kept re-applying
whatever stale local offset the ball had, so the ball still *moved with* the hand and looked
roughly right, it just sat 12 m from it.

This was measured directly. Replacing the parenting with a plain
`ball.transform.position = hand.position` and nothing else made the discard visible as a lag that
grew with arm speed:

```
gap=0.449m -> 0.840m -> 1.536m -> 1.921m
```

**Fix, two independent halves.**

1. `AnimatedBowler.HoldBall` (new) carries the ball by driving the **Rigidbody**, with
   interpolation switched off while it is held, and runs *first* in `LateUpdate` so nothing below
   it can skip the carry by throwing. Parenting is gone, so there is no local offset to lose and
   no name comparison to get wrong. `Main` restores `Interpolate` at release.
2. `AnimatedBowler.ReleaseBall` — the animation event itself — captures `ReleasePosition` from the
   hand at the exact frame the ball leaves it, and `Main` solves the delivery from *that*, not
   from the ball. Even when the carry failed completely (ball 1.2 m adrift) the delivery still
   started from the correct hand position, so the two halves cover each other.

`Main` now also places the ball on the solved release point unconditionally. That is safe because
the point comes from the hand: on a healthy delivery the ball is already there and it moves by
nothing, so there is no snap.

**`ReleaseBoxMin.y` lowered 0.80 → 0.50.** Measured release heights are 0.63–1.97 m depending on
where the arm is; 0.80 was clamping real deliveries and nudging the ball upward.

### How it was verified

Editor, capped to 72 fps with `Time.fixedDeltaTime = 0.007` so the physics/render ratio matches
the Quest, auto-bowling repeatedly:

| | before | after |
|---|---|---|
| ball-to-hand gap through the run-up | 0.45 → 1.92 m, growing | 0.012 – 0.135 m |
| release point, successive deliveries | y 0.63 → 1.66, x −7.65 → −8.58 | (−8.45, 1.97, 0.29), (−8.43, 1.97, 0.29) |
| `[Delivery]` corrections logged | every delivery clamped | none |

The residual 13 cm is a sampling artefact: the diagnostic reads in `Update`, one animation frame
before `LateUpdate` re-pins the ball. The release height rose to a plausible 1.97 m because the
point is now sampled at the animation event, when the arm is at the top, instead of from a
transform lagging behind it.

### Note on the earlier "device-only" framing

It was never device-only in principle — the same discarded write happens in the Editor. The
Editor just lost centimetres where the device lost metres, because the size of the discard
depends on the render/physics step ratio. Chasing it as an Editor-vs-device difference was a
detour; pulling the device's own log was what settled it.

### Still open (deliberately not changed)

The tuning below was almost certainly compensating for the bugs above. **Re-judge it now, before
touching any of it** — `Pitch.physicMaterial` bounciness `0.7` with combine `Maximum` (a real ball
off turf is nearer 0.3–0.5), `Fixed Timestep 0.007` (≈143 Hz, reads like a tunnelling band-aid),
contact offset `0.001`, and three inconsistent ball radii (collider 0.0495 m, renderer 0.046 m,
drag formula 0.0575 m, against a real 0.036 m).

---

## 2. Player height — FIXED

### Root cause (confirmed live)

The rig was never converted to an `XROrigin`. It is still an instance of the vendored
`OVRPlayerController.prefab`, renamed by [MigrateOVRToXR.cs](../Assets/Editor/MigrateOVRToXR.cs).
The migration **removed `OVRCameraRig`** — the component that used to put the rig in a
floor-relative tracking space — and replaced it with nothing but a `TrackedPoseDriver` on
`CenterEyeAnchor`. The rig root had been lifted to `y = 2.65` to compensate.

Measured in the Editor: **camera at y = 2.650**, fielders **1.86 m** tall. The player's eye was
roughly 0.8 m above a fielder's head.

> **Checked first, because it would have been easy to get backwards:** the world is *not* built
> oversized. Stump-to-stump measures **20.66 m** against a real 20.12 m, so the ground is 1:1.
> It is the props that are inflated (stumps ×1.25, ball ×1.38, bat mesh ×1.38) and the camera had
> been raised to match them, while the fielders stayed at true human scale. That is precisely the
> mismatch Rao reported. `ReferenceShots.cs` independently corroborates the target: its calibrated
> batsman viewpoint sits at **eye height 1.7 m**.

### What changed

- Rig root `y` → **0**.
- New [XRRigSetup.cs](../Assets/Scripts/XRRigSetup.cs) on the rig: requests a **floor-relative
  tracking origin** from every XR input subsystem (retrying for 120 frames, since subsystems are
  not always running at `Start`). With a floor origin every player gets their own correct height
  for free. When there is no headset — plain Editor play — it holds the camera at a configurable
  1.7 m so the Game view matches the device.
- `CharacterController` was **height 5, radius 0.05** (a 5 m tall, 10 cm wide capsule); now
  1.8 / 0.3 with centre `(0, 0.9, 0)`.

### How it was verified

```
camera world Y = 1.700   rig root Y = 0.000
fielder top Y  = 1.869   player eye is -0.169 m relative to fielder head
```

−0.169 m is right for a person of the same height as the fielders.

> **Not yet proven:** the floor-origin path itself. In the Editor `HasFloorOrigin` is `False` and
> the 1.7 m fallback is what runs. On a Quest it should log
> `[XRRigSetup] Floor tracking origin active` — **worth checking that line appears in logcat.**
> If a runtime ever refuses a floor origin, the component falls back to the fixed height and warns.

---

## 3. Bat orientation — FIXED (needs device confirmation)

### Root cause (confirmed live)

Two independent faults.

**(a) Euler degrees typed into a `Quaternion`'s raw components.** `Bat.prefab` held
`leftGrabOffsetRotation: {x: -35.4, y: 0.7, z: 14, w: -5}` — magnitude **38.4**, where a unit
quaternion is 1. The scene override for the right hand had `w` nudged to `1.26`, which is the
tell-tale of someone dragging all four sliders trying to make it look right.

`Transform.rotation` normalises on assignment, so the bat got a real rotation — just an arbitrary
one. Measured in the Editor:

| | Applied before | Applied now |
|---|---|---|
| Right hand | **(6.6°, 129.5°, 179.6°)** | (21.2°, −0.5°, 10.0°) |

The bat was yawed 130° and rolled essentially upside down.

**(b) The hand anchors were not tracked at all.** `OVRCameraRig` used to drive them; after it was
deleted the scene contained exactly **one** `TrackedPoseDriver`, on `CenterEyeAnchor`.
`XRControllerTracker.cs` had been written for this job and was **attached to nothing**.

### What changed

- `Bat.cs`: the serialized offsets are now `Vector3 leftGrabOffsetEuler` / `rightGrabOffsetEuler`
  in **degrees**, applied via `Quaternion.Euler`. Seeded with the angles the author meant —
  left `(-35.4, 0.7, 14)`, right `(21.18, -0.5, 9.98)` (prefab default `(21, 1.05, 10)`).
- `XRControllerTracker` attached to `LeftHandAnchor` and `RightHandAnchor` with `isLeftHand` set.
- New [BatOffsetTuner.cs](../Assets/Scripts/BatOffsetTuner.cs) on the Bat, **disabled by default**.

### The in-VR iteration loop

Tick **`Enable Tuner`** on the Bat's `BatOffsetTuner` and build. In the headset:

| Control | Effect |
|---|---|
| Off-hand thumbstick X/Y | rotate around the two axes of the current pair, 30°/s |
| Bat-hand thumbstick X/Y | slide along those axes, 6 cm/s |
| **A** | cycle the axis pair: XY → YZ → ZX |
| **B** | dump position + euler to the console *and* the overlay |
| **Y** | reset to the values it started with |

Live values render on the existing `TestDisplay` overlay, so you can read the six numbers off
without leaving VR. Send them over and I will write them into the prefab. **One build, then as many
attempts as you like** — this is what stops issue 3 costing an APK per guess.

### Also found

The bat's hand parents are **"Missing Prefab" placeholders** (guids `e6ba3498…`, `ba1fa4a8…`, which
resolve to nothing) — the hand/glove models deleted in the P2 cleanup. They sit at local identity so
the bat attaches correctly, but **the player has no visible hands.** Not fixed; flagged.

Separately: the bat *mesh* is 1.34 m long while its `BoxCollider` is 0.90 m, so the outer third of
the visible blade has no hit box. Worth a look once the orientation is settled.

---

## 4. Ground texture LOD — FIXED

> ⚠️ **Two corrections to this document's first draft**, both caught by inspecting the live scene.
> The outfield does **not** use `ground.mat` — that material is unused in this scene. It uses
> **`Ground00_baseColor.mat`**. And raising the base map's UV tiling, which the first draft
> recommended, would have been **wrong**: the texture is a whole-ground map with the pitch
> rectangle painted into it, so tiling it would repeat the pitch across the outfield.

### Root cause (confirmed live)

| Finding | Measured |
|---|---|
| Anisotropic filtering effectively off | `aniso = 1`, `Bilinear`. Android runs quality **Ultra**, whose `anisotropicTextures` is *Per Texture* — so the texture's own value wins, and it was 1. A near-horizontal plane at a grazing angle is the textbook worst case. |
| Base map crushed to 1024 | Source is **4096²**, but the Android platform override was **not enabled**, so the default cap of 1024 applied. Format was ETC2, not ASTC. |
| No high-frequency detail | 1024 px over ~135 m ≈ **13 cm per texel**. There was simply no fine detail in the texture to show up close. |
| No mesh LOD involved | Zero `LODGroup`s in the scene — this is a texture/filtering problem, not a mesh one. |

### What changed

- `Ground00_baseColor.png`: aniso **1 → 8**, Bilinear → **Trilinear**, Android override enabled at
  **2048** and **ASTC 6×6** (was 1024 / ETC2). Four times the texels, better format, same budget.
- Generated **`GrassDetail.png`** — a high-passed, mean-normalised grain map built from the existing
  tileable `grass.jpeg`. Its mean is **exactly 0.5**, so it adds close-up grass structure through
  `_DETAIL_MULX2` **without tinting or darkening the ground**. Using `grass.jpeg` directly would
  have multiplied green by green and turned the outfield dark.
- Wired into `Ground00_baseColor.mat` as `_DetailAlbedoMap`, tiled **(82, 164)** — computed from the
  real mesh size (98.7 m per UV unit) to land on **1.20 m per repeat** with square texels.

Compare `Docs/Baseline/fixes-2026-09-22-boundary.png` against `phase7-optimized-boundary.png`.

### Worth a separate look

**Android's default quality level is Ultra**, which is a heavy choice for a Quest 3 (`lodBias: 2`,
no mip limit). Not the cause of this bug, but probably not what you want either.

---

## 5. Crowd — FIXED

### Root cause (confirmed live)

The stands were a smooth conical band with the crowd atlas stretched across the slope. Measured from
the mesh: the rake runs from radius 60.5 m at y 0.5 to radius 87.5 m at y 14.5 — **27.4° from
horizontal**. Every spectator was therefore painted lying on that slope. No texture edit can fix
that; the geometry has to be vertical.

### What changed

**New [CrowdStandBuilder.cs](../Assets/Editor/CrowdStandBuilder.cs)** (menu: **Tools → CricketVR →
Rebuild Crowd Stands**). It reads the rake profile off whatever mesh is currently assigned — so the
stand stays in exactly the same volume — and rebuilds it as discrete rows of **genuinely vertical
quads**, each carrying one row of the atlas:

| | Before | After |
|---|---|---|
| Lower | 390 v / 640 t, one raked cone | 7 680 v / 3 840 t, **20 vertical rows** × 96 segments |
| Upper | 325 v / 512 t | 6 144 v / 3 072 t, **16 vertical rows** × 96 segments |

Rows overlap vertically (1.85× the step rise) so there are no see-through gaps from pitch level.
~7 k triangles total is nothing for a Quest 3.

> The builder **verifies its own winding** against the radial direction and flips the buffer if it
> came out inside-out. I got this backwards on the first run and the entire crowd silently vanished
> — the check makes that failure impossible to repeat.

**New [CrowdStand.shader](../Assets/Shaders/CrowdStand.shader)** — URP **Unlit** (was Lit; the crowd
is in shade under the roof and per-pixel lighting on that much screen area is the wrong place to
spend a tile GPU's budget), SRP-Batcher compatible, GPU instancing enabled. All motion is in the
vertex shader at roughly 10 ALU:

| Effect | How | Default |
|---|---|---|
| **Idle bob** | Per-quad random phase baked into vertex colour `.r`, so each column moves on its own beat instead of the whole ring pulsing together | **On** — 3.5 cm at 0.8 Hz |
| **Mexican wave** | Travelling gaussian around `_WavePhase`, using the angle baked into `.b`, with a slight per-row lag from `.g` | **Off** |
| **Night camera flashes** | Per-person hash against a time bucket; costs nothing while `_FlashStrength` is 0 | **Off** |

### Driving the wave

Nothing drives `_WavePhase` yet — it is deliberately left for you to hook into `Main`'s state
machine. On a boundary or a wicket:

```csharp
// once, cached
static readonly int WavePhaseId   = Shader.PropertyToID("_WavePhase");
static readonly int WaveEnabledId = Shader.PropertyToID("_WaveEnabled");

// then drive it for ~2.5 s, sweeping phase 0 -> 2*PI
crowdMaterial.SetFloat(WaveEnabledId, 1f);
crowdMaterial.SetFloat(WavePhaseId, Mathf.Lerp(0f, Mathf.PI * 2f, t));
```

Flip `_FlashStrength` to ~2 when `eStadiumMode.Night` is active for the packed-stadium look.

### Also cleaned up

`CrowdLower.asset` / `CrowdUpper.asset` (the pre-cone meshes) are now unreferenced, as are the
`…Cone.asset` pair. Left on disk deliberately — delete them once you are happy with the new stands,
per [check-asset-references-before-deleting].

---

## 6. Boundary detection was entirely missing — FIXED (found 2026-09-22, pre-existing)

Surfaced as 9 identical exceptions in the Editor console during the issue-1 physics test:

```
UnassignedReferenceException: The variable theBoundaryCollider of Main has not been assigned.
  AnimatedFielder.GetInterceptPoint ()      (Assets/Scripts/AnimatedFielder.cs:729)
  AnimatedFielder+<CalculateInterceptTime>  (Assets/Scripts/AnimatedFielder.cs:289)
```

**Pre-existing, not a regression** — `theBoundaryCollider: {fileID: 0}` is unassigned at `HEAD`
too, and the field appears nowhere in today's scene diff.

Three facts, all confirmed in the live Editor:

- `Main.theBoundaryCollider` ([Main.cs:51](../Assets/Scripts/Main.cs:51)) is **unassigned**.
- [BoundaryCollider.cs](../Assets/Scripts/BoundaryCollider.cs) is attached to **nothing** — its
  script GUID appears in no scene or prefab.
- There is **no boundary object in the scene at all**. The only `CapsuleCollider`s are the six
  stumps; nothing named "boundary" exists.

Two consequences:

1. **Fours and sixes are never detected.** `BoundaryCollider.OnTriggerExit` is the only code that
   sets `eGameState.InGame_BallPastBoundary`, so a hit ball just runs on forever.
2. **Every fielder chase throws.** `AnimatedFielder.GetInterceptPoint()` dereferences
   `Main.Instance.theBoundaryCollider.GetComponent<CapsuleCollider>()` at lines
   [695](../Assets/Scripts/AnimatedFielder.cs:695) and
   [729](../Assets/Scripts/AnimatedFielder.cs:729), so the intercept coroutine dies on the first
   frame the ball is hit.

### The radius

Rao asked for the boundary to match **the red line painted on the ground**, just inside the ad
boards. Rather than eyeball it, the line was measured: an orthographic top-down render was clipped
to a thin slice around ground level (so the boards and stands could not contribute), pure-red pixels
were extracted, and a least-squares circle was fitted through them.

```
pure-red pixels: 12561
best-fit circle: centre (1.38, -1.52)   radius 56.64 m   rms residual 0.435 m
ad board ring:   Oppo/Vivo 58.9 m, ASP_paint 59.5 m
```

**Radius 56.64 m** — 2.26 m inside the ad boards, which matches "just before the ad board and
stands". The painted line's own centre is 2.05 m off the world origin, but the trigger is
deliberately centred on the origin anyway, because
[AnimatedFielder.cs:696](../Assets/Scripts/AnimatedFielder.cs:696) computes its intercept as
`velocity.normalized * localScale.x * collider.radius` — a vector **from the origin**, with no
centre offset. Matching that convention keeps the fielders' intercept maths consistent; the 2 m
discrepancy is smaller than the line's own width.

### What was added

`Main/Colliders/BoundaryCollider` — layer **Default** (the layer the Ball on layer 6 collides with,
matching the existing wide/keeper triggers), `localScale` 1 (the fielder maths multiplies by it):

| | |
|---|---|
| `CapsuleCollider` | `isTrigger`, radius **56.64**, direction **Y**, centre zero |
| height | **200 m** |
| `BoundaryCollider` script | attached |
| `Main.theBoundaryCollider` | assigned |

Height 200 is deliberate. A capsule's ends are hemispherical, so the height must comfortably exceed
`2 × radius` (113.3 m) for a true cylindrical section to exist. At 200 m the cylinder spans
**y = −43.4 … +43.4 m**, well above any shot, so the boundary radius does not shrink for lofted
balls. At a snugger height the caps would pull a six's boundary several metres inward.

### How it was verified

In Play mode, ball settled inside the trigger first, then driven out under its own motion:

```
settled at (0.00, 3.00, 0.00)  state=InGame_Ready
  boundary fired at radius 56.98 m, height 7.57 m (t=2.09s)
final state = InGame_BallPastBoundary
```

56.98 m against a 56.64 m collider — the 0.34 m overshoot is one physics step of travel at ~35 m/s.
The console is now **clean**; the nine `AnimatedFielder` exceptions are gone.

> **One behaviour worth knowing.** Teleporting the ball while it is inside the trigger generates a
> spurious `OnTriggerExit`. That is harmless here: `StopTheBall()` is only called from the
> `InGame_Ready` case, and `BoundaryCollider` only acts in `InGame_BallHitLoop`, so the existing
> state guard already blocks it. Keep that guard if the reset path is ever reworked.

---

## 7. Pitch surface tone-matched to the ground's dirt patch — DONE (2026-09-22, follow-up)

The pitch strip read as a different material from the dirt patch painted into the ground texture
around it: a bright warm tan against a dull grey-brown band, with an obvious seam at the join.

Measured rather than eyeballed. An orthographic top-down render was profiled in 0.4 m bands out
from the pitch centreline:

```
 z band (m)   rendered rgb          what it is
  2.0..2.8    (0.820,0.755,0.652)   pitch
  3.2..3.6    (0.576,0.532,0.459)   ground dirt band
  4.0..4.4    (0.405,0.466,0.279)   fading to grass
```

The **hue already matched** — pitch `1.00:0.921:0.795`, dirt `1.00:0.924:0.797`. The whole
difference was brightness: the dirt band renders at 70% of the pitch. Rao chose to bring the pitch
down to the dirt rather than the other way round.

### What changed

`Assets/Resources/Textures/PitchSurface.png`, generated from the existing `hard.png` by a
per-channel mean transfer. The required albedo was derived from the measured lighting factor rather
than guessed:

```
lighting factor (rendered / albedo) = (1.176, 1.169, 1.181)
target rendered (the dirt band)     = (0.576, 0.532, 0.459)
=> required albedo                  = (0.490, 0.455, 0.389)
```

Deviations were scaled by the same factor as the mean, so **relative** contrast is preserved and the
grain does not look noisier as the surface darkens. `hard.png` is untouched and still on disk.

`Hard Pitch.mat` now points at `PitchSurface.png` with `_BaseColor` **white** (the texture already
carries the target albedo, so any tint would pull it off again), `_Smoothness` dropped 0.1 → **0.05**
and `_Metallic` 0 to match `Ground00_baseColor.mat` — otherwise the two surfaces catch the light
differently and still read as different materials. Tiling stays **3×3**: the painted patch is only
~30 px/m, far too coarse to use directly, so the sharp `hard.png` detail is kept and only its colour
moved.

### Result

```
inside Pitch Markings footprint   (0.580,0.540,0.466)
Pitch Base beside markings        (0.588,0.545,0.472)
Pitch Base beyond markings        (0.552,0.515,0.463)
ground dirt band (target)         (0.577,0.532,0.460)
```

All within ~0.01–0.03. `Docs/Baseline/pitch-final-*.png` for the visual.

> `PitchMarkings.mat` needed no change: it is a **transparent** overlay (`_Surface = 1`) that only
> adds the white creases and inherits whatever is beneath, so it followed the base automatically.
> Worth knowing before anyone tries to "fix" the pitch centre separately.

**Still there, if it bothers you:** the painted patch's *feathered halo* (roughly z 3.6 → 4.4 m)
still fades through a dull olive on its way to grass, because that gradient is baked into
`Ground00_baseColor.png`. Fixing it means repainting that region of the ground texture — say the
word and I will.

---

## 8. "Unity is unable to start the game on the device" — FIXED (2026-09-23)

The APK installed fine but Build and Run refused to launch it. From `Editor.log`, on every build:

```
DeploymentOperationFailedException: No activity in the manifest with action MAIN and
category LAUNCHER. Try launching the application manually on the device.
NoTargetsFoundException: Could not launch build
```

Rao's diagnosis was right: it is the Android manifest. The generated launcher activity had

```xml
<intent-filter>
    <action android:name="android.intent.action.MAIN" />
    <category android:name="com.oculus.intent.category.VR" />
    <category android:name="android.intent.category.INFO" />   <!-- no LAUNCHER -->
</intent-filter>
```

### Cause

The project has **no custom `AndroidManifest.xml`** - Unity generates it, and then the vendored
Oculus SDK rewrites it. [OVRManifestPreprocessor.cs](../Assets/Oculus/VR/Editor/OVRManifestPreprocessor.cs)
does this unconditionally, in its own words:

```csharp
// remove launcher and leanback launcher
AddOrRemoveTag(doc, ..., "android.intent.category.LAUNCHER",
    required: false, modifyIfFound: true);   // always remove launcher
// add info category
AddOrRemoveTag(doc, ..., "android.intent.category.INFO",
    required: true,  modifyIfFound: true);   // always add info launcher
```

That is the old Oculus **Store** convention - a shipped title is hidden from the 2D Android
launcher and surfaced only through the VR library. It is wrong for sideloaded development builds,
because Unity's deploy step looks for MAIN + LAUNCHER to start the app. The install succeeds and
only the launch fails, which is exactly the "deployed successfully but will not start" symptom.

It is applied by `OVRGradleGeneration` (an `IPostGenerateGradleAndroidProject` with
**callbackOrder 99999**, so it runs last).

### Fix

[RestoreLauncherCategory.cs](../Assets/Editor/RestoreLauncherCategory.cs) - an
`IPostGenerateGradleAndroidProject` with `callbackOrder = int.MaxValue`, so it runs *after* the
Oculus one, finds the intent-filter carrying `action.MAIN` and appends
`android.intent.category.LAUNCHER` if it is missing.

`INFO` and `com.oculus.intent.category.VR` are deliberately left alone - they are harmless, and
Meta's own submission template (`Assets/Oculus/VR/Editor/AndroidManifest.OVRSubmission.xml`) ships
`LAUNCHER` alongside them.

Done this way rather than by editing `OVRManifestPreprocessor.cs` so that an Oculus SDK update
cannot silently revert it.

**Verified** against the real generated manifest - the patch finds the one MAIN filter and produces:

```
<action   android:name="android.intent.action.MAIN"/>
<category android:name="com.oculus.intent.category.VR"/>
<category android:name="android.intent.category.INFO"/>
<category android:name="android.intent.category.LAUNCHER"/>
```

> **If you ever need to start a build that predates this fix**, the app is installed and works -
> only the auto-launch is missing:
> ```
> adb shell am start -n com.RaoVadapalli.CricketVR/com.unity3d.player.UnityPlayerGameActivity
> ```
> Note the entry point is **GameActivity**, not the older `UnityPlayerActivity`.

---

---

## 9. Pitch height, culling, and the stumps — FIXED (2026-09-24)

Four reports from a close-up device session, which turned out to be three different faults.

### 9a. The pitch sat 25 mm proud of the ground

Measured by raycasting straight down at five points along the strip:

| surface | was | now |
|---|---|---|
| stadium ground (`NMD_stD_pinch_0`) | −0.0051 | unchanged |
| `Pitch Base` top | +0.0100 (15.1 mm proud) | −0.0021 (3 mm) |
| `Pitch Markings` top | +0.0200 (25.1 mm proud) | −0.0001 (5 mm) |

Both slabs are `scale.y = 0.010` on a unit-extent mesh, so the top is `localPosition.y + 0.01`.
3 mm base-over-ground and 2 mm markings-over-base is far more than the depth buffer needs here —
with `near = 0.3`, `far = 1000` the worst-case precision down the length of the pitch is under
0.2 mm — so there is no z-fighting, while the lip is now only 5 mm instead of 25 mm.

The outfield (`NMD_stD_Ground00_0`) is at −0.0469, but that step already existed between the
stadium's own pitch square and the grass; it is not something the pitch slabs introduced.

### 9b. The pitch and the bowler's-end stumps "disappearing" — two unrelated causes

**The pitch: stale occlusion culling.** Renderer bounds were checked first and are exact — the
declared mesh bounds equal the real vertex extents for both slabs and all six stumps — so frustum
culling cannot drop an object that is still partly on screen. That leaves occlusion culling, and
the scene's baked Umbra data was **944 bytes for 162 renderers**, which is a degenerate or stale
bake. Cleared it (`StaticOcclusionCulling.Clear()`, `umbraDataSize` 944 → 0). An open stadium bowl
occludes almost nothing, so the data was buying no performance while wrongly culling.

*This one is reasoned, not proven:* occlusion culling cannot be exercised headlessly, so it is the
last remaining candidate after the others were eliminated rather than a reproduction. Worth a
specific look on the next device build.

**The stumps: they were being shot out of the ground.** Not a rendering fault at all. The first
device log of the session had already recorded it:

```
CONTACT 'LegStump' tag='Untagged' state=InGame_SelectDeliveryLoop
  velBefore=(0.00, 0.00, 0.00) velAfter=(-1733.63, -296.90, 314.08) |v|=1786.70
```

The carried ball is teleported ~11 m onto the bowler's hand when he jumps to the top of his
run-up. PhysX reads a kinematic body's teleport as motion, so it saw the ball sweep through the
bowler's-end stumps at **1786 m/s** and launched them. Only the batting end has a `Stumps`
component, so the bowler's end is never reset and they stayed gone — "sometimes disappear".

Fixed by taking the ball's collider out of the world whenever it is not live: disabled in
`AnimatedBowler.HoldBall` while carried, disabled in `Main.StopTheBall` before its long teleport
back from the boundary, re-enabled in `InGame_DeliverBall`.

### 9c. The batting-end stumps fell over on every reset

Reproduced in **1.0 s** of play with nothing else happening:

```
Stumps/Stump     tilt=21.22deg  pos=(10.4680, ...)   <- drifted 0.168 m
Stumps/OffStump  tilt=38.34deg  z 0.150 -> 0.2723
Stumps/LegStump  tilt=38.34deg  z -0.150 -> -0.2723
Stumps (1)/*     tilt=0.00deg   asleep                <- bowler's end, untouched
```

They splay **radially outward from (10.2, ·, 0)** — the player rig. `XRPlayerController` carries a
`CharacterController` of radius 0.3 at x = 10.2; the stumps stand at x = 10.30, z = 0/±0.15, so all
three are 0.10–0.18 m from its axis and sit *inside* it. Layer `Body` and layer `Stumps` had
collisions enabled, so the batsman's proxy capsule shoved the stumps over every frame. That is
exactly why only the batting end was affected.

Fixed in the layer collision matrix: `Body` ↔ `Stumps` now ignored, alongside the `Body` ↔ `Ball`
entry that was already there. `Stumps` ↔ `Ball` is untouched, so being bowled still works.

Three supporting fixes, all the same fault family as the ball bug in section 1:

* **`Stumps.Reset()` never zeroed velocity.** It toggled `isKinematic` around a transform write, and
  a Rigidbody keeps its velocity across that toggle, so a stump that had been knocked flying came
  back to its mark still carrying the motion. Rewritten as `ResetOne`, which forces the body
  dynamic, zeroes, writes the pose through `rb.position`/`rb.rotation` as well as the Transform
  (`autoSyncTransforms` is false), and zeroes again on the way out.
* **`Main.StopTheBall` was zeroing a body that was already kinematic.** The ball is normally parked
  by the boundary or a fielder before this runs, so the zeroing was silently discarded — every
  delivery logged *"Setting angular velocity of a kinematic body is not supported"*. It now forces
  the body dynamic first.
* **`Fielder` and `AnimatedFielder`** both set `isKinematic = true` and *then* zeroed the velocity,
  which does nothing. Order swapped, and angular velocity zeroed too.
* Re-seated all six stumps so the capsule rests exactly on the new markings surface
  (origin y 0.0400 → 0.0249, capsule bottom −0.0001 vs markings top −0.0001, zero penetration).
  The capsule sits on a 1.25 parent scale, so the bottom is 0.025 below the origin, not 0.020.

### How it was verified

Play mode, auto-bowling, several deliveries:

| | before | after |
|---|---|---|
| max stump tilt | 38.34° within 1.0 s | **0.000°** |
| max stump drift from its mark | 0.168 m | **0.00000 m** |
| stumps asleep at rest | batting end never settled | all six asleep |
| kinematic-velocity warnings | several per delivery | **none** |
| console warnings + errors | — | **zero** |

Ball delivery was re-checked at the same time and is unaffected: release points stayed within
y 1.96–2.01 across consecutive deliveries with no `[Delivery]` corrections logged.

### Still open

`Main.theStumpsScript` only points at the batting end, so `Stumps (1)` at the bowler's end has no
`Stumps` component and is never reset. Nothing knocks it over any more, but if something ever does,
it stays down. Worth wiring up when convenient.

---

## 10. Pitch detail map and the dark edge — FIXED (2026-09-24)

### 10a. Pitch Base had no detail map  (superseded by 10f)

The ground got a detail map in section 4; the pitch never did, so it went flat and smooth as soon
as the camera was close. Generated `Assets/Resources/Textures/PitchDetail.png` the same way
`GrassDetail.png` was made — greyscale high-pass of `PitchSurface.png`, mean-normalised — and wired
it into `Hard Pitch.mat` as `_DetailAlbedoMap` with `_DETAIL_MULX2`.

The recipe is kept at `Docs/tools/make_pitch_detail_map.py` so it can be regenerated or retuned.
It also makes the tile wrap: the first 64 columns and rows are cross-faded with the last 64, so
column 0 continues naturally from the final column. Measured across the wrap:

```
wrap seam X 21.28 vs interior 23.83    Y 22.86 vs interior 24.78   (levels)
```

Both seams are *below* the typical interior pixel-to-pixel difference, so the tile is seamless.

Sizing: the top surface is 34 x 6 m over UV 0..1, so tiling (40, 7.06) gives 0.85 m per repeat in
both axes — a square grain at 301 px/m, matching the ground's detail density (365 px/m) so the two
surfaces blend where they meet. Imported at 256 px, ASTC 6x6, **aniso 8**, trilinear, repeat.

| camera height | local contrast with detail | without | mean (brightness) |
|---|---|---|---|
| 0.25 m | 0.0385 | 0.0225 | 0.3326 vs 0.3346 |
| 0.60 m | 0.0337 | 0.0260 | 0.3321 vs 0.3330 |
| 1.70 m | 0.0291 | 0.0281 | 0.3242 vs 0.3246 |
| 5.00 m | 0.0900 | 0.0900 | 0.3152 vs 0.3155 |

+71% fine detail up close, fading to nothing by 5 m, with brightness unchanged at every height.

### 10b. A detail map must be neutral in LINEAR space — the ground had been 30% too dark

Adding the detail map made the pitch obviously darker. Measured by rendering the surface with and
without it:

```
PITCH   with detail (0.2344, 0.2210, 0.1962)   without (0.3424, 0.3239, 0.2896)   -> 0.685x
GROUND  with detail (0.3202, 0.4388, 0.1890)   without (0.4534, 0.6256, 0.2629)   -> 0.706x
```

**The ground had the same fault, and had done since section 4 — it had been rendering about 30%
too dark and I did not catch it.**

URP's `_DETAIL_MULX2` computes `albedo *= detail * 2`, so "no change" means the detail sample must
be **0.5 in linear space**. Both maps are a 127.5 grey, but both were imported with
`sRGBTexture = true`, so 127.5 decoded to 0.214 linear and multiplied the surface by 0.43.

Fixed by importing both detail maps as **linear** (`sRGBTexture = false`). 127.5 is then read as
0.5 directly and the multiply is exactly 1.0. Re-measured:

```
PITCH   with detail (0.3420, 0.3235, 0.2893)   without (0.3424, 0.3239, 0.2896)
GROUND  with detail (0.4523, 0.6243, 0.2623)   without (0.4534, 0.6256, 0.2629)
```

Energy-neutral to within 0.3%. The ground is correspondingly brighter than it has been since the
section 4 work.

### 10c. The dark line around the pitch was the slab's side wall, not a shadow

Shadow casting was already `Off` on both `Pitch Base` and `Pitch Markings`, so nothing was casting.
The dark line is the **vertical side wall of the slab**: `Pitch.fbx` is a solid box and its 48
side-facing triangles have horizontal normals, so they shade almost black under an overhead sun.
At `scale.y = 0.010` on a mesh spanning ±1 local units, that wall is **2 cm tall** — a very visible
dark stripe all the way round at close range.

Built `Assets/Resources/Meshes/PitchTop.asset` from the 242 up-facing triangles only (274 verts →
192, dropping the 48 side and 2 bottom triangles) and assigned it to both MeshFilters. The
**MeshColliders keep the original solid `Pitch` mesh**, so physics is untouched:

```
                 RENDER y             COLLIDER y            raycast
Pitch Markings   [-0.0001..-0.0001]   [-0.0201..-0.0001]    hits -0.0001  (unchanged)
Pitch Base       [-0.0021..-0.0021]   [-0.0221..-0.0021]    hits -0.0021  (unchanged)
```

The rendered surfaces are now zero-thickness planes at exactly the heights set in section 9a, and
the ball still lands on a 2 cm solid.

### 10d. The pitch went dark because the rebuilt mesh lost its lightmap UVs — my regression

After 10c the pitch rendered noticeably dark and would not blend into the dirt around it. The
question asked was whether the lighting needed rebaking. It did not.

`Pitch Base` and `Pitch Markings` are both lightmapped (`lightmapIndex = 0`, each with its own
`lightmapScaleOffset`), and a lightmapped renderer samples the bake through **UV2**. The
`PitchTop` mesh built in 10c was assembled from `vertices / uv / normals / triangles` only, so it
had no UV2 at all:

```
source Pitch.fbx  uv=274  uv2=274  normals=274  tangents=274
PitchTop (broken)  uv=192  uv2=0    normals=192  tangents=0
```

With UV2 missing the slabs sampled the wrong part of the lightmap and came out dark and blotchy.
Rebuilt carrying every channel the source has — uv, **uv2**, normals, tangents — and remapping them
through the same vertex remap as the positions.

```
tone across the pitch edge at x=-4          before uv2 fix        after
  z = 0.0  (on the strip)                   (0.356,0.332,0.304)   (0.596,0.553,0.481)
  z = 2.0  (on the strip)                   (0.290,0.265,0.222)   (0.596,0.553,0.481)
  z = 2.9  (edge of the strip)              (0.287,0.260,0.220)   (0.591,0.543,0.476)
  z = 3.2  (ground's painted dirt)          (0.800,0.735,0.638)   unchanged
```

The pitch is 67% brighter and now uniform along its length - the variation between z=0 and z=2.0
was the lightmap being sampled at essentially arbitrary coordinates. Against the surrounding dirt
it now reads 0.75x instead of 0.45x, which is the blend that was being asked for.

**Lesson: any mesh rebuilt in script for a lightmapped renderer must carry UV2.** Dropping it does
not error, warn, or show up in the mesh inspector at a glance - it just renders wrong. The section
10c verification checked geometry and collider heights and never looked at the lightmap, which is
why this shipped as "fixed".

### 10e. Pitch tone matched to the surrounding dirt

With 10d fixed the pitch was still reading darker than the ground's dirt square. Measured, the
difference was **purely brightness, not hue** — the per-channel ratios to the dirt were 1.342,
1.329, 1.326, near-identical — so a single neutral gain was the right correction rather than a
recolour.

Applied as a gain on `PitchSurface.png` itself (not a `_BaseColor` tint, which would have needed an
out-of-range value above 1 that the material inspector can clamp away on any later edit). The gain
is applied in **linear light**, where albedo actually multiplies: the LUT converts each sRGB byte to
linear, scales, and converts back. A naive multiply of the sRGB bytes would have undershot badly —
the 1.34x wanted in output terms is 2.08x in linear.

Two measured passes: gain 1.91 left a residual ratio of 1.040, so the final gain is **2.082**.

```
               before          after           ground dirt
pitch tone     (0.60,0.55,0.48) (0.822,0.763,0.665)  (0.825,0.762,0.665)
ratio dirt/pitch    1.34x            1.003, 0.999, 1.001
```

Matched to within 0.3% on every channel, and continuous across the edge (z=0 0.822, z=2.9 0.814,
z=3.2 0.800, z=3.6 0.791). No pixel clips: the brightest source texel reaches 230 of 255.

The original texture is preserved in git, and the whole adjustment is the single `gain` argument in
`Docs/tools/` — if the pitch should read as a slightly distinct worn strip rather than blending
completely, lower it.

### 10f. Matching the grain, not just the tone

With the tone matched the pitch still read wrong up close: coarse green-grey mottling where the
dirt beside it is fine and uniform. Measured, the cause is a difference in how the two surfaces
are built, not a difference in colour:

| | base map resolution in world terms |
|---|---|
| `Ground00_baseColor` | 2048 px over 134.82 m = **15.2 px/m** |
| `PitchSurface` (was) | 1200 x 302 over 11.33 x 2.0 m = **106 x 151 px/m** |

The ground's dirt is a very smooth base plus fine grain from its detail map - its base physically
cannot resolve anything finer than about 6.6 cm. The pitch was a photograph seven times finer, so
it supplied its own mid-frequency mottle, and a green cast (+4.4 levels of `g - (r+b)/2`) from the
grass in the photo, which the dirt does not have.

Fixed by giving the pitch the same division of labour:

* `PitchSurface.png` rebuilt by `Docs/tools/rebuild_pitch_texture.py` - the tone gain from 10e,
  then resampled down to **15.2 px/m** and back (resampling rather than blurring, so it stays
  anisotropic for free), the green cast neutralised, and the mean restored so the 10e tone match
  survives. Overall stddev falls 19.1/19.6/20.6 -> 9.4/9.1/8.5.
* The pitch now uses the **ground's own `GrassDetail` map**, at the same world density, so the fine
  grain on both sides of the boundary is literally the same texture at the same scale:
  1.6441 m (U) / 0.8221 m (V) per repeat = **311.4 px/m on both surfaces**. `PitchDetail.png`,
  made in 10a before this was understood, is deleted.

Measured across matched camera footprints, pitch against the dirt beside it:

| footprint | pitch contrast | dirt contrast | pitch rgb | dirt rgb |
|---|---|---|---|---|
| 0.063 m | 0.0565 | 0.0597 | (0.824,0.768,0.667) | (0.844,0.779,0.683) |
| 0.126 m | 0.0580 | 0.0585 | (0.821,0.765,0.664) | (0.833,0.770,0.674) |
| 0.252 m | 0.0579 | 0.0576 | (0.819,0.762,0.660) | (0.823,0.761,0.664) |
| 0.631 m | 0.0438 | 0.0586 | (0.812,0.756,0.652) | (0.800,0.744,0.638) |

Grain matches to within 5% at every close range, and green excess drops to 0.023 against the
dirt's 0.016-0.026. At the coarsest footprint the pitch is now about 25% *less* varied than the
dirt - it errs on the smooth side rather than the mottled side, which is the right direction to
miss in.

The original photo is kept at `Docs/tools/PitchSurface_source.png` so the whole pipeline - gain,
smoothing, green removal - is re-runnable and tunable from one script.

## Incidents during this work

**Rebuilt a lightmapped mesh without its UV2 (2026-09-24).** Stripping the side faces off the pitch
slab in section 10c produced a mesh with no lightmap UVs, so both pitch surfaces sampled the bake
at the wrong coordinates and rendered dark and blotchy. I reported 10c as verified having checked
only render bounds, collider bounds and raycast heights - none of which can see a lightmap fault.
Rao spotted it on the device and asked whether the lighting needed rebaking; it did not, the mesh
just needed its UV2 back. When a script rebuilds a mesh, copy every channel the source carries.


**`ProjectSettings.asset` `preloadedAssets` — I got this wrong, and it has been corrected.**

I saw the list empty, concluded that XR would not initialize in a build ("the APK would have
launched flat"), and restored the two entries. **That was wrong on both counts.**

Reading `XRGeneralBuildProcessor.cs` in `com.unity.xr.management` settles it:

- `OnPreprocessBuild` adds the per-build-target `XRGeneralSettings` **only if it is not already
  present**. If it *is* present, it takes the `else` branch and calls
  `CleanOldSettings<XRGeneralSettings>()`, which **removes it and does not re-add it**.
- `OnPostprocessBuild` **always** calls `CleanOldSettings()`. The source comment is explicit:
  *"Always remember to cleanup preloaded assets after build to make sure we don't dirty later
  builds with assets that may not be needed or are out of date."*

So **an empty list is the designed resting state**, and **a device build is exactly what empties
it**. Unity re-injects the entries for each build. (`OpenXRSettings` is handled separately by
`XRBuildProcessorHelper.SetSettingsForRuntime`, which only adds when absent and has no destructive
branch.)

Worse than merely unnecessary: a **populated** list sends the next build down that `else` branch, so
the build can be produced *without* the preloaded XR settings. The restored entries were committed,
so **the first device build off that commit is worth checking actually entered VR** rather than
launching flat.

`preloadedAssets` has now been set back to `[]`. Leave it that way; do not "restore" it. If XR ever
genuinely fails to start on device, look at the OpenXR loader list in
`Assets/XR/XRGeneralSettings.asset` (Android Providers must contain `OpenXRLoader` — it does) and
the logcat XR init lines instead.

**`Nets.unity` was re-serialized** into the Unity 6 scene format (`m_Drag` → `m_LinearDamping`,
`serializedVersion` bumps, new Rigidbody fields). Inspected: it is a pure format upgrade with no
gameplay semantics changed, and it is the upgrade Unity would apply anyway the next time that scene
is opened. Nets is **disabled in Build Settings** — only `CricketVR.unity` ships — so it does not
affect the device build. Revert it if you would rather keep the diff small.

`Nets.unity` still carries stale `rightGrabOffsetRotation.*` overrides pointing at the removed
property. Unity ignores them and will prune them when that scene is next saved; its Bat will fall
back to the prefab euler values, which differ from the old scene override by under 2°.

---

## What to check on the device build

0. **Did it enter VR at all?** The commit that build came from has `preloadedAssets` populated,
   which can send `XRGeneralBuildProcessor` down its destructive branch (see Incidents). If the APK
   launched flat, that is why — the working tree now has `preloadedAssets: []`, so rebuild from that.
1. **`[XRRigSetup] Floor tracking origin active`** in logcat — if the warning about a fixed eye
   height appears instead, the runtime refused a floor origin and issue 2 is only half solved.
2. **Stand next to a fielder.** Their eyeline should be at yours.
3. **Bowl a leg-break.** It should now change direction off the pitch, and stop curving in the air
   after it lands.
4. **The bat.** If the orientation is still off, tick `Enable Tuner`, dial it in, press **B**, and
   send me the six numbers.
5. **The crowd**, from pitch level — upright figures, gentle idle motion, no see-through gaps
   between rows.
6. **Hit one over the red line.** The state should go to `InGame_BallPastBoundary` as the ball
   crosses it, and the fielders should chase without throwing.
