"""fuse_views.py against ground truth: Pose2Sim's 4-camera demo, triangulated.

Two of the demo's cameras play the side and the front view, each through MediaPipe on its own,
one of them started 7 frames late (two different "deliveries"); the fused 3D is compared with
Pose2Sim's triangulation of all four cameras. Errors are after a similarity alignment (rotation,
scale, translation; no mirroring), over the whole sequence and per frame.

    .venv/bin/python fusion/validate_p2s.py [--side cam02 --front cam03 --drop 7]
"""
import argparse
import os
import sys
import tomllib
from types import SimpleNamespace

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from fuse_views import fuse  # noqa: E402

DEMO = os.path.expanduser("~/Documents/Git/pose2sim-env/demo")
OUT = os.path.join(os.path.dirname(HERE), "out", "fusion_val")
CMP = {0: "Nose", 11: "LShoulder", 12: "RShoulder", 13: "LElbow", 14: "RElbow", 15: "LWrist", 16: "RWrist",
       23: "LHip", 24: "RHip", 25: "LKnee", 26: "RKnee", 27: "LAnkle", 28: "RAnkle", 29: "LHeel",
       30: "RHeel", 31: "LBigToe", 32: "RBigToe"}
LIMBS = {"legs": ([25, 27], [26, 28]), "arms": ([13, 15], [14, 16])}


def load_gt():
    """Pose2Sim's triangulated points in the calibration's axes (Z up), by marker name."""
    lines = open(f"{DEMO}/pose-3d/demo_1-97_filt_butterworth.trc").read().splitlines()
    names = [n for n in lines[3].split("\t")[2:] if n]
    data = np.array([[float(v) for v in ln.split("\t")[2:2 + 3 * len(names)]] for ln in lines[5:] if ln.strip()])
    P = data.reshape(len(data), len(names), 3)
    P = np.stack([P[..., 2], P[..., 0], P[..., 1]], -1)           # TRC is Y-up: calib = (z, x, y)
    return {n: P[:, i] for i, n in enumerate(names)}


def camera(name):
    c = tomllib.load(open(f"{DEMO}/calibration/Calib_qualisys.toml", "rb"))[name]
    R, _ = cv2.Rodrigues(np.array(c["rotation"]))
    return dict(K=np.array(c["matrix"]), dist=np.array(c["distortions"]), rv=np.array(c["rotation"]),
                tv=np.array(c["translation"]), R=R)


def frame_offset(name, gt):
    """Video frame = GT frame + offset, by reprojection (label-free), as measured: cam02 1, cam03 2."""
    cam, d = camera(name), np.load(f"{OUT}/{name}.npz")
    px = d["image"][..., :2] * [d["width"], d["height"]]
    best = None
    for off in range(-6, 7):
        e = []
        for k in range(len(gt["LHip"])):
            v = k + off
            if 0 <= v < len(px):
                pts = np.array([gt[n][k] for n in CMP.values()])
                pr = cv2.projectPoints(pts, cam["rv"], cam["tv"], cam["K"], cam["dist"])[0][:, 0]
                e.append(np.median(np.linalg.norm(pr - px[v, list(CMP)], axis=1)))
        m = np.median(e)
        if best is None or m < best[0]:
            best = (m, off)
    return best[1]


def geometry_from_calib(side, front):
    """What a careful user would measure: tilt of each camera, and the front camera's yaw off
    square relative to the side camera's picture-right (fusion/geometry.py conventions)."""
    cs, cf = camera(side), camera(front)
    xs, xf = cs["R"].T @ [1, 0, 0], cf["R"].T @ [1, 0, 0]
    tilt = lambda c: float(np.degrees(np.arcsin(-(c["R"].T @ [0, 0, 1])[2])))  # noqa: E731
    a = np.degrees(np.arctan2(xs[0] * xf[1] - xs[1] * xf[0], xs[0] * xf[0] + xs[1] * xf[1]))
    kind = "front" if a > 0 else "back"
    yaw = a - (90.0 if kind == "front" else -90.0)
    roll = lambda x: float(np.degrees(np.arcsin(x[2])))  # noqa: E731
    return dict(kind=kind, yaw_front=float(yaw), tilt_side=tilt(cs), tilt_front=tilt(cf),
                fx_side=float(cs["K"][0, 0]), fx_front=float(cf["K"][0, 0]), roll_side=roll(xs), roll_front=roll(xf))


def umeyama(A, B):
    """Similarity (s, R, t) taking A onto B (N, 3), no reflection."""
    ma, mb = A.mean(0), B.mean(0)
    a, b = A - ma, B - mb
    U_, S, Vt = np.linalg.svd(b.T @ a / len(A))
    D = np.diag([1, 1, np.sign(np.linalg.det(U_ @ Vt))])
    R = U_ @ D @ Vt
    s = np.trace(np.diag(S) @ D) / (a ** 2).sum(1).mean()
    return s, R, mb - s * R @ ma


