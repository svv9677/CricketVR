using System;
using System.Collections.Generic;
using NUnit.Framework;

/// A career match the app died in is recorded the next time the career opens, as left early
/// (CareerMatchRecovery); and the phase-2 review's fixes that need no scene.
public class CareerRecoveryTests
{
    private static CareerProfile Career(int seed)
    {
        var p = new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05", trialDone = true };
        CareerProgress.EnsureSeason(p, new Random(seed));
        return p;
    }

    [SetUp]
    public void SetUp() => CareerMatchSession.ResetForTests();

    [TearDown]
    public void TearDown() => CareerMatchSession.ResetForTests();

    [Test]
    public void AMatchLeftByAKilledAppIsRecordedAsRetiredOut()
    {
        CareerProfile p = Career(1);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(2));
        Fixture f = p.season.fixtures[plan.fixture];
        CareerMatchRecovery.Begin(p, plan);
        CareerMatchRecovery.Note(p, new MatchState());           // nothing known yet: a no-op
        p.pending.teamRuns = 23; p.pending.teamWickets = 1; p.pending.teamBalls = 14;
        p.pending.runs = 11; p.pending.balls = 8; p.pending.batted = true;

        Assert.IsTrue(CareerMatchRecovery.Resolve(p));
        Assert.IsFalse(p.pending.active);
        Assert.IsTrue(f.played, "the fixture is played, not offered again with a fresh toss");
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.IsTrue(last.retired && last.isOut, "leaving counts as retiring out");
        Assert.AreEqual(11, last.runs);
        Assert.IsTrue(p.inbox.Exists(m => m.title == "The match you left"));
        Assert.IsFalse(CareerMatchRecovery.Resolve(p), "once");
    }

    [Test]
    public void NothingPendingDoesNothing()
    {
        CareerProfile p = Career(3);
        Assert.IsFalse(CareerMatchRecovery.Resolve(p));
        Assert.AreEqual(0, p.innings.Count);
    }

    [Test]
    public void TheSnapshotPutsTheRightBattersAtTheCrease()
    {
        CareerProfile p = Career(4);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(5));
        var m = new PendingMatch { active = true, plan = plan, teamRuns = 40, teamWickets = 2, teamBalls = 20, runs = 5, balls = 6, batted = true };
        MatchState s = CareerMatchRecovery.FromSnapshot(plan, m);
        Assert.AreEqual(plan.slot, s.striker, "he is in");
        Assert.IsFalse(s.Runner.isOut);
        int outs = s.batters.FindAll(b => b.isOut).Count;
        Assert.AreEqual(2, outs);
    }

    [Test]
    public void AnInningsOverBeforeHeLeftStandsAsNotOut()
    {
        CareerProfile p = Career(6);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(7));
        var r = new CareerMatchResult
        {
            runs = 30, balls = 20, batted = true, isOut = false, abandoned = true, how = "not out",
            yours = new InningsCard { runs = 120, wickets = 4, balls = plan.overs * 6, overs = plan.overs },
            theirs = new InningsCard { runs = 100, wickets = 10, balls = 60, overs = plan.overs },
            won = true, result = "won",
        };
        CareerProgress.RecordMatch(p, plan, r, new Random(8));
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.IsFalse(last.isOut);
        Assert.IsFalse(last.retired);
        Assert.AreEqual(1, p.stats.notOuts);
    }

    [Test]
    public void ANotOutWithoutFacingABallSaysNothingAboutForm()
    {
        var innings = new List<InningsRecord>
        {
            new InningsRecord { batted = true, runs = 40, balls = 25, isOut = true, rung = eRung.Club },
            new InningsRecord { batted = true, runs = 0, balls = 0, isOut = false, rung = eRung.Club },
        };
        float withIt = CareerProgressMath.Form(innings, 5);
        float without = CareerProgressMath.Form(innings.GetRange(0, 1), 5);
        Assert.AreEqual(without, withIt, 0.001f);
    }

    [Test]
    public void TheFixtureRowMarginLeavesOutTheBallsLeft()
    {
        Assert.AreEqual(" by 4 wickets", SeasonScreen.Margin("Harbour CC won by 4 wickets (7 balls left)"));
        Assert.AreEqual(" by 12 runs", SeasonScreen.Margin("Harbour CC won by 12 runs"));
    }

    [Test]
    public void TheChaseLineNamesTheSides()
    {
        var r = new AIInnings.Result { runs = 150, wickets = 4, balls = 110, overs = 20, target = 150 };
        Assert.AreEqual("Old Mill CC won by 6 wickets (10 balls left)", AIInnings.ChaseResult(r, out _, out _, "Old Mill CC", "Harbour CC"));
        r.runs = 140; r.wickets = 10;
        Assert.AreEqual("Harbour CC won by 9 runs", AIInnings.ChaseResult(r, out _, out _, "Old Mill CC", "Harbour CC"));
        Assert.AreEqual("You won by 9 runs", AIInnings.ChaseResult(r, out _, out _), "a Quick Match as before");
    }
}
