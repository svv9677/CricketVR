using NUnit.Framework;
using K = BatContact.ContactKind;

/// <summary>
/// One judge for the nets (ShotReading, ShotTips): the shot card's career word and the nets text's
/// verdict are the same real-blade call; the wide bat never turns an edge into "good contact" in
/// either; every kind of ball gets a reading and a coach's line; the words are Latin-1 and stable.
/// </summary>
public class ShotReadingTests
{
    private static BallRecord Played(K realKind, eRealContact real, float exit = 25f, float launch = 5f,
                                     float angle = 40f, eBallEnd end = eBallEnd.Runs, int runs = 2, float line = 0.05f)
    {
        var r = DrillTests.R(eBallAction.Played, real, line: line, angle: angle, runs: runs, end: end,
                             exit: exit, launch: launch, carry: 20f);
        r.realKind = (int)realKind;
        r.contact = (int)K.Good;     // the shield's call: good contact
        r.speedKph = 132f;
        return r;
    }

    [Test]
    public void TheShotCardAndTheNetsTextGiveTheSameWord()
    {
        // The shield said good contact; the real blade a thin edge. Both must say thin edge.
        BallRecord r = Played(K.ThinEdge, eRealContact.Edge);
        string card = ShotReading.ContactWord(r.realContact, r.realKind, r.realQuality);
        Assert.AreEqual("Thin edge", card);
        StringAssert.Contains("Thin edge", ShotReading.Verdict(r));
        StringAssert.DoesNotContain("Good contact", ShotReading.Verdict(r));
    }

    [Test]
    public void EveryRealContactHasItsOwnWord()
    {
        Assert.AreEqual("Middled", ShotReading.ContactWord(eRealContact.Clean, (int)K.Middled, 0.9f));
        Assert.AreEqual("Good contact", ShotReading.ContactWord(eRealContact.Clean, (int)K.Good, 0.6f));
        Assert.AreEqual("Off the toe", ShotReading.ContactWord(eRealContact.Edge, (int)K.Toe, 0.2f));
        Assert.AreEqual("Thick edge", ShotReading.ContactWord(eRealContact.Edge, (int)K.ThickEdge, 0.3f));
        Assert.AreEqual("Missed it", ShotReading.ContactWord(eRealContact.WouldHaveMissed, -1, 0f));
        // Old saves without the real kind still read sensibly.
        Assert.AreEqual("Edge", ShotReading.ContactWord(eRealContact.Edge, -1, 0.3f));
        Assert.AreEqual("Middled", ShotReading.ContactWord(eRealContact.Clean, -1, 0.85f));
    }

    [Test]
    public void OnlyMiddledAndGoodContactAreClean()
    {
        Assert.IsTrue(ShotReading.IsClean(eRealContact.Clean, (int)K.Middled));
        Assert.IsTrue(ShotReading.IsClean(eRealContact.Clean, (int)K.Good));
        Assert.IsFalse(ShotReading.IsClean(eRealContact.Clean, (int)K.Toe));
        Assert.IsFalse(ShotReading.IsClean(eRealContact.Edge, (int)K.Good));
        Assert.IsFalse(ShotReading.IsClean(eRealContact.WouldHaveMissed, -1));
    }

    [Test]
    public void DirectionsCoverTheField()
    {
        Assert.AreEqual("straight back past the bowler", ShotReading.Direction(3f));
        Assert.AreEqual("through the covers", ShotReading.Direction(55f));
        Assert.AreEqual("through midwicket", ShotReading.Direction(-55f));
        Assert.AreEqual("square through point", ShotReading.Direction(95f));
        Assert.AreEqual("fine to leg", ShotReading.Direction(-130f));
        Assert.AreEqual("behind the wicket", ShotReading.Direction(175f));
    }

    [Test]
    public void LinesAreSaidAsABatterWouldSayThem()
    {
        Assert.AreEqual("on the stumps", ShotReading.Line(0.05f, false));
        Assert.AreEqual("on the stumps", ShotReading.Line(0.4f, true));
        Assert.AreEqual("just outside off", ShotReading.Line(0.2f, false));
        Assert.AreEqual("30 cm outside off", ShotReading.Line(0.42f, false));
        Assert.AreEqual("down leg", ShotReading.Line(-0.5f, false));
    }

    [Test]
    public void EveryKindOfBallGetsAReadingAndACoachLine()
    {
        var balls = new[]
        {
            Played(K.Middled, eRealContact.Clean, exit: 33f, end: eBallEnd.Four, runs: 4),
            Played(K.Good, eRealContact.Clean, exit: 6f, end: eBallEnd.Dot, runs: 0),
            Played(K.Middled, eRealContact.Clean, exit: 35f, launch: 35f, end: eBallEnd.Six, runs: 6),
            Played(K.ThinEdge, eRealContact.Edge, line: 0.5f),
            Played(K.ThickEdge, eRealContact.Edge),
            Played(K.Toe, eRealContact.Edge),
            Played(K.Shoulder, eRealContact.Edge),
            Played(K.Handle, eRealContact.Edge),
            DrillTests.R(eBallAction.Left, line: 0.6f),
            DrillTests.R(eBallAction.Left, line: 0.05f, hitting: true),
            DrillTests.R(eBallAction.Missed, line: 0.5f),
            DrillTests.R(eBallAction.Missed, k: eBowlerKind.LegSpin, d: eDelivery.LegBreak),
            DrillTests.R(eBallAction.Missed, end: eBallEnd.Out),
        };
        foreach (BallRecord r in balls)
        {
            string reading = ShotReading.Read(r);
            Assert.IsNotEmpty(ShotTips.For(r), reading);
            foreach (char c in reading)
                Assert.LessOrEqual(c, 'ÿ', reading);
        }
    }

    [Test]
    public void AGoodLeaveSaysByHowMuchItMissedAndAStraightOneIsCalledOut()
    {
        string straight = ShotTips.For(DrillTests.R(eBallAction.Left, line: 0.05f, hitting: true));
        Assert.IsTrue(straight.Contains("hitting the stumps") || straight.Contains("would have bowled you"), straight);
        string brave = ShotTips.For(DrillTests.R(eBallAction.Left, line: 0.19f));
        Assert.IsTrue(brave.Contains("missed by 4 cm") || brave.Contains("off stump"), brave);
        StringAssert.Contains("Left alone", ShotReading.Verdict(DrillTests.R(eBallAction.Left, line: 0.6f)));
    }

    [Test]
    public void TheShotLineSaysHowHardWhereAndHowFar()
    {
        BallRecord r = Played(K.Middled, eRealContact.Clean, exit: 30f, launch: 30f, angle: -55f);
        string shot = ShotReading.Shot(r);
        StringAssert.Contains("108 km/h", shot);
        StringAssert.Contains("lofted through midwicket", shot);
        StringAssert.Contains("20 m carry", shot);
        Assert.AreEqual("", ShotReading.Shot(DrillTests.R(eBallAction.Left)));
    }

    [Test]
    public void TheCoachLineIsTheSameForTheSameBall()
    {
        BallRecord a = Played(K.Middled, eRealContact.Clean), b = Played(K.Middled, eRealContact.Clean);
        Assert.AreEqual(ShotTips.For(a), ShotTips.For(b));
    }
}