def errors(P, G, per_frame):
    """mm per joint, after one similarity for the whole sequence or one per frame."""
    if per_frame:
        e = []
        for f in range(len(P)):
            s, R, t = umeyama(P[f], G[f])
            e.append(np.linalg.norm((s * (R @ P[f].T)).T + t - G[f], axis=1))
        return np.array(e) * 1000
    s, R, t = umeyama(P.reshape(-1, 3), G.reshape(-1, 3))
    return np.linalg.norm((s * (R @ P.reshape(-1, 3).T)).T + t - G.reshape(-1, 3), axis=1).reshape(P.shape[:2]) * 1000


def gt_array(gt, frames):
    return np.stack([gt[n][frames] for n in CMP.values()], 1)


def hip_centre(X):
    return X - (X[:, [7]] + X[:, [8]]) / 2       # columns 7, 8 are LHip, RHip in CMP order


def swap_rate(px, name, gt, frames_v, frames_k):
    """Share of frames whose limb labels are crossed, judged against the GT projected into the camera."""
    cam = camera(name)
    out = {}
    for limb, (L, R) in LIMBS.items():
        nm = [CMP[i] for i in L], [CMP[i] for i in R]
        bad = 0
        for v, k in zip(frames_v, frames_k):
            gl = cv2.projectPoints(np.array([gt[n][k] for n in nm[0]]), cam["rv"], cam["tv"], cam["K"], cam["dist"])[0][:, 0]
            gr = cv2.projectPoints(np.array([gt[n][k] for n in nm[1]]), cam["rv"], cam["tv"], cam["K"], cam["dist"])[0][:, 0]
            keep = np.linalg.norm(px[v, L] - gl, axis=1).sum() + np.linalg.norm(px[v, R] - gr, axis=1).sum()
            swap = np.linalg.norm(px[v, L] - gr, axis=1).sum() + np.linalg.norm(px[v, R] - gl, axis=1).sum()
            bad += swap < keep
        out[limb] = round(100.0 * bad / max(1, len(frames_v)), 1)
    return out


def swap_rate_3d(P, G):
    """Fused left/right crossed against the GT, after the sequence alignment (CMP column order)."""
    cols = list(CMP)
    s, R, t = umeyama(P.reshape(-1, 3), G.reshape(-1, 3))
    Q = (s * (R @ P.reshape(-1, 3).T)).T.reshape(P.shape) + t
    out = {}
    for limb, (L, Rr) in LIMBS.items():
        li, ri = [cols.index(i) for i in L], [cols.index(i) for i in Rr]
        keep = np.linalg.norm(Q[:, li] - G[:, li], axis=-1).sum(1) + np.linalg.norm(Q[:, ri] - G[:, ri], axis=-1).sum(1)
        swap = np.linalg.norm(Q[:, li] - G[:, ri], axis=-1).sum(1) + np.linalg.norm(Q[:, ri] - G[:, li], axis=-1).sum(1)
        out[limb] = round(100.0 * float(np.mean(swap < keep)), 1)
    return out


def drop_frames(name, n):
    d = dict(np.load(f"{OUT}/{name}.npz"))
    F = len(d["image"])
    for k, v in d.items():
        if isinstance(v, np.ndarray) and v.ndim >= 1 and len(v) == F:
            d[k] = v[n:]
    path = f"{OUT}/{name}_drop{n}.npz"
    np.savez(path, **d)
    return path


def inject_swaps(name, spans):
    """A copy of a view with its legs (and arms, half the spans) relabelled on stretches of frames,
    as MediaPipe does side-on - to see whether fusion puts them back."""
    from swap_fix import ARMS, LEGS, _apply
    d = dict(np.load(f"{OUT}/{name}.npz"))
    for i, (f0, f1) in enumerate(spans):
        fr = np.arange(f0, min(f1, len(d["image"])))
        _apply([d["image"], d["world"], d["vis"]], LEGS, fr)
        if i % 2 and len(spans) > 1:
            _apply([d["image"], d["world"], d["vis"]], ARMS, fr)
    path = f"{OUT}/{name}_swapped.npz"
    np.savez(path, **d)
    return path


SPANS = [(15, 24), (40, 47), (60, 72)]       # 28 of 100 frames, legs; arms on the middle span


def run(a, gt, offs, geom, measured, self_cal=False, focal_known=False, swapped=None):
    w_side = int(np.load(f"{OUT}/{a.side}.npz")["width"])
    w_front = int(np.load(f"{OUT}/{a.front}.npz")["width"])
    args = SimpleNamespace(side=inject_swaps(a.side, swapped) if swapped else f"{OUT}/{a.side}.npz", front=drop_frames(a.front, a.drop), front_kind=geom["kind"],
                           arm="right", release_side=-1, release_front=-1, side_forward="right", band=0.25,
                           fx_side=geom["fx_side"] / w_side if (measured or focal_known) else 0.0,
                           fx_front=geom["fx_front"] / w_front if (measured or focal_known) else 0.0,
                           tilt_side=geom["tilt_side"] if measured else 0.0,
                           tilt_front=geom["tilt_front"] if measured else 0.0,
                           yaw_side=0.0, yaw_front=geom["yaw_front"] if measured else 0.0,
                           self_calibrate=self_cal, fit_focal=False)
    P, side, rel, keep, info, extra = fuse(args)
    k = keep - offs[a.side]
    ok = (k >= 0) & (k < len(gt["LHip"]))
    G = gt_array(gt, k[ok])
    F3 = P[ok][:, list(CMP)]
    return dict(P=F3, G=G, info=info, extra=extra, keep=keep, ok=ok, k=k)


