"""Video -> per-frame 3D body landmarks (MediaPipe Pose Landmarker, Apache-2.0).

Writes an .npz with, per frame:
  world   (F, 33, 3)  metric 3D landmarks, hip-centred, MediaPipe axes (x right, y down, z away)
  image   (F, 33, 3)  normalised image x, y and relative depth
  vis     (F, 33)     visibility
  ok      (F,)        a pose was found this frame
plus fps, width, height. With --overlay it also writes an mp4 with the skeleton drawn on,
which is the first thing to look at before trusting anything downstream.

    .venv/bin/python extract_pose.py ~/Downloads/IMG_9676.MOV out/img9676.npz --overlay
"""
import argparse
import os

import cv2
import mediapipe as mp
import numpy as np
from mediapipe.tasks.python import BaseOptions, vision

HERE = os.path.dirname(os.path.abspath(__file__))
MODEL = os.path.join(HERE, "models", "pose_landmarker_heavy.task")

BONES = [(11, 12), (11, 13), (13, 15), (12, 14), (14, 16), (11, 23), (12, 24), (23, 24),
         (23, 25), (25, 27), (27, 29), (29, 31), (27, 31), (24, 26), (26, 28), (28, 30),
         (30, 32), (28, 32), (15, 19), (16, 20), (0, 7), (0, 8)]


def make_landmarker():
    opts = vision.PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=MODEL),
        running_mode=vision.RunningMode.VIDEO,
        num_poses=1,
        min_pose_detection_confidence=0.4,
        min_pose_presence_confidence=0.4,
        min_tracking_confidence=0.4,
    )
    return vision.PoseLandmarker.create_from_options(opts)


def draw(frame, pts, w, h):
    for a, b in BONES:
        pa = (int(pts[a][0] * w), int(pts[a][1] * h))
        pb = (int(pts[b][0] * w), int(pts[b][1] * h))
        cv2.line(frame, pa, pb, (0, 255, 255), max(2, w // 600))
    for i, p in enumerate(pts):
        colour = (0, 0, 255) if i in (12, 14, 16) else (255, 0, 255) if i in (11, 13, 15) else (0, 255, 0)
        cv2.circle(frame, (int(p[0] * w), int(p[1] * h)), max(3, w // 400), colour, -1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("out")
    ap.add_argument("--start", type=float, default=0.0, help="seconds")
    ap.add_argument("--end", type=float, default=1e9, help="seconds")
    ap.add_argument("--overlay", action="store_true")
    a = ap.parse_args()

    cap = cv2.VideoCapture(a.video)
    fps = cap.get(cv2.CAP_PROP_FPS)
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    lm = make_landmarker()
    writer = None
    if a.overlay:
        ow, oh = (w // 2, h // 2) if w > 1920 else (w, h)
        writer = cv2.VideoWriter(os.path.splitext(a.out)[0] + "_overlay.mp4",
                                 cv2.VideoWriter_fourcc(*"mp4v"), fps, (ow, oh))

    world, image, vis, ok = [], [], [], []
    i = 0
    while True:
        good, frame = cap.read()
        if not good:
            break
        t = i / fps
        i += 1
        if t < a.start or t > a.end:
            continue
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        res = lm.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb), int(t * 1000))
        if res.pose_world_landmarks:
            wl = res.pose_world_landmarks[0]
            il = res.pose_landmarks[0]
            world.append([[p.x, p.y, p.z] for p in wl])
            image.append([[p.x, p.y, p.z] for p in il])
            vis.append([p.visibility for p in il])
            ok.append(True)
        else:
            world.append(np.full((33, 3), np.nan))
            image.append(np.full((33, 3), np.nan))
            vis.append(np.zeros(33))
            ok.append(False)
        if writer is not None:
            if ok[-1]:
                draw(frame, image[-1], w, h)
            cv2.putText(frame, f"{len(ok) - 1}", (30, 80), cv2.FONT_HERSHEY_SIMPLEX, 2.5, (255, 255, 255), 5)
            writer.write(cv2.resize(frame, (ow, oh)))

    if writer is not None:
        writer.release()
    np.savez(a.out, world=np.array(world, float), image=np.array(image, float), vis=np.array(vis, float),
             ok=np.array(ok), fps=fps, width=w, height=h, start=a.start)
    print(f"{len(ok)} frames, pose found in {int(np.sum(ok))}, fps {fps:.2f}, {w}x{h} -> {a.out}")


if __name__ == "__main__":
    main()
