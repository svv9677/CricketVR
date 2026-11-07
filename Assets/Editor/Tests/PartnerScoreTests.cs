using NUnit.Framework;

/// <summary>
/// PartnerScore: a watched partner's ball scores as his drawn outcome wherever the picture allows,
/// so watching him scores like simulating him (2026-11-06).
/// </summary>
public class PartnerScoreTests
{
    [TearDown]
    public void CleanUp() => PartnerScore.Disarm();

    [Test]
    public void NotArmedTheGameDecides()
    {
        PartnerScore.Disarm();
        Assert.AreEqual(2, PartnerScore.Fielded(2));
        Assert.AreEqual(0, PartnerScore.Missed());
    }

    [Test]
    public void FieldedTheIntendedRunsAreScored()
    {
        for (int intent = 0; intent <= 3; intent++)
        {
            PartnerScore.Arm(intent);
            Assert.AreEqual(intent, PartnerScore.Fielded(0), "fielded quickly");
            Assert.AreEqual(intent, PartnerScore.Fielded(3), "fielded slowly");
        }
        PartnerScore.Arm(4);
        Assert.AreEqual(4, PartnerScore.Fielded(1), "a four he meant, stopped inside the rope: all run");
        PartnerScore.Arm(6);
        Assert.AreEqual(4, PartnerScore.Fielded(2));
    }

    [Test]
    public void AWicketHeMeantIsAWicket()
    {
        PartnerScore.Arm(CareerSim.Wicket);
        Assert.AreEqual(-1, PartnerScore.Fielded(1));
        Assert.AreEqual("run out", PartnerScore.LastHow);
        PartnerScore.Arm(CareerSim.Wicket);
        Assert.AreEqual(-1, PartnerScore.Missed());
        Assert.AreEqual("c wk", PartnerScore.LastHow);
    }

    [Test]
    public void ThroughToTheKeeperIsADotUnlessHeMeantToGo()
    {
        foreach (int intent in new[] { 0, 1, 2, 4, 6 })
        {
            PartnerScore.Arm(intent);
            Assert.AreEqual(0, PartnerScore.Missed(), intent.ToString());
        }
    }

    [Test]
    public void AWideIsTheUmpires()
    {
        PartnerScore.Arm(PartnerIntent.LeaveWide);
        Assert.AreEqual(2, PartnerScore.Fielded(2));
        Assert.AreEqual(0, PartnerScore.Missed());
    }
}
