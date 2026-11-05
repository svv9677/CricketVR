using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The career ratings (CareerRatings): unknown until each has its minimum sample; a good batter
/// rates clearly above a poor one on everything measurable; the bat shield (a widened bat finding
/// balls the real blade would have missed) never raises a rating; pace and spin are measured
/// apart; only the last Window counting balls matter; temperament needs pressure balls; every
/// rating stays 0..99; and the grades, strongest / weakest and the coach's verdict. Standalone.
/// </summary>
public class CareerRatingsTests
{
    // ---- Synthetic balls

    private static BallRecord Ball(eBowlerKind kind, eLengthBand length, float line)
    {
        return new BallRecord
        {
            source = eBallSource.Nets, bowlerKind = kind, lengthBand = length, lineAtStumps = line,
            speedKph = 130f, heightAtStumps = 0.5f, action = eBallAction.Left, end = eBallEnd.Dot,
        };
    }

    private static BallRecord Clean(BallRecord r, float q, float exit, int runs)
    {
        r.action = eBallAction.Played;
        r.contact = 0;
        r.quality = q;
        r.realQuality = q;
        r.realContact = eRealContact.Clean;
        r.exitSpeed = exit;
        r.runs = runs;
        r.end = runs >= 6 ? eBallEnd.Six : runs == 4 ? eBallEnd.Four : runs > 0 ? eBallEnd.Runs : eBallEnd.Dot;
        return r;
    }

    private static BallRecord Beaten(BallRecord r)
    {
        r.action = eBallAction.Missed;
        r.realContact = eRealContact.None;
        r.contact = -1;
        return r;
    }

    private static BallRecord Out(BallRecord r, eHowOut how)
    {
        r.end = eBallEnd.Out;
        r.howOut = how;
        r.runs = 0;
        return r;
    }

    private static eBowlerKind Alternate(int i) => i % 2 == 0 ? eBowlerKind.Fast : eBowlerKind.OffSpin;

    /// Leaves outside off, defends the straight one cleanly, drives the full one for four, pulls the
    /// short one for six, works the back-of-a-length one for a single. Never out.
    private static BallRecord Good(int i, eBowlerKind kind)
    {
        switch (i % 5)
        {
            case 0: return Ball(kind, eLengthBand.GoodLength, 0.45f);
            case 1: { var r = Clean(Ball(kind, eLengthBand.GoodLength, 0.02f), 0.85f, 14f, 0); r.wouldHitStumps = true; return r; }
            case 2: return Clean(Ball(kind, eLengthBand.Full, 0.1f), 0.9f, 32f, 4);
            case 3: return Clean(Ball(kind, eLengthBand.Short, -0.3f), 0.95f, 38f, 6);
            default: return Clean(Ball(kind, eLengthBand.BackOfALength, 0.2f), 0.8f, 22f, 1);
        }
    }

    /// Nicks the one outside off (out half the time), is bowled by the straight one, "hits" the full
    /// one only because the shield is wide, and mistimes the rest.
    private static BallRecord Poor(int i, eBowlerKind kind)
    {
        switch (i % 5)
        {
            case 0:
            {
                var r = Ball(kind, eLengthBand.GoodLength, 0.45f);
                r.action = eBallAction.Played; r.contact = 1; r.quality = 0.2f; r.realQuality = 0.2f;
                r.realContact = eRealContact.Edge; r.exitSpeed = 9f;
                return (i / 5) % 2 == 0 ? Out(r, eHowOut.CaughtBehind) : r;
            }
            case 1: { var r = Beaten(Ball(kind, eLengthBand.GoodLength, 0f)); r.wouldHitStumps = true; return Out(r, eHowOut.Bowled); }
            case 2: return ShieldOnly(Ball(kind, eLengthBand.Full, 0.1f));
            case 3: return Clean(Ball(kind, eLengthBand.Short, -0.3f), 0.35f, 12f, 0);
            default: return Clean(Ball(kind, eLengthBand.BackOfALength, 0.2f), 0.3f, 11f, 0);
        }
    }

    /// Touched only on the widened bat: a sweet-looking hit the real blade would have missed.
    private static BallRecord ShieldOnly(BallRecord r)
    {
        r.action = eBallAction.Missed;
        r.contact = 0;
        r.quality = 0.95f;
        r.realQuality = 0f;
        r.realContact = eRealContact.WouldHaveMissed;
        r.exitSpeed = 30f;
        return r;
    }

