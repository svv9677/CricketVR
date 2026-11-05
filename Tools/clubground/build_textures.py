"""Builds the club ground's textures (phase 6) into Assets/ClubGround/Textures/.

Every pixel is synthesised here (smooth noise, Voronoi cells, shapes): no photograph, scan or
third-party texture goes in, so the club ground is entirely ours for the store release.
Needs numpy, scipy and PIL. Run from this folder:
    /usr/local/bin/python3 -W ignore build_textures.py [output folder] [--layout layout.json]

How each texture maps (the builder, Assets/Editor/ClubGround, must use the same):
  club_outfield.png     top view of 122 m: u = (x + 61) / 122, v = (z + 61) / 122 (the PNG's top row is
                        v = 1, i.e. +Z). Mowing stripes run along X (the pitch), 5 m a band.
  club_surround.png     the same over 400 m: u = (x + 200) / 400, v = (z + 200) / 400.
  club_grass_detail.png tileable, grey-centred (sRGB 128): the outfield's _DetailAlbedoMap, ~2.5 m a tile.
  club_atlas.png        4x4 cells of 512 px, row-major from the top left. A cell's picture is its inner
                        496 px; the 8 px gutter round it is the picture wrapped (so a cell tiles; the
                        window, which does not tile, is edge-copied). Cell i's UV rect:
                        u0 = (i % 4 * 512 + 8) / 2048, v0 = 1 - (i // 4 * 512 + 504) / 2048, size 496 / 2048.
                        Metres one cell covers: CELL_METRES below. The cells are drawn in
                        atlas_cells.py (apart, to keep each file small).
  club_picket.png       7.2 m of fence, 0.9 m high (square pixels), tiles along u: 32 pickets on 2 rails.
  club_horizon.png      tiles along u; v = 0 at HorizonBottom (-4 m), v = 1 at the top (+21 m), so the
                        ground line is v = 0.16. The bottom 15% is solid.
  club_flag.png         hoist (the pole side) at u = 0.

The contact-shadow pools in the surround (and the outfield, where a bench reaches it) are painted at
the footprints in SPOTS, which copy ClubGroundLayout (Assets/Scripts/ClubGround). The trees are a
seeded Poisson-disc in C#, so their pools come from --layout: a JSON file the builder can export from
ClubGroundLayout.Footprints() and Trees(seed):
    {"spots": [{"name", "x", "z", "yaw", "w", "d"}], "trees": [{"x", "z", "scale", "kind"}]}
"spots" replaces SPOTS; without "trees" the tree ring only gets a faint leaf-litter tint.
"""
import argparse, json, math, os
import numpy as np
from PIL import Image

EYE = (10.3, 0.0)            # ClubGroundLayout.BatterEye, xz (BallDelivery.BatsmanStumpsX)
SUN = (-0.5, 0.866)          # Sun Light 1 (50, 330, 0): which way a shadow falls, xz
# The footprint table: name, x, z, w (local X: the face toward the eye), d (local Z), yaw in degrees
# (None = faces BatterEye), how dark the pool is under it (0..1), how far it spreads (m), and how far
# it leans toward the sun's shadow (m).
SPOTS = [
    ("Pavilion",        -60.00,  30.00, 22.0, 9.0, None, 0.55, 2.2, 0.8),
    ("ScoreHut",        -56.00, -30.00,  9.6, 6.0, None, 0.50, 1.6, 0.6),
    ("Flagpole",        -55.00,  44.00,  0.6, 0.6, None, 0.30, 0.4, 0.2),
    ("RearSightscreen",  63.00,   0.00, 14.0, 1.5, None, 0.40, 1.4, 0.6),
    ("Roller",          -30.00,  62.00,  2.4, 1.4, None, 0.45, 0.6, 0.3),
    ("Bench0",           52.83,  30.50,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench1",           30.50,  52.83,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench2",          -10.59,  60.07,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench3",          -39.21,  46.73,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench4",          -39.21, -46.73,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench5",          -10.59, -60.07,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench6",           30.50, -52.83,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Bench7",           52.83, -30.50,  2.0, 0.7, None, 0.35, 0.5, 0.2),
    ("Pylon0",           45.25,  45.25,  1.5, 1.5,  45., 0.45, 0.8, 0.4),
    ("Pylon1",          -45.25,  45.25,  1.5, 1.5, 135., 0.45, 0.8, 0.4),
    ("Pylon2",          -45.25, -45.25,  1.5, 1.5, 225., 0.45, 0.8, 0.4),
    ("Pylon3",           45.25, -45.25,  1.5, 1.5, 315., 0.45, 0.8, 0.4),
]
TREE_CANOPY = (4.0, 1.6)     # canopy radius (m) at scale 1: broadleaf (kind 0), poplar (kind 1)
FENCE_R, DISC_R = 58.5, 61.0
CELL_METRES = {0: 1.95, 1: 1.2, 2: 1.2, 3: 1.2, 4: 1.2, 5: 1.0, 6: 0.5, 7: 1.0,
               8: 1.0, 9: 1.0, 10: 2.0, 11: 2.0, 12: 2.0, 13: 2.0, 14: 1.0, 15: 0.5}

def lin(rgb):
    """sRGB 0..255 -> linear: colours are mixed and shaded in linear light."""
    c = np.asarray(rgb, np.float32) / 255
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4).astype(np.float32)

