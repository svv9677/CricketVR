"""Builds the game's match sounds (everything but the bat) into Assets/Resources/Sounds/Sfx/.

Sources: CC0 recordings on freesound.org (see Assets/Resources/Sounds/Sfx/SOURCES.md for each, with
its URL), fetched as the public HQ previews next to this script, plus a little synthesis where no
usable recording exists (the heel of a footstep, the ball's whoosh, the board's knock).
Needs numpy, scipy, ffmpeg and ../bat_sounds/analyze.py. Run from this folder:
    python3 build_sfx.py <output folder>

What it writes (every clip mono, 44.1 kHz, peak -1 dBFS unless said):
  step_grass_{1..6}   a footstep on the outfield: a real grass rustle over a soft heel thud
  step_pitch_{1..4}   on the hard pitch: a sharper, drier knock, little rustle
  stride_{1..2}       the delivery stride landing: a heavy pitch step
  grunt_{1..6}        a quick bowler's effort at the release (exertion grunts)
  throw_{1..4}        a fielder's short grunt on a hard throw
  breath_{1..2}       a spinner's release: a short breath (the grunts, softened and filtered)
  glove_{1..3}        the ball into the keeper's gloves
  hands_{1..3}        into a fielder's hands (the glove takes, higher, lighter)
  bounce_pitch_{1..4} the ball pitching (real cricket-ball impacts from the nets recording)
  bounce_grass_{1..4} bouncing on the outfield (duller, lower)
  dive_{1..2}         a body landing on grass
  bails_{1..3}        bails clattering off and on to the ground
  boards_1            the ball into the advertising boards
  whoosh_{1..2}       a quick ball past the head
  crowd_four_{1..2}, crowd_six_{1..2}, crowd_wicket_{1..2}, crowd_ooh_1, crowd_groan_1, crowd_sigh_1
"""
import sys, wave
import numpy as np
from scipy.signal import butter, sosfilt
sys.path.insert(0, "../bat_sounds")
from analyze import load, SR

rng = np.random.default_rng(7)

def hp(x, f, o=4): return sosfilt(butter(o, f, "hp", fs=SR, output="sos"), x)
def lp(x, f, o=4): return sosfilt(butter(o, f, "lp", fs=SR, output="sos"), x)
def bp(x, lo, hi, o=3): return sosfilt(butter(o, [lo, hi], "bp", fs=SR, output="sos"), x)

def cut(x, t, ms, pre=0.004):
    s = max(int((t - pre) * SR), 0)
    return x[s: s + int(ms / 1000 * SR)].copy()

def env(n, tau_ms, attack_ms=0.0):
    t = np.arange(n) / SR
    e = np.exp(-t / (tau_ms / 1000))
    if attack_ms > 0:
        a = int(attack_ms / 1000 * SR)
        e[:a] *= np.linspace(0, 1, a)
    return e

def fade(x, in_ms=0.5, out_ms=20):
    x = x.copy()
    a, b = int(in_ms / 1000 * SR), int(out_ms / 1000 * SR)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x

def norm(x, db=-1.0):
    return x / (np.abs(x).max() + 1e-12) * 10 ** (db / 20)

def gate(x, keep_ms, tau_ms):
    k = int(keep_ms / 1000 * SR)
    e = np.ones(len(x)); e[k:] = env(len(x) - k, tau_ms)
    return x * e

def shift(x, rate):
    n = int(len(x) / rate)
    return np.interp(np.arange(n) * rate, np.arange(len(x)), x)

def mix(n, *parts):
    out = np.zeros(n)
    for p, g in parts:
        m = min(n, len(p)); out[:m] += p[:m] * g
    return out

def save(x, path, db=-1.0):
    x = norm(x, db)
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print("wrote", path.split("/")[-1], f"{len(x) / SR * 1000:.0f} ms")

