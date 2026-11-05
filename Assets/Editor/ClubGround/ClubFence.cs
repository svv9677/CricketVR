using UnityEngine;
using static ClubMeshKit;

/// <summary>
/// Everything round the boundary: the white picket fence at FenceRadius with its gate in front
/// of the pavilion and its 72 colliders, the rope at RopeRadius, the inner-circle markers, the
/// benches, the roller, the flagpole, both sightscreens and the four floodlight pylons.
///
/// The fence is one alpha-to-coverage band (club_picket.png repeating along it), not 400 picket
/// boxes: about 500 triangles, and at 58 m the edges stay smooth under 4x MSAA. Everything else goes
/// into the structures batch.
/// </summary>
public static class ClubFence
{
    /// The gap the gate fills, in metres of fence.
    public const float GateWidth = 3f;
    /// One repeat of club_picket.png (1024 x 128 px) along the fence: eight fence-heights, so its
    /// pixels stay square.
    public static float PicketRepeat => 8f * ClubGroundLayout.FenceHeight;
    public const int FenceSegments = 256, FenceColliders = 72;
    /// SightScreen.cs builds the black screen at runtime: 63 m out on the pitch's line toward -X,
    /// 18 m wide, 8 m high, 0.3 m thick. The frame stands just behind it.
    const float ScreenBackX = -63.15f, ScreenHalfWidth = 9f, ScreenHeight = 8f;

    // ---- Fence, gate, rope, markers ---------------------------------------------------------------

    /// `gateBearing` is the angle (radians, anticlockwise from +X seen from above) of the gate.
    public static void Fence(ClubParts p, float g, float gateBearing)
    {
        float r = ClubGroundLayout.FenceRadius, h = ClubGroundLayout.FenceHeight;
        float half = GateWidth * 0.5f / r;
        int segs = Mathf.RoundToInt(FenceSegments * (1f - half / Mathf.PI));
        Band(p.Fence, r, g - 0.02f, g + h, gateBearing + half, gateBearing + Mathf.PI * 2f - half, segs, PicketRepeat, true, 1f);
        Vector3 a = new Vector3(Mathf.Cos(gateBearing - half) * r, g, Mathf.Sin(gateBearing - half) * r);
        Vector3 b = new Vector3(Mathf.Cos(gateBearing + half) * r, g, Mathf.Sin(gateBearing + half) * r);
        Frame w = Frame.World;
        foreach (Vector3 post in new[] { a, b })
            Box(p.Structures, w, post + Vector3.up * 0.55f, new Vector3(0.12f, 1.1f, 0.12f), ClubCell.WhitePaint);
        Vector3 mid = (a + b) * 0.5f;
        GateLeaf(p.Structures, a, mid, h);
        GateLeaf(p.Structures, b, mid, h);
        for (int i = 0; i < FenceColliders; i++)
        {
            float ang = (i + 0.5f) * Mathf.PI * 2f / FenceColliders;
            Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            float width = 2f * r * Mathf.Tan(Mathf.PI / FenceColliders) * 1.04f;
            p.FenceColliders.Add(new ClubBox
            {
                name = "Club Fence Collider " + i,
                centre = radial * (r + 0.1f) + Vector3.up * (g + 0.5f),
                yawDeg = Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg,     // forward = outward
                size = new Vector3(width, 1f, 0.3f),
            });
        }
        TubeRing(p.Structures, ClubGroundLayout.RopeRadius, g + 0.035f, 0.035f, 3, FenceSegments, ClubCell.WhitePaint);
        CircleMarkers(p.Structures, g);
    }

    /// A five-bar gate leaf hung from `hinge` to `latch`, closed across the gap: a ball that runs
    /// to the gate stops at it (its collider is the fence's), so it is drawn shut.
    static void GateLeaf(MeshData md, Vector3 hinge, Vector3 latch, float h)
    {
        Frame w = Frame.World;
        foreach (float y in new[] { 0.15f, 0.36f, 0.57f, 0.78f, h - 0.04f })
            Beam(md, w, hinge + Vector3.up * y, latch + Vector3.up * y, 0.05f, ClubCell.WhitePaint);
        Beam(md, w, hinge, hinge + Vector3.up * h, 0.07f, ClubCell.WhitePaint);
        Beam(md, w, latch, latch + Vector3.up * h, 0.06f, ClubCell.WhitePaint);
        Beam(md, w, hinge + Vector3.up * 0.15f, latch + Vector3.up * (h - 0.04f), 0.05f, ClubCell.WhitePaint);
    }

