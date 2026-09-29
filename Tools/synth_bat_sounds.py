#!/usr/bin/env python3
"""
Synthesise the bat-ball contact sounds in Assets/Sounds/Bat/ (see BatSounds.cs).

Modal synthesis of a struck willow blade, in four layers:

  * the blade: its bending modes (free-free beam ratios 1 : 2.76 : 5.40 : 8.93 : 13.3), a torsional
    mode that only an off-centre hit sets twisting, and a dense scatter of small high modes that
    give wood its grain. Every mode decays by the wood's loss, tau = Q / (pi f): willow's Q is
    ~30-60, so the highs die in a few milliseconds and the lows ring on. The first version held the
    highs for 12-20 ms (Q ~130, near metal), which is what made it ping instead of knock;
  * the ball: the leather-and-cork "pock", a heavily damped resonance that starts a little sharp
    (compressed, it is stiffer) and settles as it springs back, and the seam's click;
  * the crack: on a hard, clean strike the face snaps the air - a very short bright burst;
  * the ground: two early reflections, a faint diffuse tail, and for a clean hit the slap-back
    off the stands ~170 ms later, so it sounds like a ground and not a room.

The grain above 1.5 kHz is noise through broad resonances rather than pure decaying sines (which
read as a xylophone). A thick edge is a skid along the edge - two or three strikes a few ms
apart, the clatter of a "clack"; toe, shoulder and handle hits are damped by the rubber grip and
the hands on it.

How long the ball stays on the bat decides how much of the top end is excited (a half-sine
force pulse; shorter is brighter): a middled ball leaves in well under a millisecond, a toe-ender
or a dead shoulder stays on longer and only thuds. Where it is struck decides the mix: the sweet
spot is the node of the first bending mode (no hum, no sting); the toe and shoulder sit on
antinodes and buzz up the handle; an edge twists the blade and rings the torsional mode.

Levels are matched by loudness (RMS of the first 60 ms), not peak, so a thin click and a full
knock sit where the ear expects them relative to each other. BatSounds scales on top of that by
how hard the ball was met. TAKES variations per kind (every frequency, decay and level jittered).

Run from the repo root:  python3 Tools/synth_bat_sounds.py
"""
import os
import wave

import numpy as np

RATE = 44100
TAKES = 5
OUT = os.path.join("Assets", "Sounds", "Bat")
BEAM = (1.0, 2.756, 5.404, 8.933, 13.34)

# kind:
#   contact   s, force pulse length (shorter = brighter)
#   f1        Hz, first bending mode as the hands hold it
#   bend      level of each bending mode (BEAM ratios); 0 on a node
#   q         wood Q (higher = rings longer)
#   torsion   (Hz, level): the blade twisting, from an off-centre hit
#   grain     level of the dense high modes
#   pock      (Hz, level): the ball's own compression resonance
#   click     level of the seam/surface click
#   crack     level of the air crack
#   tone      low-pass corner (Hz) over the whole, None = open
#   loud      target loudness, dB relative to middled
#   length    s
#   strikes   extra impacts after the first, [(delay s, level)]: a thick edge is a skid along the
#             edge, two or three strikes a few ms apart - the clatter that makes it a "clack"
#   glide     how far above its resting pitch the ball's pock starts (it stiffens compressed)
#   echo      level of the slap-back off the stands (~170 ms): a clean hit carries to them
#   grip      the handle's rubber grip under the hands: extra damping of the blade's top end
KINDS = {
    "middled":    dict(contact=0.0006, f1=150, bend=(0.10, 0.55, 0.80, 0.55, 0.30), q=45,
                       torsion=(1150, 0.00), grain=0.35, pock=(620, 0.60), click=0.35, crack=0.90,
                       tone=None, loud=0.0, length=0.45),
    "good":       dict(contact=0.0009, f1=148, bend=(0.25, 0.60, 0.65, 0.35, 0.15), q=40,
                       torsion=(1120, 0.10), grain=0.25, pock=(600, 0.70), click=0.25, crack=0.45,
                       tone=7000, loud=-2.5, length=0.40),
    "thick_edge": dict(contact=0.0006, f1=160, bend=(0.15, 0.30, 0.45, 0.50, 0.45), q=38,
                       torsion=(1250, 0.90), grain=0.55, pock=(700, 0.35), click=0.55, crack=0.30,
                       tone=None, loud=-4.0, length=0.35),
    "thin_edge":  dict(contact=0.0003, f1=170, bend=(0.00, 0.00, 0.04, 0.10, 0.20), q=30,
                       torsion=(1400, 0.10), grain=1.60, pock=(900, 0.03), click=1.00, crack=0.00,
                       tone=None, loud=-12.0, length=0.18),
    "toe":        dict(contact=0.0018, f1=140, bend=(0.90, 0.60, 0.25, 0.08, 0.00), q=28,
                       torsion=(1050, 0.10), grain=0.08, pock=(480, 0.80), click=0.05, crack=0.00,
                       tone=2600, loud=-5.0, length=0.40),
    "shoulder":   dict(contact=0.0014, f1=128, bend=(0.80, 0.70, 0.35, 0.12, 0.00), q=32,
                       torsion=(980, 0.15), grain=0.08, pock=(500, 0.60), click=0.08, crack=0.00,
                       tone=3200, loud=-5.5, length=0.55),
    "handle":     dict(contact=0.0016, f1=105, bend=(1.00, 0.50, 0.20, 0.05, 0.00), q=36,
                       torsion=(900, 0.10), grain=0.05, pock=(450, 0.50), click=0.05, crack=0.00,
                       tone=2600, loud=-7.0, length=0.60),
    "back":       dict(contact=0.0015, f1=150, bend=(0.40, 0.55, 0.30, 0.10, 0.00), q=30,
                       torsion=(1100, 0.15), grain=0.10, pock=(520, 0.90), click=0.10, crack=0.00,
                       tone=1900, loud=-6.0, length=0.35),
}