def single(view, name, gt, off, drop):
    """MediaPipe's own 3D for one view, as it comes out (no relabelling)."""
    world = np.load(f"{OUT}/{name}.npz")["world"][drop:]
    k = np.arange(len(world)) + drop - off
    ok = (k >= 0) & (k < len(gt["LHip"]))
    return world[ok][:, list(CMP)], gt_array(gt, k[ok])


def table_row(label, P, G):
    seq = errors(hip_centre(P), hip_centre(G), False)
    pf = errors(P, G, True)
    return label, float(seq.mean()), float(pf.mean()), seq.mean(0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--side", default="cam02")
    ap.add_argument("--front", default="cam03")
    ap.add_argument("--drop", type=int, default=7)
    a = ap.parse_args()
    gt = load_gt()
    offs = {n: frame_offset(n, gt) for n in (a.side, a.front)}
    geom = geometry_from_calib(a.side, a.front)
    print("frame offsets", offs, "geometry", {k: round(v, 1) if isinstance(v, float) else v for k, v in geom.items()})
    rows, swaps = [], {}
    configs = [(False, False, False, "fused (focal guessed, assumed square, level)"),
               (False, True, False, "fused (focal guessed, angles self-calibrated)"),
               (False, False, True, "fused (focal known, assumed square, level)"),
               (False, True, True, "fused (focal known, angles self-calibrated)"),
               (True, False, True, "fused (cameras measured)")]
    extra_cfgs = [(False, True, True, "fused, side view with injected swaps (stretches)"),
                  (False, True, True, "fused, side view with injected swaps (whole clip, legs)")]
    for measured, self_cal, focal_known, tag in configs + extra_cfgs:
        spans = None if "injected" not in tag else (SPANS if "stretches" in tag else [(0, 200)])
        r = run(a, gt, offs, geom, measured, self_cal, focal_known, swapped=spans)
        rows.append(table_row(tag, r["P"], r["G"]))
        abs_err = errors(r["P"], r["G"], False).mean()
        print(f"{tag}: frames {int(r['ok'].sum())}, absolute (root included) {abs_err:.0f} mm, info {r['info']}")
        swaps[tag] = swap_rate_3d(r["P"], r["G"])
        if "injected" in tag:
            sv = r["extra"]["views"][0]
            px = sv.px
            raw = np.load(inject_swaps(a.side, spans))
            raw_px = raw["image"][..., :2] * [raw["width"], raw["height"]]
            kk = np.arange(len(raw_px)) - offs[a.side]
            okk = (kk >= 0) & (kk < len(gt["LHip"]))
            # Only side frames on the fused timeline count for "after" (the rest are cut).
            on = np.zeros(len(raw_px), bool)
            on[r["keep"]] = True
            sel = okk & on
            t2 = "stretches" if "stretches" in tag else "whole clip"
            swaps[f"injected ({t2}) side 2D before"] = swap_rate(raw_px, a.side, gt, np.where(sel)[0], kk[sel])
            swaps[f"injected ({t2}) side 2D after continuity"] = swap_rate(r["extra"]["px_continuity"], a.side, gt,
                                                                           np.where(sel)[0], kk[sel])
            swaps[f"injected ({t2}) side 2D after cross-view"] = swap_rate(px, a.side, gt, np.where(sel)[0], kk[sel])
        if measured:
            side_v, front_v = r["extra"]["views"]
            side_px = side_v.px
            for v, nm, drop, px_after in ((side_v, a.side, 0, side_px), (front_v, a.front, a.drop, front_v.px)):
                raw = np.load(f"{OUT}/{nm}.npz")
                raw_px = (raw["image"][..., :2] * [raw["width"], raw["height"]])[drop:]
                kk = np.arange(len(raw_px)) + drop - offs[nm]
                ok = (kk >= 0) & (kk < len(gt["LHip"]))
                fv, fk = np.where(ok)[0], kk[ok]
                swaps[f"{nm} 2D raw"] = swap_rate(raw_px, nm, gt, fv, fk)
                swaps[f"{nm} 2D after"] = swap_rate(px_after, nm, gt, fv, fk)
                rows.append(table_row(f"single view {nm} (MediaPipe 3D)", *single(v, nm, gt, offs[nm], drop)))
    names = list(CMP.values())
    print("\n| method | MPJPE, sequence-aligned, hip-centred (mm) | PA-MPJPE per frame (mm) |")
    print("|---|---|---|")
    for label, seq, pf, _ in rows:
        print(f"| {label} | {seq:.0f} | {pf:.0f} |")
    print("\nper joint (sequence-aligned, mm):", " ".join(f"{n}" for n in names))
    for label, _, _, pj in rows:
        print(f"  {label}: " + " ".join(f"{x:.0f}" for x in pj))
    print("\nleft/right crossed (% of frames):", swaps)


if __name__ == "__main__":
    main()
