"""
Generate the stadium crowd atlas.

The atlas is a grid of PEOPLE_PER_REPEAT columns x ATLAS_ROWS rows. CrowdStandBuilder maps one
atlas row onto one stepped row of the stand, and repeats the atlas every MetresPerAtlasRepeat
metres around the bowl, so one cell is exactly one spectator.

Design notes, from what the previous atlas got wrong:
  * a spectator must FILL its cell. The old one drew a small figure in the bottom 60% and left
    noisy brown seat above it, so at any distance the crowd averaged to brown mud.
  * neighbours must touch. Real crowds are packed; visible background between every person is
    what turned the stand into vertical stripes.
  * the background must be flat and dark, not noisy. Per-pixel noise below ~2 screen pixels is
    just mud - contrast between head, shirt and shadow is what reads at distance.
  * the shader is opaque by design (tile-GPU budget), so gaps are PAINTED dark rather than
    left transparent.

Run from the repo root:  python3 Docs/tools/make_crowd_atlas.py
"""
import random
from PIL import Image, ImageDraw, ImageFilter

PEOPLE_PER_REPEAT = 50
ATLAS_ROWS        = 14
CELL_W, CELL_H    = 41, 73          # -> 2050 x 1022, rounded to POT below
OUT_W, OUT_H      = 2048, 1024
SS                = 3               # supersample factor
SEED              = 20260924

DST = 'Assets/Resources/Textures/CrowdProcedural.png'

# Seat / shadow behind and between people. Kept dark and desaturated so heads and shirts pop.
BACKDROP = (54, 47, 43)

SKIN = [(196,152,112), (170,124, 86), (146,102, 68), (120, 82, 54),
        (208,168,132), (152,110, 76), (98, 66, 44),  (182,138, 98)]

# An Indian cricket crowd: dominated by India blue and white, with saffron next and everything
# else sparse. Weighting matters more than variety - an evenly spread rainbow reads as confetti.
SHIRT = ([(34, 74,140)] * 9 + [(28, 96,158)] * 6 +          # India blue
         [(214,214,210)] * 7 + [(196,196,190)] * 5 +        # white / off-white
         [(198,112, 46)] * 4 + [(206,144, 58)] * 3 +        # orange / saffron
         [(150, 48, 52)] * 3 + [(116, 38, 42)] * 2 +        # red
         [(46,110, 78)] * 2 + [(64,124, 96)] * 2 +          # green
         [(186,170, 86)] * 1 + [(160, 92,120)] * 1 +        # yellow / pink, rare
         [(58, 56, 62)] * 3 + [(84, 76,118)] * 1)           # dark / purple

# Density. A real stand is patchy: some blocks packed, others half empty, with aisles and
# stragglers. A single global "empty seat" probability gives an even sprinkle, which still reads
# as a uniform mass - the variation has to be between rows and in the RUN LENGTH of the gaps.
ROW_FILL_RANGE = (0.78, 0.96)   # per-row draw rate; re-rolled for every atlas row.
                                # Note this is NOT final occupancy - a failed roll skips a
                                # RUN of seats, so 0.78..0.96 lands around 64-92% full.
GAP_RUN_MAX    = 3              # an empty stretch is 1..N seats wide, not always exactly one
WIDTH_RANGE    = (0.82, 1.08)   # person width as a fraction of the cell. Neighbours mostly
                                # touch; the gaps come from the empty RUNS, not from everyone
                                # being drawn thin, which just looks like a sparse grid.
X_JITTER       = 0.18           # sideways wander, as a fraction of the cell
SHADE_RANGE    = (0.74, 1.0)    # crowds sit under a roof - vary how lit each person is

rng = random.Random(SEED)
W, H = OUT_W * SS, OUT_H * SS
cw, ch = W / PEOPLE_PER_REPEAT, H / ATLAS_ROWS

img = Image.new('RGB', (W, H), BACKDROP)
d = ImageDraw.Draw(img)


def shade(c, f):
    return tuple(max(0, min(255, int(round(v * f)))) for v in c)


def person(cx, top, w, h, skin, shirt, arms_up):
    """One spectator, centred on cx, occupying (top .. top+h)."""
    head_r = w * 0.19
    head_cy = top + head_r * 1.25
    shoulder_y = head_cy + head_r * 1.45
    half = w * 0.46

    # torso: shoulders out to a slightly wider seated base
    d.polygon([(cx - half * 0.80, shoulder_y),
               (cx + half * 0.80, shoulder_y),
               (cx + half,        top + h),
               (cx - half,        top + h)], fill=shirt)
    # rounded shoulder caps
    d.ellipse([cx - half * 0.80 - w * 0.03, shoulder_y - h * 0.10,
               cx + half * 0.80 + w * 0.03, shoulder_y + h * 0.16], fill=shirt)
    # a little shading down the body so it is not a flat slab
    d.polygon([(cx - half, top + h * 0.72), (cx + half, top + h * 0.72),
               (cx + half, top + h), (cx - half, top + h)], fill=shade(shirt, 0.80))

    if arms_up:
        aw = w * 0.13
        for s in (-1, 1):
            ax = cx + s * half * 0.92
            d.polygon([(ax - aw, shoulder_y + h * 0.06), (ax + aw, shoulder_y + h * 0.02),
                       (ax + aw * 0.7, top - h * 0.02), (ax - aw * 0.7, top + h * 0.02)],
                      fill=skin)

    # neck then head
    d.rectangle([cx - head_r * 0.42, head_cy, cx + head_r * 0.42, shoulder_y + 1], fill=shade(skin, 0.82))
    d.ellipse([cx - head_r, head_cy - head_r * 1.12, cx + head_r, head_cy + head_r * 1.12], fill=skin)
    # hair cap on most people
    if rng.random() < 0.86:
        hair = shade(skin, rng.uniform(0.16, 0.34))
        d.chord([cx - head_r, head_cy - head_r * 1.12, cx + head_r, head_cy + head_r * 1.12],
                180, 360, fill=hair)


for row in range(ATLAS_ROWS):
    top = row * ch
    row_fill = rng.uniform(*ROW_FILL_RANGE)     # this row's own occupancy
    i = 0
    while i < PEOPLE_PER_REPEAT:
        if rng.random() > row_fill:
            i += rng.randint(1, GAP_RUN_MAX)    # an empty stretch, varying width
            continue
        cx = (i + 0.5) * cw + rng.uniform(-cw * X_JITTER, cw * X_JITTER)
        h = ch * rng.uniform(0.86, 1.00)
        # Heads sit near the TOP of the cell. Each row's riser covers more than its own step, so
        # the bottom of every quad is hidden behind the row in front - any dead space goes there,
        # never above the heads.
        t = top + (ch - h) * rng.uniform(0.0, 0.28)
        w = cw * rng.uniform(*WIDTH_RANGE)
        lit = rng.uniform(*SHADE_RANGE)
        skin = shade(rng.choice(SKIN), lit)
        shirt = shade(rng.choice(SHIRT), lit)
        arms = rng.random() < 0.08
        # draw wrapped, so the tile repeats seamlessly around the bowl
        for off in (-W, 0, W):
            if -cw * 2 < cx + off < W + cw * 2:
                person(cx + off, t, w, h, skin, shirt, arms)
        i += 1

img = img.resize((OUT_W, OUT_H), Image.LANCZOS)
img = img.filter(ImageFilter.SMOOTH)
img.save(DST)
print('wrote %s  %dx%d  cell %.1f x %.1f px per person'
      % (DST, OUT_W, OUT_H, OUT_W / PEOPLE_PER_REPEAT, OUT_H / ATLAS_ROWS))