DEFAULTS = dict(strikes=(), glide=0.15, echo=0.0, grip=0.0)
EXTRA = {
    "middled":    dict(glide=0.20, echo=0.10),
    "good":       dict(glide=0.18, echo=0.07),
    "thick_edge": dict(strikes=((0.0035, 0.55), (0.0080, 0.25)), glide=0.10, echo=0.05),
    "thin_edge":  dict(strikes=((0.0020, 0.35),), glide=0.05),
    "toe":        dict(glide=0.25, echo=0.03, grip=0.3),
    "shoulder":   dict(strikes=((0.0045, 0.20),), glide=0.15, echo=0.03, grip=0.5),
    "handle":     dict(glide=0.10, grip=0.9),
    "back":       dict(glide=0.20, echo=0.02, grip=0.3),
}
for _kind, _spec in KINDS.items():
    _spec.update({**DEFAULTS, **EXTRA.get(_kind, {})})


def jitter(rng, x, spread):
    return x * rng.uniform(1 - spread, 1 + spread)


def mode(f, tau, a, t, rng):
    return a * np.exp(-t / tau) * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi))


def wood_tau(f, q):
    """Decay time of a mode in a material of quality factor q: tau = Q / (pi f)."""
    return q / (np.pi * f)


def blade(spec, t, rng):
    out = np.zeros_like(t)
    q = jitter(rng, spec["q"], 0.12)
    f1 = jitter(rng, spec["f1"], 0.04)
    for ratio, level in zip(BEAM, spec["bend"]):
        if level <= 0:
            continue
        f = jitter(rng, f1 * ratio, 0.03)
        out += mode(f, wood_tau(f, q), jitter(rng, level, 0.2), t, rng)
    f, level = spec["torsion"]
    if level > 0:
        f = jitter(rng, f, 0.04)
        out += mode(f, wood_tau(f, q * 0.8), jitter(rng, level, 0.2), t, rng)
    out += grain(spec["grain"], q * 0.7, t, rng)
    return out


def grain(level, q, t, rng):
    """The wood's texture above ~1.5 kHz: noise through many broad resonances, each dying at the
    wood's rate. Pure decaying sines there read as a xylophone; noise bands read as timber."""
    n = len(t)
    spectrum = np.fft.rfft(rng.standard_normal(n))
    freqs = np.fft.rfftfreq(n, 1 / RATE)
    shape = np.zeros_like(freqs)
    centres = np.exp(rng.uniform(np.log(1500), np.log(8000), 24))
    for f in centres:
        width = f / q
        shape += rng.uniform(0.3, 1.0) / (1 + ((freqs - f) / width) ** 2)
    band = np.fft.irfft(spectrum * shape, n)
    band /= np.max(np.abs(band)) + 1e-9
    # A high band decays fast: tau of the band's middle frequency.
    return level * 0.35 * band * np.exp(-t / wood_tau(3000, q))


def ball(spec, t, rng):
    """The ball's own pock. Squashed against the bat it is stiffer, so it rings a little high and
    settles down in pitch as it springs back - a glide of a few milliseconds."""
    f, level = spec["pock"]
    f = jitter(rng, f, 0.06)
    freq = f * (1 + spec["glide"] * np.exp(-t / 0.004))
    phase = 2 * np.pi * np.cumsum(freq) / RATE + rng.uniform(0, 2 * np.pi)
    return jitter(rng, level, 0.15) * np.exp(-t / wood_tau(f, 4.0)) * np.sin(phase)


def pulse(contact):
    """Half-sine force pulse lasting `contact` seconds: shorter = brighter."""
    n = max(2, int(round(contact * RATE)))
    p = np.sin(np.pi * np.arange(n) / n)
    return p / p.sum()


