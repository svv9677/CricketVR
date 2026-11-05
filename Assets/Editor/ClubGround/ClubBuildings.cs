using UnityEngine;
using static ClubMeshKit;

/// <summary>
/// The pavilion and the score hut, built into the structures batch (one atlas material) in the
/// frame of their ClubGroundLayout spot: origin on the ground at the footprint's centre, local +Z
/// toward the batter, local X across the footprint (its width), local Z along it (its depth).
///
/// Pavilion: a white weatherboard block on a brick plinth under a pitched tiled roof, a clock
/// cupola on the ridge, a green name board for the club, and a veranda along the front: boarded
/// deck, white posts and rails, a green lean-to roof, deckchairs. Score hut: the same walls with
/// the scoreboard across its face (ClubScoreboardBoard: a green board of tins in a white frame,
/// where ClubScoreboard's texts go).
/// </summary>
public static class ClubBuildings
{
    const float PavilionWall = 4.5f, PavilionPlinth = 0.6f, PavilionPitch = 35f, PavilionEave = 0.4f;
    /// Veranda deck height, and its lean-to roof at the posts and at the wall.
    const float Deck = 0.45f, VerandaFront = 2.95f, VerandaWall = 3.35f;
    const float HutPlinth = 0.5f, HutPitch = 30f, HutEave = 0.3f;
    /// The board the club's name is lettered on, above the veranda roof (seen over it from the crease).
    const float BoardBottom = 3.45f, BoardTop = 4.2f;

    // ---- Pavilion -------------------------------------------------------------------------------

    public static void Pavilion(ClubParts p, Frame fr, Vector2 footprint)
    {
        float w = footprint.x, d = footprint.y;
        float back = -d * 0.5f, front = back + d * 0.72f, deckEnd = d * 0.5f - 0.3f;
        MeshData md = p.Structures;
        Walls(md, fr, w, back, front, PavilionWall, PavilionPlinth);
        float ridge = GableRoof(md, fr, w, back, front, PavilionWall, PavilionPitch, PavilionEave, ClubCell.RoofTile);
        PavilionOpenings(p, fr, w, back, front);
        Veranda(p, fr, w, front, deckEnd, d * 0.5f);
        Cupola(md, fr, (back + front) * 0.5f, ridge, PavilionPitch);
        // The name board: green, white-edged, between the veranda roof and the eaves.
        Panel(md, fr, new Vector3(0f, 0f, front + 0.025f), Vector3.left, Vector3.up, -5.6f, 5.6f, BoardBottom - 0.07f, BoardTop + 0.07f, ClubCell.WhitePaint);
        Panel(md, fr, new Vector3(0f, 0f, front + 0.035f), Vector3.left, Vector3.up, -5.5f, 5.5f, BoardBottom, BoardTop, ClubCell.GreenPaint);
        p.ClubName = new ClubLabel
        {
            pos = fr.P(0f, (BoardBottom + BoardTop) * 0.5f, front + 0.06f),
            facing = fr.f,
            rect = new Vector2(10.6f, BoardTop - BoardBottom - 0.1f),
        };
        p.Colliders.Add(new ClubBox
        {
            name = "Club Pavilion Collider", centre = fr.P(0f, ridge * 0.5f, 0f), yawDeg = fr.YawDeg,
            size = new Vector3(w, ridge, d),
        });
    }

    /// Windows round the block, double doors in the middle of the front; every pane is repeated in
    /// the night batch, lit warm, a centimetre proud of the day pane.
    static void PavilionOpenings(ClubParts p, Frame fr, float w, float back, float front)
    {
        float k = w / 22f, cz = (back + front) * 0.5f, hd = (front - back) * 0.5f;
        Vector3 fo = new Vector3(0f, 0f, front + 0.02f);
        foreach (float x in new[] { -8.6f, -6f, -3.4f, 3.4f, 6f, 8.6f })
            Window(p, fr, fo, Vector3.left, x * k, Deck + 0.8f, 1.3f, 1.6f);
        Panel(p.Structures, fr, fo, Vector3.left, Vector3.up, -0.95f, 0.95f, Deck, Deck + 2.3f, ClubCell.GreenPaint);
        foreach (float x in new[] { -0.45f, 0.45f })
            Window(p, fr, fo + Vector3.forward * 0.005f, Vector3.left, x, Deck + 1.2f, 0.6f, 0.9f);
        foreach (float z in new[] { -hd * 0.5f, hd * 0.5f })
        {
            Window(p, fr, new Vector3(w * 0.5f + 0.02f, 0f, cz), Vector3.forward, z, Deck + 0.8f, 1.2f, 1.5f);
            Window(p, fr, new Vector3(-w * 0.5f - 0.02f, 0f, cz), Vector3.back, z, Deck + 0.8f, 1.2f, 1.5f);
        }
        foreach (float x in new[] { -7f, -2.5f, 2.5f, 7f })
            Window(p, fr, new Vector3(0f, 0f, back - 0.02f), Vector3.right, x * k, Deck + 0.8f, 1.2f, 1.5f);
    }

