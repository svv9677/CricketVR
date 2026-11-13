# Bowling actions from video

Phone video of a real bowler → the game bowler's animation. Free, local, commercially usable.

```
video ──extract_pose.py──▶ 3D landmarks ──process_motion.py──▶ grounded joint track
      ──retarget_blender.py (Blender, headless)──▶ FBX on the Bowler.fbx rig + preview
      ──install_clip.py──▶ Assets/Resources/Animations/Bowling/Mocap/<Name>.fbx + .mocap.json
      ──Unity import (Assets/Editor/MocapBowlerImport.cs)──▶ humanoid clip, ReleaseBall event,
        a state in Steyn.controller reached with Action 5 (MocapDelivery: off unless switched on)
```

## Run it

One-time setup (from this folder):

```
uv venv --python 3.12 .venv && uv pip install --python .venv/bin/python mediapipe opencv-python numpy scipy matplotlib
mkdir -p models && curl -L -o models/pose_landmarker_heavy.task \
  https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/latest/pose_landmarker_heavy.task
```

Per clip:

```
./build.sh ~/Downloads/IMG_9676.MOV img9676 --end 26 --contacts "l:0-1,r:9-15,l:16-26"
open out/img9676_preview.mp4          # source | rig from behind | rig from the side
.venv/bin/python install_clip.py out/img9676 MocapPace
```

Then in Unity: **CricketVR → Bowling → Use Mocap Delivery For Pace** (a checked menu item; off by
default) makes pace and medium bowlers run in with the captured action. Spinners keep Lyon.

### Options worth knowing (process_motion.py)

| option | what it does | when to set it |
|---|---|---|
| `--start N --end N` | frames kept | cut to bound → follow-through; drop frames where the figure is small or walking away |
| `--contacts "l:a-b,r:c-d"` | footfalls (frames, relative to `--start`) | always for now: the 2D fallback fails where a foot is hidden (behind the stumps in IMG_9676) |
| `--release N` | release frame | auto picks the first time the wrist is highest above the shoulder in the picture; check the preview's RELEASE label |
| `--arm left` | bowling arm | left-armers |
| `--fx 0.72` | focal length / frame width | 0.72 is an iPhone main lens in 4K with stabilisation; 0.5x lens ≈ 0.36, 2x ≈ 1.45 |
| `--up torso` | up from the upright run-up frames' torsos | side-on footage, where the run crosses the frame and the level-camera estimate is undefined |

For footage you did not film (extract_pose.py; all off by default, which runs exactly as before):

| option | what it does |
|---|---|
| `--seed x,y` | follow the bowler (normalised point on him in the first kept frame) among several people |
| `--crop` | run the pose model on an upscaled box around him (small, distant figures) |
| `--pan` | remove a panning camera's turn, estimated from the background (camera_pan.py) |
| `--time-scale 0.25` | the clip is a quarter-speed replay: timings and speeds come out real |
| `--dedupe` | drop repeated frames (slow-motion replays of normal-rate video repeat each frame) |

Tried 2026-11-13 on fan-filmed side-on clips of Anderson (usable: the whole delivery, though the
body leans back too far at the crease) and Bumrah (not usable: he overlaps the non-striker through
the release). Footage like that is someone else's recording: for testing only, never shipped.

## What it does, and why (measured on IMG_9676, 2026-11-13)

