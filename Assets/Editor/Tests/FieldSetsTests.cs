using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The captain's plans and fields (BowlingPlans, FieldSets, FieldPlan, BatterBook): every plan's
/// field, for every bowler and either hand, keeps the fielding limits, stands inside the rope and
/// off the pitch; a left-hander's field is the right-hander's mirrored; each plan bowls what it
/// says; and the captain's book steers him away from what the batter scores off. Standalone.
/// </summary>
public class FieldSetsTests
{
    private static readonly eSwingType[] Bowlers =
        { eSwingType.Pace, eSwingType.InSwing, eSwingType.OutSwing, eSwingType.LegSpin, eSwingType.OffSpin };

    private static FieldSets.Ground Ground(eSwingType bowler, float offSign) => new FieldSets.Ground
    {
        strikerX = BallDelivery.BatsmanStumpsX, nonStrikerX = -10.36f, boundary = 56f,
        keeperX = FieldSets.KeeperX(bowler), offSign = offSign,
    };

    private static void AssertSound(List<Vector3> places, FieldSets.Ground g, string what)
    {
        Assert.AreEqual(9, places.Count, what);
        Assert.IsTrue(FieldSets.Valid(places, g), $"{what}: breaks the fielding limits");
        foreach (Vector3 p in places)
        {
            Assert.Less(new Vector2(p.x, p.z).magnitude, g.boundary - 1f, $"{what}: {p} on or over the rope");
            bool onPitch = Mathf.Abs(p.z) < 1.6f && p.x > g.nonStrikerX - 1.5f && p.x < g.strikerX - 1.2f;
            Assert.IsFalse(onPitch, $"{what}: {p} on the pitch");
        }
        for (int i = 0; i < places.Count; i++)
            for (int j = i + 1; j < places.Count; j++)
                Assert.Greater(Vector3.Distance(places[i], places[j]), 1.5f, $"{what}: two fielders on one spot");
    }

    /// A batter who has scored a lot: hits in every sector, boundaries square on the off side.
    private static BatterBook.Record Dangerous()
    {
        var r = new BatterBook.Record();
        for (int s = 0; s < BatterBook.Sectors; s++) r.wheel[s] = 3f + s;
        r.wheel[BatterBook.Sector(95f)] = 30f;
        r.wheelBoundaries[BatterBook.Sector(95f)] = 24f;
        return r;
    }

    [Test]
    public void EveryPlansFieldIsLegal()
    {
        foreach (var bowler in Bowlers)
            foreach (BowlingPlan plan in BowlingPlans.For(bowler))
                foreach (float hand in new[] { 1f, -1f })
                {
                    FieldSets.Ground g = Ground(bowler, hand);
                    string what = $"{plan.name} vs {bowler}, {(hand > 0 ? "RH" : "LH")}";
                    AssertSound(new FieldPlan(plan.field, g, new BatterBook.Record()).places, g, what);
                    // ...and still legal once the captain has changed it for a dangerous batter.
                    foreach (var aggression in new[] { FieldSets.Plan.Attack, FieldSets.Plan.Defend })
                    {
                        var layout = new FieldPlan(plan.field, g, Dangerous());
                        if (aggression == FieldSets.Plan.Attack) layout.AddCatchers(BowlingPlans.Catchers(plan.spin), 2);
                        else layout.PushBack(2);
                        layout.Adapt(2, 4f);
                        AssertSound(layout.places, g, $"{what}, {aggression} and adapted");
                    }
                }
    }

    [Test]
    public void RingFieldersStandJustInsideTheCircle()
    {
        foreach (var bowler in Bowlers)
            foreach (BowlingPlan plan in BowlingPlans.For(bowler))
                foreach (float hand in new[] { 1f, -1f })
                {
                    FieldSets.Ground g = Ground(bowler, hand);
                    var layout = new FieldPlan(plan.field, g, new BatterBook.Record());
                    for (int i = 0; i < layout.places.Count; i++)
                    {
                        if (layout.kinds[i] != FieldSets.Kind.Ring) continue;
                        Vector3 p = layout.places[i];
                        float d = Vector3.Distance(p, new Vector3(g.strikerX, 0f, 0f));
                        float edge = FieldSets.RingEdge(FieldSets.AngleOf(p, g), g);
                        string what = $"{plan.name} {layout.names[i]} ({(hand > 0 ? "RH" : "LH")})";
                        Assert.IsTrue(FieldSets.InsideRing(p, g), $"{what}: outside the circle");
                        Assert.GreaterOrEqual(d, FieldSets.RingNearest - 0.01f, $"{what}: {d:F1} m, too close to the bat");
                        // Square and behind, a few metres inside the line; straight, at the furthest a
                        // ring fielder stands (mid-off ~29 m where the line is ~47 m out).
                        if (d < FieldSets.RingFurthest - 0.01f)
                            Assert.Less(edge - d, 8f, $"{what}: {edge - d:F1} m inside the circle's line");
                    }
                }
        // The oval: 27.4 m square and behind, ~47 m straight.
        FieldSets.Ground r = Ground(eSwingType.Pace, 1f);
        Assert.AreEqual(FieldSets.RingRadius, FieldSets.RingEdge(90f, r), 0.05f);
        Assert.AreEqual(FieldSets.RingRadius, FieldSets.RingEdge(160f, r), 0.05f);
        Assert.Greater(FieldSets.RingEdge(0f, r), 45f);
    }

