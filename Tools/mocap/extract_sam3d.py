"""Video -> per-frame 3D body landmarks with Meta's SAM 3D Body, in extract_pose.py's format.

A drop-in for extract_pose.py's MediaPipe stage: same .npz keys and conventions, so
process_motion.py and everything after it run unchanged. SAM 3D Body (SAM License, commercial use
allowed; its body model MHR is Apache-2.0) fits a parametric body to each frame, so its joints
stay within human limits by construction and its depth is far better than MediaPipe's.

It runs per frame on the person box taken from a MediaPipe pass over the same video (no detector:
detectron2 does not build cleanly on macOS), with the camera's focal length given rather than
estimated. Temporal smoothing is process_motion.py's job, as for MediaPipe.

    # once: the vendor checkout and its venv (see ~/Documents/Git/sam-3d-body), and HF access to
    # facebook/sam-3d-body-dinov3 (gated; request on the model page, wait for approval)
    .venv/bin/python extract_pose.py ~/Downloads/IMG_9676.MOV out/img9676.npz --overlay
    ~/Documents/Git/sam-3d-body/.venv/bin/python extract_sam3d.py ~/Downloads/IMG_9676.MOV \
        out/img9676.npz out/img9676_sam.npz --hf facebook/sam-3d-body-dinov3
    .venv/bin/python process_motion.py out/img9676_sam.npz out/img9676_sam.json --end 45 ...

Writes <out>.npz with, per frame (MediaPipe's 33-point layout, as extract_pose.py):
  world   (F, 33, 3)  metric 3D, hip-centred, camera axes (x right, y down, z away from the camera)
  image   (F, 33, 3)  normalised image x, y; z = metric depth relative to the hips
  vis     (F, 33)     1 for points SAM 3D Body gives, 0 for the ones it has no equivalent of
  ok      (F,)        a body was fitted this frame
plus fps, width, height, start (seconds, copied from the MediaPipe pass). And <out>_raw.npz with
everything else SAM 3D Body returned (all 70 keypoints 3D/2D, camera translation, focal length,
global and per-joint rotations, MHR pose/shape/scale parameters, the box used), for later use.

    --selftest   no model needed: rebuilds 70-point output from the MediaPipe npz, converts it back
                 and checks the round trip, then writes <out>.npz from it
"""
import argparse
import os
import sys
import time

import numpy as np

VENDOR = os.path.expanduser("~/Documents/Git/sam-3d-body")

# MHR70 keypoint indices (sam_3d_body/metadata/mhr70.py) for each MediaPipe pose index.
# "third_joint" is the finger's base knuckle (MCP), which is what MediaPipe's index/pinky points are.
MP_FROM_MHR70 = {
    0: 0,                       # nose
    2: 1, 5: 2,                 # left eye, right eye
    7: 3, 8: 4,                 # ears
    11: 5, 12: 6,               # shoulders
    13: 7, 14: 8,               # elbows
    15: 62, 16: 41,             # wrists
    17: 61, 18: 40,             # pinky knuckles
    19: 49, 20: 28,             # index knuckles
    21: 45, 22: 24,             # thumbs (base)
    23: 9, 24: 10,              # hips
    25: 11, 26: 12,             # knees
    27: 13, 28: 14,             # ankles
    29: 17, 30: 20,             # heels
    31: 15, 32: 18,             # foot index = big toe
}
# MediaPipe points with no MHR70 counterpart, filled from a neighbour (vis 0).
MP_FILL = {1: 2, 3: 2, 4: 5, 6: 5, 9: 0, 10: 0}


def to_mediapipe33(kp70):
    """(..., 70, 3) MHR70 -> (..., 33, 3) MediaPipe layout, and (33,) visibility."""
    out = np.zeros(kp70.shape[:-2] + (33, kp70.shape[-1]))
    vis = np.zeros(33)
    for mp, k in MP_FROM_MHR70.items():
        out[..., mp, :] = kp70[..., k, :]
        vis[mp] = 1.0
    for mp, src in MP_FILL.items():
        out[..., mp, :] = out[..., src, :]
    return out, vis


