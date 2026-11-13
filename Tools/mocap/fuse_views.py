"""Two cameras, one bowling action: a side-on and a front-on (or behind) clip fused into 3D.

One camera has to guess depth, and from the side it cannot tell the near leg from the far one.
Two views at a wide angle each see what the other cannot: the side view sees the run and the arm's
circle, the front view sees across the body. They need not be the same ball - a bowler repeats his
action - so the clips are matched by the action's rhythm, not by a clock.

    extract_pose.py side.mov out/side.npz [--pan --seed ...]     # each view on its own
    extract_pose.py front.mov out/front.npz
    .venv/bin/python fuse_views.py --side out/side.npz --front out/front.npz --front-kind front \\
        --out out/fused.json --fbx out/fused.fbx --blend out/fused.blend --preview out/fused.mp4

Steps (fusion/): each view's labels cleaned on its own (swap_fix's motion continuity; the front
view's whole-body left/right from which side of the picture his left is on); the clips put on one
timeline (label-free heights in leg lengths, anchored at the release, dynamic time warping in a
band); two approximate pinhole cameras placed from what is known about them (fusion/geometry.py);
the side view's left/right labels chosen per limb by which agrees with the front view in 3D; every
joint triangulated, smoothed, its bones held at their median lengths; written as the joint track
retarget_blender.py takes (same keys, axes and rig scale as process_motion.py), then FBX, .blend
and a three-view preview through Blender (fusion/blender_extras.py).
"""
import argparse
import json
import os
import subprocess
import sys

import numpy as np
from scipy.signal import savgol_filter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from fusion.align import body_scale, dtw_map, features, resample, sample_at  # noqa: E402
from fusion.geometry import (cam_axes, chain_swap_cost, fix_bone_lengths, picture_right,  # noqa: E402
                             place_camera, triangulate)
from fusion.views import BODY, View, viterbi2  # noqa: E402
from process_motion import J, RIG_LEG  # noqa: E402
from swap_fix import ARMS, LEGS, _apply  # noqa: E402

BLENDER = os.environ.get("BLENDER", "/Applications/Blender.app/Contents/MacOS/Blender")
ARM_POINTS = list(range(13, 23))
# Long bones whose lengths must stay constant (self-calibration): upper arms, forearms, thighs,
# shins, and the shoulder and hip widths.
BONES_CHECK = [(11, 13), (13, 15), (12, 14), (14, 16), (23, 25), (25, 27), (24, 26), (26, 28), (11, 12), (23, 24)]


def side_sign(view, rays):
    """+1 if the side camera stands on the bowler's right (his run goes left to right in the
    picture). From the hips' travel; for a bowler standing still, from his nose ahead of his ears."""
    hip_x = (rays[:, 23, 0] / rays[:, 23, 2] + rays[:, 24, 0] / rays[:, 24, 2]) / 2
    leg = np.median(view.leg_px()) / view.fx
    travel = hip_x[-1] - hip_x[0]
    if abs(travel) > 0.5 * leg:
        return float(np.sign(travel)), "run"
    nose = view.px[:, 0, 0] - (view.px[:, 7, 0] + view.px[:, 8, 0]) / 2
    return (1.0 if np.median(nose) > 0 else -1.0), "face"


def timeline(side, front, band_s):
    """Fractional native front frame for every side frame (NaN outside the overlap)."""
    fs = features(side)
    ff_native = features(front)
    ff = resample(ff_native, front.fps, side.fps)
    anchor_f = int(round(front.release * side.fps / front.fps))
    j = dtw_map(fs, ff, side.release, anchor_f, max(2, int(round(band_s * side.fps))))
    return j * front.fps / side.fps


