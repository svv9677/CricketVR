"""The players' voices (TeamTalk): what fielders shout to their bowler and to each other, and what
the batting side calls from the boundary. Rendered with Kokoro-82M (Apache-2.0 weights and voices,
https://huggingface.co/hexgrad/Kokoro-82M), so the clips are ours to ship.

Every line is rendered in every voice, so a player keeps one voice all match: the clip
"<group>_<voice>_<n>.wav" is the n-th line of <group> in voice <voice> (GameSfx's group is
"<group>_<voice>"). Each is trimmed of silence, high-passed (a shout across a field has no
close-mic boom), levelled to one loudness and peak, and written mono 16-bit at 22.05 kHz.

Run from the repo root with a Python 3.12 that has kokoro, misaki[en], soundfile, numpy, scipy and
the spaCy model en_core_web_sm installed, and espeak-ng on the machine (brew install espeak-ng):
    ESPEAK_DATA_PATH=/opt/homebrew/share/espeak-ng-data <venv>/bin/python Tools/voices/build_voices.py
(without ESPEAK_DATA_PATH the bundled espeak loader looks for its data on its build machine and fails).
Not auditioned by a person yet: listen in the headset and re-word or re-voice a line here if one is off.
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

OUT = "Assets/Resources/Sounds/Voices"
RATE_IN, RATE_OUT = 24000, 22050

# The voices, by index (TeamTalk gives each player one): two British, three American, male.
VOICES = [("b", "bm_george"), ("b", "bm_lewis"), ("a", "am_adam"), ("a", "am_michael"), ("a", "am_eric")]

LINES = {
    # A ball that did its job: a dot, a defensive push, a ball left well alone.
    "goodball": ["Good ball!", "Well bowled!", "That's it, that's the one!", "Lovely, keep it there!",
                 "Great areas, bowler!", "Right there, mate!"],
    # Past the edge.
    "beaten": ["Ooh, so close!", "Unlucky, bowler!", "Just past the edge!", "Beaten! Lovely!",
               "Oh, what a ball!"],
    # Hit for four or six: to the bowler, to lift him.
    "console": ["It's all good, you'll get him next ball.", "Head up, bowler. Next one.",
                "No worries, it's all good.", "Shake it off, come on!", "Good shot, that. Go again.",
                "Stay with it, you'll get him."],
    "wicket": ["Yes! Got him!", "Well bowled, mate!", "Get in there!", "What a ball!", "Yes! Come on!"],
    "catch": ["Great catch!", "Brilliant hands!", "What a catch!", "Safe hands, well done!", "Yes! Great catch!"],
    "stop": ["Good stop!", "Well fielded!", "Nice work!", "Great stop, mate!"],
    # Between balls, now and then: the keeper and the close fielders keeping it lively.
    "chatter": ["Come on, boys!", "Here we go, bowler!", "Let's go, let's go!", "Keep it tight now.",
                "Big one here, come on!", "Stay switched on, boys!"],
    # The batting side, from the boundary, for a good shot.
    "shot": ["Shot!", "Great shot!", "Beautiful!", "Shot, mate!", "Lovely timing!", "Keep going!"],
    "six": ["That's huge!", "Out of here!", "What a hit!", "Massive!"],
}

SPEED = 1.08          # a touch quicker than read speech: called out, not read
TARGET_RMS_DB = -18.0
PEAK_DB = -1.0


def render(pipe, voice, text):
    parts = [np.asarray(audio, dtype=np.float32) for _, _, audio in pipe(text, voice=voice, speed=SPEED)]
    return np.concatenate(parts) if parts else np.zeros(1, np.float32)


def trim(x, rate, floor_db=-45.0, pad=0.03):
    env = np.abs(x)
    above = np.where(env > env.max() * 10 ** (floor_db / 20))[0]
    if len(above) == 0:
        return x
    p = int(pad * rate)
    return x[max(0, above[0] - p): min(len(x), above[-1] + p)]


def finish(x):
    x = sosfilt(butter(2, 140, "highpass", fs=RATE_IN, output="sos"), x)
    x = trim(x, RATE_IN)
    x = resample_poly(x, 147, 160)          # 24000 -> 22050
    rms = np.sqrt(np.mean(x ** 2)) + 1e-9
    x = x * (10 ** (TARGET_RMS_DB / 20) / rms)
    peak = np.abs(x).max() + 1e-9
    limit = 10 ** (PEAK_DB / 20)
    if peak > limit:
        x = np.tanh(x / limit) * limit        # soft-limit the odd loud syllable, not the whole line
    fade = int(0.01 * RATE_OUT)
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade:] *= np.linspace(1, 0, fade)
    return x.astype(np.float32)


def main():
    from kokoro import KPipeline
    os.makedirs(OUT, exist_ok=True)
    pipes = {}
    for v, (lang, voice) in enumerate(VOICES):
        pipe = pipes.setdefault(lang, KPipeline(lang_code=lang, repo_id="hexgrad/Kokoro-82M"))
        for group, lines in LINES.items():
            for n, text in enumerate(lines, 1):
                path = f"{OUT}/{group}_v{v}_{n}.wav"
                sf.write(path, finish(render(pipe, voice, text)), RATE_OUT, subtype="PCM_16")
                print(path, voice, text, flush=True)


if __name__ == "__main__":
    sys.exit(main())
