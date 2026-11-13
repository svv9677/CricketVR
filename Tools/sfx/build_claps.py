"""One player's clap (TeamTalk / PlayerClap): clap_1..8, single claps, each a different pair of hands.
TeamTalk plays one each time a player's palms meet, so the rhythm is his arms' own. Synthesised with
the club ground's clap (Tools/clubground/build_sounds.py: a noise crack coloured by the cupped hands'
resonance) plus a short open-air tail: ours, no third-party material.

Run from the repo root: python3 Tools/sfx/build_claps.py  (needs numpy and scipy)
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "clubground"))
import build_sounds as club  # noqa: E402

OUT = "Assets/Resources/Sounds/Sfx"


def one_clap(rng):
    fc, q, tau = rng.uniform(900, 2200), rng.uniform(1.4, 3.2), rng.uniform(6, 9)
    c = club.clap(rng, fc, q, tau)
    n = int(0.22 * club.SR)
    out = np.zeros(n)
    club.add(out, c, 0)
    tail = np.convolve(out, club.air_tail(rng, rt60=0.35, predelay_ms=10))[:n]
    return club.fade(club.hp(out + 0.5 * tail, 250), 0.2, 60)


def main():
    rng = np.random.default_rng(20261108)
    for i in range(1, 9):
        club.save(one_clap(rng), f"{OUT}/clap_{i}.wav")


if __name__ == "__main__":
    main()