def burst(level, decay, n, rng, tilt=1):
    """A very short burst of noise, differenced `tilt` times to push it toward the highs."""
    t = np.arange(n) / RATE
    noise = rng.standard_normal(n + tilt)
    for _ in range(tilt):
        noise = np.diff(noise)
    return level * noise * np.exp(-t / decay)


def lowpass(x, corner):
    if corner is None:
        return x
    # Two one-pole stages: a gentler, less "filtered" slope than one steep stage.
    a = np.exp(-2 * np.pi * corner / RATE)
    for _ in range(2):
        y = np.empty_like(x)
        acc = 0.0
        for i, v in enumerate(x):
            acc = (1 - a) * v + a * acc
            y[i] = acc
        x = y
    return x


def outdoors(x, rng, echo):
    """Ground and pitch reflections, a faint diffuse tail, and the slap-back off the stands."""
    y = x.copy()
    for delay, gain in ((0.0042, 0.25), (0.0105, 0.10)):
        k = int(jitter(rng, delay, 0.1) * RATE)
        y[k:] += gain * x[:-k]
    n = int(0.35 * RATE)
    tail = rng.standard_normal(n) * np.exp(-np.arange(n) / RATE / 0.09)
    tail = lowpass(tail, 3500) * 0.004
    y += np.convolve(x, tail)[: len(x)]
    if echo > 0:
        # ~60 m to the stands and back; air and distance take the top off it.
        k = int(jitter(rng, 0.17, 0.08) * RATE)
        if k < len(y):
            y[k:] += echo * lowpass(x[: len(y) - k], 2500)
    return y


def excitation(spec, rng):
    """The force on the blade: one half-sine pulse, or a skid of several a few ms apart."""
    p = pulse(jitter(rng, spec["contact"], 0.15))
    strikes = [(0.0, 1.0)] + [(jitter(rng, d, 0.2), jitter(rng, a, 0.2)) for d, a in spec["strikes"]]
    out = np.zeros(int(max(d for d, _ in strikes) * RATE) + len(p))
    for d, a in strikes:
        k = int(d * RATE)
        out[k:k + len(p)] += a * p
    return out


def a_weight(f):
    """IEC 61672 A-weighting as a linear gain: how loud the ear hears each frequency."""
    f2 = np.maximum(f, 1e-3) ** 2
    ra = (12194.0 ** 2 * f2 ** 2) / ((f2 + 20.6 ** 2) * np.sqrt((f2 + 107.7 ** 2) * (f2 + 737.9 ** 2)) * (f2 + 12194.0 ** 2))
    return ra * 10 ** (2.0 / 20)


def loudness(y):
    """A-weighted RMS of the first 60 ms: a 100 Hz thud and a 3 kHz click at the same plain RMS
    are nowhere near the same loudness to the ear."""
    head = y[: int(0.06 * RATE)]
    spectrum = np.fft.rfft(head) * a_weight(np.fft.rfftfreq(len(head), 1 / RATE))
    return np.sqrt(np.mean(np.fft.irfft(spectrum, len(head)) ** 2))


def make(spec, rng):
    n = int(spec["length"] * RATE)
    t = np.arange(n) / RATE
    wood = blade(spec, t, rng)
    if spec["grip"] > 0:
        # The rubber grip and the hands on it soak up the blade's top end.
        wood = (1 - spec["grip"]) * wood + spec["grip"] * lowpass(wood, 900)
    body = wood + ball(spec, t, rng)
    y = np.convolve(excitation(spec, rng), body)[:n] * 40
    y += burst(spec["click"], 0.0008, n, rng, tilt=1)
    y += burst(spec["crack"], 0.0015, n, rng, tilt=2)
    y = outdoors(lowpass(y, spec["tone"]), rng, spec["echo"])
    fade = int(0.015 * RATE)                              # no click at the cut
    y[-fade:] *= np.linspace(1, 0, fade)
    return y


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
    takes = {kind: [make(spec, rng) for _ in range(TAKES)] for kind, spec in KINDS.items()}
    # One gain for everything, set so the loudest middled take peaks at -1 dBFS; each kind is then
    # placed at its target loudness relative to middled.
    reference = np.mean([loudness(y) for y in takes["middled"]])
    placed = {kind: [y * (10 ** (KINDS[kind]["loud"] / 20) * reference / loudness(y)) for y in ys]
              for kind, ys in takes.items()}
    ceiling = 10 ** (-1 / 20)
    gain = ceiling / max(np.max(np.abs(y)) for ys in placed.values() for y in ys)
    for kind, ys in placed.items():
        for take, y in enumerate(ys, 1):
            path = os.path.join(OUT, f"{kind}_{take}.wav")
            write(path, y * gain)
            print(f"{path}  peak {20 * np.log10(np.max(np.abs(y * gain))):.1f} dBFS")


if __name__ == "__main__":
    main()
