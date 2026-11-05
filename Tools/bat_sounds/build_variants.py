"""Bat sounds, second set (2026-11-05): several takes of each kind, made from every clean strike in
the two CC0 recordings instead of one each, and processed far less - so what you hear is the real
crack of a bat, not a pitch-shifted, synthesised ring in a reverb, and two drives in a row are
never the identical sample.

Writes Assets/Sounds/Bat/{middled,good,edge}_{1..4}.wav (BatSounds picks one at random, never the
same twice running). The old single clips (build_sounds.py) stay as the fallback.

Same sources and tools as build_sounds.py (numpy, scipy, ffmpeg, the two preview MP3s next to it):
  python3 build_variants.py <output folder>

What each kind is (all measured, not auditioned - listen in the headset and tune here):
  middled - a real nets strike, untouched for its first 60 ms, over the close-miked willow bat's
            own body and ~600 Hz ring at its real pitch, ringing out over ~300 ms; outdoors, so no
            room - just one faint slap back off the stands (~180 ms, -24 dB). ~0.55 s.
  good    - the same strikes with the ring damped out and the tail gated at ~70 ms: a solid,
            flat knock. ~0.12 s.
  edge    - the strike's bright top only (high-passed at 2 kHz), thin and quick, with a light,
            high wood tick from the close-miked bat. ~60 ms.
Every take of a kind is soft-limited to the same loudness as the old clip of that kind (RMS of its
first 40 ms, LOUDNESS), peaks at -1 dBFS, so a random pick never jumps in level.
"""
import sys, wave
import numpy as np
from build_sounds import (oro, gh, seg, hp, lp, bp, fade, norm, decay_env, compress, SR)

# Clean bat-on-ball strikes (analyze.py): loud, a sharp front, broadband (flat > 0.15), and
# nothing else within ~150 ms. The other loud events in the nets recording are voices and
# balls into the net.
NETS = [4.76, 13.23, 24.92, 31.14]
# Close-miked willow, a hard tap each, picked for a clean front and no second tap inside 300 ms.
WOOD = [22.68, 12.27, 8.42, 17.88]


def take(times, src, ms):
    return [seg(src, t, ms) for t in times]


def rms(x, ms=40):
    n = int(ms / 1000 * SR)
    return float(np.sqrt(np.mean(x[:n] ** 2)) + 1e-12)


def gate(x, keep_ms, tau_ms):
    """Leave the first keep_ms alone, then let it die away with time constant tau_ms."""
    k = int(keep_ms / 1000 * SR)
    env = np.ones(len(x))
    env[k:] = decay_env(len(x) - k, tau_ms)
    return x * env


def slapback(x, delay_ms=180, db=-24, tail_ms=0):
    """One faint, dark echo off the stands - the only 'room' an open ground has."""
    d = int(delay_ms / 1000 * SR)
    out = np.zeros(len(x) + d + int(tail_ms / 1000 * SR))
    out[: len(x)] += x
    echo = lp(hp(x, 300, 2), 3500, 2) * 10 ** (db / 20)
    out[d: d + len(x)] += echo[: len(out) - d]
    return out


def notch(x, f, width, depth):
    return x - bp(x, f - width, f + width, 2) * depth


def middled(i):
    crack = take(NETS, oro, 160)[i]
    crack = hp(crack, 120, 2)                         # wind and handling rumble only
    crack = gate(crack, 30, 15)                       # the crack only: 4.76 has the ball thudding into the net 80 ms on, 13.23 a knock at 140 ms
    wood = take(WOOD, gh, 400)[i]                     # 12.27 taps again at 420 ms
    wood = hp(wood, 180, 2)
    wood = gate(wood, 25, 140)                        # the willow carries the tail, ringing out over ~350 ms
    n = int(0.42 * SR)
    x = np.zeros(n)
    x[: len(crack)] += norm(crack, -1) * 1.0
    x[: len(wood[:n])] += norm(wood, -1)[:n] * 0.7
    x = compress(x, -18, 2.5, 1.0, 80, makeup=False)  # gentle: hold the ring up under the crack
    x = slapback(x, 180 + 15 * i, -24)
    return fade(x, 0.2, 120)


def good(i):
    crack = take(NETS, oro, 110)[i]
    crack = gate(hp(crack, 150, 2), 30, 18)
    wood = take(WOOD, gh, 140)[i]
    wood = notch(notch(hp(wood, 200, 2), 603, 60, 0.95), 730, 60, 0.8)   # no ring: a dead, solid knock
    wood = gate(wood, 15, 20)
    n = int(0.12 * SR)
    x = np.zeros(n)
    x[: min(n, len(crack))] += norm(crack, -1)[:n]
    x[: min(n, len(wood))] += norm(wood, -1)[:n] * 0.6
    x = compress(x, -22, 4, 0.5, 30, makeup=False)
    return fade(x, 0.2, 30)


def edge(i):
    crack = take(NETS, oro, 70)[i]
    crack = gate(hp(crack, 2000, 4), 12, 12)
    tick = take(WOOD, gh, 60)[(i + 1) % len(WOOD)]
    tick = gate(hp(tick, 1500, 4), 6, 8)
    n = int(0.06 * SR)
    x = np.zeros(n)
    x[: min(n, len(crack))] += norm(crack, -1)[:n]
    x[: min(n, len(tick))] += norm(tick, -1)[:n] * 0.35
    return fade(x, 0.2, 20)


def write(x, path):
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())


# Loudness of each kind's first 40 ms, dBFS RMS: the old clips' (build_sounds.py), so swapping
# sets does not change how loud a shot is against the rest of the mix.
LOUDNESS = {"middled": -12.0, "good": -14.5, "edge": -17.0}


def limit(x, target_db, peak_db=-1.0):
    """Soft-limit (tanh) until the first 40 ms is target_db loud, peaks held at peak_db."""
    x = x / (np.abs(x).max() + 1e-12)
    ceiling = 10 ** (peak_db / 20)
    for drive in np.linspace(1.0, 6.0, 51):
        y = np.tanh(x * drive) / np.tanh(drive) * ceiling
        if 20 * np.log10(rms(y)) >= target_db:
            return y, drive
    return y, drive


def build_kind(name, fn, count):
    takes = [fn(i) for i in range(count)]
    out = []
    for t in takes:
        y, drive = limit(t, LOUDNESS[name])                     # every take the same loudness
        out.append(y)
        print(f"  {name}: drive {drive:.1f}, {20 * np.log10(rms(y)):.1f} dB")
    return out


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    for name, fn in [("middled", middled), ("good", good), ("edge", edge)]:
        for i, x in enumerate(build_kind(name, fn, len(NETS)), 1):
            write(x, f"{out}/{name}_{i}.wav")
            print(f"wrote {name}_{i}  {len(x) / SR * 1000:.0f} ms")
