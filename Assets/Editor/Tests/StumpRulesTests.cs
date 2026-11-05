using NUnit.Framework;

/// <summary>
/// Which stumps can bowl the striker (StumpRules). Mutation-checked 2026-11-05: with the
/// strikersEnd term dropped from CountsAsBowled, TheBowlersEndNeverBowlsAnyone fails.
/// Run: Window > General > Test Runner > EditMode > StumpRulesTests.
/// </summary>
public class StumpRulesTests
{
    private static readonly eGameState[] Live =
        { eGameState.InGame_DeliverBallLoop, eGameState.InGame_BallHit, eGameState.InGame_BallHitLoop };

    [Test]
    public void TheStrikersStumpsBowlHimWhileTheBallIsLive()
    {
        foreach (eGameState s in Live)
            Assert.IsTrue(StumpRules.CountsAsBowled(true, s), s.ToString());
    }

    [Test]
    public void TheBowlersEndNeverBowlsAnyone()
    {
        foreach (eGameState s in System.Enum.GetValues(typeof(eGameState)))
            Assert.IsFalse(StumpRules.CountsAsBowled(false, s), s.ToString());
    }

    [Test]
    public void ADeadBallBowlsNobody()
    {
        foreach (eGameState s in System.Enum.GetValues(typeof(eGameState)))
            if (System.Array.IndexOf(Live, s) < 0)
                Assert.IsFalse(StumpRules.CountsAsBowled(true, s), s.ToString());
    }
}
