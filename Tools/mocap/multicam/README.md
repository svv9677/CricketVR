# Two (or three) synced cameras → bowling action

One camera has to guess depth, and from the side it cannot tell the near leg from the far one
(the left/right swaps seen on every single-camera clip). Two cameras at about 90° triangulate every
joint instead, and Pose2Sim's OpenSim step fits a scaled human skeleton to the result, so joint
centres keep constant bone lengths and human joint ranges.

```
phone A (side-on) ─┐                                     ┌─ trc_to_track.py ─ retarget_blender.py ─ install_clip.py
phone B (front-on) ┼─ Pose2Sim: 2D pose (RTMPose) → sync → triangulate → filter → OpenSim scale + IK ─┤
(phone C, 45°)    ─┘                                     └─ preview_multicam.py (check by eye)
```

All free, commercially usable: Pose2Sim BSD-3, RTMPose (rtmlib) Apache-2.0, OpenSim Apache-2.0.

## Setup (once, outside the repo)

```
mkdir -p ~/Documents/Git/pose2sim-env && cd ~/Documents/Git/pose2sim-env
uv venv --python 3.12 .venv && uv pip install --python .venv/bin/python pose2sim onnxruntime
```

1.3 GB venv plus 150 MB of pose models downloaded on first run (~/.cache/rtmlib). OpenSim comes
from PyPI (macOS universal2 wheel); no conda needed. Measured 2026-11-13 (pose2sim 0.10.49,
opensim 4.6, onnxruntime 1.32 with CoreML): Pose2Sim's own 4-camera demo ran end to end in 91 s
(pose estimation 73 s of it), sync offsets found automatically (0/-2/-1 frames), mean reprojection
~10 px (~2 cm), IK marker RMS 2-3 cm. Bone lengths from the IK skeleton vary ±0.04 cm over the clip
against ±1-2 cm for the raw triangulated points.

## Filming

| | Camera A: side-on | Camera B: front-on | (Camera C, optional) |
|---|---|---|---|
| where | square to the pitch level with the popping crease, 10-15 m out | behind the batter's stumps (or 45° off them), 15-22 m away | 45° behind the bowler on the other side from A |
| height | ~1.2 m, tripod | ~1.5 m, tripod | ~1.5 m, tripod |
| frame | whole bowler from the last 2 strides to 2 steps after release | same | same |

- Both cameras **1080p 240 fps slo-mo**, locked focus/exposure (tap and hold), no zoom change, main
  (1x) lens, same phone model if possible.
- Get the **original 240 fps files** off the phones: AirDrop with Options → "All Photos Data", or
  Image Capture over USB. A normal share renders slo-mo to a 30 fps clip, which Pose2Sim would read
  as real time. Check with `ffprobe` that the file is ~240 fps.
- **Sync:** Pose2Sim correlates the wrists' and ankles' speeds across cameras automatically. Also
  clap once, in view of both cameras, before each delivery (a backup sync point).
- **Calibration, per phone (intrinsics):** print a checkerboard (4x7 inner corners, 60 mm squares,
  flat on card) and film it for 20-30 s at many angles and distances with the same lens/settings.
- **Calibration, per session (extrinsics):** leave the cameras where they are and take one still
  from each showing the pitch landmarks listed in `Config.toml` (stump bases/tops at both ends,
  crease corners). In nets without markings, place cones on those points with a tape measure.
  Do not move a camera between calibration and capture.

## Session folder

```
Session/                      # copy Config.toml here
├── Config.toml
├── calibration/
│   ├── intrinsics/  int_cam01_img/<checkerboard>.mov  int_cam02_img/<checkerboard>.mov
│   └── extrinsics/  ext_cam01_img/<still>.png         ext_cam02_img/<still>.png
└── videos/  cam01.mp4  cam02.mp4  (one delivery each, trimmed to a few seconds around it)
```

## Commands (from a session to a Unity clip)

```
cd <Session>
~/Documents/Git/pose2sim-env/.venv/bin/python -c "from Pose2Sim import Pose2Sim; Pose2Sim.calibration()"
#   click the 10 landmarks in order in each still; keep the reprojection errors small
~/Documents/Git/pose2sim-env/.venv/bin/python -c "from Pose2Sim import Pose2Sim; Pose2Sim.runAll(do_calibration=False)"

cd ~/Documents/Git/CricketVR/Tools/mocap
~/Documents/Git/pose2sim-env/.venv/bin/python multicam/trc_to_track.py <Session> out/d1.json --arm right
#   options: --start/--end frames, --release N, --contacts "l:a-b,r:c-d" (default: from the 3D feet),
#            --source trc (skip the skeleton fit), --up z (if auto picks the wrong vertical)
/Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P retarget_blender.py -- \
    out/d1.json ../../Assets/Resources/Models/Bowler.fbx out/d1.fbx --render out/frames_d1
.venv/bin/python multicam/preview_multicam.py out/d1.json out/frames_d1 out/d1_preview.mp4 \
    <Session>/pose/cam01_pose.mp4 <Session>/pose/cam02_pose.mp4
.venv/bin/python install_clip.py out/d1 MocapPace     # then re-measure the mark (MocapDelivery)
```

`trc_to_track.py` writes the same track as `process_motion.py` (Blender axes, ground at 0, scaled
to the rig), so the joint-limited retarget, foot pinning and Unity import are unchanged.

## Verified / not yet

- Verified on Pose2Sim's own demo (4 cameras, not bowling): install, full pipeline, the bridge
  from IK and from the TRC, retarget, preview (`out/p2s_demo_preview.mp4`).
- Not yet run on cricket footage: the pitch-landmark calibration, 240 fps sync, and a fast bowling
  arm through the 2D model (motion blur is the risk at 30-60 fps; 240 fps should avoid it).
