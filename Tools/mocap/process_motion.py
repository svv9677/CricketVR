"""Landmarks (.npz from extract_pose.py) -> a grounded, oriented joint track (.json) for retarget_blender.py.

MediaPipe's world landmarks are metric but hip-centred: they hold the pose and none of the travel.
The travel comes from the camera: with a still camera each frame's skeleton is placed where it
reprojects onto its own 2D landmarks (a pinhole fit for the translation only, since the world
landmarks are already in camera axes). The focal length is not known for phone video, so it is
the one that leaves the planted feet stillest - the stride length scales with it.

Output, in Blender axes for the game's rig: Z up, the run toward -Y (the rig faces -Y), metres,
the ground at Z = 0, the first frame's hips over the origin, joints scaled to the rig's legs.

    .venv/bin/python process_motion.py out/img9676.npz out/img9676.json --start 0 --end 60
"""
import argparse
import json

import numpy as np
from scipy.signal import savgol_filter

# MediaPipe pose indices of the joints the retarget needs.
J = dict(nose=0, l_ear=7, r_ear=8, l_sh=11, r_sh=12, l_el=13, r_el=14, l_wr=15, r_wr=16,
         l_pinky=17, r_pinky=18, l_index=19, r_index=20, l_hip=23, r_hip=24, l_knee=25, r_knee=26,
         l_ank=27, r_ank=28, l_heel=29, r_heel=30, l_toe=31, r_toe=32)
ARMS = {"l_el", "r_el", "l_wr", "r_wr", "l_pinky", "r_pinky", "l_index", "r_index"}

RIG_LEG = 0.928 - 0.036   # the game rig's hip height above its toe base (Blender inspect of Bowler.fbx)


def smooth(x, window, order=2):
    """Savitzky-Golay along time, NaN-safe by interpolating gaps first."""
    x = x.copy()
    t = np.arange(len(x))
    for idx in np.ndindex(x.shape[1:]):
        col = x[(slice(None),) + idx]
        bad = np.isnan(col)
        if bad.all():
            continue
        col[bad] = np.interp(t[bad], t[~bad], col[~bad])
        x[(slice(None),) + idx] = col
    w = min(window, len(x) - (1 - len(x) % 2))
    return savgol_filter(x, w, order, axis=0) if w > order + 1 else x


def translation(world, image, vis, fx, cx, cy, w, h):
    """Per frame, the camera-frame translation T so (world + T) reprojects onto the 2D landmarks."""
    F = len(world)
    T = np.zeros((F, 3))
    for f in range(F):
        u = image[f, :, 0] * w - cx
        v = image[f, :, 1] * h - cy
        X, Y, Z = world[f, :, 0], world[f, :, 1], world[f, :, 2]
        wt = np.clip(vis[f], 0.05, 1.0)
        # fx (X + Tx) = u (Z + Tz)  ->  fx Tx - u Tz = u Z - fx X ; same for y.
        A = np.concatenate([np.stack([np.full(33, fx), np.zeros(33), -u], 1),
                            np.stack([np.zeros(33), np.full(33, fx), -v], 1)])
        b = np.concatenate([u * Z - fx * X, v * Z - fx * Y])
        W = np.concatenate([wt, wt])
        T[f] = np.linalg.lstsq(A * W[:, None], b * W, rcond=None)[0]
    return T


def foot_slip(cam, fps):
    """How much the stiller foot moves each frame, summed: zero if one foot is always planted."""
    feet = []
    for s in ("l", "r"):
        p = (cam[:, J[f"{s}_heel"]] + cam[:, J[f"{s}_toe"]]) / 2
        feet.append(np.linalg.norm(np.diff(p, axis=0), axis=1) * fps)
    stiller = np.minimum(feet[0], feet[1])
    slip = float(np.sum(np.sort(stiller)[: int(len(stiller) * 0.6)]))   # the planted 60% of frames
    # Relative to the ground covered: a shorter focal length shrinks everything, slip included,
    # so raw slip always prefers the shortest one.
    hips = (cam[:, J["l_hip"]] + cam[:, J["r_hip"]]) / 2
    travel = float(np.sum(np.linalg.norm(np.diff(hips, axis=0), axis=1)) * fps)
    return slip / max(travel, 1e-6)