def cross_view_labels(Cs, Cf, rs, rf, ws, wf):
    """Side view's left/right per chain, chosen to agree with the front view (2-state Viterbi)."""
    report = {}
    for name, pair, switch in (("torso", BODY, 10.0), ("legs", LEGS, 3.0), ("arms", ARMS, 3.0)):
        cost = chain_swap_cost(Cs, Cf, rs, rf, ws, wf, pair)
        # In units of how well the cameras agree as labelled: with a rough camera model every
        # residual is larger, and a fixed 5 cm scale let geometry error pass for a swap (measured:
        # focal guessed 2.2x short, 2-5% of frames crossed by wrong relabels).
        cost = cost / max(0.02, float(np.median(cost[:, 0])))
        path = viterbi2(cost, switch)
        frames = np.where(path == 1)[0]
        if len(frames):
            _apply([rs, ws], pair, frames)
        report[name] = frames.tolist()
    return report


def make_cameras(side, front, keep, j, ref_s, ref_f, sgn, leg_m, geo):
    """Both cameras and their world rays on the fused timeline, for geometry
    geo = (yaw_side, yaw_front, tilt_side, tilt_front)."""
    yaw_s, yaw_f, tilt_s, tilt_f = geo
    Cs, rws = place_camera(side, side.rays(), cam_axes(picture_right("side", sgn, yaw_s), tilt_s),
                           ref_s, leg_m, body_scale(side)[ref_s])
    Cf, rwf = place_camera(front, front.rays(), cam_axes(picture_right(front.kind, 1.0, yaw_f), tilt_f),
                           ref_f, leg_m, body_scale(front)[ref_f])
    rf = sample_at(rwf, j)
    return Cs, rws[keep], Cf, rf / np.linalg.norm(rf, axis=-1, keepdims=True)


def self_calibrate(side, front, keep, j, ref_s, ref_f, sgn, leg_m, geo0, ws, wf, fit_focal=False):
    """The cameras' yaw and tilt that make the two views' rays meet best on the body: the body is
    the calibration object (no markers). Robust mean of the triangulation residual over well-seen
    points; the side camera's yaw stays fixed (it defines the run's direction)."""
    from scipy.optimize import minimize
    good = np.minimum(ws, wf) > 0.5
    fx0 = (side.fx, front.fx)

    def cost(x):
        side.fx, front.fx = fx0[0] * np.exp(x[3]), fx0[1] * np.exp(x[4])
        Cs, rs, Cf, rf = make_cameras(side, front, keep, j, ref_s, ref_f, sgn, leg_m, (geo0[0], x[0], x[1], x[2]))
        p, _ = triangulate([Cs, Cf], [rs, rf], [ws, wf])
        # The miss as an angle at each camera, not in metres: a shorter focal moves both cameras
        # closer and shrinks every metric residual, which the search took (degenerate, measured).
        ang = 0.0
        for C, r in ((Cs, rs), (Cf, rf)):
            d = p - C
            n = np.linalg.norm(d, axis=-1)
            ang = ang + np.linalg.norm(d - (d * r).sum(-1, keepdims=True) * r, axis=-1) / np.maximum(n, 1e-6)
        # Rays alone are degenerate (turned near-parallel, every ray "meets" at a small angle:
        # measured, yaw went to -75). The body breaks the tie: its bones keep their length.
        cv = [np.std(L) / (np.mean(L) + 1e-9) for L in (np.linalg.norm(p[:, a] - p[:, b], axis=-1)
                                                       for a, b in BONES_CHECK)]
        leg = np.median(np.linalg.norm(p[:, 23] - p[:, 25], axis=-1) + np.linalg.norm(p[:, 25] - p[:, 27], axis=-1))
        return (float(np.mean(np.minimum(ang[good], 0.05))) + 0.05 * float(np.mean(cv))
                + 0.02 * abs(np.log(leg / leg_m)))
    x0 = np.array([*geo0[1:], 0.0, 0.0], float)
    # A coarse grid first (yaw within +-60 of the given, tilts 0-30, a shared focal x0.6-2.5): the
    # residual has local minima, and a wrong focal length otherwise hides in the tilts (measured on
    # Pose2Sim's demo: phone-default focal 2.2x short, tilts came out 38-40 deg against 18-25).
    # Focal only on request: on Pose2Sim's demo (a subject who barely moves, so little perspective
    # change) fitting it ran to its bounds and made things worse; give it from the phone instead.
    focals = np.log([0.6, 1.0, 1.6, 2.5]) if fit_focal else [0.0]
    grid = [(x0[0] + dy, ts, tf, lf, lf) for dy in range(-60, 61, 15) for ts in (0, 15, 30)
            for tf in (0, 15, 30) for lf in focals]
    start = np.array(min(grid, key=cost), float)
    if fit_focal:
        best = minimize(cost, start, method="Nelder-Mead", options=dict(xatol=0.1, fatol=1e-6, maxiter=1500)).x
    else:
        best = start.copy()
        best[:3] = minimize(lambda y: cost(np.r_[y, 0.0, 0.0]), start[:3], method="Nelder-Mead",
                            options=dict(xatol=0.1, fatol=1e-6, maxiter=800)).x
    x = np.clip(best, [-75.0, -15.0, -15.0, np.log(0.3), np.log(0.3)], [75.0, 60.0, 60.0, np.log(4), np.log(4)])
    before, after = cost(x0), cost(x)
    side.fx, front.fx = fx0[0] * np.exp(x[3]), fx0[1] * np.exp(x[4])      # keep the fitted focals
    return (geo0[0], *x[:3]), before, after, (float(np.exp(x[3])), float(np.exp(x[4])))


