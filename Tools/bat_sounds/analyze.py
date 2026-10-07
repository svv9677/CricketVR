import subprocess, sys, json
import numpy as np
from scipy.signal import stft, find_peaks, butter, sosfilt

SR = 44100

def load(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy()

def onsets(x, min_gap=0.35, thresh_db=-34):
    hp = sosfilt(butter(4, 600, "hp", fs=SR, output="sos"), x)
    win = int(0.004 * SR)
    env = np.sqrt(np.convolve(hp ** 2, np.ones(win) / win, "same"))
    envdb = 20 * np.log10(env + 1e-9)
    peak = envdb.max()
    idx, _ = find_peaks(envdb, height=peak + thresh_db, distance=int(min_gap * SR))
    out = []
    for i in idx:
        # walk back to the foot of the rise
        j = i
        while j > 0 and envdb[j - 1] < envdb[j] and envdb[j] - envdb[max(j - 200, 0)] > 3:
            j -= 1
        out.append((max(j - int(0.004 * SR), 0), i))
    return out

def features(seg):
    """seg starts at the onset."""
    n = len(seg)
    a = np.abs(seg)
    pk = int(np.argmax(a))
    peak = a[pk]
    env = np.sqrt(np.convolve(seg ** 2, np.ones(int(0.002 * SR)) / int(0.002 * SR), "same"))
    envdb = 20 * np.log10(env / (env.max() + 1e-12) + 1e-9)
    def decay_to(db):
        after = np.where(envdb[pk:] < db)[0]
        return (after[0] / SR * 1000) if len(after) else n / SR * 1000
    f, t, Z = stft(seg[: int(0.25 * SR)], SR, nperseg=1024, noverlap=768)
    P = np.abs(Z) ** 2
    def centroid(tmax_ms, tmin_ms=0):
        sel = (t * 1000 >= tmin_ms) & (t * 1000 < tmax_ms)
        if not sel.any(): return float("nan")
        spec = P[:, sel].sum(1)
        return float((f * spec).sum() / (spec.sum() + 1e-12))
    # spectral flatness over 0.4-10 kHz in the first 60 ms
    sel = t * 1000 < 60
    spec = P[:, sel].sum(1)
    band = (f > 400) & (f < 10000)
    sb = spec[band] + 1e-12
    flat = float(np.exp(np.mean(np.log(sb))) / np.mean(sb))
    # ring: strongest narrow tonal component 80-300 ms in, in dB above the local median spectrum
    ts = (t * 1000 >= 60) & (t * 1000 < 250)
    tail = P[:, ts].sum(1) if ts.any() else np.zeros_like(f)
    tb = tail[(f > 500) & (f < 8000)]
    ring_db = float(10 * np.log10((tb.max() + 1e-12) / (np.median(tb) + 1e-12)))
    ring_f = float(f[(f > 500) & (f < 8000)][np.argmax(tb)])
    low_share = float(spec[f < 800].sum() / (spec.sum() + 1e-12))
    return dict(peak=float(20 * np.log10(peak + 1e-9)), attack_ms=pk / SR * 1000, c_first40=centroid(40), c_40_150=centroid(150, 40),
                flat=flat, low800=low_share, d20=decay_to(-20), d40=decay_to(-40), ring_db=ring_db, ring_hz=ring_f)

if __name__ == "__main__":
    for path in sys.argv[1:]:
        x = load(path)
        print(f"== {path}: {len(x)/SR:.1f}s")
        for k, (s, p) in enumerate(onsets(x)):
            seg = x[s: s + int(0.8 * SR)]
            r = features(seg)
            print(f"  #{k:02d} t={s/SR:6.2f}s peak={r['peak']:6.1f}dB att={r['attack_ms']:5.1f}ms cent0-40={r['c_first40']:6.0f} cent40-150={r['c_40_150']:6.0f} "
                  f"flat={r['flat']:.3f} low800={r['low800']:.2f} d20={r['d20']:6.0f}ms d40={r['d40']:6.0f}ms ring={r['ring_db']:4.1f}dB@{r['ring_hz']:5.0f}")
