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
    public void HeRunsWhatTheFieldingAllowsUpToWhatHeMeant()
    {
        PartnerScore.Arm(2);
        Assert.AreEqual(0, PartnerScore.Fielded(0), "fielded in two seconds: no run, though he meant two");
        Assert.AreEqual(1, PartnerScore.Fielded(1), "a one, not a pinched two");
        Assert.AreEqual(2, PartnerScore.Fielded(2));
        Assert.AreEqual(2, PartnerScore.Fielded(3), "no greedy third");
        PartnerScore.Arm(0);
        Assert.AreEqual(0, PartnerScore.Fielded(2), "he meant to block it: no run");
        PartnerScore.Arm(4);
        Assert.AreEqual(2, PartnerScore.Fielded(2), "a four stopped inside the rope: what they can run");
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