def thud(ms, f0, tau_ms):
    """A soft low impact: a damped low sine with a click of noise on its front."""
    n = int(ms / 1000 * SR); t = np.arange(n) / SR
    body = np.sin(2 * np.pi * f0 * t * (1 - 0.25 * t / (ms / 1000))) * env(n, tau_ms, 1.5)
    click = lp(rng.standard_normal(n), 2500) * env(n, 4)
    return body + 0.25 * click

def main(out):
    run = load("run_grass.mp3"); walk = load("walk_grass.mp3")
    effort = load("grunts_effort.mp3"); fight = load("grunts_fight.mp3")
    g1, g2 = load("glove1.mp3"), load("glove2.mp3")
    oro = load("oroboros.mp3"); tennis = load("tennis_grass.mp3"); wood = load("woodfoley.mp3")
    cheer1, cheer2, roar = load("cheer1.mp3"), load("cheer2.mp3"), load("roar.mp3")
    excited, groan, sigh = load("excited.mp3"), load("groan.mp3"), load("sigh.mp3")

    # Footsteps on grass: the rustle of real steps (de-noised by gating, high-passed) over a heel.
    rustles = [cut(run, t, 160) for t in (1.19, 1.60, 2.00)] + [cut(walk, t, 160) for t in (0.98, 1.33, 2.17)]
    for i, r in enumerate(rustles, 1):
        r = gate(hp(r, 700), 25, 45)
        heel = thud(140, 70 + 12 * i, 28)
        save(fade(mix(int(0.16 * SR), (norm(heel), 0.9), (norm(r), 0.55)), 0.5, 30), f"{out}/step_grass_{i}.wav")
    # On the pitch: hard, dry - the heel sharper and higher, a pinch of grit, little grass.
    for i in range(1, 5):
        heel = thud(110, 110 + 15 * i, 16)
        grit = bp(rng.standard_normal(int(0.11 * SR)), 1200, 5000) * env(int(0.11 * SR), 9)
        save(fade(mix(int(0.11 * SR), (norm(heel), 1.0), (norm(grit), 0.35)), 0.3, 20), f"{out}/step_pitch_{i}.wav")
    for i in range(1, 3):
        heel = thud(200, 55 + 10 * i, 45)
        grit = bp(rng.standard_normal(int(0.2 * SR)), 900, 4000) * env(int(0.2 * SR), 14)
        save(fade(mix(int(0.2 * SR), (norm(heel), 1.0), (norm(grit), 0.4)), 0.3, 40), f"{out}/stride_{i}.wav")

    # The quick's effort at the release, and a fielder's on a throw.
    for i, t in enumerate((0.21, 1.78, 3.10, 5.70, 8.82, 10.25), 1):
        save(fade(gate(hp(cut(effort, t, 420), 120), 180, 90), 2, 80), f"{out}/grunt_{i}.wav")
    for i, t in enumerate((0.58, 5.40, 9.14, 13.79), 1):
        save(fade(gate(hp(cut(fight, t, 300), 120), 110, 60), 2, 60), f"{out}/throw_{i}.wav")
    for i, t in enumerate((1.78, 8.82), 1):
        b = lp(hp(cut(effort, t, 380), 300), 3000)
        save(fade(gate(b, 120, 70), 25, 90), f"{out}/breath_{i}.wav", db=-8)

    # Gloves and hands.
    keeper = [cut(g1, 0.23, 140), cut(g2, 1.09, 160)]
    for i, (a, b) in enumerate([(keeper[0], keeper[1]), (keeper[1], keeper[0]), (keeper[0], keeper[0])], 1):
        save(fade(mix(int(0.16 * SR), (norm(a), 1.0), (norm(lp(b, 900)), 0.6)), 0.3, 40), f"{out}/glove_{i}.wav")
    for i, rate in enumerate((1.15, 1.25, 1.35), 1):
        save(fade(gate(shift(hp(keeper[0], 400), rate), 20, 18), 0.3, 25), f"{out}/hands_{i}.wav", db=-4)

    # The ball pitching: real ones, from the nets.
    for i, t in enumerate((10.34, 39.07, 39.51, 0.28), 1):
        save(fade(gate(hp(cut(oro, t, 120), 90), 25, 20), 0.3, 25), f"{out}/bounce_pitch_{i}.wav")
    # On the grass: a ball on turf, lower (a cricket ball is three times a tennis ball's mass).
    for i, t in enumerate((2.08, 5.00, 8.87, 13.53), 1):
        save(fade(gate(lp(shift(cut(tennis, t, 140), 0.8), 2500), 20, 22), 0.3, 25), f"{out}/bounce_grass_{i}.wav", db=-3)

    for i in range(1, 3):
        body = thud(320, 48 + 8 * i, 70)
        r = gate(hp(cut(run, 1.19 + 0.4 * i, 300), 500), 80, 90)
        save(fade(mix(int(0.32 * SR), (norm(body), 1.0), (norm(r), 0.7)), 0.5, 60), f"{out}/dive_{i}.wav")

    # Bails: two or three wooden ticks a few tens of ms apart.
    ticks = [cut(wood, t, 40) for t in (3.03, 5.29, 5.86, 7.39)]
    for i in range(1, 4):
        n = int(0.32 * SR); x = np.zeros(n)
        for k, at in enumerate(sorted(rng.uniform(0, 0.26, 3))):
            tk = norm(gate(hp(ticks[(i + k) % 4], 800), 6, 8)) * (1.0 - 0.25 * k)
            s = int(at * SR); x[s: s + len(tk)] += tk[: n - s]
        save(fade(x, 0.3, 30), f"{out}/bails_{i}.wav", db=-3)

    # The boards: a hollow knock - a low resonant body under the ball's thud.
    n = int(0.35 * SR); t = np.arange(n) / SR
    hollow = (np.sin(2 * np.pi * 180 * t) + 0.5 * np.sin(2 * np.pi * 410 * t)) * env(n, 60, 1)
    save(fade(mix(n, (norm(hollow), 0.8), (norm(gate(cut(oro, 10.34, 120), 20, 20)), 0.7)), 0.3, 60), f"{out}/boards_1.wav")

    # A quick ball past the head: filtered noise swelling and falling as it passes, the pitch
    # dropping (Doppler).
    for i, (ms, lo, hi) in enumerate([(260, 700, 5000), (200, 900, 6500)], 1):
        n = int(ms / 1000 * SR); t = np.arange(n) / (n - 1)
        noise = rng.standard_normal(n)
        swell = np.exp(-((t - 0.45) / 0.18) ** 2)
        a = bp(noise, lo, hi) * swell
        b = bp(noise, lo * 0.55, hi * 0.55) * np.exp(-((t - 0.65) / 0.2) ** 2)
        save(fade(a + 0.8 * b, 2, 30), f"{out}/whoosh_{i}.wav", db=-3)

    # The crowd's reactions, over Rao's looping crowd (CrowdController).
    save(fade(cut(cheer1, 1.0, 5000), 60, 1500), f"{out}/crowd_four_1.wav")
    save(fade(cut(cheer2, 1.5, 5000), 60, 1500), f"{out}/crowd_four_2.wav")
    save(fade(cut(roar, 0.5, 5500), 40, 1800), f"{out}/crowd_six_1.wav")
    save(fade(cut(excited, 0.4, 6500), 30, 1800), f"{out}/crowd_six_2.wav")
    save(fade(cut(excited, 7.2, 6000), 30, 1800), f"{out}/crowd_wicket_1.wav")
    save(fade(cut(excited, 22.3, 6000), 30, 1800), f"{out}/crowd_wicket_2.wav")
    save(fade(cut(groan, 0.3, 1500), 30, 500), f"{out}/crowd_ooh_1.wav")
    save(fade(cut(groan, 0.0, 3000), 30, 900), f"{out}/crowd_groan_1.wav")
    save(fade(cut(sigh, 1.0, 4000), 80, 1500), f"{out}/crowd_sigh_1.wav")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
