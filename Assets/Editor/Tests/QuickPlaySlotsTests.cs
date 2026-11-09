using NUnit.Framework;
using UnityEngine;

/// Quick play's slots (2026-11-09: a slot per length, continue / restart / new game): a match saved
/// between balls comes back as it was, and the screen says how it stands.
public class QuickPlaySlotsTests
{
    private static MatchSnapshot Chase()
    {
        var m = new MatchSnapshot { runs = 45, wickets = 2, overs = 6, balls = 3, totalOvers = 20, target = 121, striker = 1, runner = 4, bowler = 3 };
        m.bowledBalls.AddRange(new[] { "1", "4", "W", "*" });
        m.batters.Add(new MatchSnapshot.BatterLine { name = "Ishan", runs = 12, balls = 9, howOut = "c Point b Shami" });
        m.bowlers.Add(new MatchSnapshot.BowlerLine { name = "Shami", balls = 12, conceded = 14, wickets = 1 });
        m.overBowlers.AddRange(new[] { 0, 3, 0 });
        m.hasAIFirst = true;
        var ai = new AIInnings.Result { runs = 120, wickets = 7, balls = 120, overs = 20 };
        ai.overRuns.AddRange(new[] { 6, 8, 4 });
        m.aiFirst = MatchSnapshot.AILine.From(ai);
        return m;
    }

    [Test]
    public void ASavedMatchComesBackAsItWas()
    {
        var slot = new QuickPlaySlot { format = (int)eMatchFormat.Twenty, hasSetup = true, inProgress = true, match = Chase() };
        slot.setup.bowling = (int)eBowlingLevel.Hard;
        QuickPlaySlot back = JsonUtility.FromJson<QuickPlaySlot>(JsonUtility.ToJson(slot));
        MatchSnapshot m = back.match;
        Assert.AreEqual("45/2 (6.3 ov)", m.ScoreText);
        Assert.AreEqual(121, m.target);
        CollectionAssert.AreEqual(new[] { "1", "4", "W", "*" }, m.bowledBalls);
        Assert.AreEqual("c Point b Shami", m.batters[0].howOut);
        Assert.AreEqual(14, m.bowlers[0].conceded);
        CollectionAssert.AreEqual(new[] { 0, 3, 0 }, m.overBowlers);
        AIInnings.Result ai = m.aiFirst.ToResult();
        Assert.AreEqual(120, ai.runs);
        CollectionAssert.AreEqual(new[] { 6, 8, 4 }, ai.overRuns, "the AI's overs, for the replay at home");
        Assert.AreEqual((int)eBowlingLevel.Hard, back.setup.bowling);
    }

    [Test]
    public void TheScreenSaysHowAMatchStands()
    {
        var chase = new QuickPlaySlot { inProgress = true, match = Chase() };
        Assert.AreEqual("45/2 (6.3 ov)  ·  need 76 off 81", QuickPlaySlots.Progress(chase));

        MatchSnapshot first = Chase();
        first.target = 0;
        Assert.AreEqual("45/2  ·  6.3 of 20 overs", QuickPlaySlots.Progress(new QuickPlaySlot { inProgress = true, match = first }));

        var test = new MatchSnapshot { test = true, runs = 45, wickets = 2, overs = 10, totalOvers = MatchFormat.TestInningsOvers, matchBalls = 600 };
        test.innings.Add(new MatchSnapshot.InningsLine { yours = false, runs = 380, wickets = 10, balls = 600 });
        Assert.AreEqual("Day 2  ·  You 45/2  ·  AI 380", QuickPlaySlots.Progress(new QuickPlaySlot { inProgress = true, match = test }));

        Assert.AreEqual("Last match: Won by 3 wickets",
                        QuickPlaySlots.Progress(new QuickPlaySlot { inProgress = false, lastResult = "Last match: Won by 3 wickets" }));
    }

    [Test]
    public void EveryLengthHasItsOwnSlotAndNewGameStepTitle()
    {
        Assert.AreEqual(5, MatchFormat.Order.Length, "5, 10, 20, 50 overs and a Test");
        Assert.AreEqual("20-over match", MatchFormat.MatchName(eMatchFormat.Twenty));
        Assert.AreEqual("Test match", MatchFormat.MatchName(eMatchFormat.Test));
        StringAssert.EndsWith("you chase", new QuickPlaySetup { type = (int)eMatchType.Chase }.Summary);
        StringAssert.EndsWith("you bat first", new QuickPlaySetup { type = (int)eMatchType.SetTotal }.Summary);
    }

    [Test]
    public void TheQuickPlayLeaveQuestionSaysItIsSaved()
    {
        StringAssert.Contains("saved", ModeMenus.LeaveQuestion(ePlayMode.QuickPlay));
        StringAssert.Contains("retire", ModeMenus.LeaveQuestion(ePlayMode.CareerMatch), "the career's own, unchanged");
    }
}
