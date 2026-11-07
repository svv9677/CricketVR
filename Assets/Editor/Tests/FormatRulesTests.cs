using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The formats (2026-11-06): quick play's lengths (MatchFormat), the playing conditions and the way
/// each is bowled (FormatRules), and the AI's innings at each tempo (AIInnings).
/// </summary>
public class FormatRulesTests
{
    private const int Test = MatchFormat.TestInningsOvers;

    [Test]
    public void ThePowerplayIsEachFormatsOwn()
    {
        Assert.AreEqual(3, FormatRules.PowerplayOvers(10), "T10");
        Assert.AreEqual(6, FormatRules.PowerplayOvers(20), "T20");
        Assert.AreEqual(10, FormatRules.PowerplayOvers(50), "ODI");
        Assert.AreEqual(2, FormatRules.PowerplayOvers(5));
        Assert.AreEqual(0, FormatRules.PowerplayOvers(Test), "no powerplay in a Test");
    }

    [Test]
    public void FieldersOutsideTheCircle()
    {
        Assert.AreEqual(2, FormatRules.MaxOutside(5, 20), "T20 powerplay");
        Assert.AreEqual(5, FormatRules.MaxOutside(6, 20), "T20 after it");
        Assert.AreEqual(2, FormatRules.MaxOutside(9, 50), "ODI overs 1-10");
        Assert.AreEqual(4, FormatRules.MaxOutside(10, 50), "ODI overs 11-40");
        Assert.AreEqual(4, FormatRules.MaxOutside(39, 50));
        Assert.AreEqual(5, FormatRules.MaxOutside(40, 50), "ODI overs 41-50");
        Assert.AreEqual(9, FormatRules.MaxOutside(0, Test), "a Test has no limit");
    }

    [Test]
    public void BowlersOversAndSpells()
    {
        Assert.AreEqual(1, FormatRules.Quota(5));
        Assert.AreEqual(2, FormatRules.Quota(10));
        Assert.AreEqual(4, FormatRules.Quota(20));
        Assert.AreEqual(10, FormatRules.Quota(50));
        Assert.AreEqual(int.MaxValue, FormatRules.Quota(Test));
        Assert.Greater(FormatRules.MaxSpell(false, Test), FormatRules.MaxSpell(false, 20), "Test spells are longer");
        Assert.Greater(FormatRules.MaxSpell(true, Test), FormatRules.MaxSpell(false, Test), "spinners bowl longer spells");
    }

    [Test]
    public void ThePhasesOfAnInnings()
    {
        Assert.AreEqual(eGamePhase.Powerplay, FormatRules.Phase(0, 20));
        Assert.AreEqual(eGamePhase.Middle, FormatRules.Phase(10, 20));
        Assert.AreEqual(eGamePhase.Death, FormatRules.Phase(16, 20), "the last four of a T20");
        Assert.AreEqual(eGamePhase.Middle, FormatRules.Phase(39, 50));
        Assert.AreEqual(eGamePhase.Death, FormatRules.Phase(40, 50), "the last ten of an ODI");
        Assert.AreEqual(eGamePhase.Death, FormatRules.Phase(8, 10), "the last two of a T10");
        Assert.AreEqual(eGamePhase.NewBall, FormatRules.Phase(3, Test));
        Assert.AreEqual(eGamePhase.OldBall, FormatRules.Phase(30, Test));
        Assert.AreEqual(eGamePhase.Reverse, FormatRules.Phase(65, Test));
        Assert.AreEqual(eGamePhase.NewBall, FormatRules.Phase(82, Test), "the second new ball at 80");
    }

    [Test]
    public void EachPhaseFavoursItsOwnPlans()
    {
        // Lower is likelier (a change to the plan's cost).
        Assert.Less(FormatRules.PlanBias(BowlingPlans.Yorkers, eGamePhase.Death), FormatRules.PlanBias(BowlingPlans.OffTheory, eGamePhase.Death),
                    "yorkers at the death, not off theory");
        Assert.Less(FormatRules.PlanBias(BowlingPlans.OffStump, eGamePhase.NewBall), FormatRules.PlanBias(BowlingPlans.Yorkers, eGamePhase.NewBall),
                    "the corridor with a Test's new ball");
        Assert.Less(FormatRules.PlanBias(BowlingPlans.Flight, eGamePhase.OldBall), FormatRules.PlanBias(BowlingPlans.Darts, eGamePhase.OldBall),
                    "a Test spinner flights it to his close catchers");
        Assert.Less(FormatRules.PlanBias(BowlingPlans.Darts, eGamePhase.Middle), FormatRules.PlanBias(BowlingPlans.Flight, eGamePhase.Middle) + 0.01f,
                    "an ODI's middle overs contain");
    }

