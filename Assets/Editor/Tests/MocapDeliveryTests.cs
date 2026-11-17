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
