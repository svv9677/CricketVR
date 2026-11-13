"""Preview of a multi-camera capture: two camera views (Pose2Sim's 2D overlays) | rig from behind | rig
from the side, at real speed, H.264 (QuickTime plays it).

    .venv/bin/python multicam/preview_multicam.py <track.json> <frames dir> <out.mp4> \
        <cam A overlay.mp4> [<cam B overlay.mp4>] [--offset 1]

--offset: the video frame of the track's frame 0 before --start (Pose2Sim's TRC starts at frame 1
of the synced videos). The renders come from retarget_blender.py --render.
"""
import json
import os
import subprocess
import sys
import tempfile

import cv2
import numpy as np

args = [a for a in sys.argv[1:] if not a.startswith("--")]
offset = int(sys.argv[sys.argv.index("--offset") + 1]) if "--offset" in sys.argv else 1
if "--offset" in sys.argv:
    args.remove(str(offset))
track_path, frames_dir, out = args[:3]
cams = args[3:]
track = json.load(open(track_path))
H = 400
caps = [cv2.VideoCapture(c) for c in cams]
tmp = tempfile.mkdtemp()
for f in range(track["frames"]):
    tiles = []
    for cap in caps:
        cap.set(cv2.CAP_PROP_POS_FRAMES, offset + track.get("start_frame", 0) + f)
        ok, img = cap.read()
        img = img if ok else np.zeros((H, H, 3), np.uint8)
        tiles.append(cv2.resize(img, (int(img.shape[1] * H / img.shape[0]), H)))
    for v in ("back", "side"):
        img = cv2.imread(os.path.join(frames_dir, f"{v}_{f:03d}.png"))
        tiles.append(cv2.resize(img, (int(img.shape[1] * H / img.shape[0]), H)))
    row = np.hstack(tiles)
    row = row[:, : row.shape[1] // 2 * 2]
    label = f"frame {f}" + ("  RELEASE" if f == track["release_frame"] else "")
    cv2.putText(row, label, (12, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.9, (255, 255, 255), 2)
    cv2.imwrite(os.path.join(tmp, f"{f:05d}.png"), row)
subprocess.run(["ffmpeg", "-v", "error", "-y", "-framerate", str(track["fps"]), "-i", os.path.join(tmp, "%05d.png"),
                "-c:v", "libx264", "-crf", "20", "-pix_fmt", "yuv420p", out], check=True)
print(f"preview: {out}")
