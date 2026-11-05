"""Builds the club ground's sounds (phase 6). Everything is synthesised here: no recording and no
third-party material goes in. Needs numpy and scipy. Run from this folder:
    /usr/local/bin/python3 -W ignore build_sounds.py [sfx folder] [ambience folder]
(defaults: ../../Assets/Resources/Sounds/Sfx and ../../Assets/ClubGround/Audio)

What it writes (mono, 16-bit, 44.1 kHz; the one-shots peak at -1 dBFS):
  club_clap_{1..4}   polite applause from 8-15 people round a club ground (a four, a wicket)
  club_cheer_{1..2}  livelier applause with a whistle (a six)
  fence_{1..2}       the ball into the white picket fence: a light wooden knock and a rattle
  club_ambience      a seamless 30 s loop: songbirds, a breeze in the trees, a distant road and voices,
                     levelled to about -24 LUFS (ambience folder: the ClubGround prefab references it,
                     so it is not in Resources/Sounds/Sfx, which GameSfx loads whole)
GameSfx groups clips by the name before "_N": club_clap, club_cheer, fence.

How it stays natural rather than robotic: every clapper is a different pair of hands (its own
resonance, decay, rate and distance), starts late or early and stops on its own, slowing as it does;
a short open-air tail sits behind everything. The loop is built circularly (events wrap round the
end, beds are shaped in the frequency domain, the tail is a circular convolution), so it has no seam.
"""
import os, sys, wave
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100

def hp(x, f, o=2): return sosfilt(butter(o, f, "hp", fs=SR, output="sos"), x)
def lp(x, f, o=2): return sosfilt(butter(o, f, "lp", fs=SR, output="sos"), x)
def bp(x, lo, hi, o=2): return sosfilt(butter(o, [lo, hi], "bp", fs=SR, output="sos"), x)

def env(n, tau_ms, attack_ms=0.0):
    e = np.exp(-np.arange(n) / SR / (tau_ms / 1000))
    a = int(attack_ms / 1000 * SR)
    if a > 0: e[:a] *= np.linspace(0, 1, a)
    return e

def fade(x, in_ms=0.5, out_ms=20):
    x = x.copy()
    a, b = int(in_ms / 1000 * SR), int(out_ms / 1000 * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b) ** 2
    return x

def add(buf, x, at, wrap=False):
    """Mix x into buf from sample `at`; with wrap, what runs off the end comes in at the start."""
    if wrap:
        idx = (at + np.arange(len(x))) % len(buf)
        np.add.at(buf, idx, x)
    else:
        m = min(len(x), len(buf) - at)
        if m > 0: buf[at:at + m] += x[:m]

def save(x, path, db=-1.0, normalise=True):
    if normalise: x = x / (np.abs(x).max() + 1e-12) * 10 ** (db / 20)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print("wrote", os.path.basename(path), f"{len(x) / SR:.2f} s, peak {20 * np.log10(np.abs(x).max()):.1f} dBFS")

def air_tail(rng, rt60=0.55, predelay_ms=14):
    """An open ground's tail: a few late echoes off the pavilion and trees, no room. Exponentially
    decaying noise, darker as it goes on (air and leaves eat the top)."""
    m = int(rt60 * 1.2 * SR)
    t = np.arange(m) / SR
    ir = rng.standard_normal(m) * np.exp(-6.9 * t / rt60)
    ir = 0.6 * lp(ir, 5000) + 0.4 * lp(ir, 1800)
    ir[: int(predelay_ms / 1000 * SR)] = 0
    return ir / np.sqrt(np.sum(ir ** 2)) * 0.25

def clap(rng, fc, q, tau_ms):
    """One clap: a sharp crack of noise, coloured by the cupped hands' resonance (fc, q)."""
    n = int(0.06 * SR)
    burst = rng.standard_normal(n) * env(n, tau_ms, rng.uniform(0.2, 0.7))
    body = bp(burst, fc / (1 + 0.5 / q), fc * (1 + 0.5 / q))
    crack = hp(rng.standard_normal(n), 900) * env(n, tau_ms * 0.35, 0.15)
    return body / (np.abs(body).max() + 1e-9) + 0.35 * crack / (np.abs(crack).max() + 1e-9)

