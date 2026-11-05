using System;
using NUnit.Framework;

/// <summary>
/// The match situations' arithmetic (CareerSituationMath) and the parts of CareerSituations that are
/// real from the start: the resource curve rises from nothing, rain's revised target is the
/// setter's runs scaled by the overs left (164 in 20 overs is 124 in 14) and never above the full
/// target, rain leaves the overs it should at each format, the chaser's par, the plan's rng is the
/// same each time it is asked for and moves on with the next fixture, the final is the final, and
/// MatchPlan.Target with and without a revised target.
/// </summary>
public class CareerSituationMathTests
{
    [Test]
    public void Resource_StartsAtNothing_AndRisesWithTheOvers()
    {
        Assert.AreEqual(0f, CareerSituationMath.Resource(0));
        Assert.AreEqual(0f, CareerSituationMath.Resource(-3));
        float last = 0f;
        for (int overs = 1; overs <= 50; overs++)
        {
            float r = CareerSituationMath.Resource(overs);
            Assert.Greater(r, last, $"Resource({overs})");
            Assert.Less(r, 1f);
            last = r;
        }
    }

    [Test]
    public void RevisedTarget_ScalesTheRunsByTheOversLeft()
    {
        Assert.AreEqual(124, CareerSituationMath.RevisedTarget(164, 20, 14));
        Assert.AreEqual(165, CareerSituationMath.RevisedTarget(164, 20, 20), "no overs lost: the full target");
        for (int runs = 0; runs <= 250; runs += 7)
            for (int full = 1; full <= 20; full++)
                for (int overs = 1; overs <= full; overs++)
                {
                    int t = CareerSituationMath.RevisedTarget(runs, full, overs);
                    Assert.LessOrEqual(t, runs + 1, $"{runs} in {full}, cut to {overs}");
                    Assert.GreaterOrEqual(t, 1);
                }
    }

    [Test]
    public void RainOvers_LeavesAFifthToTwoFifthsLost()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            var rng = new Random(seed);
            int six = CareerSituationMath.RainOvers(6, rng), ten = CareerSituationMath.RainOvers(10, rng), twenty = CareerSituationMath.RainOvers(20, rng);
            Assert.That(six, Is.InRange(4, 5), $"6 overs, seed {seed}");
            Assert.That(ten, Is.InRange(6, 8), $"10 overs, seed {seed}");
            Assert.That(twenty, Is.InRange(12, 16), $"20 overs, seed {seed}");
        }
        Assert.That(CareerSituationMath.RainOvers(20, null), Is.InRange(12, 16), "no rng: the middle");
    }

    [Test]
    public void Par_IsTheRevisedTargetLessOne_OnlyWhenRainCutIt()
    {
        var setter = new InningsCard { runs = 164 };
        Assert.AreEqual(164, CareerSituationMath.Par(setter, new InningsCard { target = 165 }));
        Assert.AreEqual(123, CareerSituationMath.Par(setter, new InningsCard { target = 124, revised = true }));
        Assert.AreEqual(164, CareerSituationMath.Par(setter, new InningsCard { target = 0, revised = true }), "no target: the setter's runs");
        Assert.AreEqual(164, CareerSituationMath.Par(setter, null));
        Assert.AreEqual(0, CareerSituationMath.Par(null, null));
    }

    [Test]
    public void PlanRng_IsStableForACareer_AndMovesOnWithTheNextFixture()
    {
        CareerProfile p = CareerTestPlay.Career(11);
        Random a = CareerSituations.PlanRng(p), b = CareerSituations.PlanRng(p);
        int[] first = { a.Next(), a.Next(), a.Next() };
        Assert.AreEqual(first, new[] { b.Next(), b.Next(), b.Next() });

        Fixture next = CareerProgress.NextFixture(p);
        Assert.IsNotNull(next);
        next.played = true;
        Random c = CareerSituations.PlanRng(p);
        Assert.AreNotEqual(first, new[] { c.Next(), c.Next(), c.Next() });
        Assert.DoesNotThrow(() => CareerSituations.PlanRng(null).Next());
    }

    [Test]
    public void Of_TheFinalIsTheFinal()
    {
        Assert.AreEqual(eSituation.Final, CareerSituations.Of(new Fixture { final = true }));
        Assert.AreEqual(eSituation.Final, CareerSituations.Of(new Fixture { final = true, situation = eSituation.Chase }));
        Assert.AreEqual(eSituation.Chase, CareerSituations.Of(new Fixture { situation = eSituation.Chase }));
        Assert.AreEqual(eSituation.Normal, CareerSituations.Of(new Fixture()));
        Assert.AreEqual(eSituation.Normal, CareerSituations.Of(null));
    }

    [Test]
    public void Target_IsTheirRunsPlusOne_OrTheRevisedTarget()
    {
        Assert.AreEqual(0, new MatchPlan { batFirst = true, theirs = new InningsCard() }.Target, "batting first");
        Assert.AreEqual(0, new MatchPlan { batFirst = false, theirs = null }.Target, "no innings to chase");
        Assert.AreEqual(165, new MatchPlan { batFirst = false, theirs = new InningsCard { runs = 164 } }.Target);
        Assert.AreEqual(124, new MatchPlan { batFirst = false, theirs = new InningsCard { runs = 164 }, revisedTarget = 124 }.Target);
        Assert.AreEqual(0, new MatchPlan { batFirst = true, theirs = new InningsCard { runs = 164 }, revisedTarget = 124 }.Target,
                        "batting first never chases");
    }
}
