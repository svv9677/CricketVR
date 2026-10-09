"""Rebuilds Assets/Sounds/Bat/{edge,good,middled}.wav from two real cricket recordings (CC0).

Needs numpy, scipy and ffmpeg, and the two source previews next to this script:
  curl -sLo oroboros.mp3 https://cdn.freesound.org/previews/141/141249_2051721-hq.mp3
  curl -sLo gh0sty.mp3   https://cdn.freesound.org/previews/594/594342_12596088-hq.mp3
Run:  python3 build_sounds.py <output folder>      (see Assets/Sounds/Bat/SOURCES.md)
"""
import sys, wave
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from analyze import load, onsets, features, SR

oro = load("oroboros.mp3")
gh = load("gh0sty.mp3")

def at(x, t, pre=0.0015):
    """Index of the onset foot nearest time t (s)."""
    return max(int((t - pre) * SR), 0)

def seg(x, t, ms):
    s = at(x, t)
    return x[s: s + int(ms / 1000 * SR)].copy()

def hp(x, f, o=4): return sosfilt(butter(o, f, "hp", fs=SR, output="sos"), x)
def lp(x, f, o=4): return sosfilt(butter(o, f, "lp", fs=SR, output="sos"), x)
def bp(x, lo, hi, o=3): return sosfilt(butter(o, [lo, hi], "bp", fs=SR, output="sos"), x)

def fade(x, in_ms=0.5, out_ms=20):
    x = x.copy()
    a, b = int(in_ms / 1000 * SR), int(out_ms / 1000 * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x

def norm(x, peak_db=-1.0):
    return x / (np.abs(x).max() + 1e-12) * 10 ** (peak_db / 20)

def pad(x, n): return np.pad(x, (0, max(n - len(x), 0)))[:n] if n >= len(x) else x[:n]

def mix(*parts, n=None):
    n = n or max(len(p) for p in parts)
    out = np.zeros(n)
    for p, g in parts:
        out[: len(p)] += pad(p, min(len(p), n))[: n] * g if len(p) <= n else p[:n] * g
    return out

def softclip(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)

def compress(x, thresh_db=-24, ratio=5, attack_ms=0.5, release_ms=40, makeup=True):
    """Feed-forward peak compressor, sample loop on a downsampled envelope."""
    a = np.exp(-1 / (attack_ms / 1000 * SR)); r = np.exp(-1 / (release_ms / 1000 * SR))
    env = np.zeros_like(x); e = 0.0
    ab = np.abs(x)
    for i in range(len(x)):
        e = a * e + (1 - a) * ab[i] if ab[i] > e else r * e + (1 - r) * ab[i]
        env[i] = e
    db = 20 * np.log10(env + 1e-9)
    gain_db = np.where(db > thresh_db, (thresh_db - db) * (1 - 1 / ratio), 0.0)
    y = x * 10 ** (gain_db / 20)
    return norm(y, -1) if makeup else y

def reverb(x, rt60_ms, wet, lo=300, hi=6000, seed=1):
    """A short, dark room: exponentially decaying filtered noise convolved in, so the tail is the bat's own sound."""
    rng = np.random.default_rng(seed)
    n = int(rt60_ms / 1000 * SR)
    ir = rng.standard_normal(n) * np.exp(-6.9 * np.arange(n) / n)
    ir = bp(ir, lo, hi)
    ir /= np.abs(ir).max()
    wetsig = fftconvolve(x, ir * 0.05)[: len(x) + n - 1]
    out = np.zeros(len(x) + n - 1); out[: len(x)] += x; out += wet * wetsig
    return out

def fdn_reverb(x, rt60_lo=1.3, rt60_hi=0.45, wet_db=-9.0, predelay_ms=14, tail_s=1.3):
    """A real-sounding hall: pre-delay, early reflections, then an 8-line feedback delay network
    (Hadamard mixing) whose highs die faster than its lows - the dry sound is left untouched."""
    n = len(x) + int(tail_s * SR)
    src = np.zeros(n); src[: len(x)] = x
    pre = int(predelay_ms / 1000 * SR)
    # early reflections: a few discrete bounces off near walls, each darker and quieter
    er = np.zeros(n)
    for ms, g in [(11, 0.50), (17, 0.42), (23, -0.36), (31, 0.30), (41, -0.26), (53, 0.20), (67, -0.16)]:
        k = int(ms / 1000 * SR)
        er[k:] += g * lp(src, 6500 - ms * 40, 2)[: n - k]
    delays = np.array([1103, 1423, 1759, 1999, 2339, 2647, 2957, 3253])            # ~25-74 ms, mutually prime
    H = np.array([[1]])
    for _ in range(3): H = np.block([[H, H], [H, -H]])
    H = H / np.sqrt(8)
    g_lo = 10 ** (-3 * delays / SR / rt60_lo)
    g_hi = 10 ** (-3 * delays / SR / rt60_hi)
    k = (1 - g_hi / g_lo) / (1 + g_hi / g_lo)                                      # per-line damping so highs decay faster
    bufs = [np.zeros(d) for d in delays]; idx = np.zeros(8, dtype=int); lpstate = np.zeros(8)
    out = np.zeros(n)
    inp = np.zeros(n); inp[pre:] = src[: n - pre]
    for i in range(n):
        taps = np.array([bufs[j][idx[j]] for j in range(8)])
        out[i] = taps.sum() * 0.35
        mixed = H @ taps
        for j in range(8):
            lpstate[j] = (1 - k[j]) * (g_lo[j] * mixed[j]) + k[j] * lpstate[j]
            bufs[j][idx[j]] = lpstate[j] + inp[i] * 0.5
            idx[j] = (idx[j] + 1) % delays[j]
    wet = hp(out + er * 0.6, 120, 2)
    # level by energy: the whole reverb carries `wet_db` of the dry sound's energy
    wetgain = np.sqrt(10 ** (wet_db / 10) * (src ** 2).sum() / ((wet ** 2).sum() + 1e-12))
    return src + wet * wetgain

def modes(freqs_db_ms, length_ms, t0_ms=0):
    out = np.zeros(int(length_ms / 1000 * SR)); t = np.arange(len(out)) / SR
    for f, db, decay_ms in freqs_db_ms:
        env = np.exp(-6.9 * t / (decay_ms / 1000))
        s = np.sin(2 * np.pi * f * t) * env * 10 ** (db / 20)
        k = int(t0_ms / 1000 * SR); out[k:] += s[: len(out) - k]
    return out

def save(x, path):
    x = norm(x, -1.0)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())