def clapper(rng, n, t0, dur, rate, gain, energy=1.0):
    """One spectator: claps at their own rate with a human's wobble, tails off and slows to stop."""
    fc, q, tau = rng.uniform(800, 2500), rng.uniform(1.2, 3.5), rng.uniform(6, 10)
    out = np.zeros(n)
    t = t0
    while t < t0 + dur:
        k = (t - t0) / dur
        a = gain * (1 - 0.75 * max(0.0, k - 0.6) / 0.4) * rng.uniform(0.75, 1.1) * energy
        c = clap(rng, fc * (1 + 0.05 * rng.standard_normal()), q, tau * rng.uniform(0.9, 1.1))
        add(out, a * c, int(t * SR))
        t += (1 / rate) * (1 + 0.045 * rng.standard_normal()) * (1 + 0.25 * max(0.0, k - 0.6))
    # Farther away: quieter and duller (air absorbs the top over 60-90 m).
    return lp(out, 10000 - 5000 * (1 - gain))

def applause(rng, people, length, rates=(3.0, 4.6), energy=1.0):
    n = int(length * SR)
    out = np.zeros(n)
    for _ in range(people):
        t0 = 0.05 + min(rng.lognormal(np.log(0.22), 0.6), 1.0)
        dur = rng.uniform(0.45, 0.95) * (length - 0.5 - t0)
        gain = 10 ** (rng.uniform(-10, 0) / 20)
        out += clapper(rng, n, t0, dur, rng.uniform(*rates), gain, energy)
    return out

def whistle(rng, dur, f_lo, f_hi):
    """A two-finger whistle: a pure tone that swoops up and holds, a little vibrato and breath."""
    n = int(dur * SR)
    u = np.arange(n) / n
    f = f_lo + (f_hi - f_lo) * np.clip(u / 0.3, 0, 1) ** 0.6 * (1 + 0.02 * u) - (f_hi - f_lo) * 0.25 * \
        np.clip((u - 0.85) / 0.15, 0, 1) ** 2
    t = np.arange(n) / SR
    f *= 1 + 0.01 * np.sin(2 * np.pi * 5.5 * t + rng.uniform(0, 6)) + 0.006 * lp(rng.standard_normal(n), 8) * 30
    ph = 2 * np.pi * np.cumsum(f) / SR
    a = np.clip(u * dur / 0.03, 0, 1) * np.clip((1 - u) * dur / 0.07, 0, 1) * \
        (1 + 0.12 * lp(rng.standard_normal(n), 12) * 20)
    breath = lp(rng.standard_normal(n), 400) * 6 * np.sin(ph)
    return lp(a * (np.sin(ph) + 0.06 * np.sin(2 * ph) + 0.12 * breath), 8000)

def cheer(rng, length, whistles):
    out = applause(rng, 15, length, rates=(4.2, 6.2), energy=1.0)
    level = np.sqrt(np.mean(out[int(0.6 * SR): int(2.0 * SR)] ** 2))
    for at, dur, lo, hi in whistles:
        w = whistle(rng, dur, lo, hi)
        add(out, w / np.sqrt(np.mean(w ** 2)) * level * 0.7, int(at * SR))
    return out

def knock(rng, modes, rattle, ms=380):
    """A ball into a thin board: the board's few bending modes ring briefly (wood damps fast), with
    the ball's contact crack on the front and a loose picket ticking against the rail after it."""
    n = int(ms / 1000 * SR)
    t = np.arange(n) / SR
    body = np.zeros(n)
    for f, tau, a in modes:
        f *= 1 + 0.04 * rng.standard_normal()
        body += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6)) * env(n, tau * rng.uniform(0.85, 1.15), 0.3)
    crack = lp(hp(rng.standard_normal(n), 1200), 7000) * env(n, 1.4, 0.05)
    thump = np.sin(2 * np.pi * 135 * t) * env(n, 40, 1.0)
    x = body / np.abs(body).max() + 0.45 * crack / np.abs(crack).max() + 0.35 * thump
    for delay, amp in rattle:
        tick = np.zeros(n)
        m = n - int(delay * SR)
        tt = np.arange(m) / SR
        onset = 1 - np.exp(-tt / 0.0004)          # 0.4 ms: a tick, not a digital click
        for f, tau, a in modes[1:]:
            tick[n - m:] += a * np.sin(2 * np.pi * f * 1.3 * tt) * np.exp(-tt / (tau * 0.4 / 1000)) * onset
        x += amp * tick / (np.abs(tick).max() + 1e-9)
    return fade(x, 0.1, 80)

