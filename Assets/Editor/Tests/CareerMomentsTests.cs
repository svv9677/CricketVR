using System;
using NUnit.Framework;

/// <summary>
/// A career match's record moments with no scene (CareerMatchLogic.ObserveMoments and NoteSix): the
/// balls to his fifty and hundred kept the first time and never moved, a six counted only off the
/// real blade and only when it is the match's longest, Build copying them into the result, and a
/// match the app died in keeping them through CareerMatchRecovery into the record book.
/// </summary>
public class CareerMomentsTests
{
    [SetUp]
    public void SetUp()
    {
        CareerMatchSession.ResetForTests();
        RecordMoments.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        CareerMatchSession.ResetForTests();
        RecordMoments.ResetForTests();
    }

    /// A plan with eleven names, the career player at `slot`.
    private static MatchPlan Plan(int slot = 2)
    {
        var plan = new MatchPlan { slot = slot, overs = 20, batFirst = true, seed = 1234, yourTeamName = "Rivertown", opponentName = "Harbour CC" };
        for (int i = 0; i < 11; i++)
        {
            plan.order.Add(i == slot ? "Sam Carter" : $"Batter {i + 1}");
            plan.orderRatings.Add(50f);
        }
        return plan;
    }

    private static MatchState State(MatchPlan plan) => CareerMatchLogic.NewState(plan, plan.overs * 6, plan.Target);

    [Test]
    public void TheBallsToFiftyAndHundredAreKeptTheFirstTime()
    {
        MatchState s = State(Plan());
        MatchBatter you = s.You;
        you.runs = 49; you.balls = 30;
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(0, s.ballsToFifty, "not there yet");
        you.runs = 53; you.balls = 31;
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(31, s.ballsToFifty);
        you.runs = 70; you.balls = 45;
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(31, s.ballsToFifty, "kept, never moved");
        Assert.AreEqual(0, s.ballsToHundred);
        you.runs = 104; you.balls = 66;
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(66, s.ballsToHundred);
        Assert.AreEqual(31, s.ballsToFifty);
    }

    [Test]
    public void AFiftyAndAHundredInOneLookAreBothKept()
    {
        MatchState s = State(Plan());
        s.You.runs = 100; s.You.balls = 58;
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(58, s.ballsToFifty);
        Assert.AreEqual(58, s.ballsToHundred);
    }

    [Test]
    public void ObserveMomentsWatchesOnlyHim()
    {
        MatchState s = State(Plan(slot: 3));
        s.batters[0].runs = 80; s.batters[0].balls = 40;   // a partner's fifty is not his
        CareerMatchLogic.ObserveMoments(s);
        Assert.AreEqual(0, s.ballsToFifty);
        CareerMatchLogic.ObserveMoments(null);
        CareerMatchLogic.ObserveMoments(new MatchState());   // nobody is him: nothing happens
    }

