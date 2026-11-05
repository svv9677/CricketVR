using NUnit.Framework;

/// <summary>
/// No-balls and free hits (NoBalls): a no-ball is called at the release and earns a free hit; the
/// batter is protected on both; a free hit that is a wide or a no-ball passes the free hit on, a
/// legal one uses it up; and overstepping is rarer the better the attack. Standalone.
/// </summary>
public class NoBallTests
{
    private static float Never() => 1f;
    private static float Always() => 0f;

    [SetUp]
    public void Fresh() => NoBalls.NewInnings();

    [Test]
    public void ANoBallIsCalledAtTheReleaseAndEarnsAFreeHit()
    {
        NoBalls.NewDelivery(Always);
        Assert.IsTrue(NoBalls.Overstep);
        Assert.IsFalse(NoBalls.Called, "not called until it is bowled");
        NoBalls.Released();
        Assert.IsTrue(NoBalls.Called);
        Assert.IsTrue(NoBalls.Protected, "no wicket off a no-ball");
        NoBalls.Scored(true);
        Assert.IsTrue(NoBalls.NextIsFreeHit);
        NoBalls.NewDelivery(Never);
        Assert.IsTrue(NoBalls.FreeHit);
        Assert.IsTrue(NoBalls.Protected, "no wicket off a free hit");
    }

    [Test]
    public void AFreeHitCarriesOverAWideButALegalBallUsesItUp()
    {
        NoBalls.NewDelivery(Always); NoBalls.Released(); NoBalls.Scored(true);
        NoBalls.NewDelivery(Never);                   // the free hit...
        NoBalls.Released();
        NoBalls.Scored(true);                         // ...is a wide
        Assert.IsTrue(NoBalls.NextIsFreeHit, "still a free hit after a wide");
        NoBalls.NewDelivery(Never);
        NoBalls.Released();
        NoBalls.Scored(false);                        // legal
        Assert.IsFalse(NoBalls.NextIsFreeHit);
        NoBalls.NewDelivery(Never);
        Assert.IsFalse(NoBalls.Protected);
    }

    [Test]
    public void BetterAttacksOverstepLess()
    {
        float last = 1f;
        foreach (eBowlingLevel l in new[] { eBowlingLevel.Easy, eBowlingLevel.Medium, eBowlingLevel.Hard, eBowlingLevel.Pro })
        {
            float c = NoBalls.ChanceFor(l);
            Assert.Less(c, last);
            Assert.Greater(c, 0f);
            last = c;
        }
    }
}
