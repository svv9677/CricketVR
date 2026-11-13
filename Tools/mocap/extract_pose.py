"""Video -> per-frame 3D body landmarks (MediaPipe Pose Landmarker, Apache-2.0).

Writes an .npz with, per frame:
  world   (F, 33, 3)  metric 3D landmarks, hip-centred, MediaPipe axes (x right, y down, z away)
  image   (F, 33, 3)  normalised image x, y and relative depth
  vis     (F, 33)     visibility
  ok      (F,)        a pose was found this frame
plus fps, width, height. With --overlay it also writes an mp4 with the skeleton drawn on,
which is the first thing to look at before trusting anything downstream.

    .venv/bin/python extract_pose.py ~/Downloads/IMG_9676.MOV out/img9676.npz --overlay

Footage of someone else bowling, filmed from the stands (a small figure, other players about, a
camera panning to follow him), needs three more things:

  --seed x,y      the bowler, as a normalised image point in the first kept frame. Several poses are
                  detected each frame and the one whose hips are nearest the last frame's is kept;
                  a jump of more than half a body height is rejected (that frame is marked not ok).
  --crop          run the model on a box around him, upscaled to --crop-height px (MediaPipe loses
                  ~120 px figures). Each crop is a different view direction, so its world
                  landmarks are turned from the crop's axis onto the camera's (needs --fx).
  --pan           estimate the camera's turn from the background (camera_pan.py) and store it
                  (cam_R, cam_zoom) for process_motion.py.
  --time-scale s  playback speed of a slow-motion segment (0.25 = quarter speed): fps is stored as
                  the real-time rate, source fps / s, so speeds and timings come out physical.
  --dedupe        drop frames that repeat the last kept one. Broadcast "slow motion" is often
                  normal footage with each frame shown several times (anderson_stands.mp4: every
                  4th frame new); the repeats would turn into stutter. The stored fps is then the
                  rate of the frames kept (measured from how many were dropped).

    .venv/bin/python extract_pose.py clip.mp4 out/x.npz --start 29 --end 33 --time-scale 0.25 \
        --seed 0.62,0.55 --crop --pan --fx 1.6 --overlay
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


def make_landmarker(mode=vision.RunningMode.VIDEO, num_poses=1):
    opts = vision.PoseLandmarkerOptions(
        base_options=BaseOptions(model_asset_path=MODEL),
        running_mode=mode,
        num_poses=num_poses,
        min_pose_detection_confidence=0.4,
        min_pose_presence_confidence=0.4,
        min_tracking_confidence=0.4,
    )
    return vision.PoseLandmarker.create_from_options(opts)


def ray_rotation(u, v, fx, w, h):
    """The rotation taking the camera's axis (+z) onto the ray through pixel (u, v)."""
    d = np.array([(u - w / 2) / fx, (v - h / 2) / fx, 1.0])
    d /= np.linalg.norm(d)
    z = np.array([0.0, 0.0, 1.0])
    axis = np.cross(z, d)
    s, c = np.linalg.norm(axis), float(z @ d)
    if s < 1e-9:
        return np.eye(3)
    k = axis / s
    Kx = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + s * Kx + (1 - c) * Kx @ Kx


def box_of(px, margin=0.15):
    """Pixel box (x0, y0, x1, y1) around 2D landmarks, with a margin of its own height."""
    x0, y0 = np.nanmin(px, axis=0)
    x1, y1 = np.nanmax(px, axis=0)
    m = (y1 - y0) * margin
    return x0 - m, y0 - m, x1 + m, y1 + m


class Tracker:
    """Keeps one person: the detected pose whose hips are nearest the last kept pose's."""

    def __init__(self, seed_px, frame_h):
        self.hip = np.array(seed_px, float)
        self.height = frame_h * 0.25          # a guess until the first pose is kept
        self.box = None

    def pick(self, poses_px):
        best, dist = None, 1e18
        for i, px in enumerate(poses_px):
            hip = (px[23] + px[24]) / 2
            dd = np.linalg.norm(hip - self.hip)
            if dd < dist:
                best, dist = i, dd
        if best is None or dist > 0.5 * self.height:
            return None
        px = poses_px[best]
        self.hip = (px[23] + px[24]) / 2
        self.height = float(np.nanmax(px[:, 1]) - np.nanmin(px[:, 1]))
        self.box = box_of(px)
        return best

    def crop_box(self, w, h):
        """A square box about him: twice his height, centred on his hips."""
        side = max(2.0 * self.height, 160.0)
        cx, cy = self.hip
        x0 = int(np.clip(cx - side / 2, 0, max(0, w - side)))
        y0 = int(np.clip(cy - side / 2, 0, max(0, h - side)))
        return x0, y0, int(min(w, x0 + side)), int(min(h, y0 + side))


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
    ap.add_argument("--seed", default="", help="x,y: the bowler in the first kept frame, normalised")
    ap.add_argument("--crop", action="store_true", help="run the model on an upscaled box around him")
    ap.add_argument("--crop-height", type=int, default=768)
    ap.add_argument("--pan", action="store_true", help="estimate the camera's turn from the background")
    ap.add_argument("--fx", type=float, default=0.72, help="focal length / frame width (crop and pan)")
    ap.add_argument("--time-scale", type=float, default=1.0,
                    help="playback speed of the footage (0.25 = quarter-speed slow motion)")
    ap.add_argument("--dedupe", action="store_true", help="drop frames repeating the last kept one")
    a = ap.parse_args()
    if a.seed or a.crop or a.pan or a.time_scale != 1.0 or a.dedupe:
        return tracked(a)

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


