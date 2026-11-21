"""Left/right label swaps in single-camera pose, undone before anything else reads the landmarks.

Side-on (and from behind), the near and far leg look alike, and MediaPipe now and then hands a
leg's label to the other one for a few frames: a planted foot's label jumps to the lifted foot and
back (measured on the Anderson clip, 2026-11-13). Its 3D is no help: the 3D model places whatever
it calls "left" on the left of its own pelvis every frame, so a side check never fires.

What does give the swaps away is motion: a relabelled limb jumps. Per limb pair (legs: knee,
ankle, heel, toe; arms: elbow, wrist, hand points - hips and shoulders define the body and are left
alone) each frame is either as labelled or swapped, and the sequence of choices is the one with
the smoothest motion over the whole clip (Viterbi, scoring each frame against the velocity
predicted from the two before it, in 2D and 3D, each point weighted by its visibility), with a
cost for every swapped frame so it only swaps where the motion clearly says so.

Measured 2026-11-13, by eye against the raw tracks: IMG_9676 legs swap at frames 3-6 (the planted
foot's label jumps ~300 px to the lifted foot and back) and 20-21 (the "right" ankle on the hidden,
planted front foot, the "left" one in the air); Anderson's legs at 27-28. No real arm swaps in
either: the jumps there are hidden points (visibility 0.01-0.1) wandering, and a fast bowling arm
at 30 fps out-accelerates any cap, so arms need far stronger evidence than legs.

Continuity alone cannot tell which of the two consistent labellings is right, so two cricket facts
choose between the path and its complement, by a vote over frames at the release (the first frame
a wrist is clearly highest above its shoulder) - never by forcing single frames, which pushed a
false swap onto the frames after IMG_9676's hidden front foot:
  - the bowling arm's wrist is the higher one (release +-1);
  - the front foot, the non-bowling side's, is the one further along the run (release -1..+4) -
    only when the run crosses the picture (side-on). Run away from the camera, "further along"
    is depth, MediaPipe's weakest axis, and it voted IMG_9676's whole clip the wrong way round.
"""
import numpy as np

LEGS = ([25, 27, 29, 31], [26, 28, 30, 32])          # knee, ankle, heel, toe (left, right)
ARMS = ([13, 15, 17, 19, 21], [14, 16, 18, 20, 22])  # elbow, wrist, pinky, index, thumb

# Per swapped frame, in units of a typical point's squared acceleration (sweep on both clips: legs
# give the swaps above from 4 to 12; arms flag single frames below 60, none of them real).
SWAP_COST = {"legs": 12.0, "arms": 60.0}
CAP = 1e9           # no cap: capping let a whipping arm's real acceleration hide a fake swap
VIS_POWER = 4       # continuity weighted by MediaPipe's visibility ** this: a hidden foot (behind
                    # the stumps, vis ~0.75) wanders and must not read as a swap


def _chains(x, pair, s):
    """Both chains' points (2 * k, ...) with the labels as given (s=0) or swapped (s=1)."""
    left, right = pair
    return x[left + right] if s == 0 else x[right + left]


def _robust_scale(v):
    m = np.nanmedian(v)
    return m if m > 1e-9 else 1e-9


def _accel_scales(seqs):
    """Typical acceleration magnitude per point set, as labelled, for normalising each signal."""
    out = []
    for x in seqs:
        a = np.linalg.norm(x[2:] - 2 * x[1:-1] + x[:-2], axis=-1)
        out.append(_robust_scale(a))
    return out


def _accel_cost(seqs, scales, vis, pair, t, a, b, c):
    """Cost of labellings a, b, c at frames t-1, t, t+1: squared normalised accelerations, each
    point weighted by how visible it is in all three frames."""
    wt = np.minimum(np.minimum(_chains(vis[t - 1], pair, a), _chains(vis[t], pair, b)),
                    _chains(vis[t + 1], pair, c)) ** VIS_POWER
    total = 0.0
    for x, sc in zip(seqs, scales):
        acc = _chains(x[t + 1], pair, c) - 2 * _chains(x[t], pair, b) + _chains(x[t - 1], pair, a)
        r2 = np.nan_to_num(np.sum(acc ** 2, axis=-1) / sc ** 2, nan=0.0)
        total += float(np.sum(wt * np.minimum(r2, CAP)))
    return total