def first_peak(x, frac=0.85):
    """The first local maximum that reaches frac of the global maximum."""
    top = np.nanmax(x)
    for i in range(1, len(x) - 1):
        if x[i] >= frac * top and x[i] >= x[i - 1] and x[i] >= x[i + 1]:
            return i
    return int(np.nanargmax(x))


def parse_contacts(text):
    out = []
    for item in text.split(","):
        side, rng = item.strip().split(":")
        f0, f1 = rng.split("-")
        out.append({"side": side, "start": int(f0), "end": int(f1)})
    return out


def auto_contacts(image, w, h, fps, still=1.0, min_frames=2):
    """Footfalls where a foot is still in the picture (static camera): slower than `still` body
    heights a second for at least `min_frames`."""
    px = image[..., :2] * [w, h]
    body = np.nanmedian(np.linalg.norm(px[:, J["l_sh"]] - px[:, J["l_ank"]], axis=1))
    out = []
    for s in ("l", "r"):
        p = (px[:, J[f"{s}_heel"]] + px[:, J[f"{s}_toe"]]) / 2
        sp = np.r_[np.linalg.norm(np.diff(p, axis=0), axis=1), np.inf] * fps / body
        f = 0
        while f < len(sp):
            if sp[f] < still:
                e = f
                while e + 1 < len(sp) and sp[e + 1] < still:
                    e += 1
                if e - f + 1 >= min_frames:
                    out.append({"side": s, "start": f, "end": e})
                f = e + 1
            else:
                f += 1
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("npz")
    ap.add_argument("out")
    ap.add_argument("--start", type=int, default=0, help="first frame kept")
    ap.add_argument("--end", type=int, default=10 ** 9, help="last frame kept (inclusive)")
    ap.add_argument("--arm", choices=["right", "left"], default="right")
    ap.add_argument("--fx", type=float, default=0.72,
                    help="focal length as a fraction of the frame width (iPhone main lens, 4K with "
                         "stabilisation: ~0.72); 0 = fit from foot slip (rarely well determined)")
    ap.add_argument("--release", type=int, default=-1, help="release frame (relative to --start); -1 = auto")
    ap.add_argument("--contacts", default="",
                    help="footfalls, frames relative to --start, e.g. 'l:0-1,r:9-15,l:16-27'. Default: "
                         "from 2D foot speed, which fails where a foot is hidden (behind the stumps)")
    a = ap.parse_args()

    d = np.load(a.npz)
    fps = float(d["fps"])
    w, h = int(d["width"]), int(d["height"])
    sl = slice(a.start, a.end + 1)
    world, image, vis = d["world"][sl], d["image"][sl], d["vis"][sl]

    # Smooth the pose (arms lightly: the bowling arm turns over in ~0.3 s) and the 2D track.
    sm = world.copy()
    for name, i in J.items():
        sm[:, i] = smooth(world[:, i], 5 if name in ARMS else 7)
    world = sm
    image = smooth(image, 7)

    cx, cy = w / 2, h / 2
    if a.fx > 0:
        fx = a.fx * w
    else:
        cands = np.linspace(0.5, 1.4, 37) * w
        slips = []
        for c in cands:
            T = smooth(translation(world, image, vis, c, cx, cy, w, h), 9)
            slips.append(foot_slip(world + T[:, None, :], fps))
        fx = float(cands[int(np.argmin(slips))])
        print("focal fit: " + ", ".join(f"{c / w:.2f}w:{s:.3f}" for c, s in zip(cands[::4], slips[::4])))
    # The per-frame fit jitters by tens of cm in depth (the camera is behind the run, depth is the
    # weak axis): planted feet slid at 3-16 m/s. A bowler's hips travel smoothly through the
    # delivery, so the path is a cubic in time through the per-frame fits; the feet are pinned
    # later (retarget_blender.py) from the contacts.
    T_raw = translation(world, image, vis, fx, cx, cy, w, h)
    t = np.arange(len(T_raw))
    T = np.stack([np.polyval(np.polyfit(t, T_raw[:, k], 3), t) for k in range(3)], 1)
    cam = world + T[:, None, :]          # camera axes: x right, y down, z away from the camera

    hips = (cam[:, J["l_hip"]] + cam[:, J["r_hip"]]) / 2
    centre = hips[0]
    # Release: the bowling wrist highest above its shoulder in the picture, in body heights - a
    # 2D measure, so the shaky depth plays no part. Bowlers often raise the arm again in the
    # follow-through, so it is the first clear peak that counts.
    side = "r" if a.arm == "right" else "l"
    body_px = np.nanmedian(np.linalg.norm(image[:, J[f"{side}_sh"], :2] - image[:, J[f"{side}_ank"], :2], axis=1))
    lift = (image[:, J[f"{side}_sh"], 1] - image[:, J[f"{side}_wr"], 1]) / body_px
    release = a.release if a.release >= 0 else first_peak(lift)
    # Up and forward. A plane fitted under the planted feet is badly determined - every contact
    # lies along one line, the run - and tipped the whole clip ~30 deg sideways. Instead: the
    # ground contains the run, and a phone held for video is level (no roll), so its x axis lies
    # in the ground too. Measured on IMG_9676: upright frames' torso is within 2 deg of camera -y.
    travel = hips[release] - hips[0]
    up = np.cross(travel, [1.0, 0.0, 0.0])
    up /= np.linalg.norm(up)
    if up[1] > 0:                       # camera y points down
        up = -up
    fwd = travel - (travel @ up) * up
    fwd /= np.linalg.norm(fwd)
    right = np.cross(fwd, up)              # right-handed: right = forward x up

    # Blender axes: -Y forward, Z up, X to the bowler's left (the rig's +X is its left side).
    def to_blender(p):
        rel = p - centre
        return np.stack([-(rel @ right), -(rel @ fwd), rel @ up], -1)

    out = to_blender(cam)
    # Ground at the lowest planted foot height, first hips over the origin.
    feet_z = np.concatenate([out[:, J[k], 2] for k in ("l_heel", "r_heel", "l_toe", "r_toe")])
    out[..., 2] -= np.percentile(feet_z, 5)
    hip0 = (out[0, J["l_hip"]] + out[0, J["r_hip"]]) / 2
    out[..., 0] -= hip0[0]
    out[..., 1] -= hip0[1]

    # Scale to the rig: the subject's hip height over his toes, standing frames, against the rig's.
    leg = np.median(np.linalg.norm(world[:, J["l_hip"]] - world[:, J["l_ank"]], axis=1) +
                    np.linalg.norm(world[:, J["r_hip"]] - world[:, J["r_ank"]], axis=1)) / 2
    scale = RIG_LEG / (leg + 0.05)
    out *= scale

    contacts = parse_contacts(a.contacts) if a.contacts else auto_contacts(image, w, h, fps)
    res = {
        "contacts": contacts,
        "fps": fps,
        "frames": len(out),
        "start_frame": a.start,
        "release_frame": release,
        "arm": a.arm,
        "focal_px": fx,
        "scale": scale,
        "joints": {name: out[:, i].round(5).tolist() for name, i in J.items()},
    }
    with open(a.out, "w") as fh:
        json.dump(res, fh)
    hips_b = (out[:, J["l_hip"]] + out[:, J["r_hip"]]) / 2
    print(f"{len(out)} frames @ {fps:.2f}, focal {fx / w:.2f}w, release frame {release}, "
          f"scale {scale:.3f}, hips travel to release {np.linalg.norm(hips_b[release, :2] - hips_b[0, :2]):.2f} m, "
          f"total {np.linalg.norm(hips_b[-1, :2] - hips_b[0, :2]):.2f} m")


if __name__ == "__main__":
    main()