def boxes_from_mediapipe(image, vis, w, h, margin=0.12):
    """Per-frame person box (x1, y1, x2, y2, px) from MediaPipe's 2D landmarks, held over gaps."""
    boxes = np.zeros((len(image), 4))
    last = np.array([0, 0, w, h], float)
    for f in range(len(image)):
        good = (vis[f] > 0.3) & np.isfinite(image[f, :, 0])
        if good.sum() >= 8:
            xs, ys = image[f, good, 0] * w, image[f, good, 1] * h
            x1, x2, y1, y2 = xs.min(), xs.max(), ys.min(), ys.max()
            mx, my = (x2 - x1) * margin, (y2 - y1) * margin
            last = np.array([max(0, x1 - mx), max(0, y1 - my), min(w, x2 + mx), min(h, y2 + my)])
        boxes[f] = last
    return boxes


def pick_device(want):
    import torch
    if want != "auto":
        return want
    return "mps" if torch.backends.mps.is_available() else "cpu"


def shim_device(device):
    """SAM 3D Body hardcodes CUDA in places (recursive_to(..., "cuda"), tensor.cuda(),
    torch.cuda.empty_cache()). Send all of it to `device` instead; no vendor file is edited."""
    import torch
    dev = torch.device(device)
    torch.Tensor.cuda = lambda self, *a, **k: self.to(dev)
    torch.cuda.empty_cache = lambda: None
    import sam_3d_body.sam_3d_body_estimator as est_mod
    import sam_3d_body.models.meta_arch.sam3d_body as arch_mod
    orig = est_mod.recursive_to

    def recursive_to(x, target):
        return orig(x, dev if str(target).startswith("cuda") else target)

    est_mod.recursive_to = recursive_to
    arch_mod.recursive_to = recursive_to


def load_estimator(args, device):
    sys.path.insert(0, VENDOR)
    shim_device(device)
    from sam_3d_body import SAM3DBodyEstimator, load_sam_3d_body
    if args.hf:
        from huggingface_hub import snapshot_download
        root = snapshot_download(repo_id=args.hf)
        ckpt, mhr = os.path.join(root, "model.ckpt"), os.path.join(root, "assets", "mhr_model.pt")
    else:
        ckpt, mhr = args.checkpoint, args.mhr
    # load_sam_3d_body_hf drops its device argument; call the loader directly.
    model, cfg = load_sam_3d_body(checkpoint_path=ckpt, device=device, mhr_path=mhr)
    return SAM3DBodyEstimator(sam_3d_body_model=model, model_cfg=cfg)


def convert(person, w, h):
    """One SAM 3D Body person output -> (world33, image33, raw dict)."""
    kp3 = np.asarray(person["pred_keypoints_3d"], float)[:70]          # camera axes, body-centred
    kp2 = np.asarray(person["pred_keypoints_2d"], float)[:70]          # pixels
    span = np.linalg.norm(kp3[5] - kp3[13])                            # shoulder to ankle
    if span > 10:                                                      # centimetres, not metres
        kp3 = kp3 / 100.0
    world, vis = to_mediapipe33(kp3)
    mid_hip = (world[23] + world[24]) / 2
    world = world - mid_hip
    img2, _ = to_mediapipe33(np.concatenate([kp2, np.zeros((70, 1))], 1))
    image = np.stack([img2[:, 0] / w, img2[:, 1] / h, world[:, 2]], 1)
    raw = {k: np.asarray(v) for k, v in person.items()
           if v is not None and k not in ("pred_vertices", "mask")}
    return world, image, vis, raw


def write(out, world, image, vis, ok, fps, w, h, start, raws=None):
    np.savez(out, world=np.array(world, float), image=np.array(image, float), vis=np.array(vis, float),
             ok=np.array(ok), fps=fps, width=w, height=h, start=start)
    if raws:
        keys = sorted({k for r in raws if r for k in r})
        packed = {}
        for k in keys:
            shapes = {np.shape(r[k]) for r in raws if r and k in r}
            if len(shapes) == 1:
                shape = shapes.pop()
                packed[k] = np.stack([r[k] if r and k in r else np.full(shape, np.nan) for r in raws])
        np.savez(os.path.splitext(out)[0] + "_raw.npz", **packed)


