# Bat-contact sound candidates: sources and licenses

**In this folder (shipped): only the CC0 files** - middled_1-2, good_3-4, edge_1-4, toe_1-3,
handle_1-4. No attribution is needed for any of them. The CC BY files listed below (middled_3-4,
good_1-2) were not copied in; ship them only with the attribution text at the end.

Gathered 2026-09-29. All audio comes from freesound.org, via the public HQ preview MP3s on `cdn.freesound.org/previews/...`. No login was used. Each license below was re-read from the sound's page on 2026-09-29.

**How the files were made.** Each `<category>_<n>.wav` is a single hit cut out of the source recording. The source time is in the "Cut from" column. Every cut was converted to mono, 44.1 kHz, 16-bit PCM WAV, given a 3 ms fade-in and a short fade-out, and peak-normalised to -1 dBFS. The "Gain" column shows how much each file was boosted. A large boost means the original hit was quiet, so its noise floor went up by the same amount. The full source previews are kept in `raw/fs_<id>.mp3` in case you want to re-cut them. Cutting and re-encoding is allowed under both CC0 and CC-BY.

**How the categories were assigned.** I could not listen to anything. Each hit was put in a category using measurements only: peak level, spectral centroid over the first 40 ms, the share of energy below 800 Hz, and how long it takes to decay to 10% of its peak. Before shipping any of these, **audition all of them**. The "What it is" column says what the uploader recorded; the category is my inference from those measurements.

The two licenses in use:
- **CC0 1.0** (public domain): no attribution required. https://creativecommons.org/publicdomain/zero/1.0/
- **CC BY 4.0**: attribution **required** in the credits. https://creativecommons.org/licenses/by/4.0/

## Files

| File | Category | What it is | Duration | Cut from | Gain | Source URL | Author | License | Attribution required |
|---|---|---|---|---|---|---|---|---|---|
| middled_1.wav | middled | **Real cricket**: the loudest, brightest contact in an outdoor nets session (kids batting, Melbourne). Centroid about 4.5 kHz, fast decay, some outdoor ambience. Likely a clean bat strike. | 0.70 s | 4.76 s | -0.8 dB | https://freesound.org/people/OroborosNZ/sounds/141249/ | OroborosNZ | CC0 1.0 | No |
| middled_2.wav | middled | **Real cricket**: the second strong bright strike from the same nets recording. Cut at 0.5 s because the next event starts soon after. | 0.50 s | 24.92 s | +0.8 dB | https://freesound.org/people/OroborosNZ/sounds/141249/ | OroborosNZ | CC0 1.0 | No |
| middled_3.wav | middled (stand-in) | Wooden **baseball** bat hitting a baseball, single clean hit, bright crack. | 0.90 s | 10.77 s | +1.2 dB | https://freesound.org/people/CGEffex/sounds/93136/ | CGEffex | CC BY 4.0 | **Yes** |
| middled_4.wav | middled (stand-in) | Wooden baseball bat hitting a baseball, the brightest of the four hits in the recording. Very clean with low noise. | 0.90 s | 20.82 s | +1.3 dB | https://freesound.org/people/CGEffex/sounds/93136/ | CGEffex | CC BY 4.0 | **Yes** |
| good_1.wav | good (stand-in) | Wooden baseball bat hitting a baseball. More low-mid body and less crack than middled_3 and middled_4. | 0.90 s | 4.86 s | +0.8 dB | https://freesound.org/people/CGEffex/sounds/93136/ | CGEffex | CC BY 4.0 | **Yes** |
| good_2.wav | good (stand-in) | Wooden baseball bat hitting a baseball, the same fuller-bodied kind of hit as good_1. | 0.90 s | 15.68 s | +0.9 dB | https://freesound.org/people/CGEffex/sounds/93136/ | CGEffex | CC BY 4.0 | **Yes** |
| good_3.wav | good (stand-in) | A hard-hit softball, one of three hits. The bat material is not stated. Solid contact. | 0.28 s | 2.28 s | +1.0 dB | https://freesound.org/people/bowlingballout/sounds/860786/ | bowlingballout | CC0 1.0 | No |
| good_4.wav | good (stand-in) | A Finnish baseball (pesäpallo) hit outdoors in Oxford. Loud and bright. The bat material is not stated and pesäpallo bats are often composite, so the timbre may be less woody. | 0.24 s | 1.46 s | -0.8 dB | https://freesound.org/people/Urkki69/sounds/628352/ | Urkki69 | CC0 1.0 | No |
| edge_1.wav | edge | **Real cricket**: an English-willow bat (Kookaburra Kahuna) with a real 135 g Bellingham & Smith match ball bouncing on the face. A light, bright tap with a "ting" (centroid about 4 kHz). Low-velocity, so heavily boosted. | 0.36 s | 12.27 s | +12.1 dB | https://freesound.org/people/GH0STY_XD/sounds/594342/ | GH0STY_XD | CC0 1.0 | No |
| edge_2.wav | edge | **Real cricket**: another bright tap from the same willow bat and ball recording. | 0.38 s | 8.42 s | +14.3 dB | https://freesound.org/people/GH0STY_XD/sounds/594342/ | GH0STY_XD | CC0 1.0 | No |
| edge_3.wav | edge (stand-in) | Wood hitting wood. A sharp, clicky strike with thin body (centroid about 5.6 kHz). | 0.60 s | 18.31 s | +7.1 dB | https://freesound.org/people/worthahep88/sounds/319213/ | worthahep88 | CC0 1.0 | No |
| edge_4.wav | edge (stand-in) | Wood hitting wood. A bright click with a little more body than edge_3. | 0.60 s | 23.66 s | +5.8 dB | https://freesound.org/people/worthahep88/sounds/319213/ | worthahep88 | CC0 1.0 | No |
| toe_1.wav | toe (stand-in) | A softball dropped onto a wooden dugout bench. A dull, low thud (centroid about 700 Hz, 81% of energy below 800 Hz). | 0.50 s | 0.00 s | +4.8 dB | https://freesound.org/people/casemundy/sounds/330523/ | casemundy | CC0 1.0 | No |
| toe_2.wav | toe (stand-in) | A solid tennis ball dropped onto a thinly carpeted wooden floor. A dull, soft thud. | 0.60 s | 0.96 s | +12.9 dB | https://freesound.org/people/SirBagel/sounds/732813/ | SirBagel | CC0 1.0 | No |
| toe_3.wav | toe (stand-in) | A wooden stick whacking a medicine ball. A thuddy impact with low-end body. | 0.80 s | 3.09 s | +10.0 dB | https://freesound.org/people/bmacphail/sounds/569541/ | bmacphail | CC0 1.0 | No |
| handle_1.wav | handle (stand-in) | A single knock on wood. Dead and low (centroid about 560 Hz). A studio clip with a slightly raised noise floor. | 0.20 s | 0.00 s | +0.4 dB | https://freesound.org/people/NoisyRedFox/sounds/742356/ | NoisyRedFox | CC0 1.0 | No |
| handle_2.wav | handle (stand-in) | A knuckle knock on a piece of wood, recorded in a studio. Very short and dead (about 10 ms decay). | 0.32 s | 1.18 s | +12.7 dB | https://freesound.org/people/Elandre01/sounds/594389/ | Elandre01 | CC0 1.0 | No |
| handle_3.wav | handle (stand-in) | A softer knuckle knock from the same wood-knock recording. Muffled. | 0.30 s | 0.17 s | +17.9 dB | https://freesound.org/people/Elandre01/sounds/594389/ | Elandre01 | CC0 1.0 | No |
| handle_4.wav | handle (stand-in) | A wooden stick whacking a medicine ball: a muffled, deadened hit. | 0.60 s | 1.82 s | +11.0 dB | https://freesound.org/people/bmacphail/sounds/569541/ | bmacphail | CC0 1.0 | No |