def one_shots(out, rng):
    tail = air_tail(rng)
    for i, (people, length) in enumerate([(9, 3.4), (12, 3.9), (14, 4.3), (10, 3.1)], 1):
        a = applause(rng, people, length)
        a = a + fftconvolve(a, tail)[: len(a)]
        save(fade(hp(a, 160), 1, 500), f"{out}/club_clap_{i}.wav")
    for i, ws in enumerate([[(0.45, 0.95, 2300, 3100)],
                            [(0.40, 0.28, 2600, 3200), (0.82, 0.34, 2700, 3300)]], 1):
        c = cheer(rng, 4.6, ws)
        c = c + fftconvolve(c, tail)[: len(c)]
        save(fade(hp(c, 160), 1, 700), f"{out}/club_cheer_{i}.wav")
    picket = [(410, 30, 1.0), (860, 20, 0.7), (1350, 14, 0.5), (2100, 9, 0.35), (3150, 6, 0.2)]
    post = [(260, 35, 1.0), (590, 24, 0.6), (980, 15, 0.45), (1600, 9, 0.3), (2500, 5, 0.15)]
    save(knock(rng, picket, [(0.026, 0.28), (0.061, 0.12)]), f"{out}/fence_1.wav")
    save(knock(rng, post, [(0.034, 0.15)]), f"{out}/fence_2.wav")

# ---- The ambience loop ---------------------------------------------------------------------------

LOOP = 30.0

def shaped(rng, n, gain_of_f):
    """Periodic noise with a given spectrum: shaped in the frequency domain, so it loops exactly."""
    f = np.fft.rfftfreq(n, 1 / SR)
    x = np.fft.irfft(np.fft.rfft(rng.standard_normal(n)) * gain_of_f(np.maximum(f, 1.0)), n)
    return x / x.std()

def slow(rng, n, seconds):
    """A periodic, smooth 0..1 wander with features ~`seconds` long (gusts, swells)."""
    m = int(LOOP * 50)
    s = np.fft.irfft(np.fft.rfft(rng.standard_normal(m)) *
                     np.exp(-0.5 * (np.fft.rfftfreq(m, 1 / 50) * seconds * 2.5) ** 2), m)
    s = (s - s.min()) / (s.max() - s.min() + 1e-12)
    return np.interp(np.arange(n) / SR * 50, np.arange(m + 1), np.append(s, s[0]))

def beds(rng, n):
    """Breeze, leaves, a distant road: the bed under the birds."""
    gust = slow(rng, n, 5.0)
    breeze = shaped(rng, n, lambda f: np.exp(-0.5 * (f / 380) ** 2) / np.sqrt(f + 30)) * (0.35 + 0.65 * gust)
    flutter = slow(rng, n, 0.4)
    leaves = shaped(rng, n, lambda f: np.exp(-0.5 * (np.log2(f / 3500) / 0.9) ** 2)) * \
        (0.1 + gust ** 1.6) * (0.65 + 0.35 * flutter)
    road = shaped(rng, n, lambda f: np.exp(-0.5 * (np.log2(f / 110) / 1.1) ** 2))
    t = np.arange(n) / SR
    passing = sum(np.exp(-0.5 * (((t - c + LOOP / 2) % LOOP - LOOP / 2) / 2.6) ** 2) for c in (8.0, 22.5))
    car = shaped(rng, n, lambda f: np.exp(-0.5 * (np.log2(f / 420) / 0.9) ** 2)) * passing
    return 0.05 * breeze + 0.028 * leaves + 0.012 * road + 0.012 * car