    [Test]
    public void PowerplayFieldsKeepTwoOutside()
    {
        Assert.AreEqual(6, FieldCaptain.PowerplayOvers(20));
        Assert.AreEqual(10, FieldCaptain.PowerplayOvers(50));
        Assert.AreEqual(2, FieldCaptain.PowerplayOvers(5));
        Assert.AreEqual(0, FieldCaptain.PowerplayOvers(1));
        Assert.AreEqual(2, FieldCaptain.MaxOutsideIn(0, 20));
        Assert.AreEqual(5, FieldCaptain.MaxOutsideIn(6, 20));
        Assert.AreEqual(4, FieldCaptain.MaxOutsideIn(20, 50));
        Assert.AreEqual(5, FieldCaptain.MaxOutsideIn(45, 50));
        foreach (var bowler in Bowlers)
            foreach (BowlingPlan plan in BowlingPlans.For(bowler))
                foreach (float hand in new[] { 1f, -1f })
                {
                    FieldSets.Ground g = Ground(bowler, hand);
                    g.maxOutside = 2;
                    string what = $"{plan.name} vs {bowler}, {(hand > 0 ? "RH" : "LH")}, powerplay";
                    var layout = new FieldPlan(plan.field, g, Dangerous());
                    AssertSound(layout.places, g, what);
                    layout.PushBack(2);
                    layout.Adapt(2, 4f);
                    AssertSound(layout.places, g, what + ", adapted");
                    FieldSets.Count(layout.places, g, out int outside, out _, out int leg);
                    Assert.LessOrEqual(outside, 2, what);
                    Assert.LessOrEqual(leg, FieldSets.MaxLegSide, what);
                }
    }

    [Test]
    public void RingComesInForTheSingles()
    {
        FieldSets.Ground g = Ground(eSwingType.Pace, 1f);
        float usual = FieldSets.RingPlace(55f, g);
        g.ringIn = FieldSets.RingInFor1s;
        Assert.AreEqual(usual - FieldSets.RingInFor1s, FieldSets.RingPlace(55f, g), 1e-3f);
    }

    [Test]
    public void PlansHaveAWeightForEveryChannelAndBand()
    {
        foreach (BowlingPlan plan in BowlingPlans.Pace)
        {
            Assert.AreEqual(DeliveryPicker.Channels.Length, plan.lineWeights.Length, plan.name);
            Assert.AreEqual(DeliveryPicker.PaceBands.Length, plan.lengthWeights.Length, plan.name);
        }
        foreach (BowlingPlan plan in BowlingPlans.Spin)
        {
            Assert.AreEqual(DeliveryPicker.Channels.Length, plan.lineWeights.Length, plan.name);
            Assert.AreEqual(DeliveryPicker.SpinBands.Length, plan.lengthWeights.Length, plan.name);
        }
    }

    [Test]
    public void LeftHandersFieldIsTheMirror()
    {
        foreach (var bowler in Bowlers)
            foreach (BowlingPlan plan in BowlingPlans.For(bowler))
            {
                List<Vector3> rh = new FieldPlan(plan.field, Ground(bowler, 1f), new BatterBook.Record()).places;
                List<Vector3> lh = new FieldPlan(plan.field, Ground(bowler, -1f), new BatterBook.Record()).places;
                for (int i = 0; i < rh.Count; i++)
                {
                    Assert.AreEqual(rh[i].x, lh[i].x, 1e-4f);
                    Assert.AreEqual(rh[i].z, -lh[i].z, 1e-4f);
                }
            }
    }

    [Test]
    public void SlipsStandBesideTheKeeperOnTheOffSide()
    {
        FieldSets.Ground g = Ground(eSwingType.Pace, 1f);
        FieldSets.Position[] catchers = BowlingPlans.Catchers(false);
        Vector3 slip = FieldSets.Place(catchers[0], g), second = FieldSets.Place(catchers[1], g);
        Assert.That(slip.x, Is.InRange(g.keeperX - 2f, g.keeperX));
        Assert.That(slip.z, Is.InRange(1.5f, 4f), "first slip a couple of metres off the keeper, off side");
        Assert.Greater(second.z, slip.z);
    }

