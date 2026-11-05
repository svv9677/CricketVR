using System;
using System.IO;
using NUnit.Framework;
using Random = System.Random;

/// <summary>
/// Phase 7's paths that cross its units (integration note 4), each played the way the game plays
/// it, with no scene:
///   * a rain-cut chase from the plan PlanNext makes (CareerSituations.Apply), its result built by
///     CareerMatchLogic.Build and recorded by CareerProgress.RecordMatch: the fixture, the table and
///     the result screen's headline and tag all read the revised target;
///   * a rescue's collapse, replayed as CareerMatch feeds it to the scoreboard, lands on the score
///     the plan (and the hub's preview of it) promised;
///   * a six noted in the match (NoteSix) reaches the record book through Build and RecordMatch
///     (CareerRecords.AfterMatch), its clip stamp with it - not only through a recovered match;
///   * a runs goal reached in RecordMatch posts the coach's message, and CareerUpkeep then grants the
///     kit goals unlock;
///   * the season review counts the coach's goals.
/// </summary>
public class Phase7IntegrationTests
{
    private string dir;

    [SetUp]
    public void Fresh()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_phase7_integration_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        CareerMatchSession.ResetForTests();
        RecordMoments.ResetForTests();
    }

    [TearDown]
    public void Restore()
    {
        CareerMatchSession.ResetForTests();
        RecordMoments.ResetForTests();
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// A career whose trial is done (CareerTestPlay.Career never sets it).
    private static CareerProfile Career(int seed, eRung rung = eRung.Club, float ratings = 60f)
    {
        CareerProfile p = CareerTestPlay.Career(seed, rung, ratings);
        p.trialDone = true;
        return p;
    }

    /// The next match's plan the way the hub and Play match make it (CareerSituations.PlanRng),
    /// any match he is left out of played without him first.
    private static MatchPlan NextPlan(CareerProfile p)
    {
        for (int guard = 0; CareerProgress.LeftOutNext(p) && guard < 10; guard++)
            CareerProgress.PlayLeftOut(p, new Random(guard));
        MatchPlan plan = CareerProgress.PlanNext(p, CareerSituations.PlanRng(p));
        Assert.IsNotNull(plan, "there is a next match");
        return plan;
    }

    /// A Pro League career whose next match is rain-cut and has him chasing, its plan made through
    /// PlanNext (and so CareerSituations.Apply). The creation date varies the plan's rng.
    private static CareerProfile RainChase(out MatchPlan plan)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            CareerProfile p = Career(seed, eRung.ProLeague);
            p.created = $"2026-11-05 #{seed}";
            CareerProgress.NextFixture(p).situation = eSituation.RainShortened;
            plan = NextPlan(p);
            if (!plan.batFirst)
                return p;
        }
        Assert.Fail("some career chases in the rain");
        plan = null;
        return null;
    }

    /// His side's innings as it ended: `runs`/`wickets` after `balls` legal balls, him in for 30 (20).
    private static CareerMatchResult Chased(MatchPlan plan, int runs, int wickets, int balls)
    {
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        s.runs = runs; s.wickets = wickets; s.balls = balls;
        s.You.batted = true; s.You.runs = 30; s.You.balls = 20;
        return CareerMatchLogic.Build(s, plan, plan.theirs, false);
    }

    /// The match recorded; returns its fixture.
    private static Fixture Record(CareerProfile p, MatchPlan plan, CareerMatchResult r)
    {
        Fixture f = p.season.fixtures[plan.fixture];
        CareerProgress.RecordMatch(p, plan, r, new Random(plan.seed));
        Assert.IsTrue(f.played, "the fixture is recorded");
        return f;
    }

    /// Both sides' table rows: (played, won, tied, lost, points).
    private static (int, int, int, int, int) Row(Team t) => (t.played, t.won, t.tied, t.lost, t.points);

    // ---- (a) A rain-cut chase, end to end -----------------------------------------------------------

    [Test]
    public void ARainChase_PlannedThroughApply_IsCutAndRevised()
    {
        RainChase(out MatchPlan plan);
        int full = Ladder.Overs(eRung.ProLeague);
        Assert.AreEqual(eSituation.RainShortened, plan.situation);
        Assert.Less(plan.overs, full, "rain took overs");
        Assert.AreEqual(full, plan.theirs.overs, "they batted their full overs");
        Assert.AreEqual(CareerSituationMath.RevisedTarget(plan.theirs.runs, full, plan.overs), plan.revisedTarget);
        Assert.AreEqual(plan.revisedTarget, plan.Target);
    }

    [Test]
    public void ARainChaseWon_OnTheRevisedTarget_ReadsSoInTheFixtureTheTableAndTheHeadline()
    {
        CareerProfile p = RainChase(out MatchPlan plan);
        CareerMatchResult r = Chased(plan, plan.Target, 4, plan.overs * 6 - 5);
        Assert.IsTrue(r.won);
        Assert.IsTrue(r.yours.revised);
        Assert.AreEqual($"{plan.yourTeamName} won by 6 wickets (5 balls left)", r.result, "balls left of the rain's overs");

        Fixture f = Record(p, plan, r);
        Assert.AreEqual(p.season.yourTeam, f.winner);
        Assert.AreEqual(r.result, f.result, "the league reads the revised target as the match did");
        Assert.AreEqual((1, 1, 0, 0, 2), Row(p.season.Yours));
        Assert.AreEqual((1, 0, 0, 1, 0), Row(p.season.Team(plan.opponent)));
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.AreEqual((eSituation.RainShortened, plan.overs, plan.Target), (last.situation, last.overs, last.target));
        Assert.IsTrue(last.won);

        int margin = CareerResultScreen.Margin(r, plan.batFirst, out bool byRuns);
        Assert.AreEqual("WON by 6 wickets", CareerResultScreen.Headline(r.won, r.tied, margin, byRuns));
        Assert.AreEqual($"<size=55%><color=#98A2B3> · rain, target {plan.Target}</color></size>",
                        CareerResultScreen.HeadlineTag(CareerSituationText.ResultTag(plan)));
    }

    [Test]
    public void ARainChaseOneShort_IsTiedInTheFixtureAndTheTable()
    {
        CareerProfile p = RainChase(out MatchPlan plan);
        CareerMatchResult r = Chased(plan, plan.Target - 1, 7, plan.overs * 6);
        Assert.IsTrue(r.tied);
        Fixture f = Record(p, plan, r);
        Assert.AreEqual("", f.winner);
        Assert.AreEqual("Match tied", f.result);
        Assert.AreEqual((1, 0, 1, 0, 1), Row(p.season.Yours));
        Assert.AreEqual((1, 0, 1, 0, 1), Row(p.season.Team(plan.opponent)));
        Assert.AreEqual("TIED", CareerResultScreen.Headline(r.won, r.tied, CareerResultScreen.Margin(r, plan.batFirst, out bool byRuns), byRuns));
    }

    [Test]
    public void ARainChaseThreeShort_IsLostByTwoFromThePar()
    {
        CareerProfile p = RainChase(out MatchPlan plan);
        CareerMatchResult r = Chased(plan, plan.Target - 3, 8, plan.overs * 6);
        Assert.IsFalse(r.won || r.tied);
        Assert.AreEqual($"{plan.opponentName} won by 2 runs", r.result, "par is the revised target - 1, not their runs");
        Fixture f = Record(p, plan, r);
        Assert.AreEqual(plan.opponent, f.winner);
        Assert.AreEqual(r.result, f.result);
        Assert.AreEqual((1, 0, 0, 1, 0), Row(p.season.Yours));
        Assert.AreEqual((1, 1, 0, 0, 2), Row(p.season.Team(plan.opponent)));
        int margin = CareerResultScreen.Margin(r, plan.batFirst, out bool byRuns);
        Assert.AreEqual("LOST by 2 runs", CareerResultScreen.Headline(r.won, r.tied, margin, byRuns));
    }

    // ---- (b) The rescue ---------------------------------------------------------------------------

    [Test]
    public void ARescuesCollapse_ReplayedAsTheMatchFeedsIt_LandsOnThePlannedScore()
    {
        foreach (eRung rung in new[] { eRung.Club, eRung.State, eRung.ProLeague, eRung.International })
            for (int seed = 0; seed < 10; seed++)
            {
                string at = $"{rung} seed {seed}";
                CareerProfile p = Career(seed, rung);
                CareerProgress.NextFixture(p).situation = eSituation.Rescue;
                MatchPlan plan = NextPlan(p);
                Assert.AreEqual(eSituation.Rescue, plan.situation, at);
                Assert.IsNotEmpty(plan.collapse, at);
                CollectionAssert.AreEqual(plan.collapse, NextPlan(p).collapse, "the hub's preview is the match's plan");

                MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
                int scriptAt = 0, bowled = 0;
                for (int guard = 0; CareerMatch.ScriptedCode(plan, scriptAt, out int code); guard++)
                {
                    Assert.Less(guard, 1000, "the script runs out");
                    int before = bowled;
                    CareerMatchLogic.Score(s, code, null);   // HUD.ScoreSimulatedBall takes it
                    bowled++;
                    scriptAt = CareerMatch.ScriptStep(scriptAt, plan.collapse.Count, before, bowled);
                }
                Assert.AreEqual(plan.collapse.Count, scriptAt, at);
                Assert.IsTrue(CareerMatch.ScriptLanded(plan, s.runs, s.wickets), at);
                Assert.AreEqual((plan.startRuns, plan.startWickets), (s.runs, s.wickets), at);
                Assert.AreEqual(CareerSituations.RescueWickets, s.wickets, at);
                Assert.IsTrue(s.Striker.isYou || s.Runner.isYou, "he is in");
                Assert.AreEqual(0, s.You.balls, "having faced nothing");
                string come = $"You come in at {plan.startRuns}/{plan.startWickets}";
                StringAssert.StartsWith(come, CareerMatchLogic.ComeIn(s.runs, s.wickets, s.balls), "the come-in card");
                StringAssert.Contains(come, CareerSituationText.HubLine(plan, plan.slot + 1), "the hub's tile");
            }
    }

    // ---- (c) A six into the record book -----------------------------------------------------------

    /// The match's state with him on strike (his end swapped in), nothing bowled yet.
    private static MatchState OnStrike(MatchPlan plan)
    {
        MatchState s = CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);
        int you = s.YouIndex;
        if (s.runner == you) { s.runner = s.striker; s.striker = you; }
        else if (s.striker != you) s.striker = you;
        s.You.batted = true;
        return s;
    }

    /// One match he plays: a four, a six that carries `carry` off `contact` (noted as CareerMatch
    /// notes it), a single; built and recorded. Returns the result and the six's stamp.
    private static CareerMatchResult SixMatch(CareerProfile p, float carry, eRealContact contact, out string stamp)
    {
        MatchPlan plan = NextPlan(p);
        MatchState s = OnStrike(plan);
        CareerMatchLogic.Score(s, 4, null);
        CareerMatchLogic.Score(s, 6, null);
        stamp = $"{plan.seed:x8}-b{s.You.balls}";
        CareerMatchLogic.NoteSix(s, carry, contact, stamp);
        CareerMatchLogic.Score(s, 1, null);
        CareerMatchLogic.ObserveMoments(s);
        InningsCard theirs = !plan.batFirst ? plan.theirs : new InningsCard
        {
            team = plan.opponentName, runs = Math.Max(0, s.runs - 5), wickets = 10, balls = 60, overs = plan.overs, target = s.runs + 1,
        };
        CareerMatchResult r = CareerMatchLogic.Build(s, plan, theirs, false);
        Record(p, plan, r);
        return r;
    }

    [Test]
    public void ASixNotedInTheMatch_SetsTheLongestSixAndItsClip_ThroughRecordMatch()
    {
        CareerProfile p = Career(41);
        CareerMatchResult first = SixMatch(p, 82.5f, eRealContact.Clean, out string stamp1);
        Assert.AreEqual(82.5f, first.longestSix, 1e-4f, "Build carries the six");
        Assert.AreEqual(stamp1, first.sixStamp);
        RecordEntry six = p.recordBook.longestSix;
        Assert.IsTrue(six.set);
        Assert.AreEqual(82.5f, six.value, 1e-4f);
        Assert.AreEqual(stamp1, six.clipStamp, "the clip goes with the six");
        Assert.IsFalse(first.bests.Contains(eRecord.LongestSix), "a first record is not a new best");

        CareerMatchResult longer = SixMatch(p, 96f, eRealContact.Edge, out string stamp2);
        Assert.AreEqual(96f, p.recordBook.longestSix.value, 1e-4f);
        Assert.AreEqual(stamp2, p.recordBook.longestSix.clipStamp, "the longer six's clip");
        Assert.Contains(eRecord.LongestSix, longer.bests);

        SixMatch(p, 70f, eRealContact.Clean, out _);
        SixMatch(p, 120f, eRealContact.WouldHaveMissed, out _);
        Assert.AreEqual(96f, p.recordBook.longestSix.value, 1e-4f, "a shorter six, and one the shield found, change nothing");
        Assert.AreEqual(stamp2, p.recordBook.longestSix.clipStamp);
    }

    // ---- (d) A goal reached, then its kit ---------------------------------------------------------

    /// A Club career with its goals made as the hub makes them (CareerUpkeep), then a 70* in its
    /// first match recorded: past the runs goal of 60.
    private static CareerProfile GoalReached(out SeasonGoal runs)
    {
        CareerProfile p = Career(42);
        CareerUpkeep.Run(p);
        runs = p.season.goals.Find(g => g.kind == eGoalKind.Runs);
        Assert.IsNotNull(runs, "the hub set a runs goal");
        Assert.AreEqual(60f, runs.target);
        MatchPlan plan = NextPlan(p);
        Record(p, plan, CareerTestPlay.Result(plan, 70, 45, false, true));
        return p;
    }

    [Test]
    public void ARunsGoalReachedInRecordMatch_PostsTheCoachsMessage_AndUpkeepGrantsItsKit()
    {
        CareerProfile p = GoalReached(out SeasonGoal runs);
        Assert.IsTrue(runs.done, "70 of 60");
        Assert.GreaterOrEqual(p.goalsDone, 1);
        CareerMessage m = p.inbox.Find(x => x.kind == eMessageKind.Goal && x.body.StartsWith(runs.text));
        Assert.IsNotNull(m, "the coach says so");
        Assert.AreEqual("Goal reached", m.title);
        Assert.AreEqual(CoachGoals.ReachedBody(runs), m.body);
        Assert.IsFalse(p.unlocked.Contains("gloves.side"), "kit is granted by upkeep, not by the match");

        Assert.IsTrue(CareerUpkeep.Run(p), "the hub opening grants it");
        Assert.Contains("gloves.side", p.unlocked);
        CareerMessage locker = p.inbox.FindLast(x => x.kind == eMessageKind.Locker);
        Assert.IsNotNull(locker);
        StringAssert.Contains(CosmeticCatalog.FullName(CosmeticCatalog.For("gloves.side")), locker.body);
        Assert.IsFalse(CareerUpkeep.Run(p), "nothing new the second time");
    }

    // ---- (e) The season review --------------------------------------------------------------------

    [Test]
    public void TheSeasonReview_CountsTheCoachsGoals()
    {
        CareerProfile p = GoalReached(out _);
        string review = CareerProgressMath.SeasonReview(p, p.season);
        StringAssert.Contains("Coach's goals", review);
        StringAssert.Contains($"Coach's goals: {CoachGoals.Done(p.season)} of {p.season.goals.Count}.", review);

        CareerProfile none = CareerTestPlay.Career(42, eRung.Club, 60f);   // no trial: no goals
        StringAssert.DoesNotContain("Coach's goals", CareerProgressMath.SeasonReview(none, none.season));
    }
}
