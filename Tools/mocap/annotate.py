"""Hand-correct the tracked 2D points, frame by frame, before fusion.

The pose model gets most frames right and a few badly wrong (a wrist on the wrong spot, the legs
swapped, a hidden foot guessed): those few are what fusion turns into a twisted body. This shows
every frame zoomed on the bowler with each joint labelled on a pixel grid, and applies a
corrections file.

    # sheets to look at: frames 0-11 of a view, 4 per row (or e.g. "... 2 700": 2 per row, bigger)
    .venv/bin/python annotate.py sheet out/views/anderson_side.npz anderson_side.mov 0 11 out/ann/side_00.jpg
    # apply corrections -> a new npz for fuse_views.py
    .venv/bin/python annotate.py apply out/views/anderson_side.npz corrections.json out/views/anderson_side_fixed.npz

corrections.json, per view npz name (file name without folder):
    {"anderson_side.npz": {"12": {"r_wr": [812, 233], "l_ank": "hide"},
                           "13": {"swap_legs": true}}}
Positions are pixels in the full source frame (the grid on each sheet is labelled in them).
"hide" drops a point the camera cannot see (visibility 0); "swap_legs"/"swap_arms" swap the whole
left/right chain for that frame. A corrected point gets visibility 1.
"""
import json
import os
import sys

import cv2
import numpy as np

J = dict(nose=0, l_ear=7, r_ear=8, l_sh=11, r_sh=12, l_el=13, r_el=14, l_wr=15, r_wr=16,
         l_pinky=17, r_pinky=18, l_index=19, r_index=20, l_hip=23, r_hip=24, l_knee=25, r_knee=26,
         l_ank=27, r_ank=28, l_heel=29, r_heel=30, l_toe=31, r_toe=32)
SHOWN = ["nose", "l_sh", "r_sh", "l_el", "r_el", "l_wr", "r_wr", "l_hip", "r_hip", "l_knee", "r_knee",
         "l_ank", "r_ank", "l_heel", "r_heel", "l_toe", "r_toe"]
BONES = [("l_sh", "r_sh"), ("l_sh", "l_el"), ("l_el", "l_wr"), ("r_sh", "r_el"), ("r_el", "r_wr"),
         ("l_sh", "l_hip"), ("r_sh", "r_hip"), ("l_hip", "r_hip"), ("l_hip", "l_knee"), ("l_knee", "l_ank"),
         ("r_hip", "r_knee"), ("r_knee", "r_ank"), ("l_ank", "l_heel"), ("l_heel", "l_toe"), ("l_ank", "l_toe"),
         ("r_ank", "r_heel"), ("r_heel", "r_toe"), ("r_ank", "r_toe")]
LEGS = [("l_hip", "r_hip"), ("l_knee", "r_knee"), ("l_ank", "r_ank"), ("l_heel", "r_heel"), ("l_toe", "r_toe")]
ARMS = [("l_sh", "r_sh"), ("l_el", "r_el"), ("l_wr", "r_wr"), ("l_pinky", "r_pinky"), ("l_index", "r_index")]


def source_frames(d):
    """Source-video frame index for each npz frame (dedupe/start aware when the npz says)."""
    for k in ("src_frame", "frame_index", "frames"):
        if k in d.files:
            return np.asarray(d[k]).astype(int)
    start = float(d["start"]) if "start" in d.files else 0.0
    vfps = float(d["video_fps"]) if "video_fps" in d.files else float(d["fps"])
    return (np.round(start * vfps) + np.arange(len(d["ok"]))).astype(int)


