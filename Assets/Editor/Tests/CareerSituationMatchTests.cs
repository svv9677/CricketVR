using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Phase 7 situations in the career match, with no scene: a chase rain cut is won past its revised
/// par, tied on it and lost below it, with the balls left counted from the chase's own overs; the
/// result screen's margin reads the same par; the scoreboard's target is the revised one; a match
/// the app died in is finished at the rain's overs against the revised target; a collapse's script
/// is used up only as HUD takes its balls and lands where the plan said; and a match left half way
/// through a collapse is still recorded. Normal matches read exactly as before.
/// </summary>
public class CareerSituationMatchTests
{
    private string dir;

    [SetUp]
    public void Fresh()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_situation_match_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        CareerMatchSession.ResetForTests();
        MatchFlow.ClearOverrides();
    }

    [TearDown]
    public void Restore()
    {
        CareerMatchSession.ResetForTests();
        MatchFlow.ClearOverrides();
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    /// A Pro League chase of Old Mill CC's 164/6 in 20 overs, cut by rain to 14 overs: target 124.
    private static MatchPlan RainChase()
    {
        var p = new MatchPlan
        {
            rung = eRung.ProLeague, yourTeam = "t1", opponent = "t2", yourTeamName = "Harbour CC", opponentName = "Old Mill CC",
            overs = 14, slot = 1, batFirst = false, seed = 11, situation = eSituation.RainShortened,
            theirs = new InningsCard { team = "Old Mill CC", runs = 164, wickets = 6, balls = 120, overs = 20 },
        };
        p.revisedTarget = CareerSituationMath.RevisedTarget(164, 20, 14);
        for (int i = 0; i < 11; i++)
        {
            p.order.Add(i == p.slot ? "You" : "Bat" + (i + 1));
            p.orderRatings.Add(50f);
        }
        p.bowlers.AddRange(new[] { "Lee", "Khan", "Moss", "Dale", "Pike" });
        return p;
    }

    /// His side's chase stopped at `runs`/`wickets` after `balls` legal balls.
    private static CareerMatchResult Chased(MatchPlan plan, int runs, int wickets, int balls)
    {
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        s.runs = runs; s.wickets = wickets; s.balls = balls;
        return CareerMatchLogic.Build(s, plan, plan.theirs, false);
    }

    // ---- A rain-shortened chase -----------------------------------------------------------------

    [Test]
    public void ARainChase_IsWonOnTheRevisedTarget_WithItsOwnBallsLeft()
    {
        MatchPlan plan = RainChase();
        Assert.AreEqual(124, plan.Target);
        CareerMatchResult r = Chased(plan, 124, 4, 80);
        Assert.IsTrue(r.yours.revised, "the chase's card says rain cut it");
        Assert.AreEqual(14, r.yours.overs);
        Assert.AreEqual(124, r.yours.target);
        Assert.IsTrue(r.won);
        Assert.IsFalse(r.tied);
        Assert.AreEqual("Harbour CC won by 6 wickets (4 balls left)", r.result, "14 overs, not their 20: 84 - 80 balls");
    }

    [Test]
    public void ARainChase_IsTiedOneShortOfTheRevisedTarget()
    {
        CareerMatchResult r = Chased(RainChase(), 123, 5, 84);
        Assert.IsTrue(r.tied);
        Assert.IsFalse(r.won);
        Assert.AreEqual("Match tied", r.result);
    }

    [Test]
    public void ARainChase_IsLostByTheRunsShortOfPar()
    {
        CareerMatchResult r = Chased(RainChase(), 122, 7, 84);
        Assert.IsFalse(r.won);
        Assert.IsFalse(r.tied);
        Assert.AreEqual("Old Mill CC won by 1 run", r.result, "par is 123 (target 124 - 1), not their 164");
    }

    [Test]
    public void TheResultScreensMargin_ReadsTheRevisedPar()
    {
        MatchPlan plan = RainChase();
        CareerMatchResult lost = Chased(plan, 120, 7, 84);
        Assert.AreEqual(3, CareerResultScreen.Margin(lost, false, out bool byRuns));
        Assert.IsTrue(byRuns);
        CareerMatchResult won = Chased(plan, 125, 4, 82);
        Assert.AreEqual(6, CareerResultScreen.Margin(won, false, out byRuns));
        Assert.IsFalse(byRuns);
        Assert.AreEqual("LOST by 3 runs", CareerResultScreen.Headline(lost.won, lost.tied, 3, true));
    }

    [Test]
    public void ANormalChase_ReadsAsItAlwaysDid()
    {
        MatchPlan plan = RainChase();
        plan.situation = eSituation.Normal;
        plan.revisedTarget = 0;
        plan.overs = 20;
        Assert.AreEqual(165, plan.Target);
        CareerMatchResult won = Chased(plan, 166, 3, 110);
        Assert.IsFalse(won.yours.revised);
        Assert.AreEqual("Harbour CC won by 7 wickets (10 balls left)", won.result);
        CareerMatchResult lost = Chased(plan, 150, 8, 120);
        Assert.AreEqual("Old Mill CC won by 14 runs", lost.result);
        Assert.AreEqual(14, CareerResultScreen.Margin(lost, false, out bool byRuns));
        Assert.IsTrue(byRuns);
    }

    [Test]
    public void BattingFirst_IsNeverMarkedRevised()
    {
        MatchPlan plan = RainChase();
        plan.batFirst = true;
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        s.runs = 100; s.balls = 84;
        var theirs = new InningsCard { runs = 90, wickets = 10, balls = 70, overs = 14, target = 101 };
        CareerMatchResult r = CareerMatchLogic.Build(s, plan, theirs, false);
        Assert.IsFalse(r.yours.revised);
        Assert.AreEqual("Harbour CC won by 10 runs", r.result);
    }

    [Test]
    public void TheScoreboardsTarget_IsTheRevisedOne_OnlyWhenChasing()
    {
        MatchPlan plan = RainChase();
        CareerMatchSession.SetPlanForTests(plan);
        CareerMatchSession.ApplyOverrides();
        Assert.AreEqual(124, MatchFlow.TargetOverride);
        plan.batFirst = true;
        CareerMatchSession.ApplyOverrides();
        Assert.AreEqual(0, MatchFlow.TargetOverride, "batting first there is no target to override");
        plan.batFirst = false;
        plan.revisedTarget = 0;
        CareerMatchSession.ApplyOverrides();
        Assert.AreEqual(0, MatchFlow.TargetOverride, "no rain: MatchFlow's own runs + 1");
        MatchFlow.ClearOverrides();
        Assert.AreEqual(0, MatchFlow.TargetOverride);
    }

    [Test]
    public void TheResultsHeadlineTag_IsSmallMutedAndOnlyThereWithASituation()
    {
        Assert.AreEqual("", CareerResultScreen.HeadlineTag(""));
        Assert.AreEqual("", CareerResultScreen.HeadlineTag(null));
        string tag = CareerResultScreen.HeadlineTag(CareerSituationText.ResultTag(RainChase()));
        StringAssert.StartsWith("<size=55%><color=#98A2B3> · ", tag);
        StringAssert.Contains("rain, target 124", tag);
        StringAssert.EndsWith("</color></size>", tag);
    }

    // ---- A match the app died in ----------------------------------------------------------------

    private static CareerProfile Career(int seed)
    {
        var p = new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05", trialDone = true };
        CareerProgress.EnsureSeason(p, new Random(seed));
        return p;
    }

    [Test]
    public void ARainChaseRecovered_IsFinishedAtTheRainsOversAgainstTheRevisedPar()
    {
        CareerProfile p = Career(21);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(22));
        int full = Ladder.Overs(plan.rung);
        plan.situation = eSituation.RainShortened;
        plan.batFirst = false;
        plan.theirs = new InningsCard { team = plan.opponentName, runs = 60, wickets = 5, balls = full * 6, overs = full };
        plan.overs = full - 2;
        plan.revisedTarget = CareerSituationMath.RevisedTarget(60, full, plan.overs);
        Assert.Less(plan.revisedTarget, 61);
        CareerMatchRecovery.Begin(p, plan);
        p.pending.resumable = false;   // a save from before matches could be continued (2026-11-09)
        // The chase's overs all bowled, one short of the revised target: a tie on par (not a loss to 60).
        p.pending.teamRuns = plan.revisedTarget - 1; p.pending.teamWickets = 3; p.pending.teamBalls = plan.overs * 6;
        p.pending.runs = 20; p.pending.balls = 12; p.pending.batted = true;

        MatchState s = CareerMatchRecovery.FromSnapshot(plan, p.pending);
        Assert.AreEqual(plan.overs * 6, s.totalBalls, "the rain's overs");
        Assert.AreEqual(plan.revisedTarget, s.target);
        Assert.IsTrue(CareerMatchLogic.InningsDone(s), "his side's overs are done");

        Assert.IsTrue(CareerMatchRecovery.Resolve(p));
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.IsTrue(last.tied, last.result);
        Assert.AreEqual("Match tied", last.result);
        Assert.IsTrue(p.season.fixtures[plan.fixture].played);
    }

    // ---- A collapse -----------------------------------------------------------------------------

    /// A collapse script for `plan`: three wickets among dots, singles and a two, and the score it
    /// leaves (scored the way HUD scores, on CareerMatchLogic.Score).
    private static void Script(MatchPlan plan, params int[] codes)
    {
        plan.collapse.Clear();
        plan.collapse.AddRange(codes);
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        foreach (int c in codes)
            CareerMatchLogic.Score(s, c, null);
        plan.startRuns = s.runs;
        plan.startWickets = s.wickets;
    }

    /// He bats at number 5 for this match, the others keeping their order around him (as Apply does).
    private static void MoveToFive(MatchPlan plan)
    {
        string name = plan.order[plan.slot];
        float rating = plan.orderRatings[plan.slot];
        plan.order.RemoveAt(plan.slot); plan.orderRatings.RemoveAt(plan.slot);
        plan.order.Insert(CareerSituations.RescueIndex, name); plan.orderRatings.Insert(CareerSituations.RescueIndex, rating);
        plan.slot = CareerSituations.RescueIndex;
    }

    [Test]
    public void TheCollapseScript_IsUsedOnlyAsTheScoreboardTakesItsBalls_AndLandsOnThePlan()
    {
        MatchPlan plan = RainChase();
        plan.situation = eSituation.Rescue;
        plan.batFirst = true; plan.theirs = null; plan.revisedTarget = 0; plan.overs = 20;
        MoveToFive(plan);
        Script(plan, 0, CareerSim.Wicket, 1, CareerSim.Wide, 0, CareerSim.Wicket, 2, 4, 0, CareerSim.Wicket);
        Assert.AreEqual(3, plan.startWickets);

        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        int at = 0, book = 0, tries = 0, refused = 0;
        while (CareerMatch.ScriptedCode(plan, at, out int code))
        {
            // Every third try HUD turns the ball away (no bowler on yet): the script must wait for it.
            int before = book;
            if (tries++ % 3 != 2) { CareerMatchLogic.Score(s, code, null); book++; }
            else refused++;
            at = CareerMatch.ScriptStep(at, plan.collapse.Count, before, book);
        }
        Assert.Greater(refused, 0, "the test turned some balls away");
        Assert.AreEqual(plan.collapse.Count, at);
        Assert.IsTrue(CareerMatch.ScriptLanded(plan, s.runs, s.wickets));
        Assert.AreEqual(3, s.wickets);
        Assert.IsTrue(s.Striker.isYou || s.Runner.isYou, "the third wicket brings him in");
        Assert.AreEqual(0, s.You.balls, "his own balls are never scripted");
        Assert.IsFalse(CareerMatch.ScriptedCode(plan, at, out _), "after the script, CareerSim as before");
        Assert.IsFalse(CareerMatch.ScriptLanded(plan, s.runs + 1, s.wickets));
    }

    [Test]
    public void WithNoCollapse_NothingIsScripted()
    {
        MatchPlan plan = RainChase();
        Assert.IsFalse(CareerMatch.ScriptedCode(plan, 0, out _));
        plan.collapse = null;
        Assert.IsFalse(CareerMatch.ScriptedCode(plan, 0, out _), "an old save's plan");
        Assert.IsFalse(CareerMatch.ScriptedCode(null, 0, out _));
        Assert.AreEqual(0, CareerMatch.ScriptStep(0, 0, 3, 4), "nothing to advance");
        Assert.AreEqual(2, CareerMatch.ScriptStep(2, 5, 7, 7), "the ball was turned away");
        Assert.AreEqual(3, CareerMatch.ScriptStep(2, 5, 7, 8));
    }

    [Test]
    public void ARescueLeftMidCollapse_IsStillRecorded()
    {
        CareerProfile p = Career(31);
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(32));
        plan.situation = eSituation.Rescue;
        MoveToFive(plan);
        Script(plan, 0, CareerSim.Wicket, 1, 0, CareerSim.Wicket, 0, 0, CareerSim.Wicket);
        Fixture f = p.season.fixtures[plan.fixture];
        CareerMatchRecovery.Begin(p, plan);
        p.pending.resumable = false;   // a save from before matches could be continued (2026-11-09)
        // The app died one wicket into the collapse: he has not come in.
        p.pending.teamRuns = 0; p.pending.teamWickets = 1; p.pending.teamBalls = 2;

        Assert.IsTrue(CareerMatchRecovery.Resolve(p));
        Assert.IsTrue(f.played, "the fixture is played, not offered again");
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.IsTrue(last.retired && last.isOut, "leaving counts as retiring out, even before he came in");
        Assert.AreEqual(CareerSituations.RescueIndex + 1, last.position);
        Assert.IsTrue(p.inbox.Exists(m => m.title == "The match you left"));
    }
}
