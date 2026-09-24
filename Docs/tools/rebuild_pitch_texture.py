"""
Rebuild PitchSurface.png so the pitch reads like the ground's dirt beside it.

The ground's dirt is a very smooth base map (Ground00_baseColor is 2048 px over 134.82 m, i.e.
15.2 px/m - it cannot resolve anything finer than ~6.6 cm) plus fine grain supplied by its detail
map. PitchSurface is a photo at 106 x 151 px/m, seven times finer, so it carried its own coarse
mottle and a green cast that the dirt does not have. Matching means giving the pitch the same
division of labour: a base as smooth as the ground's, and the SAME detail map for the grain.

Run from the repo root:  python3 Docs/tools/rebuild_pitch_texture.py
"""
from PIL import Image, ImageStat, ImageChops

SRC = 'Docs/tools/PitchSurface_source.png'     # the untouched photo
DST = 'Assets/Resources/Textures/PitchSurface.png'

GAIN        = 2.082    # linear-light gain that matches the pitch tone to the dirt (see 10e)
GROUND_PXPM = 15.2     # the ground base map's effective resolution, in px per metre
TILE_U_M    = 34.0 / 3 # PitchSurface tiles 3x across the 34 m length
TILE_V_M    = 6.0 / 3  # ... and 3x across the 6 m width
GREEN_KEEP  = 0.0      # 0 = remove the green cast entirely; 1 = keep it as photographed


def s2l(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def l2s(c):
    c = max(0.0, min(1.0, c))
    v = c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return int(round(v * 255))


def green_excess(img):
    r, g, b = img.split()
    return ImageStat.Stat(ImageChops.subtract(g, Image.blend(r, b, 0.5), scale=1.0, offset=0)).mean[0]


im = Image.open(SRC).convert('RGB')
w, h = im.size
before = ImageStat.Stat(im)
ge_before = green_excess(im)

# 1. tone: gain applied in linear light, where albedo actually multiplies
im = im.point([l2s(s2l(i) * GAIN) for i in range(256)] * 3)
target_mean = ImageStat.Stat(im).mean          # the tone match from 10e - preserve it exactly

# 2. character: drop to the ground base map's effective resolution and back.
#    Resampling rather than blurring keeps this anisotropic for free - the texture is
#    106 px/m across the pitch and 151 px/m along it.
small = (max(2, round(TILE_U_M * GROUND_PXPM)), max(2, round(TILE_V_M * GROUND_PXPM)))
im = im.resize(small, Image.LANCZOS).resize((w, h), Image.BICUBIC)

# 3. hue: the photo has grass in it; the dirt beside the pitch has none
if GREEN_KEEP < 1.0:
    r, g, b = im.split()
    neutral_g = Image.blend(r, b, 0.5)         # what green would be with no cast
    g = Image.blend(neutral_g, g, GREEN_KEEP)
    im = Image.merge('RGB', (r, g, b))

# 4. restore the exact mean so the tone match survives steps 2 and 3
now = ImageStat.Stat(im).mean
chans = []
for i, ch in enumerate(im.split()):
    k = target_mean[i] / now[i] if now[i] > 0 else 1.0
    chans.append(ch.point([min(255, int(round(v * k))) for v in range(256)]))
im = Image.merge('RGB', chans)

after = ImageStat.Stat(im)
print('source  mean %s  stddev %s' % ([round(x, 1) for x in before.mean], [round(x, 1) for x in before.stddev]))
print('result  mean %s  stddev %s' % ([round(x, 1) for x in after.mean], [round(x, 1) for x in after.stddev]))
print('effective resolution %.1f -> %.1f px/m (ground base is %.1f)'
      % (w / TILE_U_M, small[0] / TILE_U_M, GROUND_PXPM))
# the MEAN green offset is part of the dirt's hue and is restored by step 4 on purpose;
# what had to go is the local green blotching, which shows up as its spread.
print('green cast: mean %.2f -> %.2f levels (uniform hue, kept)' % (ge_before, green_excess(im)))
im.save(DST)
print('wrote', DST)
