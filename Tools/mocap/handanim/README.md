# Hand-animating a bowling action in Blender

Use this to keyframe a bowling action yourself, or to fix up an existing one, on the game's own bowler rig. When you export, it goes straight into Unity. It gets wired into the bowling controller, and his mark is moved so his front foot lands where Steyn's does.

## One-time setup

1. **Unity:** run *CricketVR > Bowling > Export Bowling Actions For Blender*. It writes `actions/Steyn.json`, `Bumrah.json` and `Lyon.json`, each played exactly as the game plays it (state speed, a pace ball's Time, BowlerSnap's quickening).
2. **Terminal**, from the repo root:

   ```
   /Applications/Blender.app/Contents/MacOS/Blender -b --python-exit-code 1 -P Tools/mocap/handanim/build_scene.py
   ```

   This builds `Tools/mocap/handanim/BowlingAnimation.blend` (not in git; rerun to rebuild). It also installs the **Cricket** add-on into your Blender. Add `-- --no-addon` to skip the add-on.
3. Open `BowlingAnimation.blend` in Blender. Press **N** in the 3D view and open the **Cricket** tab.

## What's in the scene

| Thing | What it's for |
|---|---|
| `Bowler` | The game's rig (`Bowler.fbx`). You animate this one. |
| Actions `Steyn`, `Bumrah`, `Lyon` | The game's actions, at 30 fps, root motion included. Use them as starting points. |
| `Anderson (captured)` | The video-capture result, for reference only. It comes from fan footage, so it must not ship. |
| `Ghost` | A see-through copy of the skeleton. It plays any action beside yours so you can compare. |
| `Pitch` | The bowler's end to scale: popping crease, bowling crease, return creases and stumps. It is placed where Steyn actually lands in the game. |
| Cameras | `Cam side-on`, `Cam behind`, `Cam batter`. Numpad 0 looks through the active camera. |
| Markers | `release` (required: the frame the ball leaves the hand). `back foot` and `front foot` are optional notes. |

## How layering works

*Start from Steyn* (or another action) puts that action on a locked **Base** layer. Your keys go on an **Edits** layer on top, and they **combine** with the base (Blender's Combine blend: a rotation you key is turned on top of the base's).

- To change something, key only the bones you're changing, at the frames you're changing. Everything else plays through from the base.
- The eye button next to "Edits layer" turns your edits off, so you can compare against the base.
- If you need to change the timing itself, use *Flatten to one action*. It bakes base and edits into one new action that becomes the base. Then you can move or delete keys freely in the Dope Sheet.

## Reference footage: two views, in step with the animation

