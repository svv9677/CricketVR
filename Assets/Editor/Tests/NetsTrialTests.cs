using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The career player's nets trial (NetsTrialPlan, NetsTrial's bookkeeping, NetsTrialCard's words):
/// 30 balls in five overs - a quick, a medium-pacer, an off-spinner, a leg-spinner, then a mix of a
/// quick, the mystery spinner and a medium-pacer; the same seed bowls the same trial; every ball is
/// one that bowler really bowls (checked against BowlerRepertoire's own tables, not the plan's
/// helper), his stock ball mostly and at least one variation an over; a wide or a dead ball is
/// bowled again; each ball is stamped and kept; and everything shown is Latin-1. Standalone (no
/// scene, no save files).
/// </summary>
public class NetsTrialTests
{
    private const int Seeds = 200;

    private static bool BowledBy(eBowlerKind kind, eDelivery d)
    {
        if (kind != eBowlerKind.Mystery && d == BowlerRepertoire.StockBall(kind))
            return true;
        foreach (BowlerRepertoire.Weighted w in BowlerRepertoire.VariationWeights(kind))
            if (w.delivery == d && w.weight > 0f)
                return true;
        return false;
    }

    private static bool Stock(NetsTrialBall b) => b.kind != eBowlerKind.Mystery && b.delivery == BowlerRepertoire.StockBall(b.kind);

    [Test]
    public void ThePlanIsThirtyBallsInFiveOvers()
    {
        NetsTrialBall[] plan = NetsTrialPlan.Make(1);
        Assert.AreEqual(30, NetsSession.TrialBalls);
        Assert.AreEqual(NetsSession.TrialBalls, plan.Length);
        Assert.AreEqual(5, NetsTrialPlan.Overs);
        Assert.AreEqual(4, NetsTrialPlan.OverOf(29));
    }

    [Test]
    public void EachOverIsTheRightKindOfBowler()
    {
        eBowlerKind[] single = { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin };
        for (int seed = 0; seed < Seeds; seed++)
        {
            NetsTrialBall[] plan = NetsTrialPlan.Make(seed);
            for (int over = 0; over < 4; over++)
                for (int b = 0; b < 6; b++)
                    Assert.AreEqual(single[over], plan[over * 6 + b].kind, $"seed {seed} over {over + 1} ball {b + 1}");
            var mixed = new HashSet<eBowlerKind>();
            for (int b = 24; b < 30; b++)
                mixed.Add(plan[b].kind);
            CollectionAssert.AreEquivalent(new[] { eBowlerKind.Fast, eBowlerKind.Mystery, eBowlerKind.Medium }, mixed, $"seed {seed}");
        }
    }

    [Test]
    public void TheSameSeedBowlsTheSameTrial()
    {
        CollectionAssert.AreEqual(NetsTrialPlan.Make(42), NetsTrialPlan.Make(42));
        int differs = 0;
        for (int seed = 0; seed < 20; seed++)
            if (!SamePlan(NetsTrialPlan.Make(seed), NetsTrialPlan.Make(seed + 1000)))
                differs++;
        Assert.Greater(differs, 15, "different seeds should mostly give different trials");
    }

