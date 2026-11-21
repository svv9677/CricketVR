"""Side-by-side preview: source video (with landmarks) | rig from behind | rig from the side.

    .venv/bin/python preview.py out/x.json out/x_overlay.mp4 out/frames_x out/x_preview.mp4
"""
import json
import os
import sys

import cv2
import numpy as np

track_path, overlay, frames_dir, out = sys.argv[1:5]
track = json.load(open(track_path))
start = int(track.get("start_frame", 0))
cap = cv2.VideoCapture(overlay)
H = 480
writer = None
for f in range(track["frames"]):
    cap.set(cv2.CAP_PROP_POS_FRAMES, start + f)
    ok, src = cap.read()
    if not ok:
        break
    src = cv2.resize(src, (int(src.shape[1] * H / src.shape[0]), H))
    tiles = [src]
    for v in ("back", "side"):
        img = cv2.imread(os.path.join(frames_dir, f"{v}_{f:03d}.png"))
        tiles.append(cv2.resize(img, (int(img.shape[1] * H / img.shape[0]), H)))
    row = np.hstack(tiles)
    label = f"frame {f}" + ("  RELEASE" if f == track["release_frame"] else "")
    cv2.putText(row, label, (12, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 255, 255), 2)
    if writer is None:
        writer = cv2.VideoWriter(out, cv2.VideoWriter_fourcc(*"mp4v"), track["fps"], (row.shape[1], row.shape[0]))
    writer.write(row)
writer.release()
print(f"preview: {out}")
