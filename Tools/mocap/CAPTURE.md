# Filming bowling actions for mocap (two or three cameras)

One camera has to guess depth, and from the side it cannot tell the near leg from the far one:
that is where the leaning, the short stride and the swapped legs came from. Two cameras at a
wide angle measure depth instead (triangulation, Pose2Sim: see `multicam/`).

## Kit

- **2 phones** (3 is better), and something to hold each one still: tripods, or a phone clamp on a
  net pole. Hand-held does not work: a camera must not move between calibration and the last ball.
- A **tape measure** (10 m+) and **6–10 markers**: cones, chalk, or bright tape.
- Optional: a printed **checkerboard** (A3, taped flat to a board) for lens calibration.

## Phone settings (every phone the same)

- **Slo-mo 240 fps** (iPhone: Camera → Slo-Mo, 1080p at 240). Any phone: the highest frame rate
  it does at 1080p. The bowling arm turns over in about 0.15 s, which is 4 frames at 30 fps.
- **Stabilisation off** where the phone allows it (iPhone: Settings → Camera → Record Video →
  Enhanced Stabilization off). Stabilisation shifts the picture frame to frame, which breaks
  calibration.
- **Lock focus and exposure** (iPhone: press and hold on the bowler's spot until AE/AF LOCK).
- **No zooming, and no switching lenses** once calibration is filmed.

## Where to put the cameras

Origin = the base of the middle stump at the bowler's end. X runs up the pitch toward the batter,
Z is up, and Y is to the bowler's left as he faces the batter.

| camera | where | why |
|---|---|---|
| **A: side-on** | Square of the bowling crease, 10–15 m out, about hip height (1 m). On the side his chest faces at back-foot landing: for a right-armer, his right as he runs in | sees the stride and the arm's whole circle |
| **B: front-on** | 10–15 m down the pitch in front of him, a little to one side so the batter (or the net) is not in the way, about 1.5 m high | sees what A cannot: the feet across the crease and the arm's line |
| C (optional): behind | Behind his run-up, like IMG_9676 | a third angle catches whatever the other two lose |

The angle between A and B, seen from the crease, should be **60–90°**.

**In frame in every camera:** the whole body, from the last three strides to two steps after he
lets go, with him at least a third of the picture's height. Nothing between a camera and his
feet: check that the stumps are not hiding the front foot, as they did in IMG_9676.

## Calibration (2 minutes, before the first ball)

With every camera already fixed and recording:

1. **Scene points** (needed). Mark these with cones or tape, measure them, and film 5 seconds
   where every camera sees all of them:

   | point | x (m) | y (m) | z (m) |
   |---|---|---|---|
   | middle stump, base | 0 | 0 | 0 |
   | off and leg stumps, base (centres) | 0 | ±0.095 | 0 |
   | stump tops (all three) | 0 | 0, ±0.095 | 0.711 |
   | bowling crease meets the return creases | 0 | ±1.32 | 0 |
   | popping crease meets the return creases | 1.22 | ±1.32 | 0 |
   | batter's-end middle stump, base and top (if in view) | 20.12 | 0 | 0, 0.711 |

   These are the points `multicam/Config.toml` already lists. Nets without painted creases: lay
   them out with the tape measure. The stump tops are what make the set three-dimensional, so keep
   the stumps in.

2. **Checkerboard** (optional, improves the lens model). Film it for 20 s with each phone, at the
   same settings, tilting it and moving it around the whole picture.

## Recording

1. Start every camera.
2. **Sync:** clap sharply, or flash a phone torch, where every camera can see it. Pose2Sim also
   syncs from the motion itself; the clap is the backup.
3. Bowl. Several balls of each kind you want: pace, medium, off-spin, leg-spin, slower balls.
   Left-armers come from mirroring, so right-arm is enough.
4. Stop. Do not move a camera between balls. If one moves, film the calibration points again.

## Getting it to the Mac

AirDrop the files from every phone, with **Options → All Photos Data** switched on: a normal share
of a slo-mo clip sends a 30 fps copy, and the 240 fps frames are lost. Name them by camera and ball: `A_ball1.mov`, `B_ball1.mov`, and
so on. Also send the calibration clips and the measured points, if you laid them out yourself.
Then follow `multicam/README.md`.

## Rights

Footage of you, or of people who agree to it, can be shipped in the game. Footage of other people's
matches (YouTube, broadcast) is for testing only.
