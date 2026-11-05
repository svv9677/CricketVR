using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// A career match's scene-free parts (CareerMatchLogic, CareerMatchSession): the batting side starts
/// from the plan with the career player at his slot; a simulated ball is scored as HUD scores one
/// (strike on odd runs and at the over, a wide not one of the six, the next man in at the striker's
/// end); a batch of simulated balls stops when he is on strike, unless he is out, and at the end of
/// the innings or the cap; leaving early retires him out - it counts - and the rest of the innings is
/// played out; the result reads both innings the right way round; every line of text is Latin-1; and
/// a match finishes once. The balls come from CareerSim, so nothing here assumes what they are -
/// only what any innings must be. Saves go to a temp directory, never the real ones.
/// </summary>
public class CareerMatchTests
{
    private string dir;

    [SetUp]
    public void Fresh()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_career_match_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        CareerMatchSession.ResetForTests();
    }

    [TearDown]
    public void Restore()
    {
        CareerMatchSession.ResetForTests();
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    private static MatchPlan Plan(int slot, bool batFirst = true, int overs = 6)
    {
        var p = new MatchPlan
        {
            yourTeam = "t1", opponent = "t2", yourTeamName = "Harbour CC", opponentName = "Rivertown",
            overs = overs, slot = slot, batFirst = batFirst, seed = 7,
        };
        for (int i = 0; i < 11; i++)
        {
            p.order.Add(i == slot ? "You" : "Bat" + (i + 1));
            p.orderRatings.Add(40f);
        }
        p.bowlers.AddRange(new[] { "Lee", "Khan", "Moss", "Dale", "Pike", "Rudd", "Vane", "Cole" });
        if (!batFirst)
            p.theirs = new InningsCard { team = "Rivertown", runs = 50, wickets = 6, balls = 36, overs = overs };
        return p;
    }

    private static MatchState State(int slot, int overs = 6, int target = 0) =>
        CareerMatchLogic.NewState(Plan(slot, target == 0, overs), overs * 6, target);

    // ---- The state and scoring ------------------------------------------------------------------

    [Test]
    public void TheSideStartsFromThePlanWithYouAtYourSlot()
    {
        MatchState s = State(4);
        Assert.AreEqual(11, s.batters.Count);
        Assert.AreEqual(4, s.YouIndex);
        Assert.AreEqual("You", s.You.name);
        Assert.AreEqual(0, s.striker);
        Assert.AreEqual(1, s.runner);
        Assert.IsTrue(s.batters[0].batted && s.batters[1].batted, "the openers are in");
        Assert.IsFalse(s.You.batted, "number five is not in yet");
    }

    [Test]
    public void OddRunsAndTheEndOfAnOverChangeEnds()
    {
        MatchState s = State(6);
        CareerMatchLogic.Score(s, 1, null);
        Assert.AreEqual(1, s.striker, "a single: the other end");
        CareerMatchLogic.Score(s, 2, null);
        Assert.AreEqual(1, s.striker, "two: same end");
        CareerMatchLogic.Score(s, 4, null);
        CareerMatchLogic.Score(s, 6, null);
        CareerMatchLogic.Score(s, 0, null);
        Assert.AreEqual(1, s.batters[1].fours);
        Assert.AreEqual(1, s.batters[1].sixes);
        CareerMatchLogic.Score(s, 0, null);   // the sixth ball
        Assert.AreEqual(0, s.striker, "the over is done: ends change");
        Assert.AreEqual(13, s.runs);
        Assert.AreEqual(6, s.balls);
        CollectionAssert.AreEqual(new[] { 13 }, s.overRuns);
        Assert.AreEqual(1, s.batters[0].runs);
        Assert.AreEqual(12, s.batters[1].runs);
        Assert.AreEqual(5, s.batters[1].balls);
    }

    [Test]
    public void AWideIsTheTeamsRunAndNotABall()
    {
        MatchState s = State(6);
        CareerMatchLogic.Score(s, CareerSim.Wide, null);
        Assert.AreEqual(1, s.runs);
        Assert.AreEqual(0, s.balls);
        Assert.AreEqual(0, s.batters[0].balls);
        Assert.AreEqual(0, s.batters[0].runs);
    }

    [Test]
    public void AWicketBringsTheNextManInAtTheStrikersEnd()
    {
        MatchState s = State(2);
        CareerMatchLogic.Score(s, CareerSim.Wicket, "c Khan b Lee");
        Assert.AreEqual(1, s.wickets);
        Assert.IsTrue(s.batters[0].isOut);
        Assert.AreEqual("c Khan b Lee", s.batters[0].how);
        Assert.AreEqual(2, s.striker, "number three - the career player - comes in on strike");
        Assert.IsTrue(s.You.batted);
    }

    [Test]
    public void AWicketOffTheLastBallSendsTheNewManToTheOtherEnd()
    {
        MatchState s = State(2);
        for (int i = 0; i < 5; i++)
            CareerMatchLogic.Score(s, 0, null);
        CareerMatchLogic.Score(s, CareerSim.Wicket, "b Lee");
        Assert.AreEqual(2, s.runner, "in, then the over ended");
        Assert.AreEqual(1, s.striker);
        CollectionAssert.AreEqual(new[] { 1 }, s.overWickets);
    }

    [Test]
    public void TenWicketsIsAllOut()
    {
        MatchState s = State(6, 20);
        for (int i = 0; i < 10; i++)
        {
            Assert.IsFalse(CareerMatchLogic.InningsDone(s), $"after {i} wickets");
            CareerMatchLogic.Score(s, CareerSim.Wicket, "b Lee");
        }
        Assert.IsTrue(CareerMatchLogic.InningsDone(s));
        Assert.AreEqual(10, s.wickets);
        Assert.IsTrue(s.batters.TrueForAll(b => b.batted), "everyone batted");
    }

    [Test]
    public void TheInningsEndsAllOutOversDoneOrTargetReached()
    {
        Assert.IsTrue(CareerMatchLogic.InningsDone(0, 10, 3, 36, 0));
        Assert.IsTrue(CareerMatchLogic.InningsDone(40, 2, 36, 36, 0));
        Assert.IsTrue(CareerMatchLogic.InningsDone(51, 2, 20, 36, 51));
        Assert.IsFalse(CareerMatchLogic.InningsDone(50, 2, 20, 36, 51));
        Assert.IsFalse(CareerMatchLogic.InningsDone(50, 9, 35, 36, 0));
    }

    // ---- Batches --------------------------------------------------------------------------------

    [Test]
    public void ABatchStopsWhenYouAreOnStrikeUnlessYouAreOut()
    {
        Assert.IsTrue(CareerMatchLogic.StopBatch(true, false, false, 3, 100), "your ball is played, not simulated");
        Assert.IsFalse(CareerMatchLogic.StopBatch(false, false, false, 3, 100), "a partner on strike: go on");
        Assert.IsFalse(CareerMatchLogic.StopBatch(true, true, false, 3, 100), "out: the rest is simulated");
        Assert.IsTrue(CareerMatchLogic.StopBatch(false, true, true, 3, 100), "the innings is over");
        Assert.IsTrue(CareerMatchLogic.StopBatch(false, false, false, 100, 100), "the cap");
    }

    [Test]
    public void TheCapCoversTheWholeInningsWithRoomForWides()
    {
        Assert.GreaterOrEqual(CareerMatchLogic.BatchCap(120, 0), 120);
        Assert.GreaterOrEqual(CareerMatchLogic.BatchCap(36, 30), 6);
        Assert.Less(CareerMatchLogic.BatchCap(120, 0), 1000, "a bounded batch");
    }

    // ---- Leaving early --------------------------------------------------------------------------

    [Test]
    public void RetiringAtTheCreaseIsOutAndTheNextManTakesYourEnd()
    {
        MatchState s = State(1);
        CareerMatchLogic.Retire(s);
        Assert.IsTrue(s.You.isOut && s.You.retired);
        Assert.AreEqual(CareerMatchLogic.RetiredOut, s.You.how);
        Assert.AreEqual(1, s.wickets, "retired out counts");
        Assert.AreEqual(2, s.runner, "number three takes the non-striker's end");
        Assert.AreEqual(0, s.striker);
    }

    [Test]
    public void RetiringBeforeComingInIsOutAndHeIsPassedOver()
    {
        MatchState s = State(2);
        CareerMatchLogic.Retire(s);
        Assert.IsTrue(s.You.isOut);
        Assert.AreEqual(1, s.wickets);
        CareerMatchLogic.Score(s, CareerSim.Wicket, "b Lee");
        Assert.AreEqual(3, s.striker, "number four comes in, not the man who left");
    }

    [Test]
    public void RetiringWhenAlreadyOutChangesNothing()
    {
        MatchState s = State(0);
        CareerMatchLogic.Score(s, CareerSim.Wicket, "b Lee");
        CareerMatchLogic.Retire(s);
        Assert.AreEqual(1, s.wickets);
        Assert.IsFalse(s.You.retired);
        Assert.AreEqual("b Lee", s.You.how);
    }

    [Test]
    public void TheRestOfALeftInningsIsPlayedOut()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            MatchState s = State(3, 6);
            CareerMatchLogic.Score(s, 1, null);
            CareerMatchLogic.Retire(s);
            CareerMatchLogic.Complete(s, eBowlingLevel.Easy, Plan(3).bowlers, Plan(3).bowlers, new Random(seed));
            Assert.IsTrue(CareerMatchLogic.InningsDone(s), $"seed {seed}: finished");
            Assert.LessOrEqual(s.balls, 36);
            Assert.LessOrEqual(s.wickets, 10);
            Assert.IsTrue(s.You.isOut && s.You.retired, $"seed {seed}");
            int batters = 0;
            foreach (MatchBatter b in s.batters) batters += b.runs;
            Assert.GreaterOrEqual(s.runs, batters, "the total is the batters' runs and the extras");
            CareerMatchResult r = CareerMatchLogic.Build(s, Plan(3), null, true);
            Assert.IsTrue(r.abandoned && r.isOut && r.retired && r.batted);
            Assert.AreEqual(s.runs, r.yours.runs);
        }
    }

    // ---- The result -----------------------------------------------------------------------------

    [Test]
    public void TheResultCarriesHisInningsAndTheCard()
    {
        MatchState s = State(0);
        CareerMatchLogic.Score(s, 4, null);
        CareerMatchLogic.Score(s, 6, null);
        CareerMatchLogic.Score(s, 1, null);
        var theirs = new InningsCard { team = "Rivertown", runs = 5, wickets = 3, balls = 36, overs = 6 };
        CareerMatchResult r = CareerMatchLogic.Build(s, Plan(0), theirs, false);
        Assert.AreEqual(11, r.runs);
        Assert.AreEqual(3, r.balls);
        Assert.AreEqual(1, r.fours);
        Assert.AreEqual(1, r.sixes);
        Assert.IsTrue(r.batted);
        Assert.IsFalse(r.isOut);
        Assert.AreEqual(CareerMatchLogic.NotOut, r.how);
        Assert.AreEqual(2, r.yours.batting.Count, "only those who batted");
        Assert.IsTrue(r.yours.batting[0].isYou);
        Assert.AreEqual(CareerMatchLogic.NotOut, r.yours.batting[1].how);
        CollectionAssert.AreEqual(new[] { 11 }, r.yours.overRuns, "the over it ended in");
        Assert.IsTrue(r.won);
        Assert.AreEqual("Harbour CC won by 6 runs", r.result);
    }

    [Test]
    public void DidNotBatWhenTheInningsEndedBeforeHeCameIn()
    {
        MatchState s = State(10);
        CareerMatchResult r = CareerMatchLogic.Build(s, Plan(10), null, false);
        Assert.IsFalse(r.batted);
        Assert.AreEqual(CareerMatchLogic.DidNotBat, r.how);
        Assert.AreEqual("You: did not bat", CareerMatchLogic.CardNote(r));
    }

    [Test]
    public void TheResultReadsBothInningsTheRightWayRound()
    {
        var a = new InningsCard { runs = 150, wickets = 7, balls = 120, overs = 20 };
        var b = new InningsCard { runs = 151, wickets = 4, balls = 113, overs = 20 };
        Assert.AreEqual("Rivertown won by 6 wickets (7 balls left)",
                        CareerMatchLogic.ResultText(a, b, true, "Harbour CC", "Rivertown", out bool won, out bool tied));
        Assert.IsFalse(won || tied);
        Assert.AreEqual("Harbour CC won by 6 wickets (7 balls left)",
                        CareerMatchLogic.ResultText(b, a, false, "Harbour CC", "Rivertown", out won, out tied));
        Assert.IsTrue(won);
        var c = new InningsCard { runs = 140, wickets = 10, balls = 110, overs = 20 };
        Assert.AreEqual("Rivertown won by 10 runs",
                        CareerMatchLogic.ResultText(c, a, false, "Harbour CC", "Rivertown", out won, out tied));
        Assert.IsFalse(won);
        Assert.AreEqual("Match tied", CareerMatchLogic.ResultText(a, new InningsCard { runs = 150, overs = 20 }, true, "H", "R", out won, out tied));
        Assert.IsTrue(tied);
        Assert.IsFalse(won);
        var one = new InningsCard { runs = 151, wickets = 9, balls = 119, overs = 20 };
        Assert.AreEqual("Harbour CC won by 1 wicket (1 ball left)",
                        CareerMatchLogic.ResultText(one, a, false, "Harbour CC", "Rivertown", out won, out _));
    }

    [Test]
    public void ClosingTheOversCountsThePartOverOnce()
    {
        MatchState s = State(6);
        for (int i = 0; i < 8; i++)
            CareerMatchLogic.Score(s, 1, null);
        CareerMatchLogic.CloseOvers(s);
        CareerMatchLogic.CloseOvers(s);
        CollectionAssert.AreEqual(new[] { 6, 2 }, s.overRuns);
    }

    [Test]
    public void AStandInSideHasEleven()
    {
        Team t = CareerMatchLogic.StandInTeam("x", "Rivertown", new List<string> { "Lee", "Khan" }, null, 33f);
        Assert.AreEqual(11, t.players.Count);
        Assert.AreEqual("Khan", t.players[1].name);
        Assert.AreEqual("Rivertown 11", t.players[10].name);
        Assert.AreEqual(33f, t.players[5].bat);
    }

    // ---- The words ------------------------------------------------------------------------------

    [Test]
    public void TheCardsSayWhatHappened()
    {
        Assert.AreEqual("Ravi 18 (11) - out c Khan b Lee", CareerMatchLogic.BatterText("Ravi", 18, 11, true, "c Khan b Lee"));
        Assert.AreEqual("Ravi 4 (3) - run out", CareerMatchLogic.BatterText("Ravi", 4, 3, true, "run out"));
        Assert.AreEqual("Ravi 22 (15) not out", CareerMatchLogic.BatterText("Ravi", 22, 15, false, null));
        Assert.AreEqual("45/4 after 6.2 overs", CareerMatchLogic.ScoreAfter(45, 4, 38));
        Assert.AreEqual("You come in at 45/4 (6.2 overs)", CareerMatchLogic.ComeIn(45, 4, 38));
        Assert.AreEqual("Your innings: 34 (22), c Khan b Lee", CareerMatchLogic.YourInnings(34, 22, true, "c Khan b Lee"));
        Assert.AreEqual("A\nB\n45/4 after 6.2 overs", CareerMatchLogic.CatchUpBody(new[] { "A", "B" }, 45, 4, 38));
        var r = new CareerMatchResult { batted = true, runs = 34, balls = 22, isOut = true, how = "c Khan b Lee" };
        Assert.AreEqual("You: 34 (22) - c Khan b Lee", CareerMatchLogic.CardNote(r));
        r.isOut = false;
        Assert.AreEqual("You: 34 (22) - not out", CareerMatchLogic.CardNote(r));
    }

    [Test]
    public void HowOutComesCleanOutOfTheDismissalLine()
    {
        Assert.AreEqual("c Point b Khan", CareerMatchLogic.CleanHow("Ravi", "Ravi c Point b Khan  34 (22)"));
        Assert.AreEqual("b Khan", CareerMatchLogic.CleanHow("Ravi", "b Khan"));
        Assert.AreEqual("out", CareerMatchLogic.CleanHow("Ravi", null));
    }

    [Test]
    public void EverythingShownIsLatin1()
    {
        var all = new List<string>
        {
            CareerMatchLogic.AtTheOtherEnd, CareerMatchLogic.ComeIn(1, 1, 1), CareerMatchLogic.ScoreAfter(1, 1, 1),
            CareerMatchLogic.BatterText("A", 1, 1, true, "b B"), CareerMatchLogic.YourInnings(1, 1, false, null),
            CareerMatchLogic.CardNote(null), CareerMatchLogic.ResultText(new InningsCard(), new InningsCard { runs = 1 }, true, "A", "B", out _, out _),
        };
        foreach (string s in all)
            foreach (char c in s)
                Assert.LessOrEqual((int)c, 255, s);
    }

    // ---- The session ----------------------------------------------------------------------------

    [Test]
    public void AMatchFinishesOnce()
    {
        MatchPlan plan = Plan(3);
        CareerMatchSession.SetPlanForTests(plan);
        Assert.IsTrue(CareerMatchSession.Active);
        var first = new CareerMatchResult { runs = 12 };
        CareerMatchSession.Finish(first);
        Assert.IsFalse(CareerMatchSession.Active);
        Assert.IsTrue(CareerMatchSession.ReturnToHub);
        Assert.AreSame(first, CareerMatchSession.LastResult);
        Assert.AreSame(plan, CareerMatchSession.LastPlan);
        CareerMatchSession.Finish(new CareerMatchResult { runs = 99 });
        Assert.AreSame(first, CareerMatchSession.LastResult, "a second finish does nothing");
    }

    [Test]
    public void TheirInningsIsPlayedFromThePlanWithNoSeason()
    {
        CareerMatchSession.SetPlanForTests(Plan(3));
        Team t = CareerMatchSession.TeamOf(CareerMatchSession.Plan, false);
        Assert.AreEqual(11, t.players.Count);
        Assert.AreEqual("Lee", t.players[0].name);
        InningsCard c = CareerMatchSession.PlayTheirInnings(6, 40);
        Assert.IsNotNull(c);
        Assert.AreEqual(40, c.target);
    }
}
