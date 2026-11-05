using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The bowlers' repertoires and the captain (BowlerRepertoire, OverPlanner, BowlingAttack):
/// quicks cut it more than they swing it and medium-pacers the other way round; overs are not a
/// fixed order; the mystery spinner turns it both ways; every variation moves the way it should and
/// at the pace it should; a top-spinner kicks and a slider skids, and the solver still lands them
/// where it aimed; and the captain never bowls anyone twice running or over his quota, opens with
/// seam and bowls spin through the middle. Standalone (no scene).
/// </summary>
public class BowlerRepertoireTests
{
    private static System.Func<float> Rng(int seed)
    {
        var r = new System.Random(seed);
        return () => (float)r.NextDouble();
    }

    private static Dictionary<eDelivery, int> Count(eBowlerKind kind, int overs, int seed)
    {
        var rand = Rng(seed);
        var n = new Dictionary<eDelivery, int>();
        for (int o = 0; o < overs; o++)
        {
            var plan = new OverPlanner(kind, 2.4f, rand);
            for (int b = 0; b < 6; b++)
            {
                eDelivery d = plan.Next(b, false, 0f, rand);
                n[d] = n.TryGetValue(d, out int c) ? c + 1 : 1;
            }
        }
        return n;
    }

    private static int Get(Dictionary<eDelivery, int> n, eDelivery d) => n.TryGetValue(d, out int c) ? c : 0;

    [Test]
    public void QuicksCutMoreThanTheySwingAndMediumPacersSwingMore()
    {
        var fast = Count(eBowlerKind.Fast, 3000, 1);
        var medium = Count(eBowlerKind.Medium, 3000, 2);
        int fastCut = Get(fast, eDelivery.OffCutter) + Get(fast, eDelivery.LegCutter);
        int fastSwing = Get(fast, eDelivery.InSwinger) + Get(fast, eDelivery.OutSwinger);
        int medCut = Get(medium, eDelivery.OffCutter) + Get(medium, eDelivery.LegCutter);
        int medSwing = Get(medium, eDelivery.InSwinger) + Get(medium, eDelivery.OutSwinger);
        Assert.Greater(fastCut, fastSwing * 1.4f, $"quick: cut {fastCut}, swing {fastSwing}");
        Assert.Greater(medSwing, medCut * 2f, $"medium: swing {medSwing}, cut {medCut}");
        // Both can bowl everything.
        foreach (var n in new[] { fast, medium })
            foreach (eDelivery d in new[] { eDelivery.Stock, eDelivery.InSwinger, eDelivery.OutSwinger, eDelivery.OffCutter, eDelivery.LegCutter, eDelivery.SlowerBall })
                Assert.Greater(Get(n, d), 50, $"{d} hardly ever bowled");
    }

    [Test]
    public void OversAreNotAFixedOrderAndNoVariationComesTwiceRunning()
    {
        var rand = Rng(7);
        var seen = new HashSet<string>();
        float variations = 0f;
        const int Overs = 400;
        foreach (eBowlerKind kind in new[] { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin })
        {
            for (int o = 0; o < Overs; o++)
            {
                var plan = new OverPlanner(kind, 2.4f, rand);
                string seq = "";
                eDelivery prev = eDelivery.Stock;
                for (int b = 0; b < 6; b++)
                {
                    eDelivery d = plan.Next(b, false, 0f, rand);
                    if (d != BowlerRepertoire.StockBall(kind))
                    {
                        variations++;
                        Assert.AreNotEqual(prev, d, $"{kind}: {d} twice running");
                    }
                    prev = d;
                    seq += (int)d + ",";
                }
                seen.Add(kind + seq);
            }
        }
        Assert.Greater(seen.Count, 4 * Overs / 2, "overs repeat the same order");
        Assert.AreEqual(2.4f, variations / (4 * Overs), 0.2f, "variations an over");
    }