# The grass, sRGB: a natural English summer green, not a sports-broadcast one.
GRASS, GRASS_DRY, LONG = lin((84, 124, 50)), lin((116, 136, 60)), lin((66, 100, 42))
WORN, MEADOW, LITTER = lin((132, 138, 80)), lin((76, 104, 46)), lin((84, 86, 52))
GRAVEL, CREAM, WHITE = lin((168, 156, 132)), lin((236, 230, 212)), lin((238, 236, 228))

def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)

def save(path, rgb, alpha=None):
    a = np.round(to_srgb(rgb) * 255).astype(np.uint8)
    if alpha is not None:
        a = np.dstack([a, np.round(np.clip(alpha, 0, 1) * 255).astype(np.uint8)])
    Image.fromarray(a, "RGBA" if alpha is not None else "RGB").save(path, optimize=True)
    print("wrote", os.path.basename(path), a.shape[1], "x", a.shape[0])

def noise(shape, sigma, rng, aniso=(1.0, 1.0)):
    """Smooth noise of unit spread, features ~sigma px (sigma_y, sigma_x scaled by aniso). Filtered in
    the frequency domain, so it tiles - and a 200 px blur costs no more than a 2 px one."""
    fy = np.fft.fftfreq(shape[0])[:, None] * sigma * aniso[0]
    fx = np.fft.rfftfreq(shape[1])[None, :] * sigma * aniso[1]
    a = np.fft.irfft2(np.fft.rfft2(rng.standard_normal(shape)) * np.exp(-2 * np.pi ** 2 * (fy ** 2 + fx ** 2)),
                      s=shape)
    return ((a - a.mean()) / (a.std() + 1e-12)).astype(np.float32)

def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)

def mixc(a, b, t):
    t = np.asarray(t, np.float32)
    return a + (b - a) * t[..., None]

class Grid:
    """A top-view texture over [-half, half] m: column -> x, row -> z (top row = +Z)."""
    def __init__(self, n, half):
        self.n, self.half, self.px = n, half, 2 * half / n
        c = ((np.arange(n) + 0.5) * self.px - half).astype(np.float32)
        self.X, self.Z = np.meshgrid(c, -c)

    def window(self, x, z, r):
        """Slices of the rows and columns within r of (x, z)."""
        c0, c1 = [int(np.clip((v + self.half) / self.px, 0, self.n)) for v in (x - r, x + r + self.px)]
        r0, r1 = [int(np.clip((self.half - v) / self.px, 0, self.n)) for v in (z + r + self.px, z - r)]
        return slice(r0, r1), slice(c0, c1)

def face_eye(x, z):
    """Unity yaw (deg) that turns local +Z from (x, z) toward the batter's eye."""
    return math.degrees(math.atan2(EYE[0] - x, EYE[1] - z))

