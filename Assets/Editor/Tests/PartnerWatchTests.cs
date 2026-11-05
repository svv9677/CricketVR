using NUnit.Framework;

/// <summary>
/// Watching the partner bat (PartnerWatch), the rules that need no scene: when it is live, that a
/// ball stays the partner's from its release to its reset whatever the strike does, that a run of
/// simulated balls stops for the player at either end only while watching (phase 2's "on strike"
/// otherwise), that a partner's ball is kept out of the player's ratings and the fielding captain's
/// book (and only in a career match), and that the dice come back the same each scene.
/// </summary>
public class PartnerWatchTests
{
    private static Batsman Partner() => new Batsman("Partner") { IsHuman = false };
    private static Batsman You() => new Batsman("You") { IsHuman = true };

    [SetUp]
    public void Fresh() => PartnerWatch.Reset();

    [TearDown]
    public void CleanUp() => PartnerWatch.Reset();

    [Test]
    public void LiveOnlyWithThePartnerOnStrikeAndYouAtTheOtherEnd()
    {
        Assert.IsTrue(PartnerWatch.LiveFor(true, Partner(), You()));
        Assert.IsFalse(PartnerWatch.LiveFor(false, Partner(), You()), "watching off: phase 2");
        Assert.IsFalse(PartnerWatch.LiveFor(true, You(), Partner()), "you are on strike");
        Assert.IsFalse(PartnerWatch.LiveFor(true, Partner(), Partner()), "you are not in yet, or out: simulated");
        Assert.IsFalse(PartnerWatch.LiveFor(true, null, You()));
        Assert.IsFalse(PartnerWatch.LiveFor(true, Partner(), null));
    }

    [Test]
    public void TheBallIsFrozenFromReleaseToReset()
    {
        bool b = PartnerWatch.Frozen(eGameState.InGame_DeliverBall, false, true);
        Assert.IsTrue(b, "live at the release: the partner's ball");
        // HUD changes the strike at the dead ball, before the reset: Live then says otherwise.
        foreach (eGameState s in new[] { eGameState.InGame_DeliverBallLoop, eGameState.InGame_BallHit, eGameState.InGame_BallHitLoop,
                                         eGameState.InGame_BallFielded, eGameState.InGame_BallFielded_Loop, eGameState.InGame_BallPastBoundary,
                                         eGameState.InGame_Bowled, eGameState.InGame_BallMissed })
        {
            b = PartnerWatch.Frozen(s, b, false);
            Assert.IsTrue(b, $"still his ball at {s}");
        }
        Assert.IsFalse(PartnerWatch.Frozen(eGameState.InGame_ResetToReady, b, true), "let go at the reset");
        Assert.IsFalse(PartnerWatch.Frozen(eGameState.InGame_DeliverBall, true, false), "your ball: not his");
    }

    [Test]
    public void BetweenBallsIsWhenNoBallIsInPlay()
    {
        foreach (eGameState s in new[] { eGameState.InGame_Ready, eGameState.InGame_SelectDelivery, eGameState.InGame_SelectDeliveryLoop,
                                         eGameState.InGame_ResetToReady, eGameState.InGame_ResetToReadyLoop })
            Assert.IsTrue(PartnerWatch.BetweenBalls(s), s.ToString());
        foreach (eGameState s in new[] { eGameState.InGame_DeliverBall, eGameState.InGame_DeliverBallLoop, eGameState.InGame_BallHit,
                                         eGameState.InGame_BallHitLoop, eGameState.InGame_BallFielded_Loop, eGameState.InGame_BowledLoop,
                                         eGameState.InGame_BallMissedLoop, eGameState.InGame_BallPastBoundaryLoop })
            Assert.IsFalse(PartnerWatch.BetweenBalls(s), s.ToString());
    }

    [Test]
    public void ABatchStopsForYouAtEitherEndOnlyWhileWatching()
    {
        // Phase 2 (watching off): only on strike.
        Assert.IsTrue(PartnerWatch.YourTurn(true, true, false));
        Assert.IsFalse(PartnerWatch.YourTurn(false, true, false), "off: his balls are simulated while you watch nothing");
        // Watching: in at the non-striker's end is your turn too (his balls are bowled for real).
        Assert.IsTrue(PartnerWatch.YourTurn(false, true, true));
        Assert.IsTrue(PartnerWatch.YourTurn(true, true, true));
        Assert.IsFalse(PartnerWatch.YourTurn(false, false, true), "not in yet: simulated");
    }

    [Test]
    public void TheBatchStopsAsCareerMatchAsksIt()
    {
        const int cap = 100;
        Assert.IsTrue(CareerMatchLogic.StopBatch(PartnerWatch.YourTurn(false, true, true), false, false, 3, cap),
                      "watching, you come in as the non-striker: stop and bowl to him");
        Assert.IsFalse(CareerMatchLogic.StopBatch(PartnerWatch.YourTurn(false, true, false), false, false, 3, cap),
                       "not watching: on until you have the strike");
        Assert.IsFalse(CareerMatchLogic.StopBatch(PartnerWatch.YourTurn(false, false, true), true, false, 3, cap),
                       "out: the rest of the innings is simulated");
    }

    [Test]
    public void APartnersBallIsNotYoursOnlyInACareerMatch()
    {
        Batsman you = You();
        Assert.IsTrue(PartnerWatch.PartnersBall(Partner(), you), "kept out of your ratings and the captain's book");
        Assert.IsFalse(PartnerWatch.PartnersBall(you, you), "your own ball is measured");
        // Quick Match: nobody is the player (HUD.Human is null), every ball is his as before.
        Assert.IsFalse(PartnerWatch.PartnersBall(new Batsman("Opener"), null));
        // The nets: no striker at all.
        Assert.IsFalse(PartnerWatch.PartnersBall(null, null));
        Assert.IsFalse(PartnerWatch.PartnersBall(null, you));
    }

    [Test]
    public void TheDiceAreTheSameEachScene()
    {
        PartnerWatch.Reset();
        int a = PartnerWatch.Rng.Next();
        PartnerWatch.Reset();
        Assert.AreEqual(a, PartnerWatch.Rng.Next(), "seeded, not from the clock");
        Assert.AreEqual(new System.Random(0 ^ PartnerWatch.RngSalt).Next(), a, "no plan: seed 0 with the salt");
    }

    [Test]
    public void AnIntentReadsInTheLog()
    {
        Assert.AreEqual("W", PartnerDirector.IntentText(CareerSim.Wicket));
        Assert.AreEqual("wide", PartnerDirector.IntentText(PartnerIntent.LeaveWide));
        Assert.AreEqual("4", PartnerDirector.IntentText(4));
    }
}