    [Test]
    public void TheBallAfterABoundaryIsChangedUpMoreOften()
    {
        var rand = Rng(11);
        int calm = 0, after = 0;
        for (int o = 0; o < 4000; o++)
        {
            var a = new OverPlanner(eBowlerKind.Fast, 1.8f, rand);
            if (a.Next(3, false, 0f, rand) != eDelivery.Stock) calm++;
            var b = new OverPlanner(eBowlerKind.Fast, 1.8f, rand);
            if (b.Next(3, true, 0f, rand) != eDelivery.Stock) after++;
        }
        Assert.Greater(after, calm * 1.4f, $"changed up after a boundary {after}, otherwise {calm}");
    }

    [Test]
    public void TheMysterySpinnerTurnsItBothWays()
    {
        var n = Count(eBowlerKind.Mystery, 300, 3);
        int intoRightHander = Get(n, eDelivery.OffBreak) + Get(n, eDelivery.Googly);
        int away = Get(n, eDelivery.LegBreak) + Get(n, eDelivery.Carrom);
        Assert.Greater(intoRightHander, 1800 * 0.25f);
        Assert.Greater(away, 1800 * 0.25f);
        Assert.AreEqual(0, Get(n, eDelivery.Stock), "a mystery spinner has no stock ball");
    }

    /// Turn in degrees for a right-hander: positive is into him (toward the leg side, -Z).
    private static float MeanTurn(eBowlerKind kind, eDelivery d, out float meanSpeed)
    {
        var profiles = new BowlingProfileManager();
        float turn = 0f, speed = 0f;
        const int N = 300;
        for (int i = 0; i < N; i++)
        {
            BowlingParams p = BowlingAttack.MakeDelivery(kind, d, profiles);
            turn += p.Effects(1f).turnDegrees;
            speed += p.speedX;
        }
        meanSpeed = speed / N;
        return turn / N;
    }

    [Test]
    public void EachVariationMovesTheWayItShould()
    {
        Assert.Greater(MeanTurn(eBowlerKind.Fast, eDelivery.OffCutter, out _), 1f, "off-cutter comes in");
        Assert.Less(MeanTurn(eBowlerKind.Fast, eDelivery.LegCutter, out _), -1f, "leg-cutter goes away");
        Assert.Greater(MeanTurn(eBowlerKind.OffSpin, eDelivery.OffBreak, out _), 0.5f, "off-break comes in");
        Assert.Less(MeanTurn(eBowlerKind.LegSpin, eDelivery.LegBreak, out _), -0.5f, "leg-break goes away");
        Assert.Greater(MeanTurn(eBowlerKind.LegSpin, eDelivery.Googly, out _), 0.5f, "googly comes in");
        Assert.Less(MeanTurn(eBowlerKind.Mystery, eDelivery.Carrom, out _), -0.5f, "carrom ball goes away");
        Assert.AreEqual(0f, MeanTurn(eBowlerKind.OffSpin, eDelivery.TopSpinner, out _), 0.2f, "top-spinner goes straight on");
        float flighted = Mathf.Abs(MeanTurn(eBowlerKind.OffSpin, eDelivery.Flighted, out _));
        float stock = Mathf.Abs(MeanTurn(eBowlerKind.OffSpin, eDelivery.OffBreak, out _));
        Assert.Greater(flighted, stock * 1.15f, "the flighted ball turns more");
        var swing = BowlingAttack.MakeDelivery(eBowlerKind.Medium, eDelivery.OutSwinger, new BowlingProfileManager()).Effects(1f);
        Assert.AreNotEqual(0f, swing.swingAccelPerV2, "the outswinger swings");
    }