def spots_from(layout):
    if layout is None or "spots" not in layout:
        return SPOTS
    out = []
    for s in layout["spots"]:
        big = s["w"] * s["d"] > 20
        out.append((s["name"], s["x"], s["z"], s["w"], s["d"], s.get("yaw"),
                    0.5 if big else 0.38, 2.0 if big else 0.6, 0.7 if big else 0.25))
    return out

def paint_pools(g, occ, spots, trees):
    """Soft contact shadow: dark under each footprint, fading over its spread, leaning with the sun.
    The renderers cast no shadows (Quest budget), so this is what sits them on the grass."""
    for _, x, z, w, d, yaw, dark, spread, lean in spots:
        yaw = math.radians(face_eye(x, z) if yaw is None else yaw)
        cx, cz = x + SUN[0] * lean, z + SUN[1] * lean
        rs, cs = g.window(cx, cz, math.hypot(w, d) / 2 + spread * 5)
        dx, dz = g.X[rs, cs] - cx, g.Z[rs, cs] - cz
        lx = np.abs(dx * math.cos(yaw) - dz * math.sin(yaw)) - w / 2
        lz = np.abs(dx * math.sin(yaw) + dz * math.cos(yaw)) - d / 2
        sd = np.hypot(np.maximum(lx, 0), np.maximum(lz, 0))
        occ[rs, cs] *= 1 - dark * np.exp(-sd / spread)
    for t in trees:
        r = TREE_CANOPY[int(t.get("kind", 0))] * t.get("scale", 1.0)
        cx, cz = t["x"] + SUN[0] * r * 0.25, t["z"] + SUN[1] * r * 0.25
        rs, cs = g.window(cx, cz, r * 2.5)
        q = np.hypot(g.X[rs, cs] - cx, g.Z[rs, cs] - cz) / r
        occ[rs, cs] *= (1 - 0.42 * np.exp(-q ** 2 * 1.6)) * (1 - 0.18 * np.exp(-(q / 0.35) ** 2))
    return occ

def stripes(g, rng, R):
    """Mowing: alternate 5 m bands along X (grass laid toward / away from you), wobbling a little as a
    mower does, fading out before the boundary, where the finishing laps go round in rings."""
    zz = g.Z + 0.18 * noise(g.X.shape, 90, rng)
    band = np.clip(np.sin(np.pi * zz / 5.0) * 5, -1, 1)
    band *= 1 - sstep(52.5, 55.5, R)
    ring = np.clip(np.sin(np.pi * (R - 55.5) / 1.5) * 5, -1, 1) * sstep(54.8, 55.8, R) * (1 - sstep(58.3, 58.6, R))
    return band + 0.8 * ring

def worn_square(g, rng):
    """The square: the strip of pitches across the middle, shorter and paler, each 3.05 m strip worn
    differently (the pitch quad's soft edges blend into it), and the bowler's run-up."""
    X, Z = g.X, g.Z
    edge = 0.35 * noise(X.shape, 14, rng)
    sq = sstep(0.0, 0.8, 13 - np.abs(X) + edge) * sstep(0.0, 0.8, 10 - np.abs(Z) + edge)
    strip = np.floor((Z + 10) / 3.05).astype(int) % 7
    wear = np.array([0.35, 0.55, 0.4, 0.9, 0.5, 0.3, 0.45], np.float32)[strip]
    wear = np.where(np.abs(Z) < 1.6, 1.0, wear)
    runup = sstep(1.4, 0.4, np.abs(Z) + edge * 0.5) * sstep(-30, -14, X) * sstep(-12, -13.5, X)
    return np.clip(sq * wear * (0.75 + 0.25 * noise(X.shape, 6, rng)) + 0.4 * runup, 0, 1)

