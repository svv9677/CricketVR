using NUnit.Framework;

/// <summary>
/// ClubPitchSurface: on the match scene's 24 x 6 m pitch quad the club's prepared strip is fully
/// opaque out to the edge lines' outer edge on both sides alike, clear of the outfield within 0.1 m
/// beyond, with no edge noise; and along its length opaque past where the edge lines end. The
/// stadium's settings, for contrast, leave the edge lines on a half-clear fade (what showed at the
/// club as a line on the grass). Also PitchLines builds in the nets as in the match. Standalone.
/// </summary>
public class ClubPitchSurfaceTests
{
    /// Pitch Base: a 2 x 2 quad scaled 12 x 3 (Main/Pitch/Pitch Base in the match scene).
    const float HalfWidth = 3f, HalfLength = 12f;
    /// Main/Pitch sits 5 cm toward the bowler: the edge lines' ends from the quad's middle.
    const float QuadX = -0.05f;
    const float PoppingOut = 1.22f;

    static ClubPitchSurface.Settings Club => ClubPitchSurface.For(HalfWidth, HalfLength);

    static float Across(ClubPitchSurface.Settings s, float z) => ClubPitchSurface.Alpha(s, HalfWidth, HalfLength, z, 0f);

    [Test]
    public void TheStripIsSolidToTheEdgeLinesAndClearJustBeyond()
    {
        ClubPitchSurface.Settings s = Club;
        float lineOuter = PitchLines.PitchHalf + PitchLines.EdgeWidth * 0.5f;
        foreach (float side in new[] { -1f, 1f })
        {
            Assert.AreEqual(1f, Across(s, side * 0f), 1e-5f);
            Assert.AreEqual(1f, Across(s, side * PitchLines.PitchHalf), 1e-5f, "the edge line's middle on clay");
            Assert.AreEqual(1f, Across(s, side * lineOuter), 1e-5f, "and its outer edge");
            Assert.AreEqual(0f, Across(s, side * (lineOuter + 0.1f)), 1e-5f, "grass 10 cm beyond the line");
            Assert.AreEqual(0f, Across(s, side * 2.5f), 1e-5f);
        }
        Assert.AreEqual(Across(s, 1.6f), Across(s, -1.6f), 1e-6f, "the same on both sides");
        Assert.AreEqual(0f, s.edgeNoise, "a clean mown edge, no blotches");
        Assert.Greater(s.fadeY, 0f, "a feathered edge, not a stair-stepped cut");
        Assert.AreEqual(0.5f, s.insetY + s.fadeY + ClubPitchSurface.OpaqueHalfWidth / (2f * HalfWidth), 1e-5f,
                        "clear band + feather + solid half = half the quad");
    }

    [Test]
    public void TheStripRunsSolidPastTheEndsOfTheEdgeLines()
    {
        ClubPitchSurface.Settings s = Club;
        float batterEnd = BallDelivery.BatsmanStumpsX + PoppingOut - QuadX;
        float bowlerEnd = BallDelivery.BowlerStumpsX - PoppingOut - QuadX;
        Assert.AreEqual(1f, ClubPitchSurface.Alpha(s, HalfWidth, HalfLength, PitchLines.PitchHalf, batterEnd), 1e-5f);
        Assert.AreEqual(1f, ClubPitchSurface.Alpha(s, HalfWidth, HalfLength, PitchLines.PitchHalf, bowlerEnd), 1e-5f);
        Assert.AreEqual(0f, ClubPitchSurface.Alpha(s, HalfWidth, HalfLength, 0f, HalfLength), 1e-5f, "the quad's end is clear");
    }

    [Test]
    public void TheStadiumsFadeLeftTheEdgeLinesOnHalfClearTurf()
    {
        // Hard Pitch Blend as saved: _InsetY 0.165, _FadeWidthY 0.16, _FadeWidthX 0.05.
        var stadium = new ClubPitchSurface.Settings { insetY = 0.165f, fadeY = 0.16f, fadeX = 0.05f, edgeNoise = 0.55f };
        float atLine = Across(stadium, PitchLines.PitchHalf);
        Assert.That(atLine, Is.InRange(0.3f, 0.7f), "the edge line on a half-clear fade");
        Assert.Greater(Across(stadium, 1.9f), 0.01f, "and the strip still showing 0.4 m out on the grass");
        Assert.AreEqual(0f, Across(Club, 1.9f), 1e-5f);
    }

    [Test]
    public void PitchLinesAreBuiltInTheMatchAndTheNets()
    {
        Assert.IsTrue(PitchLines.BuildsIn("CricketVR"));
        Assert.IsTrue(PitchLines.BuildsIn(NetsSession.NetsScene));
        Assert.IsFalse(PitchLines.BuildsIn("Splash"));
        Assert.IsFalse(PitchLines.BuildsIn(""));
    }
}