    /// One pane on a wall plane (origin on the wall, `s` its right): by day, and lit for the night.
    static void Window(ClubParts p, Frame fr, Vector3 origin, Vector3 s, float cx, float y0, float w, float h)
    {
        Panel(p.Structures, fr, origin, s, Vector3.up, cx - w * 0.5f, cx + w * 0.5f, y0, y0 + h, ClubCell.Window);
        Vector3 n = Vector3.Cross(Vector3.up, s);
        Panel(p.Night, fr, origin + n * 0.01f, s, Vector3.up, cx - w * 0.5f, cx + w * 0.5f, y0, y0 + h, ClubCell.Window);
    }

    static void Veranda(ClubParts p, Frame fr, float w, float wallZ, float deckEnd, float stepEnd)
    {
        MeshData md = p.Structures;
        Box(md, fr, new Vector3(0f, (Deck - 0.05f) * 0.5f, (wallZ + deckEnd) * 0.5f),
            new Vector3(w, Deck + 0.05f, deckEnd - wallZ), ClubCell.Brick, ClubCell.VerandaBoards);
        Box(md, fr, new Vector3(0f, 0.1f, (deckEnd + stepEnd) * 0.5f),
            new Vector3(3.2f, 0.3f, stepEnd - deckEnd), ClubCell.Brick, ClubCell.VerandaBoards);
        // Posts: the outermost at the corners, one pair flanking the steps, two between on each side.
        float outer = w * 0.5f - 0.3f, inner = 1.5f, postZ = deckEnd - 0.15f;
        var xs = new float[8];
        for (int i = 0; i < 4; i++)
        {
            xs[4 + i] = inner + (outer - inner) * i / 3f;
            xs[3 - i] = -xs[4 + i];
        }
        foreach (float x in xs)
            Box(md, fr, new Vector3(x, (Deck + VerandaFront) * 0.5f, postZ), new Vector3(0.16f, VerandaFront - Deck, 0.16f), ClubCell.WhitePaint);
        for (int i = 0; i + 1 < xs.Length; i++)
        {
            if (i == 3) continue;                       // the opening onto the steps
            foreach (float y in new[] { Deck + 0.9f, Deck + 0.45f })
                Beam(md, fr, new Vector3(xs[i], y, postZ), new Vector3(xs[i + 1], y, postZ), 0.07f, ClubCell.WhitePaint);
        }
        float run = deckEnd + 0.25f - wallZ;
        RoofSlope(md, fr, w * 0.5f + 0.1f, wallZ, VerandaWall, run, (VerandaWall - VerandaFront) / run, 1f, ClubCell.GreenPaint);
        foreach (float x in new[] { -8f, -6.2f, -4.4f, -2.6f, 2.6f, 4.4f, 6.2f, 8f })
            Deckchair(p, fr.Child(new Vector3(x * w / 22f, Deck, wallZ + 1.0f), 0f));
    }

    /// A striped canvas deckchair on a wooden frame, its sitter's place recorded as a seat.
    public static void Deckchair(ClubParts p, Frame fr)
    {
        MeshData md = p.Structures;
        Vector3 t = new Vector3(0f, 0.6f, -0.65f).normalized;
        Panel(md, fr, new Vector3(0f, 0.3f, 0.3f), Vector3.left, t, -0.25f, 0.25f, 0f, 0.885f, ClubCell.Deckchair);
        foreach (float x in new[] { -0.29f, 0.29f })
        {
            Beam(md, fr, new Vector3(x, 0f, 0.4f), new Vector3(x, 0.95f, -0.4f), 0.035f, ClubCell.BenchSlats);
            Beam(md, fr, new Vector3(x, 0f, -0.35f), new Vector3(x, 0.45f, 0.25f), 0.035f, ClubCell.BenchSlats);
        }
        Beam(md, fr, new Vector3(-0.29f, 0.3f, 0.3f), new Vector3(0.29f, 0.3f, 0.3f), 0.035f, ClubCell.BenchSlats);
        p.Seats.Add(new ClubSeat { pos = fr.P(0f, 0.3f, 0.12f), facing = fr.f });
    }