def outfield(path, spots, trees, rng):
    g = Grid(2048, 61.0)
    R = np.hypot(g.X, g.Z)
    shape = g.X.shape
    col = mixc(GRASS, GRASS_DRY, np.clip(0.3 + 0.2 * noise(shape, 160, rng), 0, 1))
    band = stripes(g, rng, R)
    col = mixc(col, GRASS_DRY, 0.12 * np.clip(band, 0, 1))
    bright = 1 + 0.085 * band + 0.04 * noise(shape, 10, rng) + 0.035 * noise(shape, 1.3, rng)
    col = mixc(col, WORN, 0.7 * worn_square(g, rng))
    # Beyond the fence the grass is left longer: darker, coarser.
    long_t = sstep(58.35, 58.75, R + 0.06 * noise(shape, 3, rng))
    col = mixc(col, LONG, long_t)
    bright = bright * (1 - long_t) + long_t * (1 + 0.07 * noise(shape, 5, rng) + 0.05 * noise(shape, 1.3, rng))
    occ = np.ones(shape, np.float32)
    occ *= 1 - 0.28 * np.exp(-((R - FENCE_R) / 0.3) ** 2)   # the pickets' foot
    paint_pools(g, occ, spots, trees)
    save(path, col * (bright * occ)[..., None])

def surround(path, spots, trees, rng):
    """Beyond the fence: a mown walk to ~68 m (it meets the outfield's long grass at 61 m), rough
    meadow further out, leaf litter under the tree ring, gravel in front of the pavilion."""
    g = Grid(2048, 200.0)
    R = np.hypot(g.X, g.Z)
    shape = g.X.shape
    rough = sstep(66, 74, R + 3 * noise(shape, 40, rng))
    col = mixc(LONG, MEADOW, rough * np.clip(0.55 + 0.35 * noise(shape, 60, rng), 0, 1))
    col = mixc(col, GRASS_DRY, rough * np.clip(0.25 * noise(shape, 25, rng), 0, 0.3))
    under = sstep(70, 76, R) * (1 - sstep(104, 112, R))
    col = mixc(col, LITTER, under * (0.15 if not trees else 0.06))
    bright = 1 + (0.06 + 0.06 * rough) * noise(shape, 6, rng) + 0.04 * noise(shape, 1.2, rng)
    pav = [s for s in spots if s[0] == "Pavilion"]
    if pav:
        _, x, z, w, d, yaw, *_ = pav[0]
        yaw = math.radians(face_eye(x, z) if yaw is None else yaw)
        dx, dz = g.X - x, g.Z - z
        lx = dx * math.cos(yaw) - dz * math.sin(yaw)
        lz = dx * math.sin(yaw) + dz * math.cos(yaw)
        e = 0.3 * noise(shape, 3, rng)
        apron = sstep(0, 0.6, w / 2 + 0.5 - np.abs(lx) + e) * sstep(0, 0.6, lz - d / 2 + 0.2 + e) * \
            sstep(0, 0.6, d / 2 + 3.2 - lz + e)
        col = mixc(col, GRAVEL, apron)
        bright *= 1 + apron * 0.08 * noise(shape, 0.7, rng)
    occ = paint_pools(g, np.ones(shape, np.float32), spots, trees)
    save(path, col * (bright * occ)[..., None])

def grass_detail(path, rng):
    """Grey-centred blades and clumps; URP doubles it, so sRGB 128 leaves the colour as it is."""
    s = (512, 512)
    v = 0.09 * noise(s, 0.8, rng) + 0.06 * noise(s, 0.6, rng, (3, 1)) + 0.06 * noise(s, 0.6, rng, (1, 3))
    v += 0.07 * noise(s, 4, rng) + 0.05 * noise(s, 18, rng)
    # Gentle: sRGB +-0.045 is already +-20% in linear light, and the base map carries the clumps.
    g = np.clip(0.5 + v * (0.045 / v.std()), 0, 1)
    rgb = np.dstack([g * (1 + 0.015 * noise(s, 6, rng)), g, g * (1 - 0.015 * noise(s, 6, rng))])
    rgb = rgb - rgb.reshape(-1, 3).mean(0) + 0.5        # exactly grey-centred, in sRGB terms
    Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").save(path, optimize=True)
    print("wrote", os.path.basename(path), "512 x 512")

