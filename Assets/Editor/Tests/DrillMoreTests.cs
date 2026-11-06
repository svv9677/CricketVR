using NUnit.Framework;

/// <summary>
/// The second set of drills (DrillDefsMore, DrillScoringMore, DrillTextMore): each scoring table's
/// rows, the coach's new picks for Power and Defence, and a work line for every drill and failing.
/// The structure (twelve valid balls, legal lines, seeded order) is DrillTests', which covers
/// every drill in DrillDefs.All.
/// </summary>
public class DrillMoreTests
{
    private static BallRecord R(eBallAction a = eBallAction.Played, eRealContact c = eRealContact.Clean, float line = 0f,
                                bool hitting = false, float angle = 0f, int runs = 0, float exit = 25f, float launch = 5f,
                                float carry = 5f) =>
        DrillTests.R(a, c, line: line, hitting: hitting, angle: angle, runs: runs, exit: exit, launch: launch, carry: carry);

    private static void Is(eDrill d, BallRecord r, float value, eDrillTag tag, string why)
    {
        DrillBallScore s = DrillScoring.Score(DrillDefs.For(d), r);
        Assert.AreEqual(value, s.value, 1e-4f, why);
        Assert.AreEqual(tag, s.tag, why);
    }

    [Test]
    public void StraightHittingRewardsTheV()
    {
        Is(eDrill.StraightHitting, R(angle: 10f, exit: 24f), 1f, eDrillTag.InTheV, "hard and straight");
        Is(eDrill.StraightHitting, R(angle: -20f, exit: 12f), 0.8f, eDrillTag.InTheV, "placed straight");
        Is(eDrill.StraightHitting, R(angle: 70f), 0.4f, eDrillTag.Survived, "clean but square");
        Is(eDrill.StraightHitting, R(c: eRealContact.Edge), 0.15f, eDrillTag.Edged, "edged");
        Is(eDrill.StraightHitting, R(eBallAction.Left, hitting: true), 0f, eDrillTag.LeftStraight, "left a straight one");
    }

    [Test]
    public void SwingRewardsLeavingTheOutswingerAndPlayingTheStraightOneDown()
    {
        Is(eDrill.Swing, R(eBallAction.Left, line: 0.5f), 1f, eDrillTag.GoodLeave, "left the wide one");
        Is(eDrill.Swing, R(line: 0.5f), 0.4f, eDrillTag.Fished, "fished at the wide one");
        Is(eDrill.Swing, R(line: 0.05f, launch: 4f), 1f, eDrillTag.Survived, "straight one played down");
        Is(eDrill.Swing, R(line: 0.05f, launch: 20f), 0.6f, eDrillTag.Mistimed, "straight one in the air");
        Is(eDrill.Swing, R(c: eRealContact.Edge, line: 0.05f), 0f, eDrillTag.Edged, "edged");
    }

    [Test]
    public void PowerHittingWantsItOverTheRope()
    {
        Is(eDrill.PowerHitting, R(runs: 6), 1f, eDrillTag.Six, "six");
        Is(eDrill.PowerHitting, R(runs: 4), 0.7f, eDrillTag.Boundary, "four");
        Is(eDrill.PowerHitting, R(launch: 30f, carry: 45f), 0.4f, eDrillTag.Mistimed, "big but short");
        Is(eDrill.PowerHitting, R(eBallAction.Left), 0f, eDrillTag.Wasted, "left one in the slot");
    }

    [Test]
    public void DeathOversIsScoredByTheRuns()
    {
        Is(eDrill.DeathOvers, R(runs: 6), 1f, eDrillTag.Six, "six");
        Is(eDrill.DeathOvers, R(runs: 4), 0.85f, eDrillTag.Boundary, "four");
        Is(eDrill.DeathOvers, R(runs: 2), 0.6f, eDrillTag.Scored, "two");
        Is(eDrill.DeathOvers, R(runs: 1), 0.45f, eDrillTag.Scored, "single");
        Is(eDrill.DeathOvers, R(), 0.25f, eDrillTag.Survived, "clean dot");
        Is(eDrill.DeathOvers, R(c: eRealContact.Edge, runs: 4), 0.85f, eDrillTag.Boundary, "edged four still counts");
        Is(eDrill.DeathOvers, R(eBallAction.Missed), 0.1f, eDrillTag.Beaten, "beaten");
    }

    [Test]
    public void SoftHandsWantsItDead()
    {
        Is(eDrill.SoftHands, R(exit: 5f), 1f, eDrillTag.Blocked, "dead bat");
        Is(eDrill.SoftHands, R(exit: 20f), 0.6f, eDrillTag.Survived, "firm");
        Is(eDrill.SoftHands, R(c: eRealContact.Edge, exit: 5f), 0.4f, eDrillTag.Edged, "soft edge");
        Is(eDrill.SoftHands, R(c: eRealContact.Edge, exit: 20f), 0.1f, eDrillTag.Edged, "hard edge");
    }

    [Test]
    public void TheCoachPicksPowerHittingAndSoftHandsForThoseRatings()
    {
        var weakPower = new RatingSet();
        for (int i = 0; i < RatingSet.Count; i++) weakPower[i] = 60f;
        weakPower[1] = 30f;
        Assert.AreEqual(eDrill.PowerHitting, DrillCoach.Recommend(weakPower));
        weakPower[1] = 60f;
        weakPower[4] = 30f;
        Assert.AreEqual(eDrill.SoftHands, DrillCoach.Recommend(weakPower));
    }

    [Test]
    public void EveryNewDrillHasWordsForEveryFailing()
    {
        foreach (eDrill d in new[] { eDrill.StraightHitting, eDrill.Swing, eDrill.PowerHitting, eDrill.DeathOvers, eDrill.SoftHands })
            foreach (eDrillTag t in System.Enum.GetValues(typeof(eDrillTag)))
                Assert.IsNotEmpty(DrillTextMore.Work(d, t), $"{d} {t}");
        foreach (eDrillTag t in new[] { eDrillTag.InTheV, eDrillTag.Blocked, eDrillTag.Boundary, eDrillTag.Mistimed, eDrillTag.Scored })
            Assert.IsNotEmpty(DrillText.TagLabel(t), t.ToString());
    }
}
