import sys
from PIL import Image, ImageStat

SRC = '/private/tmp/claude-501/-Users-rao-Git-CricketVR/d3bb4587-c6c9-4882-9c40-f79fc1256ba3/scratchpad/PitchSurface_orig.png'
DST = 'Assets/Resources/Textures/PitchSurface.png'
gain = float(sys.argv[1])          # gain applied in LINEAR light, which is where albedo multiplies

def s2l(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def l2s(c):
    c = max(0.0, min(1.0, c))
    v = c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
    return int(round(v * 255))

lut = [l2s(s2l(i) * gain) for i in range(256)]
clipped = sum(1 for i in range(256) if s2l(i) * gain > 1.0)

im = Image.open(SRC)
alpha = im.split()[-1] if im.mode in ('RGBA', 'LA') else None
rgb = im.convert('RGB').point(lut * 3)
out = rgb if alpha is None else Image.merge('RGBA', rgb.split() + (alpha,))
out.save(DST)

st = ImageStat.Stat(rgb)
print('gain %.3f (linear)  mean %s  extrema %s  lut entries clipping: %d'
      % (gain, [round(x, 1) for x in st.mean], rgb.getextrema(), clipped))
