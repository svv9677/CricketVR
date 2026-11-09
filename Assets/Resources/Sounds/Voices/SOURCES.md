# Players' voices: where they come from

Built by `Tools/voices/build_voices.py` (2026-11-08); played by `TeamTalk` through `GameSfx`.

Every clip is spoken by **Kokoro-82M** (https://huggingface.co/hexgrad/Kokoro-82M), a text-to-speech
model whose weights and voices are released under **Apache-2.0**: the audio it makes is ours to ship,
commercially, with no attribution needed in the game. No recording of a real person is used.

| Voice index | Kokoro voice |
|---|---|
| v0 | bm_george (British male) |
| v1 | bm_lewis (British male) |
| v2 | am_adam (American male) |
| v3 | am_michael (American male) |
| v4 | am_eric (American male) |

`<group>_v<voice>_<n>.wav` is line n of a group in that voice; the lines are in `build_voices.py`
(LINES). Each is trimmed, high-passed at 140 Hz, levelled to -18 dBFS RMS with a -1 dBFS soft peak,
mono 16-bit 22.05 kHz; imported Vorbis, compressed in memory.

**Not auditioned by a person yet**: listen in the headset and change a line's wording or voice in
`build_voices.py` if it sounds off.