def note(rng, dur, f0, kind):
    """One note of a songbird: a pure whistle that sweeps, bends, trills or holds."""
    n = int(dur * SR)
    u = np.arange(n) / max(n - 1, 1)
    span = f0 * rng.uniform(0.15, 0.45)
    f = {"up": f0 - span / 2 + span * u, "down": f0 + span / 2 - span * u,
         "bend": f0 + span * np.sin(np.pi * u) - span / 2,
         "trill": f0 + 0.6 * span * np.sin(2 * np.pi * rng.uniform(22, 34) * u * dur),
         "hold": f0 * (1 + 0.02 * np.sin(2 * np.pi * 9 * u * dur))}[kind]
    ph = 2 * np.pi * np.cumsum(f) / SR
    a = np.sin(np.pi * np.clip(u, 0, 1)) ** 0.7 * (1 - 0.4 * u)
    return a * (np.sin(ph) + 0.08 * np.sin(2 * ph))

def phrase(rng, song):
    """A phrase from this bird's repertoire: its own notes, in its own order, slightly varied."""
    parts = []
    for dur, f0, kind, gap in song:
        parts.append(note(rng, dur * rng.uniform(0.92, 1.08), f0 * rng.uniform(0.97, 1.03), kind))
        parts.append(np.zeros(int(gap * rng.uniform(0.85, 1.2) * SR)))
    return np.concatenate(parts + [np.zeros(int(0.3 * SR))])

def make_song(rng):
    kinds = ["up", "down", "bend", "trill", "hold"]
    return [(rng.uniform(0.05, 0.18), rng.uniform(1900, 4300), kinds[rng.integers(0, 5)], rng.uniform(0.03, 0.09))
            for _ in range(rng.integers(5, 10))]

def birds(rng, n):
    """Two songbirds (one nearer, one across the ground) and sparrows chipping in the hedges."""
    out = np.zeros(n)
    for gain, cut, every, start in ((1.0, 11000, (5.5, 8.5), 1.3), (0.38, 6500, (6.5, 11.0), 4.1)):
        songs = [make_song(rng) for _ in range(3)]
        t = start
        while t < LOOP - 0.5:
            p = lp(phrase(rng, songs[rng.integers(0, 3)]), cut)
            add(out, gain * p, int(t * SR), wrap=True)
            t += rng.uniform(*every)
    t = 0.7
    while t < LOOP - 0.3:
        for k in range(rng.integers(2, 5)):
            d = rng.uniform(0.025, 0.05)
            m = int(d * SR)
            u = np.arange(m) / m
            f = rng.uniform(5200, 6500) - rng.uniform(1500, 2500) * u
            c = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.pi * u) ** 1.5
            g = 0.22 * rng.uniform(0.6, 1.0)
            add(out, g * lp(c, 7000), int((t + k * rng.uniform(0.09, 0.16)) * SR), wrap=True)
        t += rng.uniform(2.0, 5.0)
    return out

VOWELS = [(730, 1090), (530, 1840), (300, 2200), (570, 840), (440, 1020), (660, 1720)]