def selftest(args):
    """Round trip through the 70-point layout using the MediaPipe pass as stand-in model output."""
    d = np.load(args.mediapipe)
    w, h = int(d["width"]), int(d["height"])
    worlds, images, viss, oks = [], [], [], []
    inv = {k: mp for mp, k in MP_FROM_MHR70.items()}
    worst = 0.0
    for f in range(len(d["world"])):
        kp3 = np.zeros((70, 3))
        kp2 = np.zeros((70, 2))
        for k, mp in inv.items():
            kp3[k] = d["world"][f, mp] + [0.1, -0.2, 3.0]                   # body-centred, offset
            kp2[k] = d["image"][f, mp, :2] * [w, h]
        world, image, vis, _ = convert({"pred_keypoints_3d": kp3, "pred_keypoints_2d": kp2}, w, h)
        mp_world = d["world"][f] - (d["world"][f, 23] + d["world"][f, 24]) / 2
        mapped = list(MP_FROM_MHR70)
        worst = max(worst, float(np.nanmax(np.abs(world[mapped] - mp_world[mapped]))),
                    float(np.nanmax(np.abs(image[mapped, :2] - d["image"][f, mapped, :2]))))
        worlds.append(world)
        images.append(image)
        viss.append(vis)
        oks.append(True)
    write(args.out, worlds, images, viss, oks, float(d["fps"]), w, h, float(d["start"]))
    print(f"selftest: {len(worlds)} frames, {len(MP_FROM_MHR70)} of 33 points mapped, "
          f"worst round-trip error {worst:.2e} -> {args.out}")
    return 0 if worst < 1e-9 else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("mediapipe", help="extract_pose.py's .npz for the same video (person boxes, frame range)")
    ap.add_argument("out")
    ap.add_argument("--hf", default="", help="Hugging Face repo, e.g. facebook/sam-3d-body-dinov3")
    ap.add_argument("--checkpoint", default="", help="model.ckpt (with model_config.yaml beside it)")
    ap.add_argument("--mhr", default="", help="assets/mhr_model.pt")
    ap.add_argument("--fx", type=float, default=0.72, help="focal length / frame width (iPhone main lens, 4K: 0.72)")
    ap.add_argument("--device", default="auto", help="auto (mps, else cpu), mps or cpu")
    ap.add_argument("--full", action="store_true", help="also run the hand decoder (slower, more CUDA-only paths)")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args()
    if args.selftest:
        return selftest(args)
    if not (args.hf or (args.checkpoint and args.mhr)):
        ap.error("give --hf, or --checkpoint and --mhr")

    import cv2
    import torch

    d = np.load(args.mediapipe)
    fps, w, h, start = float(d["fps"]), int(d["width"]), int(d["height"]), float(d["start"])
    n = len(d["world"])
    boxes = boxes_from_mediapipe(d["image"], d["vis"], w, h)
    device = pick_device(args.device)
    est = load_estimator(args, device)
    fx = args.fx * w
    K = torch.tensor([[[fx, 0, w / 2], [0, fx, h / 2], [0, 0, 1]]], dtype=torch.float32)

    cap = cv2.VideoCapture(args.video)
    first = int(round(start * fps))
    cap.set(cv2.CAP_PROP_POS_FRAMES, first)
    worlds, images, viss, oks, raws = [], [], [], [], []
    t0 = time.time()
    for f in range(n):
        good, frame = cap.read()
        if not good:
            break
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        people = est.process_one_image(rgb, bboxes=boxes[f][None], cam_int=K,
                                       inference_type="full" if args.full else "body")
        if people:
            world, image, vis, raw = convert(people[0], w, h)
            worlds.append(world)
            images.append(image)
            viss.append(vis)
            oks.append(True)
            raws.append(raw)
        else:
            worlds.append(np.full((33, 3), np.nan))
            images.append(np.full((33, 3), np.nan))
            viss.append(np.zeros(33))
            oks.append(False)
            raws.append(None)
        if f == 0 or (f + 1) % 10 == 0:
            print(f"frame {f + 1}/{n}  {(time.time() - t0) / (f + 1):.2f} s/frame on {device}", flush=True)
    write(args.out, worlds, images, viss, oks, fps, w, h, start, raws)
    print(f"{len(worlds)} frames, body fitted in {int(np.sum(oks))}, "
          f"{(time.time() - t0) / max(1, len(worlds)):.2f} s/frame on {device} -> {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
