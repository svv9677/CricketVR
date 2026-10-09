# Bat contact sounds: three clips, and where they come from

Exactly three sounds are used (BatSounds.cs): `edge.wav`, `good.wav`, `middled.wav`.

| Clip | Wanted | What it is | Length |
|---|---|---|---|
| edge.wav | sharp, quick | A real cricket strike from a nets session, high-passed at 1.8 kHz so only the bright click is left, with a little 3-7 kHz presence. | 55 ms |
| good.wav | dense, flat | A real cricket strike (broader one) with its decay gated so only the hit is left, plus the willow bat's low-mid body (250-1400 Hz) with the bat's 600 Hz note taken out, then compressed and soft-clipped so it is thick and has no ring. | 140 ms |
| middled.wav | crisp, resounding | A bright real cricket crack on top, the willow bat's body under it, and the bat's own resonance (its ~600 Hz mode and the upper modes measured in the willow-bat recording) ringing on, all inside a hall: 14 ms pre-delay, early reflections, then an 8-line feedback delay network (low end rings ~1.3 s, highs ~0.45 s). The dry crack is untouched. Resonances are spaced apart so they do not beat like a tremolo. | 1860 ms |

## Source recordings (both CC0 1.0: no attribution needed)

- "Cricket nets" by OroborosNZ, https://freesound.org/people/OroborosNZ/sounds/141249/ - real bats on real balls, outdoors (Melbourne). Hits used: 24.92 s (edge), 13.23 s (good), 31.14 s (middled crack). The other loud events in that recording are shouting, not bat hits.
- "Cricket ball bouncing on a bat" by GH0STY_XD, https://freesound.org/people/GH0STY_XD/sounds/594342/ - an English-willow bat (Kookaburra Kahuna) with a real 135 g match ball. Used for the wood's body and ring (hit at 22.31 s, cut before the next tap in the recording).

Both were fetched as the public HQ preview MP3s on cdn.freesound.org (no login).

## How they were made

`Tools/bat_sounds/build_sounds.py` rebuilds them from the two previews (`analyze.py` finds the hits). Nobody has listened to them: they were chosen and checked by measurement and spectrogram - onset sharpness, brightness, density (spectral flatness), length, and whether there is a ring - against the words above. **Audition them in the headset** and adjust the numbers in `build_sounds.py` if one is off.

This replaces an earlier set of 17 recordings (several of them baseball, softball and dropped-ball stand-ins, two with a second bounce inside the clip) and the older synthesised set.
