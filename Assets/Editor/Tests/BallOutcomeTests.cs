using NUnit.Framework;

/// <summary>
/// What each scorebook entry (HUD.BowledBalls) shows over the stumps and has the umpire signal
/// (HUD.Describe), and the batters' and bowlers' figures. Standalone - no scene.
/// Run: Window > General > Test Runner > EditMode > BallOutcomeTests.
/// </summary>
public class BallOutcomeTests
{
    [TestCase("*", eGameState.InGame_BallFielded, "0", UmpireSignal.None)]
    [TestCase("2", eGameState.InGame_BallFielded, "2", UmpireSignal.None)]
    [TestCase("4", eGameState.InGame_BallFielded, "4", UmpireSignal.None)]        // run, not a boundary
    [TestCase("4", eGameState.InGame_BallPastBoundary, "FOUR", UmpireSignal.Four)]
    [TestCase("6", eGameState.InGame_BallPastBoundary, "SIX", UmpireSignal.Six)]
    [TestCase("W", eGameState.InGame_Bowled, "BOWLED", UmpireSignal.Out)]
    [TestCase("W", eGameState.InGame_BallFielded, "OUT", UmpireSignal.Out)]       // caught
    [TestCase("wd", eGameState.InGame_BallMissed, "WIDE", UmpireSignal.Wide)]
    [TestCase("3wd", eGameState.InGame_BallFielded, "WIDE +2", UmpireSignal.Wide)]
    [TestCase("1b", eGameState.InGame_BallFielded, "1 BYE", UmpireSignal.Bye)]
    [TestCase("4b", eGameState.InGame_BallPastBoundary, "4 BYES", UmpireSignal.Bye)]
    public void EntryReadsAndIsSignalled(string entry, eGameState state, string label, UmpireSignal signal)
    {
        BallOutcome o = HUD.Describe(entry, state);
        Assert.AreEqual(label, o.Label);
        Assert.AreEqual(signal, o.Signal);
    }

    [Test]
    public void FiguresAndStrikeRate()
    {
        var bat = new Batsman("Ishan") { Runs = 23, Balls = 17 };
        Assert.AreEqual("23 (17)", HUD.RunsAndBalls(bat));
        Assert.AreEqual("135.3", HUD.StrikeRate(bat));
        Assert.AreEqual("-", HUD.StrikeRate(new Batsman("New")), "no balls faced: no strike rate");
        var bowler = new Bowler("Bumrah") { Balls = 15, Conceded = 12, Wickets = 1 };
        Assert.AreEqual("2.3-12-1", HUD.Figures(bowler));
    }
}