def tracked(a):
    """The bowler among other people, optionally cropped and with the camera's pan (see top)."""
    from camera_pan import PanTracker

    cap = cv2.VideoCapture(a.video)
    src_fps = cap.get(cv2.CAP_PROP_FPS)
    fps = src_fps / a.time_scale
    w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    fx = a.fx * w
    if not a.seed:
        raise SystemExit("--seed x,y is needed to know which person is the bowler")
    sx, sy = (float(v) for v in a.seed.split(","))
    track = Tracker((sx * w, sy * h), h)
    lm = make_landmarker(vision.RunningMode.IMAGE if a.crop else vision.RunningMode.VIDEO, 2 if a.crop else 4)
    pan = PanTracker(fx, w, h) if a.pan else None
    writer = None
    if a.overlay:
        ow, oh = (w // 2, h // 2) if w > 1920 else (w, h)
        writer = cv2.VideoWriter(os.path.splitext(a.out)[0] + "_overlay.mp4",
                                 cv2.VideoWriter_fourcc(*"mp4v"), src_fps, (ow, oh))

    world, image, vis, ok = [], [], [], []
    i, seen, last_small = 0, 0, None
    while True:
        good, frame = cap.read()
        if not good:
            break
        t = i / src_fps
        i += 1
        if t < a.start or t > a.end:
            continue
        seen += 1
        if a.dedupe:
            small = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY), (160, 90)).astype(np.float32)
            if last_small is not None and np.abs(small - last_small).mean() < 2.0:
                continue
            last_small = small
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        if a.crop:
            x0, y0, x1, y1 = track.crop_box(w, h)
            sc = a.crop_height / max(1, y1 - y0)
            sub = cv2.resize(np.ascontiguousarray(rgb[y0:y1, x0:x1]), (int((x1 - x0) * sc), a.crop_height),
                             interpolation=cv2.INTER_CUBIC)
            res = lm.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(sub)))
            cw, ch = sub.shape[1], sub.shape[0]
            poses_px = [np.array([[x0 + p.x * cw / sc, y0 + p.y * ch / sc] for p in pl]) for pl in res.pose_landmarks]
        else:
            res = lm.detect_for_video(mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb), int(t * 1000))
            poses_px = [np.array([[p.x * w, p.y * h] for p in pl]) for pl in res.pose_landmarks]
        k = track.pick(poses_px) if poses_px else None
        if k is not None:
            wl = np.array([[p.x, p.y, p.z] for p in res.pose_world_landmarks[k]])
            if a.crop:
                # The crop's own axis runs through its centre; turn its world landmarks onto the camera's.
                wl = wl @ ray_rotation((x0 + x1) / 2, (y0 + y1) / 2, fx, w, h).T
            px = poses_px[k]
            il = res.pose_landmarks[k]
            world.append(wl.tolist())
            image.append([[px[j, 0] / w, px[j, 1] / h, il[j].z] for j in range(33)])
            vis.append([p.visibility for p in il])
            ok.append(True)
        else:
            world.append(np.full((33, 3), np.nan))
            image.append(np.full((33, 3), np.nan))
            vis.append(np.zeros(33))
            ok.append(False)
        if pan is not None:
            pan.step(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY), track.box)
        if writer is not None:
            if ok[-1]:
                draw(frame, image[-1], w, h)
            if a.crop:
                cv2.rectangle(frame, (x0, y0), (x1, y1), (255, 255, 0), 2)
            cv2.putText(frame, f"{len(ok) - 1}", (30, 80), cv2.FONT_HERSHEY_SIMPLEX, 2.5, (255, 255, 255), 5)
            writer.write(cv2.resize(frame, (ow, oh)))

    if writer is not None:
        writer.release()
    if a.dedupe and ok:
        fps = fps * len(ok) / seen          # the kept frames' own rate
    extra = {}
    if pan is not None:
        extra = dict(cam_R=np.array(pan.rotations), cam_zoom=np.array(pan.zooms))
        print(pan.summary())
    np.savez(a.out, world=np.array(world, float), image=np.array(image, float), vis=np.array(vis, float),
             ok=np.array(ok), fps=fps, src_fps=src_fps, time_scale=a.time_scale, width=w, height=h,
             start=a.start, fx=fx, **extra)
    print(f"{len(ok)} frames (of {seen} in the window), pose found in {int(np.sum(ok))}, real-time fps "
          f"{fps:.2f} (source {src_fps:.2f}, playback x{a.time_scale:g}), {w}x{h} -> {a.out}")


if __name__ == "__main__":
    main()