    private static List<BallRecord> Batter(int count, Func<int, eBowlerKind, BallRecord> make, Func<int, eBowlerKind> kind)
    {
        var list = new List<BallRecord>(count);
        for (int i = 0; i < count; i++)
            list.Add(make(i, kind(i)));
        return list;
    }

    private static void AssertSame(RatingSet a, RatingSet b)
    {
        for (int i = 0; i < RatingSet.Count; i++)
            Assert.AreEqual(a[i], b[i], 1e-4f, RatingSet.Names[i]);
    }

    // ---- Tests

    [Test]
    public void NothingIsKnownWithoutBalls()
    {
        foreach (RatingSet r in new[] { CareerRatings.Compute(null), CareerRatings.Compute(new List<BallRecord>()) })
        {
            Assert.AreEqual(0, r.sample);
            for (int i = 0; i < RatingSet.Count; i++)
                Assert.IsFalse(RatingSet.Known(r[i]), RatingSet.Names[i]);
        }
    }

    [Test]
    public void EachRatingIsUnknownUntilItHasItsMinimumSample()
    {
        var balls = new List<BallRecord>();
        for (int i = 0; i < 5; i++)
            balls.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.8f, 25f, 4));
        RatingSet five = CareerRatings.Compute(balls);
        Assert.AreEqual(5, five.sample);
        Assert.IsFalse(RatingSet.Known(five.power), "power needs 6 clean strikes");
        Assert.IsFalse(RatingSet.Known(five.pace), "vs pace needs 6 balls");

        balls.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.8f, 25f, 4));
        balls.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.8f, 25f, 4));
        RatingSet seven = CareerRatings.Compute(balls);
        Assert.IsTrue(RatingSet.Known(seven.power));
        Assert.IsTrue(RatingSet.Known(seven.pace));
        Assert.IsFalse(RatingSet.Known(seven.timing), "timing needs 8 attempts");
        Assert.IsFalse(RatingSet.Known(seven.spin), "no spin faced");

        balls.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.8f, 25f, 4));
        Assert.IsTrue(RatingSet.Known(CareerRatings.Compute(balls).timing));
    }

    [Test]
    public void AGoodBatterRatesClearlyAboveAPoorOneOnEverythingMeasurable()
    {
        RatingSet good = CareerRatings.Compute(Batter(60, Good, Alternate));
        RatingSet poor = CareerRatings.Compute(Batter(60, Poor, Alternate));
        int measured = 0;
        for (int i = 0; i < RatingSet.Count; i++)
        {
            if (!RatingSet.Known(good[i]) || !RatingSet.Known(poor[i])) continue;
            measured++;
            Assert.Greater(good[i], poor[i] + 15f, RatingSet.Names[i]);
        }
        Assert.AreEqual(6, measured, "everything but temperament is measured from 60 nets balls");
        Assert.IsFalse(RatingSet.Known(good.temperament));
    }

    [Test]
    public void TheBatShieldNeverRaisesTiming()
    {
        var shield = new List<BallRecord>();
        var beaten = new List<BallRecord>();
        var playedShield = new List<BallRecord>();
        for (int i = 0; i < 20; i++)
        {
            shield.Add(ShieldOnly(Ball(eBowlerKind.Fast, eLengthBand.GoodLength, 0.1f)));
            beaten.Add(Beaten(Ball(eBowlerKind.Fast, eLengthBand.GoodLength, 0.1f)));
            var p = ShieldOnly(Ball(eBowlerKind.Fast, eLengthBand.GoodLength, 0.1f));
            p.action = eBallAction.Played;   // even if the ledger called it played
            playedShield.Add(p);
        }
        RatingSet s = CareerRatings.Compute(shield), b = CareerRatings.Compute(beaten), ps = CareerRatings.Compute(playedShield);
        Assert.AreEqual(b.timing, s.timing, 1e-4f);
        Assert.AreEqual(b.timing, ps.timing, 1e-4f);
        Assert.IsFalse(RatingSet.Known(s.power), "a hit only the widened bat made is not power");
    }

    [Test]
    public void OnlyTheRealBladeQualityCounts()
    {
        var widened = new List<BallRecord>();
        var real = new List<BallRecord>();
        for (int i = 0; i < 20; i++)
        {
            var w = Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.4f, 20f, 1);
            w.quality = 0.95f;   // sweet on the widened bat, ordinary on the real one
            widened.Add(w);
            real.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.4f, 20f, 1));
        }
        AssertSame(CareerRatings.Compute(real), CareerRatings.Compute(widened));
    }

    [Test]
    public void PaceAndSpinAreMeasuredApart()
    {
        var balls = new List<BallRecord>();
        for (int i = 0; i < 20; i++)
        {
            balls.Add(Clean(Ball(eBowlerKind.Fast, eLengthBand.Full, 0.1f), 0.9f, 30f, 4));
            balls.Add(Beaten(Ball(eBowlerKind.LegSpin, eLengthBand.GoodLength, 0.1f)));
        }
        RatingSet r = CareerRatings.Compute(balls);
        Assert.Greater(r.pace, r.spin + 20f);

        RatingSet paceOnly = CareerRatings.Compute(Batter(30, Good, i => eBowlerKind.Medium));
        Assert.IsTrue(RatingSet.Known(paceOnly.pace));
        Assert.IsFalse(RatingSet.Known(paceOnly.spin));
        RatingSet spinOnly = CareerRatings.Compute(Batter(30, Good, i => eBowlerKind.Mystery));
        Assert.IsFalse(RatingSet.Known(spinOnly.pace));
        Assert.IsTrue(RatingSet.Known(spinOnly.spin));
    }

    [Test]
    public void OnlyTheLastWindowCountingBallsMatter()
    {
        List<BallRecord> good = Batter(CareerRatings.Window, Good, Alternate);
        var history = Batter(100, Poor, Alternate);
        history.AddRange(good);
        for (int i = 0; i < 5; i++)
        {
            var wide = Ball(eBowlerKind.Fast, eLengthBand.Short, 1.2f);
            wide.wide = true;
            wide.end = eBallEnd.Wide;
            history.Add(wide);
        }
        RatingSet windowed = CareerRatings.Compute(history);
        Assert.AreEqual(CareerRatings.Window, windowed.sample);
        AssertSame(CareerRatings.Compute(good), windowed);
    }

    [Test]
    public void TemperamentNeedsPressureBalls()
    {
        Assert.IsFalse(RatingSet.Known(CareerRatings.Compute(Batter(200, Good, Alternate)).temperament));

        List<BallRecord> steady = Batter(40, Good, Alternate), wobbly = Batter(40, Good, Alternate);
        for (int i = 0; i < 20; i++)
        {
            var s = Good(i, Alternate(i)); s.pressure = true; steady.Add(s);
            var w = Poor(i, Alternate(i)); w.pressure = true; wobbly.Add(w);
        }
        RatingSet calm = CareerRatings.Compute(steady), nervy = CareerRatings.Compute(wobbly);
        Assert.IsTrue(RatingSet.Known(calm.temperament));
        Assert.Greater(calm.temperament, nervy.temperament + 15f);

        List<BallRecord> eleven = Batter(40, Good, Alternate);
        for (int i = 0; i < 11; i++) { var s = Good(i, Alternate(i)); s.pressure = true; eleven.Add(s); }
        Assert.IsFalse(RatingSet.Known(CareerRatings.Compute(eleven).temperament), "12 pressure balls at least");
    }

    [Test]
    public void EveryRatingStaysWithinTheScale()
    {
        var rnd = new Random(7);
        var balls = new List<BallRecord>();
        for (int i = 0; i < 700; i++)
        {
            var r = Ball((eBowlerKind)rnd.Next(5), (eLengthBand)rnd.Next(5), (float)(rnd.NextDouble() * 2 - 1));
            r.action = (eBallAction)rnd.Next(3);
            r.realContact = (eRealContact)rnd.Next(4);
            r.contact = rnd.Next(-1, 3);
            r.quality = (float)(rnd.NextDouble() * 2 - 0.5);
            r.realQuality = (float)(rnd.NextDouble() * 2 - 0.5);
            r.exitSpeed = (float)(rnd.NextDouble() * 90);
            r.end = (eBallEnd)rnd.Next(8);
            r.runs = rnd.Next(0, 8);
            r.wouldHitStumps = rnd.Next(4) == 0;
            r.pressure = rnd.Next(3) == 0;
            r.wide = rnd.Next(20) == 0;
            balls.Add(r);
            if (i % 50 != 49) continue;
            RatingSet set = CareerRatings.Compute(balls);
            Assert.LessOrEqual(set.sample, CareerRatings.Window);
            for (int k = 0; k < RatingSet.Count; k++)
                Assert.IsTrue(set[k] == RatingSet.Unknown || (set[k] >= 0f && set[k] <= 99f), RatingSet.Names[k] + " " + set[k]);
        }
    }

    [Test]
    public void GradesHaveTheirBoundaries()
    {
        Assert.AreEqual("--", CareerRatings.Grade(RatingSet.Unknown));
        Assert.AreEqual("Raw", CareerRatings.Grade(0f));
        Assert.AreEqual("Raw", CareerRatings.Grade(29.9f));
        Assert.AreEqual("Developing", CareerRatings.Grade(30f));
        Assert.AreEqual("Developing", CareerRatings.Grade(44.9f));
        Assert.AreEqual("Solid", CareerRatings.Grade(45f));
        Assert.AreEqual("Solid", CareerRatings.Grade(59.9f));
        Assert.AreEqual("Strong", CareerRatings.Grade(60f));
        Assert.AreEqual("Strong", CareerRatings.Grade(74.9f));
        Assert.AreEqual("Elite", CareerRatings.Grade(75f));
        Assert.AreEqual("Elite", CareerRatings.Grade(99f));
    }

    [Test]
    public void StrongestAndWeakestAreTheKnownExtremes()
    {
        var none = new RatingSet();
        Assert.AreEqual(-1, CareerRatings.Strongest(none));
        Assert.AreEqual(-1, CareerRatings.Weakest(none));

        var r = new RatingSet { timing = 70f, power = 40f, judgement = 20f };
        Assert.AreEqual(0, CareerRatings.Strongest(r));
        Assert.AreEqual(5, CareerRatings.Weakest(r));

        var tie = new RatingSet { pace = 50f, spin = 50f };
        Assert.AreEqual(2, CareerRatings.Strongest(tie), "first on a tie");
        Assert.AreEqual(2, CareerRatings.Weakest(tie));
    }

    [Test]
    public void TheVerdictIsAPlainLineNamingStrengthAndWeakness()
    {
        var sets = new[]
        {
            new RatingSet(),
            new RatingSet { timing = 50f },
            new RatingSet { timing = 82f, judgement = 31f },
            new RatingSet { timing = 40f, power = 20f },
            new RatingSet { pace = 50f, spin = 51f },
            CareerRatings.Compute(Batter(60, Good, Alternate)),
            CareerRatings.Compute(Batter(60, Poor, Alternate)),
        };
        foreach (RatingSet s in sets)
        {
            string v = CareerRatings.Verdict(s);
            Assert.IsFalse(string.IsNullOrEmpty(v));
            foreach (char c in v)
                Assert.LessOrEqual((int)c, 255, "Latin-1 only: " + v);
        }
        StringAssert.Contains("Face a few more balls", CareerRatings.Verdict(sets[0]));
        string named = CareerRatings.Verdict(sets[2]);
        StringAssert.Contains("timing", named);
        StringAssert.Contains("outside off", named);
    }

    [Test]
    public void TheVerdictHandlesTiesAndALowEvenGame()
    {
        // Two equal known ratings are two things known, not one.
        string tie = CareerRatings.Verdict(new RatingSet { pace = 50f, spin = 50f });
        StringAssert.DoesNotContain("Face a few more balls", tie);
        StringAssert.Contains("well-balanced", tie);

        // Everything close together but Raw is not "no real weak spot".
        string low = CareerRatings.Verdict(new RatingSet { timing = 14f, power = 16f, pace = 15f, spin = 17f });
        StringAssert.DoesNotContain("no real weak spot", low);
        StringAssert.Contains("where the work is", low);
    }

    [Test]
    public void SurvivingWithoutScoringIsNotStrong()
    {
        // Leaves everything outside off and blocks everything on the stumps, sweetly, for 150 balls
        // and never scores: a solid nets session, but not Strong batting against pace.
        var balls = new List<BallRecord>();
        for (int i = 0; i < 150; i++)
        {
            if (i % 2 == 0) { balls.Add(Ball(eBowlerKind.Fast, eLengthBand.GoodLength, 0.45f)); continue; }
            var b = Clean(Ball(eBowlerKind.Fast, eLengthBand.GoodLength, 0.02f), 0.75f, 12f, 0);
            b.wouldHitStumps = true;
            balls.Add(b);
        }
        RatingSet r = CareerRatings.Compute(balls);
        Assert.IsTrue(RatingSet.Known(r.pace));
        Assert.Less(r.pace, 60f, "a batter who never scores is not Strong vs pace");
        Assert.GreaterOrEqual(r.pace, 45f, "but surviving well is Solid");
    }
}