# ---- source hits (times from analyze.py) -----------------------------------------------------
# OroborosNZ, real cricket nets: clean strikes
crack_sharp = seg(oro, 24.92, 90)        # short, bright, clean
crack_bright = seg(oro, 31.14, 90)
crack_dense = seg(oro, 13.23, 130)       # broader, a little more body
# GH0STY_XD, willow bat and a real ball: the wood's own body and ring
body_a = seg(gh, 22.31, 520)
body_b = seg(gh, 12.27, 520)

def decay_env(n, tau_ms):
    return np.exp(-np.arange(n) / SR / (tau_ms / 1000))

def edge():
    x = hp(crack_sharp, 1800)[: int(0.055 * SR)]
    # a touch of presence where a snick lives
    x = x + 0.35 * bp(x, 3000, 7000)
    return fade(norm(x), 0.3, 22)

def good():
    top = crack_dense[: int(0.10 * SR)]
    top = fade(top * decay_env(len(top), 30), 0.3, 12)                   # the hit, not the nets around it
    body = bp(body_a[: int(0.15 * SR)], 250, 1400)
    body = body * decay_env(len(body), 22)                               # no ring: gone in ~50 ms
    body = body - bp(body, 560, 650, 2) * 0.9                            # and none of the bat's 600 Hz note
    x = mix((norm(top, -3), 1.0), (norm(body, -3), 0.7), n=int(0.14 * SR))
    x = compress(x, -26, 6, 0.3, 30)
    x = softclip(norm(x), 2.2)                                            # dense: peaks pushed down into the body
    return fade(norm(x), 0.3, 40)

def middled():
    top = crack_bright[: int(0.06 * SR)]
    top = fade(norm(top + 0.25 * hp(top, 3500), -2), 0.3, 14)
    body = bp(body_a[: int(0.30 * SR)], 380, 1500)                       # cut before the next tap in the recording
    body = fade(body, 0.3, 90)
    ring = modes([(603, -15, 400), (1190, -24, 260), (1325, -29, 220), (1880, -30, 180)], 560, 5)   # spaced wide: close modes beat like a tremolo
    x = mix((top, 1.0), (norm(body, -19), 0.8), (ring, 1.0), n=int(0.56 * SR))
    x = fdn_reverb(x)                                                    # a hall around the crack: the tail is what resounds
    x = compress(x, -30, 2.0, 0.4, 200)
    return fade(norm(x), 0.3, 500)

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in [("edge", edge), ("good", good), ("middled", middled)]:
        save(fn(), f"{out}/{name}.wav")
        print("wrote", name)