    [Test]
    public void DefendingPutsMenBackWhereTheBatterHits()
    {
        FieldSets.Ground g = Ground(eSwingType.Pace, 1f);
        var layout = new FieldPlan(BowlingPlans.OffStump.field, g, Dangerous());
        layout.PushBack(2);
        layout.Adapt(2, 4f);
        bool deepPoint = false;
        for (int i = 0; i < layout.places.Count; i++)
            if (layout.kinds[i] == FieldSets.Kind.Deep && BatterBook.Sector(FieldSets.AngleOf(layout.places[i], g)) == BatterBook.Sector(95f))
                deepPoint = true;
        Assert.IsTrue(deepPoint, "a man on the rope where the boundaries go: " + layout);
    }

    [Test]
    public void AttackingAddsCatchers()
    {
        FieldSets.Ground g = Ground(eSwingType.OffSpin, 1f);
        var layout = new FieldPlan(BowlingPlans.Darts.field, g, new BatterBook.Record());
        layout.AddCatchers(BowlingPlans.Catchers(true), 2);
        int close = 0;
        // The spinner's slip stands beside the keeper (a Slip place), short leg and silly point Close.
        foreach (var k in layout.kinds) if (k == FieldSets.Kind.Close || k == FieldSets.Kind.Slip) close++;
        Assert.AreEqual(2, close, layout.ToString());
    }

    /// Each plan bowls more of what it is named for than plain bowling does.
    [Test]
    public void PlansBowlWhatTheySay()
    {
        float Short(BowlingPlan p) { var s = BowlingPlans.Share(p, eSwingType.Pace); float x = 0; for (int z = 0; z < 4; z++) x += s[z, 4] + s[z, 3]; return x; }
        float Yorker(BowlingPlan p) { var s = BowlingPlans.Share(p, eSwingType.Pace); float x = 0; for (int z = 0; z < 4; z++) x += s[z, 0]; return x; }
        float Wide(BowlingPlan p) { var s = BowlingPlans.Share(p, eSwingType.Pace); float x = 0; for (int b = 0; b < 5; b++) x += s[0, b]; return x; }
        float Stumps(BowlingPlan p) { var s = BowlingPlans.Share(p, eSwingType.Pace); float x = 0; for (int b = 0; b < 5; b++) x += s[2, b]; return x; }
        Assert.Greater(Short(BowlingPlans.ShortBall), 1.4f * Short(null));
        Assert.Greater(Yorker(BowlingPlans.Yorkers), 2f * Yorker(null));
        Assert.Greater(Wide(BowlingPlans.OffTheory), 1.5f * Wide(null));
        Assert.Greater(Stumps(BowlingPlans.AttackStumps), 1.4f * Stumps(null));
    }

    /// A batter who pulls every short ball for four, and edges the full ones outside off: the
    /// short-ball plan is the dearest, and pitching it up outside off is cheaper.
    [Test]
    public void TheBookSteersThePlan()
    {
        var book = new BatterBook();
        for (int i = 0; i < 24; i++)
        {
            book.Add(eSwingType.Pace, 0.0f, eLengthBand.Short, 4, true, false, false, -80f);
            book.Add(eSwingType.Pace, 0.05f, eLengthBand.BackOfALength, 2, true, false, false, -70f);
            book.Add(eSwingType.Pace, 0.3f, eLengthBand.Full, 0, true, i % 3 == 0, i % 12 == 0, float.NaN);
            book.Add(eSwingType.Pace, 0.7f, eLengthBand.GoodLength, 0, true, i % 4 == 0, false, float.NaN);
        }
        float shortCost = book.Cost(BowlingPlans.Share(BowlingPlans.ShortBall, eSwingType.Pace), eSwingType.Pace);
        float offCost = book.Cost(BowlingPlans.Share(BowlingPlans.OffTheory, eSwingType.Pace), eSwingType.Pace);
        Assert.Greater(shortCost, offCost + 0.3f);
        // ...and against spin nothing is known yet: the pace book does not leak into it.
        Assert.AreEqual(0f, book.For(eSwingType.OffSpin).totalBalls);
    }

    [Test]
    public void AnglesRoundTrip()
    {
        FieldSets.Ground g = Ground(eSwingType.Pace, -1f);
        foreach (float a in new[] { 0f, 40f, 92f, 150f, -60f, -155f })
        {
            Vector3 p = FieldSets.Place(new FieldSets.Position("", FieldSets.Kind.Ring, a, 20f), g);
            Assert.AreEqual(a, FieldSets.AngleOf(p, g), 0.01f);
        }
    }
}