    [Test]
    public void ASixCountsOnlyOffTheRealBlade()
    {
        MatchState s = State(Plan());
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, 110f, eRealContact.WouldHaveMissed, "x"), "the shield's six never counts");
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, 110f, eRealContact.None, "x"));
        Assert.AreEqual(0f, s.longestSix);
        Assert.AreEqual("", s.sixStamp);
        Assert.IsTrue(CareerMatchLogic.NoteSix(s, 74f, eRealContact.Edge, "e"), "an edge over the rope off the real blade counts");
        Assert.IsTrue(CareerMatchLogic.NoteSix(s, 80f, eRealContact.Clean, "c"));
        Assert.AreEqual(80f, s.longestSix);
        Assert.AreEqual("c", s.sixStamp);
    }

    [Test]
    public void OnlyTheMatchsLongestSixIsKept()
    {
        MatchState s = State(Plan());
        Assert.IsTrue(CareerMatchLogic.NoteSix(s, 90f, eRealContact.Clean, "first"));
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, 90f, eRealContact.Clean, "tie"), "a tie keeps the first");
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, 72f, eRealContact.Clean, "short"));
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, float.NaN, eRealContact.Clean, "nan"));
        Assert.IsFalse(CareerMatchLogic.NoteSix(s, 0f, eRealContact.Clean, "zero"));
        Assert.IsFalse(CareerMatchLogic.NoteSix(null, 100f, eRealContact.Clean, "none"));
        Assert.AreEqual("first", s.sixStamp);
        Assert.IsTrue(CareerMatchLogic.NoteSix(s, 95f, eRealContact.Clean, null));
        Assert.AreEqual("", s.sixStamp, "no stamp is an empty one");
    }

    [Test]
    public void BuildCopiesTheMoments()
    {
        MatchPlan plan = Plan();
        MatchState s = State(plan);
        s.You.runs = 101; s.You.balls = 60; s.You.batted = true;
        CareerMatchLogic.ObserveMoments(s);
        CareerMatchLogic.NoteSix(s, 88.5f, eRealContact.Clean, "000004d2-b44");
        CareerMatchResult r = CareerMatchLogic.Build(s, plan, new InningsCard { team = "Harbour CC", runs = 90, overs = 20 }, false);
        Assert.AreEqual(60, r.ballsToFifty);
        Assert.AreEqual(60, r.ballsToHundred);
        Assert.AreEqual(88.5f, r.longestSix);
        Assert.AreEqual("000004d2-b44", r.sixStamp);
        Assert.IsNotNull(r.bests);
        Assert.IsEmpty(r.bests, "the record book says which records broke, later");
    }

    [Test]
    public void TheRecoverySnapshotKeepsTheMoments()
    {
        MatchPlan plan = Plan();
        var p = new CareerProfile { name = "Sam Carter" };
        CareerMatchRecovery.Begin(p, plan);
        p.pending.resumable = false;   // a save from before matches could be continued (2026-11-09)
        MatchState s = State(plan);
        s.ballsToFifty = 33; s.ballsToHundred = 0; s.longestSix = 91f; s.sixStamp = "abc-b12";
        CareerMatchRecovery.Note(p, s);
        Assert.AreEqual(33, p.pending.ballsToFifty);
        Assert.AreEqual(91f, p.pending.longestSix);
        Assert.AreEqual("abc-b12", p.pending.sixStamp);
        MatchState back = CareerMatchRecovery.FromSnapshot(plan, p.pending);
        Assert.AreEqual(33, back.ballsToFifty);
        Assert.AreEqual(0, back.ballsToHundred);
        Assert.AreEqual(91f, back.longestSix);
        Assert.AreEqual("abc-b12", back.sixStamp);
        p.pending.sixStamp = null;
        Assert.AreEqual("", CareerMatchRecovery.FromSnapshot(plan, p.pending).sixStamp);
    }

    [Test]
    public void AMatchTheAppDiedInKeepsItsRecordsInTheBook()
    {
        var p = new CareerProfile { name = "Sam Carter", nation = "AUS", created = "2026-11-05", trialDone = true };
        CareerProgress.EnsureSeason(p, new Random(7));
        MatchPlan plan = CareerProgress.PlanNext(p, new Random(8));
        CareerMatchRecovery.Begin(p, plan);
        p.pending.resumable = false;   // a save from before matches could be continued (2026-11-09)
        MatchState s = State(plan);
        s.runs = 60; s.wickets = 1; s.balls = 40;
        s.You.runs = 55; s.You.balls = 30; s.You.batted = true; s.You.sixes = 2;
        CareerMatchLogic.ObserveMoments(s);
        CareerMatchLogic.NoteSix(s, 84f, eRealContact.Clean, "dead-b30");
        CareerMatchRecovery.Note(p, s);

        Assert.IsTrue(CareerMatchRecovery.Resolve(p));
        InningsRecord last = p.innings[p.innings.Count - 1];
        Assert.AreEqual(30, last.ballsToFifty);
        Assert.AreEqual(84f, last.longestSix);
        Assert.AreEqual(30f, p.recordBook.fastestFifty.value);
        Assert.AreEqual(84f, p.recordBook.longestSix.value);
        Assert.AreEqual("dead-b30", p.recordBook.longestSix.clipStamp);
    }
}
