# Bat contact sounds: three clips, and where they come from

## Current set (2026-11-05): four takes of each kind

BatSounds plays `middled_1..4`, `good_1..4` and `edge_1..4` (`*Takes` on the Bat prefab), picked at random and never the same take twice running. They come from `Tools/bat_sounds/build_variants.py`, which uses every clean strike in the nets recording (4.76, 13.23, 24.92, 31.14 s) and four hard taps of the close-miked willow (22.68, 12.27, 8.42, 17.88 s). They are much less processed than the set below: no pitch shift, no synthesised ring, no reverb.

| Kind | What it is | Length |
|---|---|---|
| middled_N | The real nets crack (gated at 30 ms: 4.76 has the ball thudding into the net 80 ms on), over the willow's own body and ~600 Hz ring at its real pitch, ringing out over ~350 ms. One faint echo off the stands (180-225 ms, -24 dB), because an open ground has no room. | 0.60-0.65 s |
| good_N | The same cracks, gated tighter, with the willow's 600/730 Hz ring notched out: a solid, dead knock. | 120 ms |
| edge_N | The crack's top only (high-passed at 2 kHz), plus a faint high wood tick. | 60 ms |

Every take is soft-limited to the old clip's loudness for its kind (RMS of the first 40 ms: middled -12, good -14.5, edge -17 dBFS), peaking at -1 dBFS, so a pick never jumps in level. **Still not auditioned by a person**: chosen by measurement (envelope, brightness, flatness, ring, second-hit check). Tune the numbers in `build_variants.py` if one is off.

Other cricket-bat recordings on Freesound were checked on 2026-11-05: the rest are CC BY-NC (DanielRousseau 366780, Robinhood76 76951, Sound-Art-UK 835231, Elsje_ 655759), so they can't go in a game that might be sold; one CC0 match clip (lithium11 345692) has been taken down.

## The older set: the single clips, still the fallback

Exactly three sounds were used (BatSounds.cs): `edge.wav`, `good.wav`, `middled.wav`. They stay as the fallback when a `*Takes` list is empty.

| Clip | Wanted | What it is | Length |
|---|---|---|---|
| edge.wav | sharp, quick | A real cricket strike from a nets session, high-passed at 1.8 kHz so only the bright click is left, with a little 3-7 kHz presence. | 55 ms |
| good.wav | dense, flat | A real cricket strike (broader one) with its decay gated so only the hit is left, plus the willow bat's low-mid body (250-1400 Hz) with the bat's 600 Hz note taken out, then compressed and soft-clipped so it is thick and has no ring. | 140 ms |
| middled.wav | deep, crisp, resounding | A real cricket crack and the willow bat's body, both played back slower (about 4-5 semitones down), a low thump under them, and a ring near 330 Hz (the bat's measured ~600 Hz mode brought down about an octave), in a small room: 14 ms pre-delay, early reflections, then an 8-line feedback delay network (lows ring ~0.8 s, highs ~0.25 s, reverb at -15 dB of the dry hit). The dry crack is untouched. Resonances are spaced apart so they do not beat like a tremolo. | 1460 ms |

## Source recordings (both CC0 1.0: no attribution needed)

- "Cricket nets" by OroborosNZ, https://freesound.org/people/OroborosNZ/sounds/141249/ - real bats on real balls, outdoors (Melbourne). Hits used: 24.92 s (edge), 13.23 s (good), 31.14 s (middled crack). The other loud events in that recording are shouting, not bat hits.
- "Cricket ball bouncing on a bat" by GH0STY_XD, https://freesound.org/people/GH0STY_XD/sounds/594342/ - an English-willow bat (Kookaburra Kahuna) with a real 135 g match ball. Used for the wood's body and ring (hit at 22.31 s, cut before the next tap in the recording).

Both were fetched as the public HQ preview MP3s on cdn.freesound.org (no login).

## How they were made

`Tools/bat_sounds/build_sounds.py` rebuilds them from the two previews (`analyze.py` finds the hits). Nobody has listened to them: they were chosen and checked by measurement and spectrogram - onset sharpness, brightness, density (spectral flatness), length, and whether there is a ring - against the words above. **Audition them in the headset** and adjust the numbers in `build_sounds.py` if one is off.

This replaces an earlier set of 17 recordings (several of them baseball, softball and dropped-ball stand-ins, two with a second bounce inside the clip) and the older synthesised set.