    /// White discs round the inner circle, which is the Law's oval: FieldSets.RingRadius from the
    /// segment between the two middle stumps, so straight alongside the pitch and round at the ends.
    static void CircleMarkers(MeshData md, float g)
    {
        const int Count = 28;
        float r = FieldSets.RingRadius, x0 = BallDelivery.BowlerStumpsX, x1 = BallDelivery.BatsmanStumpsX;
        float straight = x1 - x0, perimeter = 2f * straight + 2f * Mathf.PI * r;
        for (int i = 0; i < Count; i++)
        {
            Vector3 c = OvalPoint(i * perimeter / Count, r, x0, x1);
            NGon(md, Frame.World, c + Vector3.up * (g + 0.012f), Vector3.right, Vector3.forward, 0.2f, 8, ClubCell.WhitePaint);
        }
    }

    /// The point `u` metres round the oval, starting at (x0, R) and heading +X.
    public static Vector3 OvalPoint(float u, float r, float x0, float x1)
    {
        float straight = x1 - x0, arc = Mathf.PI * r;
        if (u < straight) return new Vector3(x0 + u, 0f, r);
        u -= straight;
        if (u < arc) { float a = Mathf.PI * 0.5f - u / r; return new Vector3(x1 + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); }
        u -= arc;
        if (u < straight) return new Vector3(x1 - u, 0f, -r);
        u -= straight;
        float b = -Mathf.PI * 0.5f - u / r;
        return new Vector3(x0 + Mathf.Cos(b) * r, 0f, Mathf.Sin(b) * r);
    }

    // ---- Benches, roller, flagpole ------------------------------------------------------------------

