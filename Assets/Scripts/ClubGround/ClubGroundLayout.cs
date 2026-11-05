using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where everything at the club ground stands: the one source of truth for every radius and
/// placement, read by the prefab builder, the runtime (glow points) and, through
/// Tools/clubground/layout_table.json (ClubGroundLayoutTests writes and checks it), by the texture
/// script that paints contact shadows under the same footprints and trees.
///
/// Conventions. The batter is at BallDelivery.BatsmanStumpsX looking down -X at the bowler. A
/// bearing is degrees from +X towards +Z about the ground's centre: (r cos b, 0, r sin b). A yaw is
/// Unity's (degrees about +Y; local +Z ends up along (sin yaw, 0, cos yaw)), and a footprint is
/// (width along local X, depth along local Z). Spots face the batter's eye unless noted, so a
/// pavilion's veranda and a scoreboard's figures face him.
///
/// Rules (ClubGroundLayoutTests): nothing but the sightscreen inside the bowler's-arm wedge (a
/// building behind the bowler's hand hides the ball at release); the pavilion and the score hut
/// within 40 deg of his line and 60-80 m away (in view, not in the way); no two footprints
/// overlapping; the fence 1.5 m+ beyond the rope so a fielder's overrun (rope + 1) never meets it.
/// </summary>
public static class ClubGroundLayout
{
    // ---- Radii and heights (from the ground's centre, metres) --------------------------------

    /// The rope is the scene's BoundaryCollider radius, which fielding, catching and scoring all
    /// cache in Awake: the club keeps it rather than re-plumbing them.
    public const float RopeRadius = 56.64f;
    public const float FenceRadius = 58.5f, FenceHeight = 0.9f;
    /// The top of the club outfield collider: where BallFlight assumes the ground is.
    public static float OutfieldTop => BallFlight.OutfieldY;
    public const float OutfieldDisc = 61f, SurroundRadius = 200f;
    public const float TreeInner = 72f, TreeOuter = 105f;
    /// Inside the fog's linear 100-350 m (so it is not washed out by day); the bottom below the
    /// ground so no strip of bare sky shows under it.
    public const float HorizonRadius = 180f, HorizonHeight = 25f, HorizonBottom = -4f;

    public static readonly Vector3 BatterEye = new Vector3(BallDelivery.BatsmanStumpsX, 1.6f, 0f);
    public const float ArmWedgeDeg = 12f, ArmWedgeFrom = 50f;

    /// The scene's Replay Cam sits just above the black sightscreen: its frame stays below
    /// SightscreenFrameTop and more than ReplayCamClear from the camera (ClearOfReplayCam).
    public static readonly Vector3 ReplayCam = new Vector3(-62.15f, 9.93f, 0f);
    public const float ReplayCamClear = 1.5f, SightscreenFrameTop = 9.3f;

    public const float BenchRadius = 61f, PylonRadius = 64f, PylonHeight = 22f;

    // ---- Placements -------------------------------------------------------------------------

    public struct Spot
    {
        public string name;
        public Vector3 pos;
        public float yawDeg;
        public Vector2 footprint;
    }

    public const int Broadleaf = 0, Poplar = 1;

    public struct TreeSpot
    {
        public Vector3 pos;
        public float scale;
        /// Broadleaf (0) or Poplar (1).
        public int kind;
    }

    /// 76.4 m from the eye, 23 deg off his line, on the +Z side.
    public static readonly Spot Pavilion = Facing("Pavilion", -60f, 30f, 22f, 9f);
    /// 72.8 m, 24 deg, on the -Z side: the board is read from the crease.
    public static readonly Spot ScoreHut = Facing("ScoreHut", -56f, -30f, 9.6f, 6f);
    /// Beside the pavilion, on the side away from the wedge.
    public static readonly Spot Flagpole = Facing("Flagpole", -55f, 44f, 1f, 1f);
    /// The frame dressing the scene's black sightscreen (SightScreen.cs: x = -63, 18 m wide):
    /// the one thing allowed in the wedge.
    public static readonly Spot SightscreenFrame = Facing("SightscreenFrame", -63f, 0f, 19f, 1.2f);
    /// Behind the batter, for the view from the bowler's end and the broadcast cameras.
    public static readonly Spot RearSightscreen = Facing("RearSightscreen", 63f, 0f, 14f, 1.5f);
    public static readonly Spot Roller = Facing("Roller", -30f, 62f, 2.4f, 1.6f);

    /// Bearings of the 8 benches at BenchRadius: either side of the batter's view, clear of the wedge,
    /// the buildings and the pylons.
    public static readonly float[] BenchBearings = { 80f, 100f, 115f, 128f, 232f, 250f, 270f, 290f };
    public static readonly Spot[] Benches = Ring("Bench", BenchBearings, BenchRadius, 2.4f, 0.9f);

