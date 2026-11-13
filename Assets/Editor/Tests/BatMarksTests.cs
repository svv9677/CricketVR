using NUnit.Framework;
using UnityEngine;

/// The ball's prints on the bat (BatMarks, 2026-11-09): skewed along the way the ball slid, in its
/// leather, faint one at a time.
public class BatMarksTests
{
    [Test]
    public void ABallMetSquareOnPrintsRoundAndAGlanceSmears()
    {
        Assert.AreEqual(1f, BatMarks.SmearOf(0f, 20f), 1e-4f, "square on: round");
        float slight = BatMarks.SmearOf(5f, 20f), glance = BatMarks.SmearOf(15f, 8f);
        Assert.Greater(slight, 1f);
        Assert.Greater(glance, slight, "the more it slid for how hard it came in, the longer");
        Assert.AreEqual(BatMarks.LongestSmear, BatMarks.SmearOf(40f, 1f), 1e-4f, "never longer than the longest");
    }

    [Test]
    public void APrintIsFaintAndTheBallsColour()
    {
        Assert.Less(BatMarks.Strongest, 0.15f, "one print alone is barely there");
        Assert.Less(BatMarks.Faintest, BatMarks.Strongest);
        // Outside a quick-play Test it is the white ball.
        if (!TestLook.Applies)
        {
            Color c = BatMarks.LeatherNow();
            Assert.Greater(Mathf.Min(c.r, c.g, c.b), 0.85f, "white ball, white print");
        }
    }

    [Test]
    public void ThePrintFadesOutAtItsEdge()
    {
        Texture2D t = BatMarks.PrintTexture(true);
        Color32[] px = t.GetPixels32();
        int n = t.width;
        Assert.AreEqual(0, px[0].a, "nothing at the corner");
        Assert.Greater(px[(n / 2) * n + n / 2].a, 60, "something in the middle");
        Object.DestroyImmediate(t);
    }
}
