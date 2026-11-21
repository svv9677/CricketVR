"""Reference footage for the Blender hand-animation scene: one view (out/views/manifest.json) as an image
sequence plus what Blender needs to keep it in step with the animation and on top of the rig.

    cd Tools/mocap && .venv/bin/python -I handanim/make_reference.py steyn_side steyn_front [--video-dir DIR]

For each view it reads the source video exactly as extract_pose.py did (same window, same --dedupe rule),
so image k is the frame the pose in out/views/<view>.npz frame k came from. Writes
handanim/reference/<view>/0001.jpg... and meta.json:
  - "time":   each image's real time in seconds (video time x the clip's slow-motion factor);
  - "events": back foot / front foot / release as image indices (from the manifest), which Blender
              matches to the scene's markers of the same names, so the footage follows the timing of
              the action being animated rather than its own;
  - "run_dir":  +1 if he runs left to right across the picture, -1 if right to left (which side of him
              a side-on camera was on);
  - "hips", "torso": the bowler's mid-hip point and hip-to-shoulder length in each image (normalised,
              lightly smoothed), so Blender can sit the footage's bowler on the rig.
The footage stays on this machine (reference/ is gitignored): most of it is other people's video.
"""
import argparse
import json
import os
import sys

import cv2
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
MOCAP = os.path.dirname(HERE)
MAX_W = 1280


def smooth(a, k=3):
    """Gaps filled by interpolation, then a centred moving average over k frames."""
    a = np.array(a, float)
    idx = np.arange(len(a))
    for c in range(a.shape[1]):
        good = np.isfinite(a[:, c])
        if good.sum() >= 2:
            a[:, c] = np.interp(idx, idx[good], a[good, c])
    pad = np.pad(a, ((k // 2, k // 2), (0, 0)), mode="edge")
    return np.array([pad[i:i + k].mean(axis=0) for i in range(len(a))])


def frames_as_extracted(video, start, end, dedupe):
    """(time, frame) for every frame extract_pose.py kept from this window."""
    cap = cv2.VideoCapture(video)
    if not cap.isOpened():
        raise SystemExit(f"make_reference: cannot open {video}")
    fps = cap.get(cv2.CAP_PROP_FPS)
    i, last = 0, None
    while True:
        good, frame = cap.read()
        if not good:
            break
        t = i / fps
        i += 1
        if t < start or t > end:
            continue
        if dedupe:
            small = cv2.resize(cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY), (160, 90)).astype(np.float32)
            if last is not None and np.abs(small - last).mean() < 2.0:
                continue
            last = small
        yield t, frame


def make(view, manifest, video_dir):
    v = next((x for x in manifest["views"] if x["name"] == view), None)
    if v is None:
        raise SystemExit(f"make_reference: no view {view} in the manifest")
    npz = np.load(os.path.join(MOCAP, "out", "views", view + ".npz"))
    n = len(npz["ok"])
    video = os.path.join(video_dir, v["command"].split()[2].split("/")[-1])
    start, end = v["window_s"]
    out = os.path.join(HERE, "reference", view)
    os.makedirs(out, exist_ok=True)
    for f in os.listdir(out):
        if f.endswith(".jpg"):
            os.remove(os.path.join(out, f))
    times, size = [], None
    for k, (t, frame) in enumerate(frames_as_extracted(video, start, end, "--dedupe" in v["command"])):
        h, w = frame.shape[:2]
        if w > MAX_W:
            frame = cv2.resize(frame, (MAX_W, int(h * MAX_W / w)), interpolation=cv2.INTER_AREA)
        size = (frame.shape[1], frame.shape[0])
        cv2.imwrite(os.path.join(out, f"{k + 1:04d}.jpg"), frame, [cv2.IMWRITE_JPEG_QUALITY, 88])
        times.append(t)
    if len(times) != n:
        raise SystemExit(f"make_reference: {view}: {len(times)} frames from the video but {n} in its npz - "
                         "not the same frames, refusing to write a reference that would drift")
    slow = v.get("slowmo_factor") or 1.0
    img = npz["image"]                                   # (n, 33, 3): x, y normalised to the frame
    hips = (img[:, 23, :2] + img[:, 24, :2]) / 2
    shoulders = (img[:, 11, :2] + img[:, 12, :2]) / 2
    # Torso length in units of the frame's longer side (x and y are normalised separately).
    W, H = int(npz["width"]), int(npz["height"])
    d = (shoulders - hips) * np.array([W, H]) / max(W, H)
    torso = np.linalg.norm(d, axis=1)[:, None]
    events = {name: v.get(key) for name, key in (("back foot", "back_foot_contact"),
                                                 ("front foot", "front_foot_contact"),
                                                 ("release", "release_frame")) if v.get(key) is not None}
    # Which way he runs across the picture (side-on only): +1 left to right, -1 right to left, read from
    # his lean through the delivery stride (head ahead of hips). Not from where his hips go on screen: a
    # panning camera follows him, and a clip can cut to the other side of the ground mid-run (steyn_side).
    rel = events.get("release", n - 1)
    ks = range(max(events.get("front foot", rel - 3), 0), min(rel + 3, n))
    lean = [img[k, 0, 0] - hips[k, 0] for k in ks if np.isfinite(img[k, 0, 0]) and np.isfinite(hips[k, 0])]
    run_dir = int(np.sign(np.median(lean))) if lean else 1
    meta = {
        "run_dir": run_dir or 1,
        "view": view, "kind": v["view"], "bowler": v["bowler"], "arm": v.get("arm", "right"),
        "source": v["source"], "frames": n, "width": size[0], "height": size[1],
        "time": [round((t - times[0]) * slow, 5) for t in times],
        "events": events,
        "hips": np.round(smooth(hips), 5).tolist(),
        "torso": np.round(smooth(torso)[:, 0], 5).tolist(),
    }
    json.dump(meta, open(os.path.join(out, "meta.json"), "w"))
    print(f"make_reference: {view}: {n} frames {size[0]}x{size[1]}, {meta['time'][-1]:.2f} s real, "
          f"events {events}, runs {'left to right' if run_dir >= 0 else 'right to left'} -> {out}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("views", nargs="+")
    ap.add_argument("--video-dir", default="", help="where the source videos are (default: the manifest's footage_dir)")
    a = ap.parse_args()
    manifest = json.load(open(os.path.join(MOCAP, "out", "views", "manifest.json")))
    video_dir = a.video_dir or manifest["footage_dir"]
    for view in a.views:
        make(view, manifest, video_dir)


if __name__ == "__main__":
    sys.exit(main())