Durations were measured with `ffprobe -show_entries format=duration`.

## Attribution text (required if any CC BY file ships)

Only middled_3, middled_4, good_1 and good_2 need attribution. They all come from one source.

> "Hitting baseball w. wooden bat.wav" by CGEffex (https://freesound.org/people/CGEffex/sounds/93136/), licensed under CC BY 4.0 (https://creativecommons.org/licenses/by/4.0/). Trimmed, converted to mono and normalised.

If you want an all-CC0 set, drop those four files. That leaves middled with middled_1 and middled_2 (the real cricket hits) and good with good_3 and good_4.

## Considered and rejected

| Source | Why it was rejected |
|---|---|
| freesound 366780 "CricketBall Hitting Bat" (DanielRousseau) | CC BY-NC 3.0, which does not allow commercial use |
| freesound 655759 "Cricket Ball Tapping" (Elsje_) | CC BY-NC 4.0 |
| freesound 835231 "Cricket match at Keswick" | CC BY-NC 4.0, and it is ambience |
| freesound 345692 "MICHAEL_HUSSEY_SIX1" (lithium11) | CC0, but the preview file is broken (1 KB, reported duration 0.001 s) |
| freesound 521433 "Test Match Perc Loop 1" (DozyPilchard) | CC0, but it is a sequenced music loop with identical repeats, not a natural hit |
| freesound 816772 and 813396 (Luisa_Sanchez) | CC0, but recorded with a piezo mic: no energy below 800 Hz, which sounds unnatural |
| freesound 731821 "Sound of Hitting Bat" (softball) | CC0, but very bright (4-5 kHz) with a ringing tail, which suggests an aluminium bat |
| freesound 851452, 684607, 252044 | Aluminium or metal bats |
| freesound 266639 to 266642 (Eelke, baseball-hit-01 to 04) | CC BY, and processed with reverb and layered mixes rather than raw recordings |
| freesound 223608 and 223609 (CTCollab) | Layered with chest thumps and tree kicks, so not a clean recording |
| freesound 214989 | Includes a crowd cheer |
| **pixabay.com** | Search pages and sound pages return **HTTP 403** to curl (bot protection), and no CDN audio URL could be extracted, so it was **skipped**. Relevant items exist there under the Pixabay Content License but were not downloaded: "Plastic Bat Hits Ball - Kids Playing Cricket" (plastic bat) and "Cricket ball hitting bet and crowd people" (includes crowd). |

## Unused extra CC0 previews in raw/

These were downloaded and kept in case they are useful:

- 725672: racquetball hitting a wood surface, 3 hits
- 560258: a small baseball bat tapped on a wooden floor
- 266595: an old board hitting a baseball (CC BY 3.0)