    public static readonly float[] PylonBearings = { 45f, 135f, 225f, 315f };
    public static readonly Spot[] Pylons = Ring("Pylon", PylonBearings, PylonRadius, 2f, 2f);

    /// Every placement, in a fixed order (the layout table's order).
    public static IEnumerable<Spot> Footprints()
    {
        yield return Pavilion;
        yield return ScoreHut;
        yield return Flagpole;
        yield return SightscreenFrame;
        yield return RearSightscreen;
        yield return Roller;
        foreach (Spot b in Benches) yield return b;
        foreach (Spot p in Pylons) yield return p;
    }

    /// The pylon heads: where the night glows hang (StadiumLighting.SetGlowBanks).
    public static Vector3[] GlowPoints()
    {
        var points = new Vector3[Pylons.Length];
        for (int i = 0; i < Pylons.Length; i++)
            points[i] = Pylons[i].pos + Vector3.up * PylonHeight;
        return points;
    }

    // ---- Geometry ---------------------------------------------------------------------------

    public static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);

    /// Degrees between the batter's line (-X from his eye) and the direction to this point, 0..180.
    public static float OffLineDeg(Vector2 xz)
    {
        Vector2 d = xz - XZ(BatterEye);
        return Mathf.Atan2(Mathf.Abs(d.y), -d.x) * Mathf.Rad2Deg;
    }

    public static float FromEye(Vector2 xz) => (xz - XZ(BatterEye)).magnitude;

    /// Behind the bowler's hand as the batter sees it: anything here moves or contrasts with the
    /// ball at release.
    public static bool InBowlersArm(Vector2 xz) => FromEye(xz) > ArmWedgeFrom && OffLineDeg(xz) <= ArmWedgeDeg;

    /// Whether a point of the sightscreen frame keeps out of the replay camera's way.
    public static bool ClearOfReplayCam(Vector3 p) =>
        p.y <= SightscreenFrameTop && Vector3.Distance(p, ReplayCam) > ReplayCamClear;

    private static Vector2 Right(Spot s)
    {
        float y = s.yawDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(y), -Mathf.Sin(y));
    }

    private static Vector2 Forward(Spot s)
    {
        float y = s.yawDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(y), Mathf.Cos(y));
    }

    /// Whether a ground point is on the spot's footprint grown by `margin` on every side.
    public static bool Contains(Spot s, Vector2 xz, float margin = 0f)
    {
        Vector2 d = xz - XZ(s.pos);
        return Mathf.Abs(Vector2.Dot(d, Right(s))) <= s.footprint.x * 0.5f + margin &&
               Mathf.Abs(Vector2.Dot(d, Forward(s))) <= s.footprint.y * 0.5f + margin;
    }

    /// The footprint's four ground corners (grown by `margin`), anticlockwise seen from above.
    public static Vector2[] Corners(Spot s, float margin = 0f)
    {
        Vector2 c = XZ(s.pos);
        Vector2 r = Right(s) * (s.footprint.x * 0.5f + margin), f = Forward(s) * (s.footprint.y * 0.5f + margin);
        return new[] { c - r - f, c + r - f, c + r + f, c - r + f };
    }

    /// Whether two footprints touch, `a` grown by `margin` (separating-axis test on the 4 edge normals).
    public static bool Overlaps(Spot a, Spot b, float margin = 0f)
    {
        Vector2[] ca = Corners(a, margin), cb = Corners(b);
        Vector2[] axes = { Right(a), Forward(a), Right(b), Forward(b) };
        foreach (Vector2 axis in axes)
        {
            Span(ca, axis, out float aMin, out float aMax);
            Span(cb, axis, out float bMin, out float bMax);
            if (aMax < bMin || bMax < aMin) return false;
        }
        return true;
    }

    private static void Span(Vector2[] corners, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue; max = float.MinValue;
        foreach (Vector2 c in corners)
        {
            float p = Vector2.Dot(c, axis);
            min = Mathf.Min(min, p); max = Mathf.Max(max, p);
        }
    }

    private static Spot Facing(string name, float x, float z, float width, float depth)
    {
        Vector2 d = XZ(BatterEye) - new Vector2(x, z);
        return new Spot
        {
            name = name, pos = new Vector3(x, 0f, z), yawDeg = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg,
            footprint = new Vector2(width, depth),
        };
    }

    private static Spot[] Ring(string name, float[] bearings, float radius, float width, float depth)
    {
        var spots = new Spot[bearings.Length];
        for (int i = 0; i < bearings.Length; i++)
        {
            float b = bearings[i] * Mathf.Deg2Rad;
            spots[i] = Facing(name + i, radius * Mathf.Cos(b), radius * Mathf.Sin(b), width, depth);
        }
        return spots;
    }

    // ---- Trees ------------------------------------------------------------------------------

    /// Trees in all, the poplars among them, and darts thrown per pass before giving up.
    public const int TreeTarget = 45, PoplarTarget = 7, TreeAttempts = 4000;
    /// Poisson-disc spacing between trunks, and the clear ground kept round every footprint.
    public const float TreeSpacing = 11f, TreeClearance = 3f;
    /// Trees within this many degrees of the pavilion (from the batter's eye) and beyond it are
    /// poplars: a tall dark backdrop behind the white building, closer together (they are narrow)
    /// so they read as a row, not two strays.
    public const float PoplarArcDeg = 14f, PoplarSpacing = 6f;

    /// The least distance allowed between two trunks of these kinds.
    public static float Spacing(int kindA, int kindB) => kindA == Poplar && kindB == Poplar ? PoplarSpacing : TreeSpacing;

    /// About TreeTarget trees in the TreeInner..TreeOuter ring, none in the wedge, none within
    /// TreeClearance of a footprint, none nearer than FenceRadius + 8. Poplars first, so the small
    /// arc behind the pavilion gets its row before the broadleaves fill the ring (one pass took
    /// two). Same seed, same trees, on any runtime: its own integer generator (UnityEngine.Random is
    /// native and global; System.Random's sequence is not promised across runtimes, and the layout
    /// table is made outside Unity).
    public static List<TreeSpot> Trees(int seed)
    {
        var trees = new List<TreeSpot>();
        uint state = Seed(seed);
        Scatter(trees, ref state, Poplar, PoplarTarget);
        Scatter(trees, ref state, Broadleaf, TreeTarget);
        return trees;
    }

    /// Dart-throws trees of one kind, each only where that kind grows, until `upTo` trees in all.
    private static void Scatter(List<TreeSpot> trees, ref uint state, int kind, int upTo)
    {
        for (int n = 0; n < TreeAttempts && trees.Count < upTo; n++)
        {
            // Uniform over the ring's area, not its radius (else they bunch at the inner edge).
            float r = Mathf.Sqrt(Mathf.Lerp(TreeInner * TreeInner, TreeOuter * TreeOuter, Next01(ref state)));
            float b = Next01(ref state) * 2f * Mathf.PI;
            float s = Next01(ref state);
            var xz = new Vector2(r * Mathf.Cos(b), r * Mathf.Sin(b));
            if (BehindPavilion(xz) != (kind == Poplar) || !TreeAllowed(xz) || Crowded(trees, xz, kind)) continue;
            float scale = kind == Poplar ? Mathf.Lerp(0.9f, 1.2f, s) : Mathf.Lerp(0.8f, 1.3f, s);
            trees.Add(new TreeSpot { pos = new Vector3(xz.x, 0f, xz.y), scale = scale, kind = kind });
        }
    }

    /// The ground rules for a trunk, without the spacing.
    public static bool TreeAllowed(Vector2 xz)
    {
        if (xz.magnitude < FenceRadius + 8f || InBowlersArm(xz)) return false;
        foreach (Spot s in Footprints())
            if (Contains(s, xz, TreeClearance)) return false;
        return true;
    }

    private static bool Crowded(List<TreeSpot> trees, Vector2 xz, int kind)
    {
        foreach (TreeSpot t in trees)
        {
            float d = Spacing(kind, t.kind);
            if ((XZ(t.pos) - xz).sqrMagnitude < d * d) return true;
        }
        return false;
    }

    /// Within PoplarArcDeg of the pavilion and beyond it, as the batter sees it: where poplars grow.
    public static bool BehindPavilion(Vector2 xz)
    {
        Vector2 eye = XZ(BatterEye), d = xz - eye, p = XZ(Pavilion.pos) - eye;
        float off = Mathf.DeltaAngle(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg);
        return Mathf.Abs(off) <= PoplarArcDeg && d.magnitude > p.magnitude;
    }

    private static uint Seed(int seed)
    {
        uint s = unchecked((uint)seed * 2654435761u) ^ 0x9E3779B9u;
        return s == 0 ? 1u : s;
    }

    /// xorshift32, top 24 bits: [0, 1).
    private static float Next01(ref uint s)
    {
        s ^= s << 13; s ^= s >> 17; s ^= s << 5;
        return (s >> 8) * (1f / 16777216f);
    }
}
