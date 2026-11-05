"""Builds the crowd's individual voices into Assets/Resources/Sounds/Crowd/ (2026-11-05: "each person
should cheer individually, not just playing a combined sound"). CrowdVoices plays them from many
places round the stands, each at its own moment and pitch, so a boundary is heard as a crowd of
people, not one recording.

Sources: CC0 recordings on freesound.org (Assets/Resources/Sounds/Crowd/SOURCES.md), the HQ previews
next to this script. Needs numpy, scipy, ffmpeg and ../bat_sounds/analyze.py.
    python3 build_crowd_voices.py <output folder>

  voice_N    one person: "woohoo", "yeah!", "yay", a whoop, an exclamation (0.4-1.6 s)
  clap_N     one person clapping (~2.5 s)
  whistle_N  a whistle
Each is levelled by loudness (RMS), not peak, so no one voice jumps out of the crowd.
"""
import sys, wave
import numpy as np
from scipy.signal import butter, sosfilt
sys.path.insert(0, "../bat_sounds")
from analyze import load, SR

def hp(x, f, o=4): return sosfilt(butter(o, f, "hp", fs=SR, output="sos"), x)

def cut(x, t, dur):
    s = max(int(t * SR), 0)
    return x[s: s + int(dur * SR)].copy()

def fade(x, in_ms=8, out_ms=120):
    x = x.copy()
    a, b = int(in_ms / 1000 * SR), min(int(out_ms / 1000 * SR), len(x) // 2)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x

def save(x, path, rms_db=-20.0):
    r = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x * (10 ** (rms_db / 20) / r)
    peak = np.abs(x).max()
    if peak > 0.95: x = x * (0.95 / peak)              # never clip: quieter instead
    pcm = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    print("wrote", path.split("/")[-1], f"{len(x) / SR:.2f} s")

# (file, start s, length s): one utterance each, found by build-time segmentation.
VOICES = [
    ("woohoo", 0.08, 0.5), ("yahoo", 0.14, 0.85), ("yeah3", 0.10, 0.5), ("yeah1", 0.04, 0.95),
    ("yeah2", 0.0, 1.2), ("yay3", 0.0, 0.62), ("yay3", 1.44, 0.8), ("yay3", 3.08, 0.85),
    ("yay_yahoo", 4.44, 1.6), ("yay_yahoo", 10.72, 1.1), ("yay_yahoo", 15.9, 1.0),
    ("golf_fan", 0.16, 1.5), ("cowboys", 0.0, 0.7), ("cowboys", 0.96, 1.4),
    ("women", 0.38, 1.5), ("women", 2.1, 1.5), ("cheers_yays", 0.14, 1.4), ("cheers_yays", 2.4, 1.4),
    ("cheers_yays", 4.6, 1.4), ("yay_dr", 0.02, 1.5),
]
CLAPS = [("clapping", 0.2, 2.6), ("clapping", 2.9, 2.6), ("claps01", 0.5, 2.6), ("claps01", 4.0, 2.6),
         ("claps01", 7.5, 2.6), ("soloclap", 0.1, 2.4)]
WHISTLES = [("whistle1", 0.0, 0.95), ("whistles", 0.5, 1.4), ("whistles", 13.46, 1.6)]

def main(out):
    cache = {}
    def src(n):
        if n not in cache: cache[n] = load(n + ".mp3")
        return cache[n]
    for i, (n, t, d) in enumerate(VOICES, 1):
        save(fade(hp(cut(src(n), t, d), 150), 6, 140), f"{out}/voice_{i}.wav", -20)
    for i, (n, t, d) in enumerate(CLAPS, 1):
        save(fade(hp(cut(src(n), t, d), 250), 20, 400), f"{out}/clap_{i}.wav", -24)
    for i, (n, t, d) in enumerate(WHISTLES, 1):
        save(fade(hp(cut(src(n), t, d), 600), 10, 150), f"{out}/whistle_{i}.wav", -24)

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