    /// A clock cupola astride the ridge: weatherboard box, green pyramid cap, clock at 10 past 10.
    static void Cupola(MeshData md, Frame fr, float z, float ridge, float pitchDeg)
    {
        const float Half = 0.8f, Top = 1.25f;
        float sink = Half * Mathf.Tan(pitchDeg * Mathf.Deg2Rad) + 0.3f;
        Box(md, fr, new Vector3(0f, ridge + (Top - sink) * 0.5f, z), new Vector3(Half * 2f, Top + sink, Half * 2f),
            ClubCell.Weatherboard, ClubCell.WhitePaint);
        Pyramid(md, fr, new Vector3(0f, ridge + Top, z), Half + 0.15f, 0.95f, ClubCell.GreenPaint);
        float cy = ridge + 0.35f;
        Vector3 face = new Vector3(0f, cy, z + Half);
        NGon(md, fr, face + Vector3.forward * 0.01f, Vector3.left, Vector3.up, 0.62f, 20, ClubCell.ScorePlate);
        NGon(md, fr, face + Vector3.forward * 0.02f, Vector3.left, Vector3.up, 0.55f, 20, ClubCell.WhitePaint);
        Hand(md, fr, face + Vector3.forward * 0.03f, -60f, 0.3f, 0.06f);   // hour hand on the 10
        Hand(md, fr, face + Vector3.forward * 0.035f, 60f, 0.45f, 0.04f);  // minute hand on the 2
    }