def _viterbi(seqs, vis, pair, unary):
    """The swap sequence (F,) minimising unary + second-order motion cost (states: last two frames)."""
    F = len(unary)
    if F < 3:
        return np.argmin(unary, axis=1)
    scales = _accel_scales([np.stack([np.concatenate([x[f][pair[0]], x[f][pair[1]]]) for f in range(F)])
                            for x in seqs])
    # State at step t is (s[t-1], s[t]); stepping to (s[t], s[t+1]) scores the acceleration at t.
    cost = {(a, b): unary[0][a] + unary[1][b] for a in (0, 1) for b in (0, 1)}
    back = []
    for t in range(1, F - 1):
        new, ptr = {}, {}
        for b in (0, 1):
            for c in (0, 1):
                options = [(cost[(a, b)] + _accel_cost(seqs, scales, vis, pair, t, a, b, c), a) for a in (0, 1)]
                best, arg = min(options)
                new[(b, c)] = best + unary[t + 1][c]
                ptr[(b, c)] = arg
        cost = new
        back.append(ptr)
    b, c = min(cost, key=cost.get)
    path = [c, b]                                  # s[F-1], s[F-2], then backwards
    for ptr in reversed(back):
        path.append(ptr[(path[-1], path[-2])])
    return np.array(path[::-1])


def _release_frame(image):
    """The first frame either wrist is clearly at its highest above its shoulder (label-free)."""
    body = np.nanmedian(np.linalg.norm(image[:, 11, :2] - image[:, 27, :2], axis=1))
    lift = np.maximum(image[:, 11, 1] - image[:, 15, 1], image[:, 12, 1] - image[:, 16, 1]) / body
    top = np.nanmax(lift)
    for i in range(1, len(lift) - 1):
        if lift[i] >= 0.85 * top and lift[i] >= lift[i - 1] and lift[i] >= lift[i + 1]:
            return i
    return int(np.nanargmax(lift))


def _apply(arrs, pair, swapped):
    left, right = pair
    for x in arrs:
        tmp = x[swapped][:, left].copy()
        x[np.ix_(swapped, left)] = x[swapped][:, right]
        x[np.ix_(swapped, right)] = tmp


def _orient(path, agrees, window):
    """Continuity fixes where labels flip, not which labelling is right: keep the path, or take its
    complement, whichever agrees with the cricket fact on more frames of the window."""
    keep = sum(agrees(t, path[t]) for t in window)
    flip = sum(agrees(t, 1 - path[t]) for t in window)
    return 1 - path if flip > keep else path, keep, flip


def fix_swaps(world, image, vis, cam, arm="right"):
    """Undo left/right swaps (on copies). `cam` is the camera-frame skeleton (world plus the per-frame
    translation, in one camera's axes), used only for which way the run goes.
    Returns world, image, vis and a report."""
    world, image, vis, cam = world.copy(), image.copy(), vis.copy(), cam.copy()
    F = len(world)
    rel = _release_frame(image)
    bowl_left = arm == "left"
    report = {"release_frame": rel}
    seqs = [image[..., :2], world, cam]

    def prior(kind):
        u = np.zeros((F, 2))
        u[:, 1] = SWAP_COST[kind]
        return u

    # Arms: the bowling wrist is the higher one at the release.
    def arm_agrees(t, s):
        higher_left = (image[t, 11, 1] - image[t, 15, 1]) > (image[t, 12, 1] - image[t, 16, 1])
        return (higher_left != (s == 1)) == bowl_left
    path, keep, flip = _orient(_viterbi(seqs, vis, ARMS, prior("arms")), arm_agrees,
                               range(max(0, rel - 1), min(F, rel + 2)))
    sw = np.where(path == 1)[0]
    _apply([world, image, vis, cam], ARMS, sw)
    report["arms"], report["arms_vote"] = sw.tolist(), (int(keep), int(flip))

    # Legs: the front foot, the non-bowling side's, is ahead along the run from the release on.
    hips = (cam[:, 23] + cam[:, 24]) / 2
    run = hips[-1] - hips[0]
    run[1] = 0.0                                  # along the ground (camera y is roughly vertical)
    run /= np.linalg.norm(run) + 1e-9

    def leg_agrees(t, s):
        ahead_left = ((cam[t, 27] - cam[t, 28]) @ run > 0) != (s == 1)
        return ahead_left != bowl_left
    path = _viterbi(seqs, vis, LEGS, prior("legs"))
    side_on = abs(run[0]) > abs(run[2])            # the run crosses the picture
    keep = flip = 0
    if side_on:
        path, keep, flip = _orient(path, leg_agrees, range(max(0, rel - 1), min(F, rel + 5)))
    sw = np.where(path == 1)[0]
    _apply([world, image, vis], LEGS, sw)
    report["legs"], report["legs_vote"] = sw.tolist(), (int(keep), int(flip)) if side_on else "skipped (run away from the camera)"
    return world, image, vis, report


def label_jumps(image, pair, body_px, thresh=0.25):
    """Frames where a chain's 2D points jump further than `thresh` body heights from the velocity
    prediction - the signature of a relabelled limb (a sanity count, before vs after)."""
    x = np.stack([np.concatenate([f[pair[0]], f[pair[1]]]) for f in image[..., :2]])
    acc = np.linalg.norm(x[2:] - 2 * x[1:-1] + x[:-2], axis=-1) / body_px
    return int(np.sum(np.nanmax(acc, axis=1) > thresh))