def smooth(P, fps):
    out = P.copy()
    for j in range(33):
        sec = 0.10 if j in ARM_POINTS else 0.15
        w = max(5, int(round(sec * fps)) | 1)
        if len(P) > w:
            out[:, j] = savgol_filter(P[:, j], w, 2, axis=0)
    return out


def prepare(a):
    """Both views loaded and cleaned on their own, and on one timeline."""
    side = View(a.side, "side", 0, a.release_side)
    front = View(a.front, a.front_kind, 0, a.release_front)
    for v, frac in ((side, a.fx_side), (front, a.fx_front)):
        if frac > 0:
            v.fx = frac * v.w
        v.continuity_fix()
    front.orient_by_side()
    j = timeline(side, front, a.band)
    keep = np.where(~np.isnan(j))[0]
    if len(keep) < 5:
        raise SystemExit("the two clips do not overlap: check the release frames (--release-side/--release-front)")
    return side, front, j[keep], keep


def fuse(a):
    side, front, j, keep = prepare(a)
    px_continuity = side.px.copy()
    rel = int(np.argmin(np.abs(keep - side.release)))           # the release on the fused timeline
    sgn, how = (side_sign(side, side.rays()) if a.side_forward == "auto"
                else ((1.0 if a.side_forward == "right" else -1.0), "given"))
    ref_s, ref_f = side.release, int(np.clip(round(j[rel]), 0, len(front) - 1))
    leg_m = (side.leg_m() + front.leg_m()) / 2
    wf = np.clip(sample_at(front.vis, j), 0.05, 1.0)
    geo, calib, labels = (a.yaw_side, a.yaw_front, a.tilt_side, a.tilt_front), None, {}
    # Calibrate, relabel the side view against the front, and calibrate again on the clean labels
    # (a limb mislabelled the whole clip otherwise skews the first fit: measured, 84 mm against 56).
    for _ in range(2 if a.self_calibrate else 1):
        ws = np.clip(side.vis[keep], 0.05, 1.0)
        if a.self_calibrate:
            geo, before, after, fxs = self_calibrate(side, front, keep, j, ref_s, ref_f, sgn, leg_m, geo, ws, wf,
                                                     a.fit_focal)
            calib = dict(yaw_front=round(float(geo[1]), 1), tilt_side=round(float(geo[2]), 1),
                         tilt_front=round(float(geo[3]), 1), focal_x_side=round(fxs[0], 2),
                         focal_x_front=round(fxs[1], 2), fx_side_px=round(side.fx), fx_front_px=round(front.fx),
                         residual_before_rad=round(before, 4), residual_after_rad=round(after, 4))
        Cs, rs, Cf, rf = make_cameras(side, front, keep, j, ref_s, ref_f, sgn, leg_m, geo)
        found = cross_view_labels(Cs, Cf, rs.copy(), rf, ws.copy(), wf)
        for chain, frames in found.items():
            if frames:
                side.swap({"torso": BODY, "legs": LEGS, "arms": ARMS}[chain], keep[np.array(frames)])
            labels[chain] = sorted(set(labels.get(chain, [])) ^ set(int(f) for f in frames))
    ws = np.clip(side.vis[keep], 0.05, 1.0)
    Cs, rs, Cf, rf = make_cameras(side, front, keep, j, ref_s, ref_f, sgn, leg_m, geo)
    P, res = triangulate([Cs, Cf], [rs, rf], [ws, wf])
    P, _ = fix_bone_lengths(smooth(P, side.fps))
    info = dict(side_sign=(sgn, how), self_calibration=calib,
                cam_side=Cs.round(2).tolist(), cam_front=Cf.round(2).tolist(),
                residual_m=float(np.median(res)), cross_view_swaps={k: len(v) for k, v in labels.items()},
                side_report=side.report, front_report=front.report, frames=len(keep),
                first_side_frame=int(keep[0]), front_frame_at_release=float(j[rel]), release=rel, leg_m=leg_m)
    extra = dict(views=(side, front), j=j, cross=labels, residual=res, px_continuity=px_continuity)
    return P, side, rel, keep, info, extra