    [Test]
    public void ATestAttacksAndALimitedOversDeathDefends()
    {
        Assert.AreEqual(FieldSets.Plan.Attack, FormatRules.Aggression(eGamePhase.NewBall, false, 0, 12, 70f, 1));
        Assert.AreEqual(FieldSets.Plan.Standard, FormatRules.Aggression(eGamePhase.OldBall, false, 0, 12, 120f, 3),
                        "even a batter getting away gets a standard field, never a defensive one");
        Assert.AreEqual(FieldSets.Plan.Defend, FormatRules.Aggression(eGamePhase.Death, false, 0, 12, 110f, 1));
        Assert.AreEqual(4, FormatRules.MaxCatchers(eGamePhase.NewBall), "three slips and a gully");
        Assert.AreEqual(2, FormatRules.MaxCatchers(eGamePhase.Powerplay));
    }

    [Test]
    public void TheFormatsStepThroughTheListedLengths()
    {
        eMatchFormat was = MatchFormat.Current;
        int overs = GameSettings.Overs;
        try
        {
            MatchFormat.Current = eMatchFormat.Five;
            Assert.AreEqual(5, GameSettings.Overs);
            var seen = new List<string> { MatchFormat.Label() };
            for (int i = 0; i < 6; i++) { MatchFormat.Next(); seen.Add(MatchFormat.Label()); }
            CollectionAssert.AreEqual(new[] { "5 overs", "10 overs", "20 overs", "50 overs", "Test match", "Test match", "Test match" }, seen);
            Assert.AreEqual(50, GameSettings.Overs, "a Test leaves the overs alone");
            MatchFormat.Previous();
            Assert.AreEqual(eMatchFormat.Fifty, MatchFormat.Current);
            Assert.AreEqual(eMatchFormat.Ten, MatchFormat.Nearest(8), "an old save's overs, to the nearest listed");
        }
        finally
        {
            MatchFormat.Current = was;
            GameSettings.Overs = overs;
        }
    }

    // ---- The AI's innings at each tempo --------------------------------------------------------

    private static void Average(int overs, AIInnings.Tempo tempo, out float runs, out float wickets, out float oversBatted)
    {
        var rng = new System.Random(7);
        runs = wickets = oversBatted = 0f;
        const int N = 200;
        for (int i = 0; i < N; i++)
        {
            AIInnings.Result r = AIInnings.Play(overs, 0, eBowlingLevel.Medium, rng, tempo, 0);
            runs += r.runs; wickets += r.wickets; oversBatted += r.balls / 6f;
        }
        runs /= N; wickets /= N; oversBatted /= N;
    }

    [Test]
    public void AnODIIsPlayedAtAnODIsRate()
    {
        Average(50, AIInnings.Tempo.OneDay, out float runs, out float wickets, out float overs);
        Assert.That(runs / overs, Is.InRange(4.6f, 6.4f), $"runs an over ({runs:F0} in {overs:F1})");
        Assert.That(wickets, Is.InRange(6f, 10f), "most of the side used up");
        Assert.AreEqual(AIInnings.Tempo.OneDay, AIInnings.TempoFor(50));
        Assert.AreEqual(AIInnings.Tempo.Short, AIInnings.TempoFor(20));
    }

    [Test]
    public void ATestInningsIsPlayedAtATestsRate()
    {
        Average(Test, AIInnings.Tempo.Test, out float runs, out float wickets, out float overs);
        Assert.That(runs / overs, Is.InRange(2.8f, 4f), $"runs an over ({runs:F0} in {overs:F1})");
        Assert.That(overs, Is.InRange(70f, 130f), "bowled out in a day or so");
        Assert.AreEqual(10f, wickets, 0.01f, "a Test innings ends all out");
    }

    [Test]
    public void ATestSideDeclares()
    {
        AIInnings.Result r = AIInnings.Play(Test, 0, eBowlingLevel.Pro, new System.Random(3), AIInnings.Tempo.Test, 150);
        Assert.IsTrue(r.declared || r.wickets >= 10);
        if (r.declared)
            Assert.GreaterOrEqual(r.runs, 150);
    }
}
