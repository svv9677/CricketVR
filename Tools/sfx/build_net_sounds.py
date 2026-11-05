"""Builds the practice nets' sound: the ball into the netting. Everything is synthesised here: no
recording and no third-party material goes in. Needs numpy and scipy. Run from this folder:
    /usr/local/bin/python3 -W ignore build_net_sounds.py [sfx folder]
(default: ../../Assets/Resources/Sounds/Sfx)

What it writes (mono, 16-bit, 44.1 kHz, peak -1 dBFS):
  net_{1..4}   the ball into a hanging nylon net on a steel frame
GameSfx groups clips by the name before "_N": net. NetsBackstop plays it, louder the harder the ball
comes in.

A ball into a net is three things at once, each made here and mixed:
  * the give: the net bellies and stops the ball softly - a short, low, padded thump (no crack:
    nothing hard is struck);
  * the cord: the mesh whips and rubs - a swish of band-passed noise, bright at first and darker as
    it settles, with a few quick flutters as the cords slap back;
  * the frame: the poles and rail the net hangs off ring faintly - a few inharmonic partials,
    quiet and short, so it reads as steel at a distance rather than a bell.
Each take varies the give's pitch, the swish's brightness and length and the frame's partials, so
four in a row do not sound like one.
"""
import os, sys, wave
import numpy as np
from scipy.signal import butter, sosfilt

SR = 44100
rng = np.random.default_rng(11)


def lp(x, f, o=4): return sosfilt(butter(o, f, "lp", fs=SR, output="sos"), x)
def bp(x, lo, hi, o=3): return sosfilt(butter(o, [lo, hi], "bp", fs=SR, output="sos"), x)


def env(n, tau_ms, attack_ms=0.0):
    t = np.arange(n) / SR
    e = np.exp(-t / (tau_ms / 1000))
    if attack_ms > 0:
        a = int(attack_ms / 1000 * SR)
        e[:a] *= np.linspace(0, 1, a)
    return e


def fade(x, out_ms=25):
    x = x.copy()
    b = int(out_ms / 1000 * SR)
    x[-b:] *= np.linspace(1, 0, b)
    return x


def save(x, path, db=-1.0):
    x = x / (np.abs(x).max() + 1e-12) * 10 ** (db / 20)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print("wrote", os.path.basename(path), f"{len(x) / SR * 1000:.0f} ms")


def give(n, f0):
    """The net taking the ball: a padded low thump, its pitch sagging as the net stretches."""
    t = np.arange(n) / SR
    sweep = f0 * (1 - 0.35 * np.minimum(t / 0.06, 1))
    body = np.sin(2 * np.pi * np.cumsum(sweep) / SR) * env(n, 32, 6)
    pad = lp(rng.standard_normal(n), 400) * env(n, 18, 4)
    return body + 0.5 * pad


def cord(n, bright, tau_ms):
    """The mesh: a swish that darkens as it settles, with the cords slapping back a few times."""
    noise = rng.standard_normal(n)
    early = bp(noise, 1800, min(bright, 9000)) * env(n, tau_ms * 0.45, 3)
    late = bp(noise, 500, 2400) * env(n, tau_ms, 10)
    swish = early + 0.7 * late
    flutter = np.zeros(n)
    at = 0.012
    for k in range(rng.integers(3, 6)):
        at += rng.uniform(0.018, 0.045)
        s = int(at * SR)
        if s >= n: break
        m = min(int(0.02 * SR), n - s)
        flutter[s:s + m] += bp(rng.standard_normal(m), 1200, 5000) * env(m, 4) * (0.55 ** k)
    return swish + 0.6 * flutter


def frame(n, base):
    """The poles and rail ringing faintly: a few inharmonic partials, short and quiet."""
    t = np.arange(n) / SR
    out = np.zeros(n)
    for ratio, amp, tau in ((1.0, 1.0, 90), (2.76, 0.55, 60), (5.40, 0.3, 40), (8.93, 0.15, 25)):
        f = base * ratio * rng.uniform(0.98, 1.02)
        out += amp * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * env(n, tau, 1)
    return out


def take(i):
    n = int(rng.uniform(0.42, 0.52) * SR)
    x = (0.5 * give(n, rng.uniform(85, 120)) +
         1.0 * cord(n, rng.uniform(3600, 5200), rng.uniform(70, 110)) +
         0.07 * frame(n, rng.uniform(380, 520)))
    return fade(x)


def main(out):
    os.makedirs(out, exist_ok=True)
    for i in range(1, 5):
        save(take(i), os.path.join(out, f"net_{i}.wav"))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join("..", "..", "Assets", "Resources", "Sounds", "Sfx"))
