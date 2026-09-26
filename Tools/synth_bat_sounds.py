#!/usr/bin/env python3
"""
Synthesise the bat-ball contact sounds in Assets/Sounds/Bat/ (see BatSounds.cs).

Modal synthesis: a struck willow blade rings at a handful of damped resonances, and how long the
ball stays on the bat decides how bright the strike is - a middled ball leaves in well under a
millisecond (a sharp impulse excites every mode: the crack), a toe-ender or a dead shoulder stays on
longer (a soft pulse: only the low modes, a thud). Shoulder and handle hits add the low buzz of the
handle ringing - the sting that travels up to the hands.

Each kind gets TAKES variations (frequencies, decays and levels jittered) so repeated shots differ.
Run from the repo root:  python3 Tools/synth_bat_sounds.py
"""
import os
import wave

import numpy as np

RATE = 44100
TAKES = 3
OUT = os.path.join("Assets", "Sounds", "Bat")

# kind: contact time (s), modes [(Hz, decay s, amplitude)], transient noise level, buzz (Hz, decay, amp),
#       low-pass corner (Hz, None = open), peak level, length (s)
KINDS = {
    "middled":    dict(contact=0.0007, modes=[(165, .050, .55), (640, .036, .50), (1260, .030, .90),
                                              (2180, .020, .75), (3350, .012, .40)],
                       noise=.55, buzz=None, lowpass=None, peak=.98, length=.40),
    "good":       dict(contact=0.0010, modes=[(155, .045, .60), (600, .032, .55), (1180, .024, .70),
                                              (2050, .014, .40)],
                       noise=.30, buzz=None, lowpass=6000, peak=.85, length=.35),
    "thick_edge": dict(contact=0.0005, modes=[(900, .012, .35), (1850, .018, .80), (2950, .014, .90),
                                              (4350, .010, .60), (6100, .006, .40)],
                       noise=.65, buzz=(310, .05, .10), lowpass=None, peak=.80, length=.30),
    "thin_edge":  dict(contact=0.0003, modes=[(3100, .006, .50), (4600, .006, .60), (6900, .004, .50)],
                       noise=.40, buzz=None, lowpass=None, peak=.40, length=.15),
    "toe":        dict(contact=0.0018, modes=[(210, .030, .90), (470, .025, .60), (880, .018, .30)],
                       noise=.08, buzz=(120, .09, .15), lowpass=2500, peak=.75, length=.35),
    "shoulder":   dict(contact=0.0014, modes=[(140, .055, .60), (380, .040, .50), (720, .020, .30),
                                              (1300, .010, .15)],
                       noise=.10, buzz=(95, .18, .30), lowpass=3500, peak=.72, length=.50),
    "handle":     dict(contact=0.0016, modes=[(110, .070, .50), (260, .055, .40), (920, .012, .40)],
                       noise=.12, buzz=(82, .25, .45), lowpass=3000, peak=.65, length=.55),
    "back":       dict(contact=0.0015, modes=[(250, .030, .80), (540, .028, .50), (1100, .014, .30)],
                       noise=.10, buzz=None, lowpass=1800, peak=.70, length=.30),
}


def pulse(contact):
    """Half-sine force pulse lasting `contact` seconds: shorter = brighter."""
    n = max(2, int(round(contact * RATE)))
    return np.sin(np.pi * np.arange(n) / n)


def ring(modes, n, rng):
    t = np.arange(n) / RATE
    out = np.zeros(n)
    for f, d, a in modes:
        f *= rng.uniform(0.96, 1.04)
        d *= rng.uniform(0.85, 1.15)
        a *= rng.uniform(0.8, 1.2)
        out += a * np.exp(-t / d) * np.sin(2 * np.pi * f * t + rng.uniform(0, 0.3))
    return out


def transient(level, n, rng):
    """The click of the seam and surface: a very short burst of bright noise."""
    t = np.arange(n) / RATE
    noise = np.diff(rng.standard_normal(n + 1))          # differenced = tilted to the highs
    return level * noise * np.exp(-t / 0.0012)


def lowpass(x, corner):
    if corner is None:
        return x
    a = np.exp(-2 * np.pi * corner / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def reflections(x):
    """Two early reflections off the ground and the pitch, so it sounds outdoors, not in a box."""
    y = x.copy()
    for delay, gain in ((0.0045, 0.22), (0.011, 0.10)):
        k = int(delay * RATE)
        y[k:] += gain * x[:-k]
    return y


def make(spec, rng):
    n = int(spec["length"] * RATE)
    modes = ring(spec["modes"], n, rng)
    if spec["buzz"]:
        modes += ring([spec["buzz"]], n, rng)
    y = np.convolve(pulse(spec["contact"]), modes)[:n]
    y += transient(spec["noise"], n, rng)
    y = reflections(lowpass(y, spec["lowpass"]))
    fade = int(0.01 * RATE)                               # no click at the cut
    y[-fade:] *= np.linspace(1, 0, fade)
    return y / np.max(np.abs(y)) * spec["peak"]


def write(path, y):
    data = (np.clip(y, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def main():
    os.makedirs(OUT, exist_ok=True)
    rng = np.random.default_rng(1729)
    for kind, spec in KINDS.items():
        for take in range(1, TAKES + 1):
            path = os.path.join(OUT, f"{kind}_{take}.wav")
            write(path, make(spec, rng))
            print(path)


if __name__ == "__main__":
    main()