    [Test]
    public void EachVariationIsBowledAtItsOwnPace()
    {
        MeanTurn(eBowlerKind.Fast, eDelivery.Stock, out float fastStock);
        MeanTurn(eBowlerKind.Medium, eDelivery.Stock, out float mediumStock);
        MeanTurn(eBowlerKind.Fast, eDelivery.SlowerBall, out float slower);
        MeanTurn(eBowlerKind.Fast, eDelivery.OffCutter, out float cutter);
        MeanTurn(eBowlerKind.OffSpin, eDelivery.OffBreak, out float offBreak);
        MeanTurn(eBowlerKind.OffSpin, eDelivery.Slider, out float armBall);
        MeanTurn(eBowlerKind.OffSpin, eDelivery.Flighted, out float flighted);
        Assert.Greater(fastStock, mediumStock * 1.05f, "a quick is quicker than a medium-pacer");
        Assert.Less(slower, fastStock * 0.82f, "slower ball");
        Assert.Less(cutter, fastStock * 0.96f, "the cutter is taken off a little");
        Assert.Greater(armBall, offBreak * 1.07f, "the arm ball is quicker");
        Assert.Less(flighted, offBreak * 0.9f, "flighted is slower");
    }

    /// Highest point after the first bounce of a ball bowled at a good length.
    private static float RiseAfterBounce(BallFlight.DeliveryEffects fx, out float pitchX)
    {
        Vector3 hand = new Vector3(-9.4f, 2.2f, 0.6f);
        BallDelivery.Solution s = BallDelivery.Solve(hand, 4f, 22f, 0.05f, fx);
        pitchX = s.pitchX;
        var samples = new List<BallFlight.Sample>();
        BallFlight.Simulate(s.releasePosition, s.releaseVelocity, fx, 2f, samples, null, 0.005f);
        float top = 0f;
        foreach (var x in samples)
            if (x.bounced && x.position.x < BallDelivery.BatsmanStumpsX) top = Mathf.Max(top, x.position.y);
        return top;
    }

    [Test]
    public void TopSpinnerKicksSliderSkidsAndBothLandWhereAimed()
    {
        float plain = RiseAfterBounce(default, out float p0);
        float top = RiseAfterBounce(new BallFlight.DeliveryEffects { dip = 1.4f, bounceScale = 1.15f }, out float p1);
        float slide = RiseAfterBounce(new BallFlight.DeliveryEffects { bounceScale = 0.8f }, out float p2);
        Assert.Greater(top, plain + 0.05f, $"top-spinner {top:0.00} vs {plain:0.00}");
        Assert.Less(slide, plain - 0.05f, $"slider {slide:0.00} vs {plain:0.00}");
        Assert.AreEqual(4f, p0, 0.15f);
        Assert.AreEqual(4f, p1, 0.15f, "the solver allows for the dip");
        Assert.AreEqual(4f, p2, 0.15f);
    }

    [Test]
    public void TheCaptainRotatesWithinQuotasOpensWithSeamAndBowlsSpinInTheMiddle()
    {
        var kinds = new[] { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.Medium, eBowlerKind.Fast, eBowlerKind.OffSpin, eBowlerKind.LegSpin, eBowlerKind.Mystery, eBowlerKind.LegSpin };
        var rand = Rng(5);
        int seamInPowerplay = 0, spinInMiddle = 0, middle = 0, powerplay = 0;
        const int Innings = 200, Overs = 20;
        for (int inn = 0; inn < Innings; inn++)
        {
            var bowlers = new List<Bowler>();
            for (int i = 0; i < kinds.Length; i++) bowlers.Add(new Bowler("B" + i, kinds[i]));
            BowlingAttack.NewInnings();
            Bowler last = null;
            for (int over = 0; over < Overs; over++)
            {
                int i = BowlingAttack.PickNextBowler(bowlers, last, over, Overs, rand);
                Bowler b = bowlers[i];
                Assert.AreNotSame(last, b, "same bowler twice running");
                b.Balls += 6;
                Assert.LessOrEqual(BowlingAttack.OversBowled(b), BowlingAttack.Quota(Overs), "over his quota");
                bool spin = BowlerRepertoire.IsSpin(b.Kind);
                if (over < 6) { powerplay++; if (!spin) seamInPowerplay++; }
                else if (over < 16) { middle++; if (spin) spinInMiddle++; }
                BowlingAttack.OverDone(b);
                last = b;
            }
        }
        Assert.Greater(seamInPowerplay, powerplay * 0.75f, "seam in the powerplay");
        Assert.Greater(spinInMiddle, middle * 0.55f, "spin through the middle");
    }
}
