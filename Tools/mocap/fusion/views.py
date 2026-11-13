"""One camera's track for fusion: rays in the camera's first-frame axes, labels cleaned.

A view is an extract_pose.py .npz plus what fusion needs to know about the camera: which way it
looks at the bowler (side / front / back), its focal length, and where the release is. Points
become viewing rays (pan undone when the npz carries cam_R), so a long broadcast lens and a close
phone are handled by the same pinhole model.
"""
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
from swap_fix import ARMS, LEGS, SWAP_COST, _apply, _release_frame, _viterbi  # noqa: E402

# Every MediaPipe left/right pair: a whole-body relabel swaps all of them.
LR_PAIRS = [(1, 4), (2, 5), (3, 6), (7, 8), (9, 10), (11, 12), (13, 14), (15, 16), (17, 18),
            (19, 20), (21, 22), (23, 24), (25, 26), (27, 28), (29, 30), (31, 32)]
BODY = ([p[0] for p in LR_PAIRS], [p[1] for p in LR_PAIRS])


class View:
    """kind: 'side' (square to the run), 'front' (down the pitch, facing him) or 'back'."""

    def __init__(self, path, kind, fx=0.0, release=-1, start=0, end=None):
        d = np.load(path)
        sl = slice(start, end)
        self.kind, self.path = kind, path
        self.w, self.h = int(d["width"]), int(d["height"])
        self.fps = float(d["fps"])
        self.world = d["world"][sl].astype(float)
        self.vis = d["vis"][sl].astype(float)
        im = d["image"][sl].astype(float)
        self.px = np.stack([im[..., 0] * self.w, im[..., 1] * self.h], -1)
        if fx > 0:
            self.fx = fx
        elif "fx" in d.files and float(d["fx"]) > 0:
            self.fx = float(d["fx"])
        else:
            self.fx = 0.72 * self.w                     # iPhone main lens, 4K, stabilised
        self.R = d["cam_R"][sl] if "cam_R" in d.files else np.repeat(np.eye(3)[None], len(self.px), 0)
        self._fill_gaps()
        self.release = release if release >= 0 else int(_release_frame(im))
        self.report = {}

    def __len__(self):
        return len(self.px)

    def _fill_gaps(self):
        """Frames with no pose (NaN) take their neighbours' values, visibility 0."""
        bad = np.isnan(self.px[..., 0])
        t = np.arange(len(self.px))
        for j in range(33):
            b = bad[:, j]
            if b.all() or not b.any():
                continue
            for arr in (self.px, self.world):
                for k in range(arr.shape[-1]):
                    arr[b, j, k] = np.interp(t[b], t[~b], arr[~b, j, k])
            self.vis[b, j] = 0.0

    def rays(self):
        """(F, 33, 3) unit viewing rays in the first frame's camera axes (x right, y down, z ahead)."""
        p = np.concatenate([(self.px[..., 0:1] - self.w / 2) / self.fx,
                            (self.px[..., 1:2] - self.h / 2) / self.fx, np.ones_like(self.px[..., :1])], -1)
        p = np.einsum("fji,fkj->fki", self.R, p)       # R[t].T @ ray: frame t axes -> frame 0
        return p / np.linalg.norm(p, axis=-1, keepdims=True)

    def leg_px(self):
        """Per-frame 2D leg length (hip-knee-ankle, mean of both legs), pixels."""
        def seg(a, b):
            return np.linalg.norm(self.px[:, a] - self.px[:, b], axis=-1)
        return (seg(23, 25) + seg(25, 27) + seg(24, 26) + seg(26, 28)) / 2

    def leg_m(self):
        """The leg's 3D length (hip-knee-ankle) from MediaPipe's metric landmarks, median."""
        def seg(a, b):
            return np.linalg.norm(self.world[:, a] - self.world[:, b], axis=-1)
        return float(np.median((seg(23, 25) + seg(25, 27) + seg(24, 26) + seg(26, 28)) / 2))

    def swap(self, pair, frames):
        """Swap a left/right point set on the given frames, in every per-point array."""
        if len(frames):
            _apply([self.px, self.world, self.vis], pair, np.asarray(frames))

    def continuity_fix(self):
        """swap_fix's motion-continuity pass per limb pair, without its cricket orientation votes:
        across views, the other camera decides which labelling is right (fuse_views)."""
        for name, pair in (("legs", LEGS), ("arms", ARMS)):
            u = np.zeros((len(self), 2))
            u[:, 1] = SWAP_COST[name]
            path = _viterbi([self.px, self.world], self.vis, pair, u)
            self.swap(pair, np.where(path == 1)[0])
            self.report[f"continuity_{name}"] = int(np.sum(path))

    def orient_by_side(self, switch=40.0):
        """Behind the run-up the bowler's left is on the picture's left. MediaPipe cannot see his
        face from there and may label the whole body as if he faced the camera; those whole-body
        relabels are undone where shoulders and hips are clearly apart across the picture (Viterbi
        keeps it from flickering). From the front it reads his face, and its labels are kept: on
        Pose2Sim's demo, forcing "left on the picture's right" relabelled all 93 frames of a view
        whose raw labels were right on every frame (the subject does not face that camera)."""
        if self.kind != "back":
            return
        want = -1.0                                            # sign of (left - right) in image x
        width = np.nanmedian(np.abs(self.px[:, 11, 0] - self.px[:, 12, 0])) + 1e-6
        d = ((self.px[:, 11, 0] - self.px[:, 12, 0]) + (self.px[:, 23, 0] - self.px[:, 24, 0])) / (2 * width)
        u = np.stack([np.maximum(0, -want * d), np.maximum(0, want * d)], 1) * 10.0
        path = viterbi2(u, switch)
        self.swap(BODY, np.where(path == 1)[0])
        self.report["orient_relabelled"] = int(np.sum(path))


def viterbi2(unary, switch):
    """Two-state Viterbi: per-frame costs (F, 2) and a cost for every change of state."""
    F = len(unary)
    cost = unary[0].copy()
    back = np.zeros((F, 2), int)
    for t in range(1, F):
        stay, move = cost, cost[::-1] + switch
        back[t] = np.where(stay <= move, [0, 1], [1, 0])
        cost = np.minimum(stay, move) + unary[t]
    s = int(np.argmin(cost))
    path = [s]
    for t in range(F - 1, 0, -1):
        s = back[t][s]
        path.append(s)
    return np.array(path[::-1])
