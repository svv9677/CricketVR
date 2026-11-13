"""Smooth a finished animation on the bowler rig without losing the action, inside Blender:

    Blender -b out/anim/x.blend -P fusion/smooth_anim.py -P fusion/blender_extras.py -- \
        out/anim/x_smooth.fbx --save-blend out/anim/x_smooth.blend --preview-dir out/anim/x_smooth_frames

Each bone's local rotation is split into swing (where the bone points) and twist (roll about its
own length), and the two are treated differently, because they go wrong differently (measured on
Anderson, 2026-11-13, fusion/jitter.py):

- twist: the cameras barely see a limb's roll, least of all with the elbow straight, so it was a
  guess per frame (upper-arm twist 75, 18, -28, 25, 48 deg on consecutive frames). Heavily smoothed.
- swing: real and fast (the bowling arm turns over in a few frames), with the odd one-frame
  glitch (bowling-arm swing 71, 33, 59). One-frame outliers are replaced from their neighbours
  (Hampel test), then lightly smoothed - lightest on the bowling arm, so the whip survives.
- knees and elbows are hinges: a one-frame snap (knee 0 -> 48 deg) is eased by the same pass.

The hips' position is smoothed too. Feet that were pinned stay close (the report prints how far
the planted feet move); the jitter before and after is printed by fusion/jitter.py.
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
OUT_FBX = ARGS[0]
P = "mixamorig5:"

# (swing sigma, twist sigma) in frames. Bowling arm lightest; twist heavy everywhere it is a guess.
SIGMA = {
    "Hips": (1.2, 1.5), "Spine": (1.2, 2.0), "Spine1": (1.2, 2.0), "Spine2": (1.2, 2.0),
    "Neck": (1.5, 2.5), "Head": (1.5, 2.5),
    "RightShoulder": (1.0, 3.0), "RightArm": (0.8, 3.5), "RightForeArm": (0.9, 3.5), "RightHand": (1.0, 3.0),
    "LeftShoulder": (1.2, 3.0), "LeftArm": (1.3, 3.5), "LeftForeArm": (1.3, 3.5), "LeftHand": (1.3, 3.0),
    "LeftUpLeg": (1.2, 2.5), "LeftLeg": (1.3, 2.5), "LeftFoot": (1.3, 2.5), "LeftToeBase": (1.5, 2.5),
    "RightUpLeg": (1.2, 2.5), "RightLeg": (1.3, 2.5), "RightFoot": (1.3, 2.5), "RightToeBase": (1.5, 2.5),
}


def gauss(x, sigma):
    """Gaussian smoothing along axis 0, edges held (no pull toward zero at the clip's ends)."""
    if sigma <= 0:
        return x
    r = int(math.ceil(3 * sigma))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    pad = np.concatenate([np.repeat(x[:1], r, 0), x, np.repeat(x[-1:], r, 0)])
    return np.stack([np.convolve(pad[:, c], k, mode="valid") for c in range(x.shape[1])], 1)


def hampel(x, half=2, n_sig=2.5):
    """Replace one-frame outliers (per component, vs the median of +-half frames) by that median."""
    y = x.copy()
    for i in range(len(x)):
        lo, hi = max(0, i - half), min(len(x), i + half + 1)
        win = x[lo:hi]
        med = np.median(win, 0)
        mad = 1.4826 * np.median(np.abs(win - med), 0) + 1e-3
        bad = np.abs(x[i] - med) > n_sig * mad
        bad &= np.abs(x[i] - med) > 0.12          # radians: ignore sub-7-degree wobble
        y[i, bad] = med[bad]
    return y


def split(q):
    """Local rotation -> (swing as a rotation vector with no Y part, twist angle about Y)."""
    tw = Quaternion((q.w, 0.0, q.y, 0.0))
    if tw.magnitude < 1e-8:
        tw = Quaternion()
    tw.normalize()
    sw = q @ tw.inverted()
    if sw.w < 0:
        sw.negate()
    ang = 2 * math.atan2(tw.y, tw.w)
    v = Vector((sw.x, sw.y, sw.z))
    s = v.length
    rv = v * (2 * math.atan2(s, sw.w) / s) if s > 1e-9 else Vector((0.0, 0.0, 0.0))
    return np.array([rv.x, rv.y, rv.z]), ang


def join(rv, ang):
    v = Vector(rv)
    a = v.length
    sw = Quaternion(v.normalized(), a) if a > 1e-9 else Quaternion()
    tw = Quaternion(Vector((0.0, 1.0, 0.0)), ang)
    return sw @ tw


def foot_track(arm, f0, f1):
    sc = bpy.context.scene
    out = {"l": [], "r": []}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        out["l"].append((arm.matrix_world @ arm.pose.bones[P + "LeftFoot"].head).copy())
        out["r"].append((arm.matrix_world @ arm.pose.bones[P + "RightFoot"].head).copy())
    return out


def track():
    """The joint track the animation was made from (fused frames), if it is beside the .blend."""
    import json
    blend = bpy.data.filepath
    stem = blend.rsplit("_", 1)[0] if blend.endswith(("_final.blend", "_smooth.blend")) else blend[:-6]
    for cand in (stem + "_fixed.json", stem + ".json"):
        if os.path.exists(cand):
            return json.load(open(cand))
    return {}


def contacts():
    return track().get("contacts", [])


# The bowling arm's whip is real only from the downswing to just after the release (measured on
# Anderson: arm turning 20-33 deg/frame there). Outside it the arm moves slowly, and a fast turn
# is an error (49-67 deg/frame after the ball had gone; 38 at the gather with both hands at his
# chin). So: light smoothing in the whip, heavy outside, eased between.
WHIP = (7, 3)            # frames before / after the release
HEAVY = 2.6              # swing sigma outside the whip


def whip_weight(n, release):
    """1 inside the whip (keep it light), 0 outside, a 2-frame ease at each end."""
    w = np.zeros(n)
    a, b = release - WHIP[0], release + WHIP[1]
    for i in range(n):
        if a <= i <= b:
            w[i] = 1.0
        elif a - 2 <= i < a:
            w[i] = (i - (a - 2)) / 2.0
        elif b < i <= b + 2:
            w[i] = ((b + 2) - i) / 2.0
    return w


def align_bone(arm, pb, want_dir):
    """Turn a bone about its head so it points along want_dir (world), the least turn."""
    mw = arm.matrix_world
    head, tail = mw @ pb.head, mw @ pb.tail
    rot = (tail - head).normalized().rotation_difference(want_dir.normalized())
    to_arm = mw.to_3x3().normalized().inverted()
    axis, ang = rot.to_axis_angle()
    R = Matrix.Rotation(ang, 4, (to_arm @ axis).normalized())
    h = pb.head.copy()
    pb.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()


def repin(arm, f0, f1, before):
    """Planted feet back where they were before smoothing: two-bone IK in the knee's own plane.
    The hip-to-ankle distance is smoothed over each footfall, so the knee bends and straightens
    gradually: pinned exactly, the knee locked straight in one frame wherever the hips sat a
    little far from the pin (back knee 48, 29, 1 deg on Anderson). The foot gives a few mm along
    the leg's line instead."""
    sc = bpy.context.scene
    side = {"l": "Left", "r": "Right"}
    for c in contacts():
        S = side[c["side"]]
        frames = list(range(max(f0, c["start"]), min(f1, c["end"]) + 1))
        if not frames:
            continue
        geo = []
        for f in frames:
            sc.frame_set(f)
            mw = arm.matrix_world
            up, lo = arm.pose.bones[P + S + "UpLeg"], arm.pose.bones[P + S + "Leg"]
            H, K, A = mw @ up.head, mw @ lo.head, mw @ lo.tail
            geo.append((H, K, A, before[c["side"]][f - f0]))
        l1 = (geo[0][1] - geo[0][0]).length
        l2 = (geo[0][2] - geo[0][1]).length
        reach = l1 + l2
        dist = np.array([[min((T - H).length, reach * 0.995)] for H, K, A, T in geo])
        dist = gauss(dist, 1.5)[:, 0]
        for f, (H, K, A, T), d in zip(frames, geo, dist):
            sc.frame_set(f)
            mw = arm.matrix_world
            up, lo = arm.pose.bones[P + S + "UpLeg"], arm.pose.bones[P + S + "Leg"]
            H, K = mw @ up.head, mw @ lo.head
            u = (T - H).normalized()
            d = float(min(max(d, abs(l1 - l2) + 1e-4), reach * 0.995))
            v = (K - H) - u * (K - H).dot(u)
            if v.length < 1e-6:
                continue
            v.normalize()
            a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
            k_new = H + u * a + v * math.sqrt(max(l1 * l1 - a * a, 0.0))
            align_bone(arm, up, k_new - H)
            align_bone(arm, lo, (H + u * d) - (mw @ lo.head))
            for pb in (up, lo):
                pb.keyframe_insert("rotation_quaternion", frame=f)
    print(f"smooth: re-pinned {len(contacts())} footfalls")


def even_out(new, chain, release, n):
    """Same path, smoother timing, for the bowling arm from its downswing to the end: the data
    crammed the follow-through into a few frames (49, 67, 48 deg/frame after the release against
    20-33 through it). Each frame's pose is taken from the original path at a time chosen so the
    arm's angular speed rises and falls smoothly; the path and its end points are unchanged."""
    lead = chain[0]
    a = max(0, release - WHIP[0] - 4)        # start in the slow part, so the change of pace has no seam
    q = new[lead]
    steps = np.array([q[i].rotation_difference(q[i + 1]).angle for i in range(a, n - 1)])
    if len(steps) < 4 or steps.sum() < 1e-6:
        return
    smooth = gauss(steps[:, None], 1.6)[:, 0]
    smooth *= steps.sum() / smooth.sum()
    cum = np.concatenate([[0.0], np.cumsum(steps)])
    cum_new = np.concatenate([[0.0], np.cumsum(smooth)])
    t = np.interp(cum_new, cum, np.arange(len(cum)))        # original frame (from a) for each new frame
    for b in chain:
        if b not in new:
            continue
        src = [x.copy() for x in new[b]]
        for k, tk in enumerate(t):
            i = int(np.floor(tk))
            frac = tk - i
            j = min(i + 1, len(cum) - 1)
            new[b][a + k] = src[a + i].slerp(src[a + j], frac)
    print(f"smooth: follow-through re-timed from frame {a}: steps {np.degrees(steps).round(0).tolist()} "
          f"-> {np.degrees(smooth).round(0).tolist()}")


def main():
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    f0, f1 = (int(v) for v in arm.animation_data.action.frame_range)
    sc = bpy.context.scene
    before = foot_track(arm, f0, f1)

    data = {}
    hips_loc = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        hips_loc.append(np.array(arm.pose.bones[P + "Hips"].location))
        for b in SIGMA:
            pb = arm.pose.bones.get(P + b)
            if pb is None:
                continue
            data.setdefault(b, []).append(pb.matrix_basis.to_quaternion())

    tr = track()
    release = int(tr.get("release_frame", -100))
    side = "Right" if tr.get("arm", "right") == "right" else "Left"
    whip_bones = {side + "Arm", side + "ForeArm"} if release >= 0 else set()
    print(f"smooth: release frame {release}, whip on {sorted(whip_bones)}")
    new = {}
    for b, qs in data.items():
        sw, tw = zip(*(split(q) for q in qs))
        sw = np.array(sw)
        tw = np.unwrap(np.array(tw))[:, None]
        s_sig, t_sig = SIGMA[b]
        # the arms' glitches can last two frames (bowling-arm swing 57, 24, 49): a wider window
        half = 3 if b.endswith("Arm") or b.endswith("ForeArm") else 2
        clean = hampel(sw, half)
        sw = gauss(clean, s_sig)
        if b in whip_bones:
            heavy = gauss(clean, HEAVY)
            w = whip_weight(len(sw), release)[:, None]
            sw = w * sw + (1 - w) * heavy
        tw = gauss(hampel(tw), t_sig)
        new[b] = [join(sw[i], tw[i, 0]) for i in range(len(qs))]
    hips = gauss(hampel(np.array(hips_loc)), 1.0)
    if whip_bones:
        even_out(new, [side + "Arm", side + "ForeArm", side + "Hand"], release, len(hips))

    for i, f in enumerate(range(f0, f1 + 1)):
        for b, qs in new.items():
            pb = arm.pose.bones[P + b]
            q = qs[i]
            prev = pb.rotation_quaternion
            if i and q.dot(prev) < 0:
                q.negate()
            pb.rotation_quaternion = q
            pb.keyframe_insert("rotation_quaternion", frame=f)
        hb = arm.pose.bones[P + "Hips"]
        hb.location = Vector(hips[i])
        hb.keyframe_insert("location", frame=f)

    repin(arm, f0, f1, before)
    after = foot_track(arm, f0, f1)
    for s in ("l", "r"):
        moved = [(a - b).length for a, b in zip(after[s], before[s])]
        planted = [moved[f - f0] for c in contacts() if c["side"] == s
                   for f in range(max(f0, c["start"]), min(f1, c["end"]) + 1)]
        print(f"smooth: {s} foot moved mean {np.mean(moved) * 100:.1f} cm (max {max(moved) * 100:.1f}); "
              f"while planted mean {np.mean(planted) * 100 if planted else 0:.1f} cm")

    for o in bpy.data.objects:
        o.select_set(o == arm or (o.type == "MESH" and o.parent == arm))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(OUT_FBX), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=True,
                             bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_simplify_factor=0.0, bake_anim_force_startend_keying=True)
    print(f"smooth: exported {OUT_FBX}")


main()