| stage | method | why this one |
|---|---|---|
| pose | MediaPipe Pose Landmarker (heavy), 33 metric 3D points per frame | Apache-2.0 code and weights, runs on the Mac CPU (105 4K frames in 11 s) |
| travel | per frame, the translation that reprojects the 3D skeleton onto its own 2D points (static camera), then a cubic in time | the per-frame fit jitters by tens of cm in depth: planted feet "slid" at 3-16 m/s |
| up / forward | up ⟂ run and ⟂ the camera's x axis (a phone held for video is level); forward = the hips' travel to the release | a plane fitted under the feet is degenerate (all contacts on one line) and tipped the clip 30° sideways |
| bone rotations | per bone, the rotation carrying its rest frame (bone direction + hinge axis) onto the captured one; spine/neck slerped between pelvis, chest and head | only directions transfer, so the subject's proportions do not distort the rig |
| hands, ankles | hands follow the forearm; the ankle is a hinge, flat on the ground while planted, the captured pitch (clamped) in the air | MediaPipe's hand and foot points are its noisiest: taking roll from them corkscrewed the forearm and broke the ankles (below) |
| joint limits | elbow, knee, ankle as hinges with no hyperextension; every other joint inside a swing cone and twist range (`LIMITS`), applied after the solve, after smoothing and after the foot IK | nothing stopped a joint going anywhere: 92% of frames had a joint past human range |
| smoothing | Savitzky-Golay per bone on local rotations: 5-frame cubic on the bowling arm, 9-frame quadratic elsewhere, 7 on the ankles | depth noise frame to frame was the jitter; the light window keeps the arm's whip at the release |
| feet | each footfall pinned where it landed (analytic two-bone IK in the knee's own plane); the body moved up/down so planted ankles sit at standing height | without it every foot slid and floated |

Measured in Unity (humanoid muscles, body only, fingers excluded, 60 Hz game time; ±1 = the
human range; jitter = mean |second difference| per muscle per frame):

| clip | jitter | frames with a muscle past ±1.05 | worst |
|---|---|---|---|
| Steyn Cleaned | 0.0185 | 6% | Upper Leg Front-Back 1.22 |
| Bumrah | 0.0148 | 36% | Arm Twist 1.61 |
| MocapPace, unconstrained | 0.0609 | 92% | Foot Twist 2.85, Foot Up-Down 2.45, Hand In-Out 2.19, Forearm Twist 1.99 (jitter 0.61) |
| MocapPace, constrained | 0.0113 | 0% | largest 1.01 (Upper Leg Twist) |

### Known limits of this first clip

- Filmed from behind the bowler: depth runs along the run, which is MediaPipe's weakest axis. The
  delivery stride comes out shorter than it was, and from ~frame 28 (walking away, small in frame)
  the whole body leans 30-40° — hence `--end 26`.
- The front foot lands behind the stumps, so its landmarks wander and swap with the other foot:
  contacts are given by hand.
- Fingers are not captured (BowlerGrip poses them while he carries the ball); wrists have no
  flexion and forearms no roll of their own, by design (see hands above).
- The limits make every pose humanly possible, not physically right: there is no balance or
  momentum model, and the depth errors (short stride, lean) are still in the directions.
- The clip is 0.87 s, bound to early follow-through; the controller blends to Idle after it.
- Unity warns that the thighs carry a near-1.0 scale curve (float noise from the bake); it is
  discarded on import and harmless.

### In the game (Nets, editor play mode, 2026-11-13)

The mark is moved for the captured action (`MocapDelivery.Mark`): its own ground to the front-foot
landing is shorter than Steyn's. **CricketVR → Bowling → Measure Delivery Clips** prints, from each
clip's start, the root, front toe and hand at the release:

| clip | front toe at release | hand at release |
|---|---|---|
| Steyn Cleaned | 3.75 m on, 0.31 m to his right | 3.88 m on, 0.36 m right, 1.86 m up |
| Bumrah | 3.49 m, 0.24 m | 3.96 m, 0.72 m, 1.65 m |
| MocapPace (constrained, legs un-swapped) | 2.22 m, 0.02 m | 2.66 m, 0.14 m, 1.77 m |

Hence `MarkForward` 1.53 m and `MarkWide` 0.22 m (earlier cuts: unconstrained 2.40 / 0.11, so 1.35
and 0.25; constrained 2.46 / 0.14, so 1.29 and 0.22). Un-swapping the legs (swap_fix.py) also took
jitter from 0.0113 to 0.0106, still 0% of frames past range. With those first-cut values it played as a left-arm pace ball in Nets with the front
toe planted at x −9.31..−9.36 (popping crease −9.14, so ~0.2 m behind it, a fair ball), released at
(−9.13, 1.95, −0.56); the constrained clip is not yet re-checked in play. Re-run the measurement
and update those two constants whenever the clip changes. Not yet seen in a headset.

## Filming a better source (the biggest quality lever)

1. **Side-on**, square to the run, 10-15 m away, at about hip height, on a tripod. Depth then
   lies across the body's narrow axis instead of along the arm's swing.
2. **Slo-mo**: iPhone 240 fps (1080p). The bowling arm turns over in ~0.15 s — that is 4-5
   frames at 30 fps but 36 at 240 fps, and no motion blur. Set `--fx` for the lens used.
3. The **whole body in frame** from the last two strides to two steps after the release; nothing
   in front of the feet (stand the camera so the stumps are not between it and the bowler).
4. Plain, contrasting clothes; one person in frame if possible.
5. Your own footage, or a friend's with their OK. Broadcast footage is someone else's recording;
   the game is going to a store.

## Upgrade path: SAM 3D Body (Meta, Nov 2025)

Best single-camera body model available that allows commercial use. **SAM License**: worldwide,
royalty-free, commercial use allowed. Its body rig, MHR, is Apache-2.0. It is much better than
MediaPipe on depth and unusual poses, and gives hands and feet. Its 3D joints would replace
MediaPipe's in `extract_pose.py`; everything after that stays the same.

Blocked on access: the checkpoints are gated on Hugging Face (`facebook/sam-3d-body-dinov3`; the
local token gets HTTP 403). Request access on that page with your HF account. Use only the official
repo: mirrors' `.ckpt` files are pickles, which can run code when loaded.

Ruled out:

| tool | why not |
|---|---|
| GVHMR, WHAM, TRAM, 4D-Humans/HMR2, NLF | built on SMPL, whose licence is non-commercial (GVHMR's own: "prohibited for commercial use") |
| DeepMotion, Rokoko Video, Move.ai, Plask | paid or credit-limited (the Bumrah/Lyon/Steyn clips came from DeepMotion: `hips_JNT` rig) |
| MotionBERT, RTMW3D | permissive code, but weights trained on Human3.6M (research-only data) |