    /// A clock hand from the face's centre, `angleDeg` clockwise from 12 as seen from the front.
    static void Hand(MeshData md, Frame fr, Vector3 centre, float angleDeg, float len, float width)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector2 along = new Vector2(Mathf.Sin(a), Mathf.Cos(a)), across = new Vector2(along.y, -along.x) * (width * 0.5f);
        Vector2 tail = -along * 0.06f, tip = along * len;
        Poly(md, fr, centre, Vector3.left, Vector3.up,
             new[] { tail - across, tail + across, tip + across, tip - across }, ClubCell.ScorePlate);
    }

    /// A four-sided cap from a square base (half-width `half`) to a point `height` above it.
    static void Pyramid(MeshData md, Frame fr, Vector3 baseCentre, float half, float height, ClubCell cell)
    {
        Vector2 uv = ClubAtlas.UV(cell).center;
        Vector3 apex = fr.P(baseCentre + Vector3.up * height);
        Vector3[] c =
        {
            fr.P(baseCentre + new Vector3(-half, 0f, -half)), fr.P(baseCentre + new Vector3(half, 0f, -half)),
            fr.P(baseCentre + new Vector3(half, 0f, half)), fr.P(baseCentre + new Vector3(-half, 0f, half)),
        };
        Vector3 mid = fr.P(baseCentre);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = c[i], b = c[(i + 1) % 4];
            Vector3 n = Vector3.Cross(b - a, apex - a).normalized;
            if (Vector3.Dot(n, (a + b) * 0.5f - mid) < 0f) n = -n;
            md.Tri(md.Vert(a, n, uv), md.Vert(b, n, uv), md.Vert(apex, n, uv), n);
        }
        Panel(md, fr, baseCentre, Vector3.left, Vector3.forward, -half, half, -half, half, ClubCell.WhitePaint);
    }

    // ---- Score hut -------------------------------------------------------------------------------

    /// The score hut: the same walls as the pavilion, tall enough (ClubScoreboardBoard.WallHeight,
    /// 5.2 m) for the 9 x 4.4 m board across its front, whose tins and texts ClubScoreboardBoard
    /// lays out from ClubScoreboardLayout.
    public static void ScoreHut(ClubParts p, Frame fr, Vector2 footprint)
    {
        float w = footprint.x, d = footprint.y, back = -d * 0.5f, front = d * 0.5f;
        MeshData md = p.Structures;
        float wall = ClubScoreboardBoard.WallHeight;
        Walls(md, fr, w, back, front, wall, HutPlinth);
        float ridge = GableRoof(md, fr, w, back, front, wall, HutPitch, HutEave, ClubCell.RoofTile);
        ClubScoreboardBoard.Build(p, fr, front);
        Panel(md, fr, new Vector3(w * 0.5f + 0.02f, 0f, 0f), Vector3.forward, Vector3.up, -0.5f, 0.5f, 0f, 2.1f, ClubCell.GreenPaint);
        Window(p, fr, new Vector3(0f, 0f, back - 0.02f), Vector3.right, 0f, 1.3f, 1.2f, 1.2f);
        Window(p, fr, new Vector3(-w * 0.5f - 0.02f, 0f, 0f), Vector3.back, 0f, 1.3f, 1.2f, 1.2f);
        p.Colliders.Add(new ClubBox
        {
            name = "Club Score Hut Collider", centre = fr.P(0f, ridge * 0.5f, 0f), yawDeg = fr.YawDeg,
            size = new Vector3(w, ridge, d),
        });
    }

    // ---- Shared shapes ----------------------------------------------------------------------------

    /// The four walls of a block x -w/2..w/2, z back..front: brick to `plinth`, weatherboard to `top`.
    /// Each wall's `s` axis is its right as seen from outside.
    static void Walls(MeshData md, Frame fr, float w, float back, float front, float top, float plinth)
    {
        float hw = w * 0.5f, cz = (back + front) * 0.5f, hd = (front - back) * 0.5f;
        WallFace(md, fr, new Vector3(0f, 0f, front), Vector3.left, hw, top, plinth);
        WallFace(md, fr, new Vector3(0f, 0f, back), Vector3.right, hw, top, plinth);
        WallFace(md, fr, new Vector3(hw, 0f, cz), Vector3.forward, hd, top, plinth);
        WallFace(md, fr, new Vector3(-hw, 0f, cz), Vector3.back, hd, top, plinth);
    }

    static void WallFace(MeshData md, Frame fr, Vector3 origin, Vector3 s, float half, float top, float plinth)
    {
        Panel(md, fr, origin, s, Vector3.up, -half, half, -0.05f, plinth, ClubCell.Brick);
        Panel(md, fr, origin, s, Vector3.up, -half, half, plinth, top, ClubCell.Weatherboard);
    }

    /// A pitched roof over the block, ridge along X: weatherboard gables, two tiled slopes with
    /// white soffits and fascias, overhanging `eave` at the eaves and the verges. Returns the
    /// ridge height.
    static float GableRoof(MeshData md, Frame fr, float w, float back, float front, float top, float pitchDeg, float eave, ClubCell cell)
    {
        float hd = (front - back) * 0.5f, cz = (back + front) * 0.5f, tan = Mathf.Tan(pitchDeg * Mathf.Deg2Rad);
        float ridge = top + hd * tan;
        var gable = new[] { new Vector2(-hd, top), new Vector2(hd, top), new Vector2(0f, ridge) };
        Poly(md, fr, new Vector3(w * 0.5f, 0f, cz), Vector3.forward, Vector3.up, gable, ClubCell.Weatherboard);
        Poly(md, fr, new Vector3(-w * 0.5f, 0f, cz), Vector3.back, Vector3.up, gable, ClubCell.Weatherboard);
        RoofSlope(md, fr, w * 0.5f + eave, cz, ridge, hd + eave, tan, 1f, cell);
        RoofSlope(md, fr, w * 0.5f + eave, cz, ridge, hd + eave, tan, -1f, cell);
        Box(md, fr, new Vector3(0f, ridge + 0.04f, cz), new Vector3(w + eave * 2f + 0.05f, 0.14f, 0.3f), cell);
        return ridge;
    }

    /// One roof slope falling from its high edge (z = highZ, y = highY) for `run` metres toward
    /// local +Z (dir 1) or -Z (dir -1) at `tan` rise per metre, `halfW` either side of the middle:
    /// tiled top, white underside, eave fascia and verge edges, 0.12 m thick.
    static void RoofSlope(MeshData md, Frame fr, float halfW, float highZ, float highY, float run, float tan, float dir, ClubCell cell)
    {
        const float Thick = 0.12f;
        float lowZ = highZ + dir * run, lowY = highY - run * tan;
        Vector3 t = new Vector3(0f, tan, -dir).normalized;
        Vector3 s = dir > 0f ? Vector3.left : Vector3.right;
        Vector3 n = Vector3.Cross(t, s);
        float len = run * Mathf.Sqrt(1f + tan * tan);
        Vector3 low = new Vector3(0f, lowY + 0.02f, lowZ);
        Panel(md, fr, low, s, t, -halfW, halfW, 0f, len, cell);
        Panel(md, fr, low - n * Thick, -s, t, -halfW, halfW, 0f, len, ClubCell.WhitePaint);
        Panel(md, fr, new Vector3(0f, lowY, lowZ), s, Vector3.up, -halfW, halfW, -0.2f, 0.02f, ClubCell.WhitePaint);
        // The verges: the slab's cut edge at each end, as tall as the slab is thick measured upright.
        Vector2 drop = new Vector2(0f, -Thick * Mathf.Sqrt(1f + tan * tan));
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 vs = side > 0f ? Vector3.forward : Vector3.back;   // the verge's right, seen from outside
            Vector2 a = new Vector2(side * lowZ, lowY + 0.02f), b = new Vector2(side * highZ, highY + 0.02f);
            Poly(md, fr, new Vector3(side * halfW, 0f, 0f), vs, Vector3.up, new[] { a, b, b + drop, a + drop }, ClubCell.WhitePaint);
        }
    }
}