    private static bool SamePlan(NetsTrialBall[] a, NetsTrialBall[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i].kind != b[i].kind || a[i].delivery != b[i].delivery)
                return false;
        return true;
    }

    [Test]
    public void EveryBallIsOneThatBowlerBowls()
    {
        for (int seed = 0; seed < Seeds; seed++)
            foreach (NetsTrialBall b in NetsTrialPlan.Make(seed))
            {
                Assert.IsTrue(BowledBy(b.kind, b.delivery), $"seed {seed}: {b.kind} does not bowl {b.delivery}");
                Assert.IsTrue(NetsTrialPlan.Valid(b));
            }
    }

    [Test]
    public void StockBallsDominateAndEveryOverHasAVariation()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            NetsTrialBall[] plan = NetsTrialPlan.Make(seed);
            int stockInTrial = 0;
            for (int over = 0; over < 5; over++)
            {
                int stock = 0, varied = 0;
                for (int b = over * 6; b < over * 6 + 6; b++)
                {
                    if (Stock(plan[b])) stock++;
                    else varied++;
                    Assert.AreEqual(!Stock(plan[b]), NetsTrialPlan.IsVariation(plan[b]));
                }
                Assert.GreaterOrEqual(varied, 1, $"seed {seed} over {over + 1} has no variation");
                if (over < 4)
                    Assert.GreaterOrEqual(stock, 4, $"seed {seed} over {over + 1}: the stock ball should dominate");
                stockInTrial += stock;
            }
            Assert.Greater(stockInTrial, NetsSession.TrialBalls / 2, $"seed {seed}");
        }
    }

    [Test]
    public void TheMixedOverHasOneSeamVariationAndTwoDifferentMysteryBalls()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            NetsTrialBall[] plan = NetsTrialPlan.Make(seed);
            int seamVaried = 0;
            var mystery = new List<eDelivery>();
            for (int b = 24; b < 30; b++)
            {
                if (plan[b].kind == eBowlerKind.Mystery) mystery.Add(plan[b].delivery);
                else if (!Stock(plan[b])) seamVaried++;
            }
            Assert.AreEqual(1, seamVaried, $"seed {seed}");
            Assert.AreEqual(2, mystery.Count);
            Assert.AreNotEqual(mystery[0], mystery[1], $"seed {seed}: the mystery spinner repeated himself");
        }
    }

    // ---- The counting rule ----------------------------------------------------------------------

    private static BallRecord Ball(eBallEnd end, bool wide = false, bool noBall = false) =>
        new BallRecord { end = end, wide = wide, noBall = noBall, bowlerKind = eBowlerKind.Fast };

    [Test]
    public void AWideOrADeadBallIsBowledAgain()
    {
        Assert.IsTrue(NetsTrialPlan.Advances(Ball(eBallEnd.Dot)));
        Assert.IsTrue(NetsTrialPlan.Advances(Ball(eBallEnd.Out)));
        Assert.IsTrue(NetsTrialPlan.Advances(Ball(eBallEnd.Runs)));
        Assert.IsTrue(NetsTrialPlan.Advances(Ball(eBallEnd.NoBall, noBall: true)), "a no-ball was a ball he could play");
        Assert.IsFalse(NetsTrialPlan.Advances(Ball(eBallEnd.Wide, wide: true)));
        Assert.IsFalse(NetsTrialPlan.Advances(Ball(eBallEnd.Wide)));
        Assert.IsFalse(NetsTrialPlan.Advances(Ball(eBallEnd.Dot, wide: true)));
        Assert.IsFalse(NetsTrialPlan.Advances(Ball(eBallEnd.Dead)));
        Assert.IsFalse(NetsTrialPlan.Advances(null));
    }

    [Test]
    public void EveryFacedBallIsKeptButOnlyCountingOnesMoveTheTrialOn()
    {
        var p = new CareerProfile { name = "Test" };
        Assert.IsFalse(NetsTrial.Measure(p, Ball(eBallEnd.Wide, wide: true), eNetsMode.Trial));
        Assert.IsFalse(NetsTrial.Measure(p, Ball(eBallEnd.Dead), eNetsMode.Trial));
        Assert.IsTrue(NetsTrial.Measure(p, Ball(eBallEnd.Dot), eNetsMode.Trial));
        Assert.AreEqual(3, p.balls.Count, "wides and dead balls are kept (the ratings decide what they mean)");
        Assert.AreEqual(1, p.totals.balls);
        foreach (BallRecord r in p.balls)
            Assert.AreEqual(eBallSource.Trial, r.source);
        Assert.IsNotNull(p.ratings);

        Assert.IsFalse(NetsTrial.Measure(p, Ball(eBallEnd.Dot), eNetsMode.Practice), "practice has no trial to move on");
        Assert.AreEqual(eBallSource.Nets, p.balls[p.balls.Count - 1].source);
        Assert.IsFalse(NetsTrial.Measure(null, Ball(eBallEnd.Dot), eNetsMode.Trial));
    }

    [Test]
    public void TheTrialSetsTheStartingRatingsAsACopy()
    {
        var p = new CareerProfile { name = "Test" };
        NetsTrial.Measure(p, Ball(eBallEnd.Dot), eNetsMode.Trial);
        RatingSet r = NetsTrial.RecordTrial(p);
        Assert.IsTrue(p.trialDone);
        Assert.AreEqual(1, p.totals.trials);
        Assert.AreNotSame(r, p.trialRatings, "the caller's copy must not be the saved trial ratings");
        Assert.AreNotSame(p.ratings, p.trialRatings, "the trial's ratings must not move with the live ones");
        Assert.AreEqual(p.trialRatings.sample, r.sample);
        Assert.AreEqual(p.ratings.sample, p.trialRatings.sample, "one trial and nothing else: the same balls");
    }

    [Test]
    public void TheSeedIsFixedForOneCareerInOneSlot()
    {
        var a = new CareerProfile { name = "Asha", created = "2026-11-05" };
        var b = new CareerProfile { name = "Asha", created = "2026-11-05" };
        Assert.AreEqual(NetsTrialPlan.SeedFor(a, 0), NetsTrialPlan.SeedFor(b, 0));
        Assert.AreNotEqual(NetsTrialPlan.SeedFor(a, 0), NetsTrialPlan.SeedFor(a, 1));
        Assert.AreNotEqual(NetsTrialPlan.SeedFor(a, 0), NetsTrialPlan.SeedFor(new CareerProfile { name = "Ravi", created = "2026-11-05" }, 0));
    }

    // ---- What is shown --------------------------------------------------------------------------

    private static void AssertLatin1(string s)
    {
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not in the TMP default font: {s}");
    }

    [Test]
    public void TheProgressLineSaysWhichBallAndWhoIsBowling()
    {
        string s = NetsTrialCard.ProgressText(12, eBowlerKind.OffSpin);
        StringAssert.Contains("NETS TRIAL", s);
        StringAssert.Contains("Ball 12 of 30", s);
        StringAssert.Contains("Off-spinner", s);
        StringAssert.Contains("Ball 30 of 30", NetsTrialCard.ProgressText(31, eBowlerKind.Fast), "never past the last ball");
        StringAssert.Contains("1 ball measured", NetsTrialCard.PracticeText(1));
        foreach (eBowlerKind k in System.Enum.GetValues(typeof(eBowlerKind)))
            AssertLatin1(NetsTrialCard.ProgressText(1, k));
    }

    [Test]
    public void TheResultCardListsTheSevenRatings()
    {
        var r = new RatingSet { timing = 62f, power = 40.4f };
        string body = NetsTrialCard.ResultBody(r, "Lovely timing \u2014 work on \u201Cjudgement\u201D\u2026");
        foreach (string name in RatingSet.Names)
            StringAssert.Contains(name, body);
        StringAssert.Contains("62", NetsTrialCard.RatingLine(0, 62f));
        StringAssert.Contains(CareerRatings.Grade(62f), NetsTrialCard.RatingLine(0, 62f));
        StringAssert.Contains("40", NetsTrialCard.RatingLine(1, 40.4f));
        StringAssert.Contains("--", NetsTrialCard.RatingLine(2, RatingSet.Unknown), "not measured yet: no number guessed");
        StringAssert.Contains("Press A to continue", body);
        StringAssert.Contains("Lovely timing - work on \"judgement\"...", body);
        AssertLatin1(body);
        AssertLatin1(NetsTrialCard.ResultHeading);
        Assert.AreEqual("Ok?", NetsTrialCard.Latin1("Ok\u263A"));
    }

    [Test]
    public void AnAbandonedAttemptIsNotPartOfTheTrialsRatings()
    {
        var p = new CareerProfile { name = "Test" };
        // 20 balls of an attempt left half way: all bowled.
        for (int i = 0; i < 20; i++)
        {
            BallRecord r = Ball(eBallEnd.Out);
            r.source = eBallSource.Trial;
            p.Add(r);
        }
        // A wide and 30 counting balls of the finished attempt.
        BallRecord wide = Ball(eBallEnd.Wide, wide: true);
        wide.source = eBallSource.Trial;
        p.Add(wide);
        for (int i = 0; i < NetsSession.TrialBalls; i++)
        {
            BallRecord r = Ball(eBallEnd.Dot);
            r.source = eBallSource.Trial;
            p.Add(r);
        }
        List<BallRecord> trial = NetsTrial.TrialBalls(p.balls, NetsSession.TrialBalls);
        Assert.AreEqual(NetsSession.TrialBalls, trial.Count, "the finished attempt's 30 only");
        Assert.IsFalse(trial.Exists(b => b.Out), "none of the abandoned attempt's balls");
        RatingSet r2 = NetsTrial.RecordTrial(p);
        Assert.AreEqual(NetsSession.TrialBalls, r2.sample);
        Assert.AreEqual(50, p.ratings.sample, "the career's own ratings keep every real ball");
    }
}
