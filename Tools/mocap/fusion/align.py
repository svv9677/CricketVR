"""Putting two views of the same action on one timeline.

The two clips need not be the same ball: a bowler repeats his action, so what both cameras share
is its rhythm - when the arms go up, when each knee lifts. Features are label-free (of each
left/right pair, the higher and the lower point's height) so swapped labels cannot mislead the
match, and they are in body heights so a far and a near camera compare. The release frames anchor
the match; dynamic time warping, kept to a band around that anchor, absorbs the rest.
"""
import numpy as np
from scipy.signal import savgol_filter

PAIRS = [(15, 16), (13, 14), (25, 26), (27, 28)]     # wrists, elbows, knees, ankles


def body_scale(view, seconds=0.4):
    """Per-frame pixels per leg length: the longest the 2D leg gets within a window (a straight,
    upright leg is not foreshortened), smoothed. Follows a bowler running toward the camera."""
    leg = view.leg_px()
    k = max(1, int(round(seconds * view.fps / 2)))
    peak = np.array([np.nanmax(leg[max(0, t - k): t + k + 1]) for t in range(len(leg))])
    w = min(len(peak) - (1 - len(peak) % 2), 2 * k + 1)
    return savgol_filter(peak, w, 1) if w >= 3 else peak


def features(view):
    """(F, 10) label-free heights in leg lengths: each pair's higher and lower point, the hips and
    the shoulders, relative to the clip's median hip height."""
    up = -view.px[..., 1] / body_scale(view)[:, None]
    hip = (up[:, 23] + up[:, 24]) / 2
    cols = []
    for a, b in PAIRS:
        cols += [np.maximum(up[:, a], up[:, b]), np.minimum(up[:, a], up[:, b])]
    cols += [hip, (up[:, 11] + up[:, 12]) / 2]
    f = np.stack(cols, 1) - np.median(hip)
    return f - np.median(f, axis=0)


def resample(x, fps_from, fps_to):
    """Linear resampling of a per-frame array to another frame rate (real time kept)."""
    n = int(np.floor((len(x) - 1) * fps_to / fps_from)) + 1
    t = np.arange(n) * fps_from / fps_to
    i = np.clip(np.floor(t).astype(int), 0, len(x) - 2)
    a = (t - i).reshape((-1,) + (1,) * (x.ndim - 1))
    return x[i] * (1 - a) + x[i + 1] * a


def dtw_map(fs, ff, anchor_s, anchor_f, band):
    """For each side frame, the matching (fractional) front frame: DTW inside a band of `band`
    frames around the anchor-aligned diagonal, then made smooth and monotonic. NaN outside overlap."""
    S, Fn = len(fs), len(ff)
    off = anchor_f - anchor_s
    # The clips cover different stretches of the action: match only where both have frames on the
    # anchor-aligned diagonal, from its first common frame to its last.
    i0, i1 = max(0, -off), min(S - 1, Fn - 1 - off)
    if i1 - i0 < 3:
        return np.full(S, np.nan)
    n = i1 - i0 + 1
    D = np.full((n, Fn), np.inf)
    for a in range(n):
        i = i0 + a
        lo, hi = max(0, i + off - band), min(Fn - 1, i + off + band)
        for j in range(lo, hi + 1):
            c = float(np.sum((fs[i] - ff[j]) ** 2))
            if a == 0 and j == i + off:
                D[a, j] = c
                continue
            prev = min(D[a - 1, j] if a else np.inf, D[a, j - 1] if j else np.inf,
                       D[a - 1, j - 1] if a and j else np.inf)
            D[a, j] = c + prev
    a, j = n - 1, i1 + off
    if not np.isfinite(D[a, j]):
        return np.full(S, np.nan)
    path = [(a, j)]
    while (a, j) != (0, i0 + off):
        steps = [(D[a - 1, j - 1] if a and j else np.inf, a - 1, j - 1),
                 (D[a - 1, j] if a else np.inf, a - 1, j), (D[a, j - 1] if j else np.inf, a, j - 1)]
        best, a, j = min(steps)
        if not np.isfinite(best):
            break
        path.append((a, j))
    path = np.array(path[::-1])
    m = np.full(S, np.nan)
    for s in np.unique(path[:, 0]):
        m[i0 + s] = path[path[:, 0] == s, 1].mean()
    return _smooth_monotonic(m)


def _smooth_monotonic(m):
    ok = ~np.isnan(m)
    if ok.sum() < 5:
        return m
    idx = np.where(ok)[0]
    v = m[ok]
    w = min(len(v) - (1 - len(v) % 2), 9)
    v = savgol_filter(v, w, 1) if w >= 3 else v
    v = np.maximum.accumulate(v)
    out = np.full_like(m, np.nan)
    out[idx] = v
    return out


def sample_at(x, j):
    """x (Fn, ...) at fractional frames j (S,), linearly; NaN j gives NaN rows."""
    Fn = len(x)
    jj = np.clip(np.nan_to_num(j, nan=0.0), 0, Fn - 1)
    i = np.clip(np.floor(jj).astype(int), 0, max(0, Fn - 2))
    a = (jj - i).reshape((-1,) + (1,) * (x.ndim - 1))
    out = x[i] * (1 - a) + x[np.minimum(i + 1, Fn - 1)] * a
    out[np.isnan(j)] = np.nan
    return out