def auto_contacts(out, fps, low=0.07, still=1.0, min_s=0.05):
    """Footfalls from the fused feet: sole near the ground and slow along it."""
    res = []
    for s, (h, t) in (("l", (29, 31)), ("r", (30, 32))):
        foot = (out[:, h] + out[:, t]) / 2
        z = np.minimum(out[:, h, 2], out[:, t, 2])
        sp = np.r_[np.linalg.norm(np.diff(foot[:, :2], axis=0), axis=1), np.inf] * fps
        ok = (z < low) & (sp < still)
        f = 0
        while f < len(ok):
            if ok[f]:
                e = f
                while e + 1 < len(ok) and ok[e + 1]:
                    e += 1
                if (e - f + 1) / fps >= min_s:
                    res.append({"side": s, "start": int(f), "end": int(e)})
                f = e + 1
            else:
                f += 1
    return sorted(res, key=lambda c: c["start"])


def to_track(P, fps, rel, arm, start_frame, contacts_arg):
    """World (F, L, U) metres -> retarget_blender's joint track (Blender axes, rig scale)."""
    out = np.stack([P[..., 1], -P[..., 0], P[..., 2]], -1)          # X = his left, -Y forward, Z up
    feet_z = np.concatenate([out[:, k, 2] for k in (29, 30, 31, 32)])
    out[..., 2] -= np.percentile(feet_z, 5)
    hip0 = (out[0, 23] + out[0, 24]) / 2
    out[..., 0] -= hip0[0]
    out[..., 1] -= hip0[1]
    leg = np.median(np.linalg.norm(P[:, 23] - P[:, 27], axis=1) + np.linalg.norm(P[:, 24] - P[:, 28], axis=1)) / 2
    scale = RIG_LEG / (leg + 0.05)
    out *= scale
    if contacts_arg:
        contacts = [{"side": s.split(":")[0], "start": int(s.split(":")[1].split("-")[0]),
                     "end": int(s.split(":")[1].split("-")[1])} for s in contacts_arg.split(",")]
    else:
        contacts = auto_contacts(out, fps)
    return {"contacts": contacts, "fps": fps, "frames": len(out), "start_frame": start_frame,
            "release_frame": int(rel), "arm": arm, "scale": scale, "source": "fuse_views",
            "joints": {name: out[:, i].round(5).tolist() for name, i in J.items()}}, out


