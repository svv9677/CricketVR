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

## Reference video

1. Make an image sequence from a clip. 30 fps keeps it in step with the timeline:

   ```
   ffmpeg -i myclip.mov -vf "fps=30,scale=960:-1" Tools/mocap/handanim/reference/myclip/%04d.png
   ```

   For slow-motion footage, add `setpts=<factor>*PTS` before `fps`, so the sequence plays at real speed.
2. Click *Add reference images…*, pick the folder, and choose the view (side-on, from behind, front-on). The images appear on a see-through plane beside the run.
3. Slide *Starts at frame* (in the redo panel at bottom-left, or the empty's Object Data properties). Line the clip's back-foot landing up with the bowler's.

`reference/` is gitignored. Only use footage you're allowed to use; your own filming is best (`../CAPTURE.md`).

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
| `cricket_bowling_anim.py` | The Cricket tab: start-from, ghost, reference, markers, export |
| `Assets/Editor/MocapBowlerImport.cs` | Unity: imports the FBX and wires (or removes) controller states |
| `Assets/Scripts/Bowling/MocapDelivery.cs` | Game: picks the delivery and moves the mark, from the sidecars |
