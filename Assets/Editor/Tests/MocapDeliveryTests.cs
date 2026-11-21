using NUnit.Framework;

/// A captured action bowled by a medium-pacer (MocapDelivery.AsMedium): his own mark, run-up and
/// action speed when the clip's sidecar has medium values, the quick's otherwise.
public class MocapDeliveryTests
{
    private static MocapDelivery.Delivery Quick(MocapDelivery.Variant medium) => new MocapDelivery.Delivery
    {
        state = "Clip", action = 7, markForward = 1.1f, markWide = 0.6f, leftBack = 1.9f, runPace = 1f,
        actionSpeed = 1f, medium = medium,
    };

    [Test]
    public void MediumValuesReplaceTheQuicks()
    {
        var m = MocapDelivery.AsMedium(Quick(new MocapDelivery.Variant
        {
            markForward = 0.4f, markWide = 0.2f, leftBack = 1.2f, runPace = 0.9f, actionSpeed = 0.89f,
        }));
        Assert.AreEqual(0.4f, m.markForward, 1e-6f);
        Assert.AreEqual(0.2f, m.markWide, 1e-6f);
        Assert.AreEqual(1.2f, m.leftBack, 1e-6f);
        Assert.AreEqual(0.9f, m.runPace, 1e-6f);
        Assert.AreEqual(0.89f, m.actionSpeed, 1e-6f);
        Assert.AreEqual("Clip", m.state);
        Assert.AreEqual(7, m.action);
    }

    [Test]
    public void WithoutMediumValuesAMediumPacerBowlsItAsAQuick()
    {
        var m = MocapDelivery.AsMedium(Quick(null));
        Assert.AreEqual(1.1f, m.markForward, 1e-6f);
        Assert.AreEqual(1f, m.runPace, 1e-6f);
        Assert.AreEqual(1f, m.actionSpeed, 1e-6f);
    }

    /// With one clip, a bowler's draw is his own action or the clip, half and half; every choice
    /// comes up when there are more.
    [Test]
    public void TheDrawIsHisOwnActionOrAClipEquallyOften()
    {
        int own = 0, clip = 0;
        for (int roll = 0; roll < 1000; roll++)
            if (MocapDelivery.Pick(1, 1, roll) < 0) own++; else clip++;
        Assert.AreEqual(500, own);
        Assert.AreEqual(500, clip);
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int roll = 0; roll < 30; roll++)
            seen.Add(MocapDelivery.Pick(1, 2, roll));
        CollectionAssert.AreEquivalent(new[] { -1, 0, 1 }, seen);
    }

    [Test]
    public void NoClipsMeansHisOwnAction()
    {
        Assert.AreEqual(-1, MocapDelivery.Pick(1, 0, 7));
        Assert.AreEqual(-1, MocapDelivery.Pick(0, 0, 7));
    }

    /// Forced (MocapDelivery.Force true: no choice of his own) a draw always lands on a clip, and a
    /// negative roll is a draw like any other.
    [Test]
    public void ForcedAlwaysAClip()
    {
        for (int roll = -5; roll < 50; roll++)
            Assert.GreaterOrEqual(MocapDelivery.Pick(0, 2, roll), 0);
        Assert.GreaterOrEqual(MocapDelivery.Pick(1, 1, int.MinValue), -1);
    }

    /// Each bowler draws when he is made, so a side of several gets a mix.
    [Test]
    public void BowlersDrawTheirOwn()
    {
        var rolls = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 8; i++)
            rolls.Add(new Bowler("B" + i, eBowlerKind.Fast).ActionRoll);
        Assert.Greater(rolls.Count, 1);
    }

    /// The clips in the project: every one loads with an action speed, and a medium variant only
    /// when its sidecar has a medium run-up pace.
    [Test]
    public void SidecarsLoad()
    {
        MocapDelivery.Reload();
        foreach (MocapDelivery.Delivery d in MocapDelivery.Captured)
        {
            Assert.Greater(d.actionSpeed, 0f, d.state);
            if (d.medium != null)
            {
                Assert.Greater(d.medium.runPace, 0f, d.state);
                Assert.Greater(d.medium.actionSpeed, 0f, d.state);
            }
        }
    }
}