def talker(rng, dur, f0):
    """A voice across the ground: a glottal buzz through moving vowel formants in syllables. Heard
    only as murmur - distance takes the consonants - so no words are needed."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    pitch = f0 * (1 + 0.1 * (slow(rng, n, 0.5) - 0.5)) * (1.05 - 0.1 * t / dur)
    ph = 2 * np.pi * np.cumsum(pitch) / SR
    src = sum(np.sin(k * ph) / k for k in range(1, 25))
    sylls = np.cumsum(rng.uniform(0.13, 0.28, int(dur / 0.13) + 2))
    which = np.searchsorted(sylls, t)
    pos = (t - np.append(0, sylls)[which]) / (np.append(sylls[0], np.diff(sylls))[which])
    amp = np.sin(np.pi * np.clip(pos, 0, 1)) ** 0.8 * (rng.random(len(sylls) + 1) > 0.15)[which]
    vow = rng.integers(0, len(VOWELS), len(sylls) + 1)[which]
    y = np.zeros(n)
    for v, (f1, f2) in enumerate(VOWELS):
        m = vow == v
        if m.any():
            y += np.where(m, bp(src, f1 * 0.8, f1 * 1.2) + 0.5 * bp(src, f2 * 0.88, f2 * 1.12), 0)
    return fade(lp(y * lp(amp, 30), 1300), 30, 120)

def voices(rng, n):
    out = np.zeros(n)
    for f0, count in ((110, 3), (125, 3), (205, 3), (180, 2)):
        for _ in range(count):
            dur = rng.uniform(1.2, 3.0)
            add(out, rng.uniform(0.5, 1.0) * talker(rng, dur, f0), int(rng.uniform(0, LOOP) * SR), wrap=True)
    return out

def loudness(x):
    """Integrated loudness (LUFS), ITU-R BS.1770-4: K-weighting, 400 ms blocks, gated."""
    def biquad(kind, fc, q, g):
        w0, A = 2 * np.pi * fc / SR, 10 ** (g / 40)
        al, c = np.sin(w0) / (2 * q), np.cos(w0)
        if kind == "shelf":
            b = [A * ((A + 1) + (A - 1) * c + 2 * np.sqrt(A) * al), -2 * A * ((A - 1) + (A + 1) * c),
                 A * ((A + 1) + (A - 1) * c - 2 * np.sqrt(A) * al)]
            a = [(A + 1) - (A - 1) * c + 2 * np.sqrt(A) * al, 2 * ((A - 1) - (A + 1) * c),
                 (A + 1) - (A - 1) * c - 2 * np.sqrt(A) * al]
        else:
            b, a = [1, -2, 1], [1 + al, -2 * c, 1 - al]
        return np.array([b + a]) / a[0]
    k = sosfilt(np.vstack([biquad("shelf", 1500, 1 / np.sqrt(2), 4.0), biquad("hp", 38, 0.5, 0)]), x)
    blk, hop = int(0.4 * SR), int(0.1 * SR)
    z = np.array([np.mean(k[i:i + blk] ** 2) for i in range(0, len(k) - blk, hop)])
    z = z[-0.691 + 10 * np.log10(z + 1e-20) > -70]
    rel = -0.691 + 10 * np.log10(z.mean()) - 10
    z = z[-0.691 + 10 * np.log10(z) > rel]
    return -0.691 + 10 * np.log10(z.mean())

def ambience(path, rng, target=-24.0):
    n = int(LOOP * SR)
    dry_birds, bed, talk = birds(rng, n), beds(rng, n), voices(rng, n)
    talk *= 0.012 / (np.sqrt(np.mean(talk ** 2)) + 1e-12)
    wet = dry_birds * 0.6 + talk * 2.0
    ir = np.zeros(n); tail = air_tail(rng, rt60=0.9, predelay_ms=25); ir[:len(tail)] = tail
    wet = np.fft.irfft(np.fft.rfft(wet) * np.fft.rfft(ir), n)      # circular: the tail wraps round
    x = 0.32 * dry_birds + bed + talk + wet
    x = np.fft.irfft(np.fft.rfft(x) * (1 - np.exp(-0.5 * (np.fft.rfftfreq(n, 1 / SR) / 45) ** 2)), n)  # no rumble
    x *= 10 ** ((target - loudness(np.tile(x, 2))) / 20)
    peak = 20 * np.log10(np.abs(x).max())
    if peak > -1.0:
        raise SystemExit(f"ambience peaks at {peak:.1f} dBFS at {target} LUFS: lower the birds")
    save(x, path, normalise=False)
    print(f"  {loudness(np.tile(x, 2)):.1f} LUFS; birds {loudness(np.tile(0.32 * dry_birds, 2)):.0f}, "
          f"bed {loudness(np.tile(bed, 2)):.0f}, voices {loudness(np.tile(talk, 2)):.0f} (before levelling)")

def main():
    sfx = sys.argv[1] if len(sys.argv) > 1 else "../../Assets/Resources/Sounds/Sfx"
    amb = sys.argv[2] if len(sys.argv) > 2 else "../../Assets/ClubGround/Audio"
    os.makedirs(sfx, exist_ok=True); os.makedirs(amb, exist_ok=True)
    one_shots(sfx, np.random.default_rng(61))
    ambience(f"{amb}/club_ambience.wav", np.random.default_rng(30))

if __name__ == "__main__":
    main()
