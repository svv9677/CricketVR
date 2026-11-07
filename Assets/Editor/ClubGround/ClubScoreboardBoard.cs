using UnityEngine;
using static ClubMeshKit;

/// <summary>
/// The score hut's board as geometry, from ClubScoreboardLayout: a white frame (the hut's trim),
/// the green plate the labels are painted on, and a tin behind every figure - dark tins under
/// cream figures, light tins under the batters' numbers - all in the structures batch, so the
/// board adds triangles (about 60) and no draw call. Also where its two texts go (ClubParts.Figures
/// for the labels and figures, ScoreHeader for the home side's name).
///
/// The hut's frame (ClubBuildings): local +Z toward the batter, so a point x across the board from
/// its left edge, as the batter sees it, is local x = Width / 2 - x (his left is local +X).
/// </summary>
public static class ClubScoreboardBoard
{
    /// The plate's bottom edge above the ground, and the weatherboard left above the frame.
    /// Raised (2026-11-06: "too far away and some of it is obscured"): at 0.5 m the fence, the
    /// benches and the fielders hid its lower bands from the crease; at 2.6 m all of it clears them.
    public const float PlateBottom = 2.6f, AboveFrame = 0.2f, FrameBorder = 0.1f;
    public static float PlateTop => PlateBottom + ClubScoreboardLayout.Height;
    /// The hut's walls: the frame's top, then a band of weatherboard under the eaves.
    public static float WallHeight => PlateTop + FrameBorder + AboveFrame;
    /// Out from the hut's front wall: the frame's depth, then the plate, the tins and the texts,
    /// each clear of the one behind by a centimetre or more (no z-fighting at 73 m).
    const float FrameDepth = 0.08f, PlateZ = 0.09f, TinZ = 0.11f, TextZ = 0.14f;

    public static void Build(ClubParts p, Frame fr, float front)
    {
        MeshData md = p.Structures;
        float w = ClubScoreboardLayout.Width, h = ClubScoreboardLayout.Height, cy = PlateBottom + h * 0.5f;
        Box(md, fr, new Vector3(0f, cy, front + FrameDepth * 0.5f),
            new Vector3(w + 2f * FrameBorder, h + 2f * FrameBorder, FrameDepth), ClubCell.WhitePaint);
        Plate(md, fr, front + PlateZ, 0f, w, 0f, h, ClubCell.GreenPaint);
        foreach (ClubScoreboardLayout.Field f in ClubScoreboardLayout.Fields)
        {
            if (f.tin == ClubScoreboardLayout.Tin.None) continue;
            ClubScoreboardLayout.Row r = ClubScoreboardLayout.Rows[f.row];
            ClubCell cell = f.tin == ClubScoreboardLayout.Tin.Light ? ClubCell.WhitePaint : ClubCell.ScorePlate;
            for (int i = 0; i < f.cells; i++)
            {
                float x = f.left + (i + 0.5f) * r.pitch;
                Plate(md, fr, front + TinZ, x - r.tinWidth * 0.5f, x + r.tinWidth * 0.5f, r.tinTop, r.tinBottom, cell);
            }
        }
        p.Figures = FiguresText(fr, front);
        p.ScoreHeader = HeaderText(fr, front);
    }

    /// A board point (x from its left as the batter sees it, y down from its top) in the hut's frame,
    /// `z` out from the front wall.
    public static Vector3 Local(float x, float y, float z) =>
        new Vector3(ClubScoreboardLayout.Width * 0.5f - x, PlateTop - y, z);

    /// The labels-and-figures text: aligned BaselineLeft, so its first line's baseline is the rect's
    /// middle (TMP's Baseline anchor) and its lines start at the rect's left edge, the board's x = 0.
    /// The rect is centred on the first band's label baseline; it reaches from the plate's top.
    static ClubLabel FiguresText(Frame fr, float front)
    {
        float first = ClubScoreboardLayout.Rows[0].labelBaseline;
        return new ClubLabel
        {
            pos = fr.P(Local(ClubScoreboardLayout.Width * 0.5f, first, front + TextZ)), facing = fr.f,
            rect = new Vector2(ClubScoreboardLayout.Width, 2f * first),
        };
    }

    /// The home side's name, centred in the header band, the band's margins clear either side.
    static ClubLabel HeaderText(Frame fr, float front)
    {
        float y = ClubScoreboardLayout.HeaderTop + ClubScoreboardLayout.HeaderHeight * 0.5f;
        return new ClubLabel
        {
            pos = fr.P(Local(ClubScoreboardLayout.Width * 0.5f, y, front + TextZ)), facing = fr.f,
            rect = new Vector2(ClubScoreboardLayout.Width - 2f * ClubScoreboardLayout.HeaderMargin, ClubScoreboardLayout.HeaderHeight),
        };
    }

    /// A rectangle on the board, x0..x1 across and y0..y1 down (board metres), facing the batter.
    static void Plate(MeshData md, Frame fr, float z, float x0, float x1, float y0, float y1, ClubCell cell)
    {
        // Panel's s axis is Vector3.left (the batter's right): s = -local x = board x - Width / 2.
        float half = ClubScoreboardLayout.Width * 0.5f;
        Panel(md, fr, new Vector3(0f, 0f, z), Vector3.left, Vector3.up, x0 - half, x1 - half, PlateTop - y1, PlateTop - y0, cell);
    }
}