    /// A park bench, green iron ends and wooden slats, `seats` places along it.
    public static void Bench(ClubParts p, Frame fr, float length, int seats)
    {
        MeshData md = p.Structures;
        float half = length * 0.5f - 0.12f;
        foreach (float x in new[] { -half, half })
        {
            Box(md, fr, new Vector3(x, 0.225f, 0.14f), new Vector3(0.06f, 0.45f, 0.06f), ClubCell.GreenPaint);
            Box(md, fr, new Vector3(x, 0.45f, -0.19f), new Vector3(0.06f, 0.9f, 0.06f), ClubCell.GreenPaint);
            Box(md, fr, new Vector3(x, 0.67f, -0.02f), new Vector3(0.06f, 0.05f, 0.44f), ClubCell.GreenPaint);
        }
        Box(md, fr, new Vector3(0f, 0.45f, 0f), new Vector3(length, 0.05f, 0.42f), ClubCell.BenchSlats);
        float tilt = 12f * Mathf.Deg2Rad;
        Frame back = fr.Child(new Vector3(0f, 0.72f, -0.21f),
                              new Vector3(0f, Mathf.Sin(tilt), Mathf.Cos(tilt)), new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt)));
        Box(md, back, Vector3.zero, new Vector3(length, 0.38f, 0.04f), ClubCell.BenchSlats);
        for (int k = 0; k < seats; k++)
        {
            float x = (k - (seats - 1) * 0.5f) * 0.75f;
            p.Seats.Add(new ClubSeat { pos = fr.P(x, 0.475f, 0.03f), facing = fr.f });
        }
    }

    /// The heavy roller: an iron drum on a green frame with its long U handle.
    public static void Roller(MeshData md, Frame fr)
    {
        Cylinder(md, fr, new Vector3(-0.9f, 0.6f, 0f), new Vector3(0.9f, 0.6f, 0f), 0.6f, 0.6f, 14, ClubCell.RollerIron, true, true);
        Beam(md, fr, new Vector3(-1.06f, 0.6f, 0f), new Vector3(1.06f, 0.6f, 0f), 0.08f, ClubCell.GreenPaint);
        foreach (float x in new[] { -0.98f, 0.98f })
        {
            Box(md, fr, new Vector3(x, 0.6f, 0f), new Vector3(0.05f, 0.5f, 0.5f), ClubCell.GreenPaint);
            Beam(md, fr, new Vector3(x, 0.6f, 0f), new Vector3(x * 0.36f, 1f, -1.9f), 0.06f, ClubCell.GreenPaint);
        }
        Beam(md, fr, new Vector3(-0.45f, 1f, -1.9f), new Vector3(0.45f, 1f, -1.9f), 0.06f, ClubCell.GreenPaint);
    }

    /// A white flagpole; records where Club Flag hangs (hoist edge at the pole, facing `toward`).
    public static void Flagpole(ClubParts p, Vector3 foot, Vector3 toward)
    {
        const float Height = 9f;
        Frame fr = Frame.Yaw(foot, 0f);
        Cylinder(p.Structures, fr, Vector3.zero, Vector3.up * Height, 0.075f, 0.045f, 8, ClubCell.WhitePaint);
        Box(p.Structures, fr, new Vector3(0f, Height + 0.07f, 0f), Vector3.one * 0.14f, ClubCell.WhitePaint);
        Box(p.Structures, fr, new Vector3(0f, 0.15f, 0f), new Vector3(0.5f, 0.3f, 0.5f), ClubCell.WhitePaint);
        Vector3 face = new Vector3(toward.x - foot.x, 0f, toward.z - foot.z).normalized;
        Vector3 fly = Vector3.Cross(Vector3.up, -face);    // the flag's local +X (see ClubGroundBuilder's Flag)
        p.FlagFacing = face;
        p.FlagHoist = foot + Vector3.up * (Height - 0.1f - 0.9f) + fly * 0.06f;
    }

    // ---- Sightscreens ----------------------------------------------------------------------------

    /// The white sightscreen behind the batter: four boards on a green frame with raking struts.
    public static void RearSightscreen(MeshData md, Frame fr)
    {
        foreach (float x in new[] { -5.2f, -1.73f, 1.73f, 5.2f })
            Box(md, fr, new Vector3(x, 3.1f, 0f), new Vector3(3.42f, 5f, 0.08f), ClubCell.WhitePaint);
        foreach (float x in new[] { -7f, -3.5f, 0f, 3.5f, 7f })
        {
            Box(md, fr, new Vector3(x, 2.9f, -0.15f), new Vector3(0.14f, 5.8f, 0.14f), ClubCell.GreenPaint);
            Beam(md, fr, new Vector3(x, 4.6f, -0.15f), new Vector3(x, 0f, -2.2f), 0.1f, ClubCell.GreenPaint);
            Beam(md, fr, new Vector3(x, 0.06f, 0.3f), new Vector3(x, 0.06f, -2.3f), 0.12f, ClubCell.GreenPaint);
        }
        Beam(md, fr, new Vector3(-7f, 0.35f, -0.15f), new Vector3(7f, 0.35f, -0.15f), 0.12f, ClubCell.GreenPaint);
    }

    /// A frame behind the black runtime sightscreen, so it stands on something: posts, top and
    /// bottom rails, struts raking back. Kept under SightscreenFrameTop and ReplayCamClear from the
    /// replay camera that sits just above the screen.
    public static void ScreenFrame(MeshData md, float g)
    {
        Frame w = Frame.World;
        float x = ScreenBackX - 0.25f, top = g + ScreenHeight + 0.05f;
        foreach (float z in new[] { -ScreenHalfWidth - 0.1f, -4.55f, 0f, 4.55f, ScreenHalfWidth + 0.1f })
        {
            Box(md, w, new Vector3(x, (g + top) * 0.5f, z), new Vector3(0.15f, top - g, 0.15f), ClubCell.GreenPaint);
            Beam(md, w, new Vector3(x - 0.05f, g + 6f, z), new Vector3(x - 2.8f, g, z), 0.12f, ClubCell.GreenPaint);
        }
        foreach (float y in new[] { g + 0.3f, top - 0.1f })
            Beam(md, w, new Vector3(x, y, -ScreenHalfWidth - 0.1f), new Vector3(x, y, ScreenHalfWidth + 0.1f), 0.14f, ClubCell.GreenPaint);
    }

    // ---- Floodlight pylons -------------------------------------------------------------------------

    /// Four pylons whose lamp banks sit just behind ClubGroundLayout's glow points, facing the
    /// field as StadiumLighting's glow quads do (toward (0, 2, 0)), so at night the glow sits on the
    /// bank's face. Each pole gets a collider.
    public static void Pylons(ClubParts p, Vector3[] heads, float g)
    {
        MeshData md = p.Structures;
        for (int i = 0; i < heads.Length; i++)
        {
            Vector3 h = heads[i], n = (new Vector3(0f, 2f, 0f) - h).normalized;
            Vector3 c = h - n * 0.35f;
            Frame bank = Frame.Facing(c, n, Vector3.up);
            Box(md, bank, Vector3.zero, new Vector3(4.4f, 2.9f, 0.35f), ClubCell.RollerIron, ClubCell.RollerIron, true);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    float x = -1.5f + col, y = -0.9f + row * 0.9f;
                    Panel(md, bank, new Vector3(0f, 0f, 0.185f), Vector3.left, Vector3.up, x - 0.4f, x + 0.4f, y - 0.35f, y + 0.35f, ClubCell.WhitePaint);
                }
            Vector3 nh = new Vector3(n.x, 0f, n.z).normalized;
            Vector3 foot = new Vector3(c.x, g, c.z) - nh * 0.45f;
            Vector3 top = new Vector3(foot.x, c.y - 0.2f, foot.z);
            Frame w = Frame.World;
            Cylinder(md, w, foot, top, 0.42f, 0.2f, 8, ClubCell.RollerIron);
            Box(md, w, foot + Vector3.up * 0.25f, new Vector3(1.3f, 0.6f, 1.3f), ClubCell.WhitePaint);
            Beam(md, w, top + Vector3.down * 0.4f, c - n * 0.17f, 0.15f, ClubCell.RollerIron);
            float poleH = top.y - g;
            p.Colliders.Add(new ClubBox
            {
                name = "Club Pylon Collider " + i, centre = foot + Vector3.up * (poleH * 0.5f), yawDeg = 0f,
                size = new Vector3(0.9f, poleH, 0.9f),
            });
        }
    }
}