def supersample(fn, w, h, k=4):
    """Coverage of a shape (fn(x, y) -> bool, y up from the bottom, in output px) averaged over k*k
    sub-samples: clean anti-aliased edges for alpha-to-coverage."""
    o = (np.arange(k) + 0.5) / k
    x = (np.arange(w)[None, :, None] + o[None, None, :]).repeat(h, 0)
    acc = np.zeros((h, w), np.float32)
    for oy in o:
        y = (h - 1 - np.arange(h) + oy)[:, None, None]
        acc += fn(x, y).mean(axis=2)
    return acc / k

def picket(path, rng):
    W, H, n = 1024, 128, 32
    pitch, half = W / n, 9.5
    def pick(x, y):
        dx = np.abs((x % pitch) - pitch / 2)
        top = 112 + 15 * (1 - (dx / half) ** 1.4).clip(0, 1)   # a rounded point
        return (dx < half) & (y < top)
    def rails(x, y):
        return ((y > 20) & (y < 33)) | ((y > 86) & (y < 99)) | np.zeros_like(x, bool)
    a_p, a_r = supersample(pick, W, H), supersample(rails, W, H)
    x = np.arange(W)[None, :]
    yb = (H - 1 - np.arange(H))[:, None] / H
    dx = ((x % pitch) - pitch / 2) / half
    tint = 1 + 0.025 * rng.standard_normal(n)[(x // pitch).astype(int) % n]
    shade = (1 - 0.10 * np.clip(dx, 0, 1) ** 3 + 0.03 * np.clip(-dx, 0, 1)) * tint
    shade = shade * (1 + 0.03 * noise((H, W), 0.8, rng, (6, 0.3))) * (0.86 + 0.14 * sstep(0, 0.14, yb))
    face = WHITE * shade[..., None]
    face = mixc(face, lin((120, 128, 92)), 0.35 * (1 - sstep(0.0, 0.07, yb)))   # grass-splashed foot
    rail = WHITE * 0.62 * (1 + 0.04 * noise((H, W), 2, rng))[..., None]
    rgb = mixc(rail, face, np.clip(a_p / np.maximum(np.maximum(a_p, a_r), 1e-6), 0, 1))
    alpha = np.maximum(a_p, a_r)
    rgb = np.where(alpha[..., None] > 0.01, rgb, WHITE * 0.85)    # no dark fringe as it mips
    save(path, rgb, alpha)

def crowns(W, H, rng, count, base, hmin, hmax, rmin, rmax, poplar=0.0):
    """A treeline as a skyline: round crowns (a few tall narrow poplars) merged into one canopy over a
    hedge at `base` (fractions of H), wrapped so the strip tiles, with a ragged leafy edge. Returns
    coverage and a shade, lighter toward the top of each crown."""
    k = 4
    xs = (np.arange(W * k) + 0.5) / k
    top = base * H + 1.2 * noise((1, W * k), 40 * k, rng)[0]
    ctop, crad = np.full_like(xs, -1.0), np.ones_like(xs)
    for _ in range(count):
        cx, h, r = rng.uniform(0, W), rng.uniform(hmin, hmax) * H, rng.uniform(rmin, rmax)
        w = r * (0.4 if rng.random() < poplar else 1.0)
        if w < r: h *= 1.2
        dx = (xs - cx + W / 2) % W - W / 2
        idx = np.where(np.abs(dx) < 1.6 * w)[0]
        u = np.abs(dx[idx]) / w
        # The round top, then a sloping shoulder (not a cliff) where it meets a lower neighbour.
        cap = np.where(u < 1, h - r * (1 - np.sqrt(np.clip(1 - u * u, 0, 1))), h - r - 1.8 * r * (u - 1))
        up = cap > top[idx]
        top[idx[up]], ctop[idx[up]], crad[idx[up]] = cap[up], h, r
    yu = (H - 0.5 - np.arange(H))[:, None].astype(np.float32)
    edge = 1.6 * noise((H, W * k), 1.2, rng, (1, k))
    cov = np.clip(top[None, :] + edge - yu + 0.5, 0, 1)
    t = np.clip((yu - (ctop - 2 * crad)[None, :]) / (2 * crad[None, :]), 0, 1)
    lit = np.where(ctop[None, :] > 0, 0.6 + 0.42 * t, 0.66)
    return cov.reshape(H, W, k).mean(2), lit.reshape(H, W, k).mean(2)

def horizon(path, rng):
    """Far hills, a hazy middle treeline and a darker near hedgerow with trees: the land beyond the
    real trees, so the sky never meets bare ground. Colours carry the haze; the shader adds fog."""
    W, H = 2048, 256
    ridge = 0.50 + 0.07 * noise((1, W), 160, rng)[0] + 0.025 * noise((1, W), 30, rng)[0]
    yu = (H - 0.5 - np.arange(H))[:, None] / H
    hills = np.clip((ridge[None, :] - yu) * H + 0.5, 0, 1)
    mid, mid_lit = crowns(W, H, rng, 200, 0.29, 0.32, 0.40, 12, 24, poplar=0.06)
    near, near_lit = crowns(W, H, rng, 110, 0.20, 0.24, 0.34, 9, 18, poplar=0.12)
    near = np.maximum(near, (yu < 0.15).astype(np.float32))
    tex = noise((H, W), 1.2, rng) * 0.06 + noise((H, W), 4, rng) * 0.05
    hill_c = mixc(lin((150, 168, 172)), lin((132, 150, 148)), np.clip((ridge[None, :] - yu) * 6, 0, 1))
    rgb = hill_c * (1 + 0.5 * tex)[..., None]
    rgb = mixc(rgb, lin((96, 118, 100)) * (mid_lit * (1 + tex))[..., None], mid)
    rgb = mixc(rgb, lin((66, 90, 56)) * (near_lit * (1 + 1.5 * tex))[..., None], near)
    rgb = mixc(rgb, lin((58, 78, 48)), np.clip(1 - yu / 0.17, 0, 1) * near)        # the hedge's foot
    alpha = np.maximum(np.maximum(hills, mid), near)
    rgb = np.where(alpha[..., None] > 0.01, rgb, lin((150, 168, 172)))
    save(path, rgb, alpha)

def flag(path, rng):
    """The club's flag, made up: bottle green over cream on the diagonal, a maroon band between,
    a canvas sleeve at the hoist. No crest, no lettering."""
    W, H = 256, 128
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    d = (x / W) + (y / H) - 1.0                   # < 0: the upper hoist triangle
    green, cream, maroon = lin((28, 74, 46)), lin((232, 222, 192)), lin((118, 32, 40))
    rgb = mixc(green, cream, sstep(-0.004, 0.004, d))
    rgb = mixc(rgb, maroon, sstep(0.075, 0.068, np.abs(d)))
    weave = 1 + 0.035 * (np.sin(x * np.pi) * np.sin(y * np.pi)) + 0.03 * noise((H, W), 0.7, rng)
    rgb = rgb * weave[..., None]
    rgb = mixc(rgb, lin((214, 208, 190)), (x < 12).astype(np.float32))
    rgb = rgb * (1 - 0.25 * ((np.abs(x - 12.5) < 1) | (x < 1) | (y < 1) | (y > H - 2)))[..., None]
    save(path, rgb)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out", nargs="?", default="../../Assets/ClubGround/Textures")
    ap.add_argument("--layout", default=None)
    a = ap.parse_args()
    layout = json.load(open(a.layout)) if a.layout else None
    spots, trees = spots_from(layout), (layout or {}).get("trees", [])
    from atlas_cells import atlas
    os.makedirs(a.out, exist_ok=True)
    rng = np.random.default_rng(6)
    outfield(f"{a.out}/club_outfield.png", spots, trees, rng)
    surround(f"{a.out}/club_surround.png", spots, trees, rng)
    grass_detail(f"{a.out}/club_grass_detail.png", rng)
    save(f"{a.out}/club_atlas.png", atlas(np.random.default_rng(16)))
    picket(f"{a.out}/club_picket.png", rng)
    horizon(f"{a.out}/club_horizon.png", rng)
    flag(f"{a.out}/club_flag.png", rng)

if __name__ == "__main__":
    main()
