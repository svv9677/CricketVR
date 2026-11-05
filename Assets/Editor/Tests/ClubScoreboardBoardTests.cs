using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The score hut's board as ClubScoreboardBoard builds it into the hut: one tin per figure cell
/// (and nothing for the star), all on the plate and in front of it, facing the batter; the texts'
/// rects where the layout says (the figures' first baseline on the first band's label baseline,
/// the header inside its band); the hut tall enough for it. Built whole without a scene.
/// </summary>
public class ClubScoreboardBoardTests
{
    static int TinCount()
    {
        int n = 0;
        foreach (ClubScoreboardLayout.Field f in ClubScoreboardLayout.Fields)
            if (f.tin != ClubScoreboardLayout.Tin.None) n += f.cells;
        return n;
    }

    [Test]
    public void EveryFigureCellGetsATinOnThePlateFacingTheBatter()
    {
        var p = new ClubParts();
        Frame fr = Frame.Yaw(new Vector3(-56f, 0f, -30f), 65f);
        ClubScoreboardBoard.Build(p, fr, 3f);
        Assert.AreEqual(27, TinCount(), "3 + 2 + 2 + 1, two batters of 2 + 3, and 3 x 3");
        // The frame (a box: five faces, ten triangles), the plate, and two triangles a tin.
        Assert.AreEqual(10 + 2 + 2 * TinCount(), p.Structures.TriangleCount);
        float half = ClubScoreboardLayout.Width * 0.5f + ClubScoreboardBoard.FrameBorder;
        for (int i = 0; i < p.Structures.Verts.Count; i++)
        {
            Vector3 v = p.Structures.Verts[i] - fr.o;
            float across = Vector3.Dot(v, fr.r), up = Vector3.Dot(v, fr.u);
            Assert.That(Mathf.Abs(across), Is.LessThanOrEqualTo(half + 1e-3f));
            Assert.That(up, Is.InRange(ClubScoreboardBoard.PlateBottom - ClubScoreboardBoard.FrameBorder - 1e-3f,
                                       ClubScoreboardBoard.PlateTop + ClubScoreboardBoard.FrameBorder + 1e-3f));
        }
        // The plate and the tins face the batter (local +Z).
        int facing = 0;
        foreach (Vector3 n in p.Structures.Normals)
            if (Vector3.Dot(n, fr.f) > 0.99f) facing++;
        Assert.GreaterOrEqual(facing, 4 * (1 + TinCount()));
    }

    [Test]
    public void TheTextsSitWhereTheLayoutPutsThem()
    {
        var p = new ClubParts();
        Frame fr = Frame.Yaw(new Vector3(10f, 0f, 5f), 30f);
        ClubScoreboardBoard.Build(p, fr, 3f);
        float firstBaseline = ClubScoreboardLayout.Rows[0].labelBaseline;
        Vector3 expect = fr.P(ClubScoreboardBoard.Local(ClubScoreboardLayout.Width * 0.5f, firstBaseline, 0f));
        Vector3 figures = p.Figures.pos - fr.f * Vector3.Dot(p.Figures.pos - expect, fr.f);
        Assert.Less(Vector3.Distance(figures, expect), 1e-3f, "BaselineLeft: the rect's middle is the first baseline");
        Assert.AreEqual(ClubScoreboardLayout.Width, p.Figures.rect.x, 1e-4f, "lines start at the board's left edge");
        Assert.Greater(Vector3.Dot(p.Figures.pos - fr.o, fr.f), 3f + 0.11f, "in front of the tins");

        Vector3 h = p.ScoreHeader.pos - fr.o;
        float up = Vector3.Dot(h, fr.u);
        Assert.That(ClubScoreboardBoard.PlateTop - up, Is.InRange(ClubScoreboardLayout.HeaderTop,
                    ClubScoreboardLayout.HeaderTop + ClubScoreboardLayout.HeaderHeight));
        Assert.LessOrEqual(p.ScoreHeader.rect.x, ClubScoreboardLayout.Width);
        Assert.Greater(Vector3.Dot(p.Figures.facing, fr.f), 0.999f);
        Assert.Greater(Vector3.Dot(p.ScoreHeader.facing, fr.f), 0.999f);
    }

    [Test]
    public void TheHutIsTallEnoughForTheBoard()
    {
        Assert.Greater(ClubScoreboardBoard.WallHeight, ClubScoreboardBoard.PlateTop + ClubScoreboardBoard.FrameBorder);
        ClubParts p = ClubGeometry.Build(ClubAtlas.AllPeople(), false, new StringBuilder());
        ClubBox hut = p.Colliders.Find(b => b.name == "Club Score Hut Collider");
        Assert.Greater(hut.size.y, ClubScoreboardBoard.WallHeight, "the collider reaches the ridge");
        Vector3 to = ClubGroundLayout.BatterEye - p.ScoreHeader.pos;
        to.y = 0f;
        Assert.Greater(Vector3.Dot(p.ScoreHeader.facing, to.normalized), 0.9f, "the board faces the crease");
    }
}