def blender(a):
    rig = a.rig
    cmd = [BLENDER, "-b", "--python-exit-code", "1", "-P", os.path.join(HERE, "retarget_blender.py"),
           "-P", os.path.join(HERE, "fusion", "blender_extras.py"), "--", a.out, rig, a.fbx]
    if a.blend:
        cmd += ["--save-blend", a.blend]
    if a.preview:
        frames_dir = os.path.splitext(a.preview)[0] + "_frames"
        cmd += ["--preview-dir", frames_dir]
    r = subprocess.run(cmd, capture_output=True, text=True)
    for line in r.stdout.splitlines():
        if line.startswith(("ground", "exported", "extras", "Traceback", "Error")) or "Error" in line:
            print("  blender:", line)
    if r.returncode != 0:
        print(r.stderr[-2000:])
        raise SystemExit("Blender failed")
    if a.preview:
        with open(a.out) as fh:
            fps = f"{json.load(fh)['fps']:.3f}"
        ins = []
        for v in ("side", "front", "q34"):
            ins += ["-framerate", fps, "-i", os.path.join(frames_dir, f"{v}_%04d.png")]
        subprocess.run(["ffmpeg", "-v", "error", "-y", *ins, "-filter_complex", "[0][1][2]hstack=inputs=3",
                        "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", a.preview], check=True)
        print("preview:", a.preview)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--side", required=True, help="extract_pose.py .npz of the side-on view")
    ap.add_argument("--front", required=True, help="extract_pose.py .npz of the front-on or behind view")
    ap.add_argument("--front-kind", choices=["front", "back"], default="front",
                    help="front: down the pitch facing him; back: behind his run-up")
    ap.add_argument("--arm", choices=["right", "left"], default="right")
    ap.add_argument("--release-side", type=int, default=-1, help="release frame in the side npz (-1 = auto)")
    ap.add_argument("--release-front", type=int, default=-1, help="release frame in the front npz (-1 = auto)")
    ap.add_argument("--fx-side", type=float, default=0.0, help="focal / width (0: from the npz, else 0.72)")
    ap.add_argument("--fx-front", type=float, default=0.0)
    ap.add_argument("--tilt-side", type=float, default=0.0, help="degrees the camera looks down")
    ap.add_argument("--tilt-front", type=float, default=0.0)
    ap.add_argument("--yaw-side", type=float, default=0.0, help="degrees off square, about vertical")
    ap.add_argument("--yaw-front", type=float, default=0.0)
    ap.add_argument("--side-forward", choices=["auto", "right", "left"], default="auto",
                    help="which way he runs across the side view's picture")
    ap.add_argument("--no-self-calibrate", dest="self_calibrate", action="store_false",
                    help="use --yaw/--tilt as given instead of refining them from the body")
    ap.add_argument("--fit-focal", action="store_true",
                    help="also fit the focal lengths (weak unless he travels toward a camera)")
    ap.add_argument("--band", type=float, default=0.25, help="time-warp band, seconds")
    ap.add_argument("--contacts", default="", help="footfall override, e.g. 'l:0-5,r:9-15'")
    ap.add_argument("--out", required=True, help="joint track .json for retarget_blender.py")
    ap.add_argument("--fbx", default="")
    ap.add_argument("--blend", default="")
    ap.add_argument("--preview", default="", help=".mp4: side, front and 3/4 views, H.264")
    ap.add_argument("--rig", default=os.path.join(HERE, "..", "..", "Assets", "Resources", "Models", "Bowler.fbx"))
    a = ap.parse_args()

    P, side, rel, keep, info, _ = fuse(a)
    track, out = to_track(P, side.fps, rel, a.arm, int(keep[0]), a.contacts)
    with open(a.out, "w") as fh:
        json.dump(track, fh)
    np.save(os.path.splitext(a.out)[0] + "_world.npy", P)
    print(json.dumps(info, indent=1))
    print(f"{track['frames']} frames @ {side.fps:.2f}, release {rel}, contacts "
          f"{[(c['side'], c['start'], c['end']) for c in track['contacts']]} -> {a.out}")
    if a.fbx:
        blender(a)


if __name__ == "__main__":
    main()