def sheet(npz, video, f0, f1, out, per_row=4, tile=420):
    d = np.load(npz, allow_pickle=True)
    img = d["image"]
    w, h = int(d["width"]), int(d["height"])
    src = source_frames(d)
    cap = cv2.VideoCapture(video)
    tiles = []
    for f in range(f0, min(f1, len(img) - 1) + 1):
        cap.set(cv2.CAP_PROP_POS_FRAMES, int(src[f]))
        ok, frame = cap.read()
        if not ok:
            continue
        pts = {n: (img[f, i, 0] * w, img[f, i, 1] * h) for n, i in J.items()}
        good = [p for n, p in pts.items() if n in SHOWN and np.isfinite(p[0])]
        if not good:
            continue
        xs, ys = [p[0] for p in good], [p[1] for p in good]
        cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
        half = max(max(xs) - min(xs), max(ys) - min(ys)) * 0.75 + 20
        x0, y0 = int(max(0, cx - half)), int(max(0, cy - half))
        x1, y1 = int(min(w, cx + half)), int(min(h, cy + half))
        crop = frame[y0:y1, x0:x1].copy()
        s = tile / max(1, max(x1 - x0, y1 - y0))
        crop = cv2.resize(crop, (int((x1 - x0) * s), int((y1 - y0) * s)))
        step = 20 if (x1 - x0) < 400 else 50
        for gx in range((x0 // step + 1) * step, x1, step):
            X = int((gx - x0) * s)
            cv2.line(crop, (X, 0), (X, crop.shape[0]), (90, 90, 90), 1)
            if (gx // step) % 2 == 0:
                cv2.putText(crop, str(gx), (X + 2, 12), 0, 0.35, (255, 255, 255), 1)
        for gy in range((y0 // step + 1) * step, y1, step):
            Y = int((gy - y0) * s)
            cv2.line(crop, (0, Y), (crop.shape[1], Y), (90, 90, 90), 1)
            if (gy // step) % 2 == 0:
                cv2.putText(crop, str(gy), (2, Y - 2), 0, 0.35, (255, 255, 255), 1)

        def P(n):
            x, y = pts[n]
            return int((x - x0) * s), int((y - y0) * s)
        for a, b in BONES:
            if np.isfinite(pts[a][0]) and np.isfinite(pts[b][0]):
                col = (255, 140, 0) if a.startswith("l_") and b.startswith("l_") else \
                      (0, 0, 255) if a.startswith("r_") and b.startswith("r_") else (0, 220, 0)
                cv2.line(crop, P(a), P(b), col, 2)
        for n in SHOWN:
            if not np.isfinite(pts[n][0]):
                continue
            col = (255, 140, 0) if n.startswith("l_") else (0, 0, 255) if n.startswith("r_") else (0, 220, 0)
            v = float(d["vis"][f, J[n]])
            cv2.circle(crop, P(n), 4, col, -1 if v > 0.5 else 1)
            cv2.putText(crop, n.replace("_", ""), (P(n)[0] + 4, P(n)[1] - 3), 0, 0.4, col, 1)
        canvas = np.zeros((tile, tile, 3), np.uint8)
        canvas[:crop.shape[0], :crop.shape[1]] = crop
        cv2.putText(canvas, f"f{f}", (6, tile - 8), 0, 0.8, (0, 255, 255), 2)
        tiles.append(canvas)
    while len(tiles) % per_row:
        tiles.append(np.zeros((tile, tile, 3), np.uint8))
    rows = [np.hstack(tiles[i:i + per_row]) for i in range(0, len(tiles), per_row)]
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    cv2.imwrite(out, np.vstack(rows))
    print(f"{out}: frames {f0}-{f1}")


def apply(npz, corrections, out):
    d = dict(np.load(npz, allow_pickle=True))
    key = os.path.basename(npz)
    fixes = json.load(open(corrections)).get(key, {})
    w, h = int(d["width"]), int(d["height"])
    n = 0
    for fs, ops in fixes.items():
        f = int(fs)
        for name, val in ops.items():
            pairs = LEGS if name == "swap_legs" else ARMS if name == "swap_arms" else None
            if pairs is not None and val:
                for a, b in pairs:
                    for k in ("image", "world", "vis"):
                        ia, ib = J[a], J[b]
                        d[k][f, [ia, ib]] = d[k][f, [ib, ia]]
                n += 1
            elif val == "hide":
                d["vis"][f, J[name]] = 0.0
                n += 1
            else:
                d["image"][f, J[name], 0] = val[0] / w
                d["image"][f, J[name], 1] = val[1] / h
                d["vis"][f, J[name]] = 1.0
                n += 1
    np.savez(out, **d)
    print(f"{out}: {n} corrections applied from {corrections} [{key}]")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "sheet":
        # optional: [per_row] [tile_px], e.g. 2 700 for a closer look
        extra = [int(v) for v in sys.argv[7:9]]
        sheet(sys.argv[2], sys.argv[3], int(sys.argv[4]), int(sys.argv[5]), sys.argv[6], *extra)
    elif cmd == "apply":
        apply(sys.argv[2], sys.argv[3], sys.argv[4])
