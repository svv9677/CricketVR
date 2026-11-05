"""The 16 cells of the club ground's atlas (club_atlas.png), all synthesised: see build_textures.py
for the layout, the gutters and how many metres a cell covers (CELL_METRES).

  0 weatherboard   1 brick          2 roof tile      3 window
  4 veranda boards 5 green paint    6 bench slats    7 black score plate
  8 roller iron    9 white paint   10-13 foliage A-D (mid, dark, yellow, blue-green)
 14 bark          15 deckchair stripes
Every cell but the window tiles. Colours are linear RGB.
"""
import numpy as np
from build_textures import lin, noise, sstep, mixc, CREAM, WHITE

S, CELL, GUT = 496, 512, 8
YY, XX = np.mgrid[0:S, 0:S].astype(np.float32)

def n(rng, sigma, aniso=(1.0, 1.0)):
    return noise((S, S), sigma, rng, aniso)

def pick(rng, palette, count):
    """A colour per board / brick / tile, drawn from a palette, with a little spread."""
    p = np.stack([lin(c) for c in palette])
    return p[rng.integers(0, len(p), count)] * (1 + 0.04 * rng.standard_normal((count, 1)))

def boards(rng, count, gap, colour, var, grain):
    """Horizontal boards across the cell: a tint per board, grain along it, dark gaps between."""
    h = S / count
    idx = (YY // h).astype(int) % count
    f = (YY % h) / h
    tint = 1 + var * rng.standard_normal(count)[idx]
    g = 1 + grain * n(rng, 1.0, (0.6, 14)) + 0.6 * grain * n(rng, 3, (0.5, 8))
    jx = rng.uniform(0, S, count)[idx]
    joint = np.minimum(np.abs(XX - jx), S - np.abs(XX - jx)) < 1.2
    v = tint * g * np.where(joint, 0.45, 1.0)
    if gap > 0:
        v *= np.where(f > 1 - gap, 0.18, 0.86 + 0.14 * sstep(0, 0.25, f) * sstep(1 - gap, 1 - gap - 0.2, f))
    return colour * v[..., None], f

def weatherboard(rng):
    """Painted cream clapboard: each board's top sits in the shadow of the lap above."""
    c, f = boards(rng, 13, 0, CREAM, 0.012, 0.025)
    shade = (0.52 + 0.48 * sstep(0.0, 0.3, f)) * np.where(f > 0.93, 1.04, 1.0)
    worn = 0.5 * sstep(2.0, 2.6, n(rng, 3) + 0.4 * n(rng, 0.8))
    return mixc(c * shade[..., None], lin((150, 138, 118)) * shade[..., None], worn)

def brick(rng):
    rows, per, mortar = 16, 5, 3.5
    ch, bw = S / rows, S / per
    row = (YY // ch).astype(int)
    xo = XX + (row % 2) * bw / 2
    bi = (xo // bw).astype(int) % per
    fx, fy = xo % bw, YY % ch
    cols = pick(rng, [(150, 70, 52), (138, 62, 46), (160, 84, 62), (124, 58, 44), (170, 96, 70),
                      (112, 56, 46)], rows * per)[row % rows * per + bi]
    e = np.minimum(np.minimum(fx - mortar, bw - fx), np.minimum(fy - mortar, ch - fy))
    face = cols * ((0.86 + 0.14 * sstep(0, 4, e)) * (1 + 0.08 * n(rng, 0.7) + 0.06 * n(rng, 4)))[..., None]
    mort = lin((156, 148, 132)) * ((1 + 0.1 * n(rng, 0.8)) * np.where(fy < 1.6, 0.7, 1.0))[..., None]
    return np.where(((fx < mortar) | (fy < mortar))[..., None], mort, face)

def roof(rng):
    """Clay plain tiles, half-bond, the row above shading each tile's top; moss low on a few."""
    rows, per = 10, 6
    rh, tw = S / rows, S / per
    row = (YY // rh).astype(int)
    fy = (YY % rh) / rh
    xo = XX + (row % 2) * tw / 2
    ti = (xo // tw).astype(int) % per
    fx = (xo % tw) / tw
    cols = pick(rng, [(150, 72, 48), (136, 64, 44), (164, 86, 58), (120, 60, 44), (142, 78, 56)],
                rows * per)[row % rows * per + ti]
    shade = (0.58 + 0.44 * sstep(0, 0.75, fy)) * (1 - 0.1 * (2 * fx - 1) ** 2)
    shade *= np.where(fy > 0.94, 0.72, 1.0) * np.where((fx < 0.015) | (fx > 0.985), 0.4, 1.0)
    c = cols * (shade * (1 + 0.07 * n(rng, 0.8) + 0.05 * n(rng, 5)))[..., None]
    moss = sstep(1.3, 2.1, n(rng, 9) + 0.6 * n(rng, 2)) * sstep(0.4, 0.7, fy)
    return mixc(c, lin((96, 104, 62)) * shade[..., None], 0.8 * moss)

def window(rng):
    """A white six-over-six sash window: sky in the top of the glass, the dark room below, curtains."""
    fr, rail, bar = 30, 7, 7
    inner = S - 2 * fr
    glass = (XX > fr) & (XX < S - fr) & (YY > fr) & (YY < S - fr) & (np.abs(YY - S / 2) > rail)
    for k in (1, 2):
        glass &= np.abs(XX - (fr + k * inner / 3)) > bar / 2
    for cy in (fr + inner / 4, S - fr - inner / 4):
        glass &= np.abs(YY - cy) > bar / 2
    refl = np.clip(0.75 - YY / S * 0.95 + 0.18 * np.sin((XX + YY) * 0.013), 0, 1)
    g = mixc(lin((30, 36, 44)), lin((132, 150, 170)), refl)
    curtain = ((XX < fr + 64) | (XX > S - fr - 64)).astype(np.float32) * (0.75 + 0.25 * np.sin(XX * 0.16))
    g = mixc(g, lin((120, 112, 92)) * 0.55, curtain * (1 - refl) * 0.8)
    near = np.zeros_like(XX)
    for s in (1, 2, 3, 4):              # the frame's shadow on the glass, top and left
        near = np.maximum(near, (glass & ~np.roll(glass, s, 0)) | (glass & ~np.roll(glass, s, 1)))
    g = g * (1 - 0.45 * near)[..., None]
    frame = WHITE * (1 + 0.02 * n(rng, 1.2, (8, 0.4)))[..., None] * \
        np.where((XX < 3) | (YY < 3) | (XX > S - 4) | (YY > S - 4), 0.8, 1.0)[..., None]
    return np.where(glass[..., None], g, frame)

def veranda(rng):
    c, _ = boards(rng, 10, 0.06, lin((128, 106, 80)), 0.07, 0.09)
    return c * (1 + 0.04 * n(rng, 12))[..., None]

def paint(rng, colour, brush=0.035, scuff=0.0):
    """Flat paint with brush marks (vertical) and, for a worked object, scuffs."""
    v = 1 + brush * n(rng, 1.2, (10, 0.4)) + 0.025 * n(rng, 24)
    c = colour * v[..., None]
    if scuff:
        c = mixc(c, c * 1.35 + 0.02, scuff * sstep(1.6, 2.4, n(rng, 2, (1, 4)) + 0.5 * n(rng, 0.6)))
    return c

def green(rng):
    return paint(rng, lin((34, 70, 46)), scuff=0.6)

def bench(rng):
    c, _ = boards(rng, 5, 0.2, lin((150, 128, 98)), 0.05, 0.1)
    return c

def plate(rng):
    return lin((20, 21, 22)) * (1 + 0.08 * n(rng, 2) + 0.06 * n(rng, 30))[..., None]

def roller(rng):
    c = paint(rng, lin((44, 62, 48)), 0.03, scuff=0.5)
    rust = 0.7 * sstep(2.0, 2.7, n(rng, 6) + 0.4 * n(rng, 1.5))
    return mixc(c, lin((112, 66, 40)) * (1 + 0.15 * n(rng, 0.8))[..., None], rust)

def white(rng):
    c = paint(rng, WHITE, 0.02)
    return c * (1 - 0.06 * sstep(1.8, 2.6, n(rng, 1.0) + 0.4 * n(rng, 8)))[..., None]

def worley(rng, cells):
    """Tileable Voronoi: distance to the nearest and second-nearest point (in cells) and its id."""
    c = S / cells
    jit = rng.random((cells, cells, 2))
    ci, cj = (YY // c).astype(int), (XX // c).astype(int)
    f1 = np.full((S, S), 1e9, np.float32)
    f2, ids = f1.copy(), np.zeros((S, S), int)
    for di in (-1, 0, 1):
        for dj in (-1, 0, 1):
            a, b = ci + di, cj + dj
            j = jit[a % cells, b % cells]
            d = np.hypot(YY - (a + j[..., 0]) * c, XX - (b + j[..., 1]) * c) / c
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            ids = np.where(closer, (a % cells) * cells + b % cells, ids)
            f1 = np.minimum(f1, d)
    return f1, f2, ids

def foliage(rng, base, light):
    """Leaf clusters for the opaque low-poly crowns: leaves (small cells) bunched into clumps lit in
    the middle, dark gaps where you see into the tree."""
    f1, f2, ids = worley(rng, 31)
    F1, _, _ = worley(rng, 8)
    tint = 1 + 0.16 * rng.standard_normal(31 * 31)[ids]
    hue = rng.random(31 * 31)[ids]
    leaf = (1 - 0.4 * np.clip(f1 / 0.75, 0, 1) ** 1.6) * (0.75 + 0.25 * sstep(0.0, 0.14, f2 - f1))
    clump = 0.5 + 0.62 * np.clip(1 - F1 / 0.9, 0, 1)
    holes = sstep(-1.0, -1.7, n(rng, 9) + 0.4 * n(rng, 2))
    v = leaf * clump * (1 - 0.75 * holes) * tint
    return mixc(lin(base), lin(light), hue * 0.4) * v[..., None]

def bark(rng):
    r = np.abs(n(rng, 2.0, (12, 1)) + 0.3 * n(rng, 1.0, (6, 1)))
    fiss = sstep(0.3, 0.0, r)
    plates = 1 + 0.12 * n(rng, 3, (6, 1)) + 0.07 * n(rng, 0.8)
    c = lin((104, 94, 82)) * (plates * (1 - 0.6 * fiss))[..., None]
    return mixc(c, lin((112, 120, 92)) * plates[..., None], 0.5 * sstep(1.4, 2.0, n(rng, 12)))

def deckchair(rng):
    pal = np.stack([lin(c) for c in ((176, 52, 46), (232, 222, 196), (40, 58, 96), (232, 222, 196))])
    c = pal[(XX // 62).astype(int) % 4]
    weave = 1 + 0.05 * np.sin(XX * np.pi / 2) * np.sin(YY * np.pi / 2) + 0.03 * n(rng, 0.6) + 0.04 * n(rng, 30)
    return c * weave[..., None]

CELLS = [weatherboard, brick, roof, window, veranda, green, bench, plate, roller, white,
         lambda r: foliage(r, (60, 98, 40), (96, 130, 50)), lambda r: foliage(r, (40, 70, 34), (62, 92, 44)),
         lambda r: foliage(r, (98, 124, 46), (138, 150, 62)), lambda r: foliage(r, (58, 90, 70), (84, 118, 92)),
         bark, deckchair]

def atlas(rng):
    out = np.zeros((4 * CELL, 4 * CELL, 3), np.float32)
    for i, fn in enumerate(CELLS):
        c = np.pad(fn(rng), ((GUT, GUT), (GUT, GUT), (0, 0)), mode="edge" if fn is window else "wrap")
        r, k = divmod(i, 4)
        out[r * CELL:(r + 1) * CELL, k * CELL:(k + 1) * CELL] = c
    return out
