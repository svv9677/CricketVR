"""Each fused frame beside the two source frames it came from, to check the result by eye.

    .venv/bin/python fusion/review.py out/anim/anderson_align.npz \
        out/views/anderson_side_ann.npz side.mp4 out/views/anderson_back_ann.npz back.mp4 \
        out/anim/anderson_frames out/anim/review/anderson [--side-render lside] [--per-sheet 6]

Columns: side source | render from the matching side | front/behind source | render from there.
The view npz files need a src_frame array (the source-video frame of each npz frame).
"""
import argparse
import os

import cv2
import numpy as np

SHOWN = [0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32]
H = 360


def crop_source(cap, d, f):
    f = int(np.clip(round(f), 0, len(d["image"]) - 1))
    cap.set(cv2.CAP_PROP_POS_FRAMES, int(d["src_frame"][f]))
    ok, frame = cap.read()
    if not ok:
        return np.zeros((H, H, 3), np.uint8), f
    w, h = int(d["width"]), int(d["height"])
    p = d["image"][f, SHOWN, :2] * [w, h]
    p = p[np.isfinite(p).all(1)]
    cx, cy = p.mean(0) if len(p) else (w / 2, h / 2)
    half = (np.ptp(p, 0).max() * 0.7 + 30) if len(p) else h / 2
    x0, y0 = int(max(0, cx - half)), int(max(0, cy - half))
    x1, y1 = int(min(w, cx + half)), int(min(h, cy + half))
    crop = frame[y0:y1, x0:x1]
    return cv2.resize(crop, (int(crop.shape[1] * H / max(1, crop.shape[0])), H)), f


def render(frames_dir, name, i):
    img = cv2.imread(os.path.join(frames_dir, f"{name}_{i:04d}.png"))
    if img is None:
        return np.zeros((H, H, 3), np.uint8)
    # the renders are tall; keep the middle around the body
    hh, ww = img.shape[:2]
    return cv2.resize(img, (int(ww * H / hh), H))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("align")
    ap.add_argument("side_npz")
    ap.add_argument("side_video")
    ap.add_argument("front_npz")
    ap.add_argument("front_video")
    ap.add_argument("frames_dir")
    ap.add_argument("out_prefix")
    ap.add_argument("--side-render", default="lside", help="side or lside: the render matching the side clip")
    ap.add_argument("--front-render", default="back", help="back or front")
    ap.add_argument("--per-sheet", type=int, default=6)
    a = ap.parse_args()

    al = np.load(a.align)
    ds, df = np.load(a.side_npz), np.load(a.front_npz)
    cs, cf = cv2.VideoCapture(a.side_video), cv2.VideoCapture(a.front_video)
    rows = []
    os.makedirs(os.path.dirname(a.out_prefix) or ".", exist_ok=True)
    n_sheets = 0
    for i, (fs, ff) in enumerate(zip(al["side_frame"], al["front_frame"])):
        s_img, s_f = crop_source(cs, ds, fs)
        f_img, f_f = crop_source(cf, df, ff)
        row = np.hstack([s_img, render(a.frames_dir, a.side_render, i), f_img, render(a.frames_dir, a.front_render, i)])
        tag = f"fused {i}  side {s_f}  {a.front_render} {f_f}" + ("  RELEASE" if i == int(al["release"]) else "")
        cv2.rectangle(row, (0, 0), (len(tag) * 11 + 10, 26), (0, 0, 0), -1)
        cv2.putText(row, tag, (6, 19), 0, 0.6, (0, 255, 255), 2)
        rows.append(row)
        if len(rows) == a.per_sheet or i == len(al["side_frame"]) - 1:
            wmax = max(r.shape[1] for r in rows)
            rows = [np.hstack([r, np.zeros((H, wmax - r.shape[1], 3), np.uint8)]) for r in rows]
            cv2.imwrite(f"{a.out_prefix}_{n_sheets:02d}.jpg", np.vstack(rows))
            n_sheets += 1
            rows = []
    print(f"{n_sheets} sheets -> {a.out_prefix}_NN.jpg")


if __name__ == "__main__":
    main()