**Side-on | front-on views** (Cricket tab) splits the 3D view in two. The left half is side-on and the right half looks through `Cam front-on` (from the batter's end). The side-on camera is on whichever side of him the footage was filmed from: `Cam side-on` (his right) when he runs left to right across the picture, `Cam side-on left` when he runs right to left. Otherwise the rig is mirrored against the footage. Steyn's and Anderson's side clips are both filmed from his left. Each shows a bowler's real footage behind the rig, and the rig is see-through (X-ray).

- **Which footage:** click **Steyn** or **Anderson** under *Reference footage*. The build loads Steyn's footage by default.
- **Timing:** the footage follows the scene's **back foot**, **front foot** and **release** markers. It is stretched between them so his back foot lands, front foot lands and ball leaves on the same frames as the action you're animating. Move a marker with *Mark here* and the footage re-times.
  - The markers start where the base action's own feet land (Steyn 22 / 27 / 28, Bumrah 18 / 26 / 27, Lyon 21 / 29 / 32, Anderson 0 / 12 / 15).
- **Pin:** with **Pin** on, the footage's bowler stays on the rig: his hips on the rig's hips, sized so his hip-to-shoulder length matches. Turn it off to see the footage's own camera movement. Use **Refit size** after big changes, and **Opacity** for each clip.
- **The pitch** is hidden while the footage is on, because its turf would cover his feet. **Pitch** toggles it.
- **Leaving a camera view:** if you orbit away from a camera, press **Numpad 0** in that view to look through its camera again.

To make footage (side-on and front-on of the same bowler, listed in `../out/views/manifest.json`), run from `Tools/mocap`:

```
.venv/bin/python -I handanim/make_reference.py steyn_side steyn_front anderson_side anderson_back
```

`make_reference.py` reads the run direction from his lean through the delivery stride, not from where he goes on screen: a panning camera follows him, and `steyn_side` cuts to the other side of the ground mid-run (frames 1–14 from his right, 19–27 from his left). It reads each source video exactly as `extract_pose.py` did, so image *k* is the frame tracked pose *k* came from. It refuses to write if the frame counts differ. Each clip goes to `reference/<view>/` (gitignored), with a `meta.json` holding each image's real time, the three events and the bowler's hips. Those clips are other people's footage: use them for reference on this machine only. Your own filming is the one to ship from (`../CAPTURE.md`).

## Export to Unity

1. Put the `release` marker on the frame the ball leaves the hand (*Mark here* in the Markers box).
2. Click **Export to Unity** and give it a name, e.g. `HandPace`. It writes:
   - `Assets/Resources/Animations/Bowling/Mocap/<Name>.fbx`: deform bones only, every frame baked, no key reduction.
   - `<Name>.mocap.json`: the sidecar with frames, fps, release frame, action number and timing. It also holds his **mark**: Steyn's front-toe distance minus yours. If your bowler lands 0.4 m further on than Steyn, his mark moves 0.4 m back.
3. Switch to Unity. It imports the FBX as Humanoid with a `ReleaseBall` event on your release frame. It adds a state to `Steyn.controller` (Run → `<Name>` → Follow Through), and `MocapDelivery` picks it up from the sidecar. No code changes are needed.
4. Turn on *CricketVR > Bowling > Use Mocap Delivery For Pace* and bowl in the Nets. Pace and medium bowlers use your action (hand-made clips have priority 10, above the captured ones). Spinners keep Lyon.

To re-export, just export again with the same name. Any timing you tuned by hand in the sidecar (`exit_time`, `blend`, `run_exit`, …) is kept. To remove a clip, delete its `.fbx` and `.mocap.json`; Unity removes its state from the controller.

### Sidecar fields you might tune in Unity

| Field | Meaning | Default |
|---|---|---|
| `run_exit` | How far through the run cycle it hands over to your clip | 0.537 (as Run → Steyn) |
| `blend` | Seconds of blend from the run into your clip | 0.3 |
| `clip_offset` | Where in your clip the blend starts (0–1) | 0 |
| `exit_time` / `exit_blend` | When and how quickly it blends into Follow Through | Leaves enough frames to finish the blend inside the clip |
| `mark_forward` / `mark_wide` | Metres his mark moves on / to the side | Worked out on export |
| `priority` | Which delivery wins when several exist | 10 for hand-made |

## Checks done (2026-11-13)

| Check | Result |
|---|---|
| Steyn, Bumrah and Lyon loaded into Blender vs Unity's joint positions | 2.7–3.0 cm mean, 6.6 cm max (the two rigs' bind poses differ by 2.2 cm mean) |
| Steyn exported from Blender unchanged, then played as `HandTest` | Same as Steyn: body muscles differ by 0.02 of their range on average; ground covered to the release 3.53 vs 3.49 m |
| `HandTest` in play (Nets), right and left arm | Run > HandTest > Follow Through > Idle; release (-8.78, 1.92, 0.31) vs Steyn's (-8.79, 1.96, 0.33); front toe -9.06 vs -8.95; arm never inside the body |

## Files

| File | Job |
|---|---|
| `Assets/Editor/BowlingClipExport.cs` | Unity: game actions → `actions/*.json` |
| `load_action.py` | Blender: one `actions/*.json` → an action on the rig (`--check` reports joint error) |
| `build_scene.py` | Builds `BowlingAnimation.blend` and installs the add-on |
| `cricket_bowling_anim/__init__.py` | The Cricket tab: start-from, ghost, markers (found from the feet), export |
| `cricket_bowling_anim/reference.py` | Footage behind the cameras: timing to the markers, pinning to the rig, the two views |
| `make_reference.py` | Footage → `reference/<view>/` image sequence + `meta.json` |
| `Assets/Editor/MocapBowlerImport.cs` | Unity: imports the FBX and wires (or removes) controller states |
| `Assets/Scripts/Bowling/MocapDelivery.cs` | Game: picks the delivery and moves the mark, from the sidecars |
