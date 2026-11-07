using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// DrillGoals: every drill has a goal it is possible to meet, and a ball that counts toward it is
/// always one the drill's score also rated well - the two never disagree (2026-11-06).
/// </summary>
public class DrillGoalTests
{
    private static IEnumerable<BallRecord> Balls()
    {
        eBallAction[] actions = { eBallAction.Played, eBallAction.Left, eBallAction.Missed };
        eRealContact[] contacts = { eRealContact.Clean, eRealContact.Edge, eRealContact.WouldHaveMissed };
        float[] angles = { -120f, -60f, -10f, 0f, 15f, 60f, 100f };
        float[] speeds = { 5f, 15f, 30f };
        float[] lines = { 0f, 0.5f };
        foreach (eBallAction a in actions)
        foreach (eRealContact c in contacts)
        foreach (float ang in angles)
        foreach (float sp in speeds)
        foreach (float line in lines)
        foreach (int runs in new[] { 0, 1, 2, 4, 6 })
            yield return new BallRecord
            {
                action = a, realContact = c, shotAngle = ang, exitSpeed = sp, lineAtStumps = line, runs = runs,
                end = runs == 6 ? eBallEnd.Six : runs == 4 ? eBallEnd.Four : runs > 0 ? eBallEnd.Runs : eBallEnd.Dot,
                wouldHitStumps = line == 0f, launchDeg = sp > 20f ? 30f : 5f, carry = sp > 20f ? 60f : 5f,
                bowlerKind = eBowlerKind.OffSpin, delivery = eDelivery.OffBreak, heightAtStumps = 0.6f,
            };
    }

    [Test]
    public void EveryDrillHasAGoalItCanMeet()
    {
        foreach (DrillDef d in DrillDefs.All)
        {
            DrillGoals.Goal g = DrillGoals.For(d.id);
            Assert.IsNotEmpty(g.text, d.name);
            Assert.IsNotEmpty(g.rule, d.name);
            Assert.That(g.target, Is.InRange(1, DrillDefs.BallsPerDrill), d.name);
        }
    }

    [Test]
    public void ABallThatCountsIsOneTheScoreRatedWell()
    {
        foreach (DrillDef d in DrillDefs.All)
        foreach (BallRecord r in Balls())
        {
            DrillBallScore s = DrillScoring.Score(d, r);
            bool counts = DrillGoals.Counts(d.id, r, s.tag);
            if (counts)
                Assert.GreaterOrEqual(s.value, 0.6f, $"{d.name}: {s.tag}");
            Assert.IsNotEmpty(DrillGoals.Why(d.id, r, s.tag, counts), d.name);
            if (!counts)
                Assert.AreNotEqual("Played well", DrillGoals.Why(d.id, r, s.tag, false), $"{d.name}: a miss never reads as played well");
        }
    }

    [Test]
    public void TheTallyCountsTheGoal()
    {
        DrillDef y = DrillDefs.For(eDrill.Yorkers);
        var balls = new List<BallRecord>();
        for (int i = 0; i < DrillDefs.BallsPerDrill; i++)
            balls.Add(new BallRecord { action = eBallAction.Played, realContact = i < 9 ? eRealContact.Clean : eRealContact.Edge });
        DrillResult r = DrillScoring.Tally(y, balls);
        Assert.AreEqual(9, r.hits);
        StringAssert.Contains("Goal met", DrillText.GoalLine(r));
        StringAssert.Contains("9 of 12 counted (needed 8)", DrillText.GoalLine(r));
    }

    [Test]
    public void OutNeverCounts()
    {
        var r = new BallRecord { action = eBallAction.Played, realContact = eRealContact.Clean, end = eBallEnd.Out, runs = 0 };
        foreach (DrillDef d in DrillDefs.All)
            Assert.IsFalse(DrillGoals.Counts(d.id, r, DrillScoring.Score(d, r).tag), d.name);
    }
}
