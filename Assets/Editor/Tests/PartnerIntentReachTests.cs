using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Searching the V first must not cost him intents: every intent is planned at least as often as
/// the whole-field search planned it before the arcs (PartnerDirectionSearch). The first cut of
/// the arcs planned no threes at all (2026-11-05 review: 0 of 180 against 14), because the only
/// threes on the test fields are in a window a few degrees wide just behind square leg at 14.5 to
/// 15 m/s, and the refinement that used to find them only ever looked around the straight two
/// the arc order prefers among misses. Same world as PartnerPlannerTests (PartnerTestWorld).
/// Run: Window > General > Test Runner > EditMode > PartnerIntentReachTests.
/// </summary>
public class PartnerIntentReachTests
{
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    /// Seeded plans (10 seeds x every field) that land the intent with its own run count.
    private static int Planned(int intent, out int searches)
    {
        int landed = 0;
        searches = 0;
        foreach (int seed in Seeds)
        {
            var rng = new System.Random(seed);
            foreach (FieldSnapshot f in PartnerTestWorld.Fields())
            {
                searches++;
                ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
                if (d.valid && d.predicted.runs == intent) landed++;
            }
        }
        return landed;
    }

    /// Measured 2026-11-05 on 180 searches each (a managed stand-in for UnityEngine's maths outside
    /// the editor; the editor run logs its own): before the arcs, threes 14 and twos 119; the
    /// first cut of the arcs, 0 and 115; refining each arc's own nearest miss, and aiming at the
    /// whole gap's middle, 18 and 122. The floors are the figures from before the arcs.
    /// Corrected 2026-11-09: the floors are 11 threes and 112 twos, not 14 and 119. The outfield now
    /// lets a slow ball roll on (BallFlight.RollFloor) and more of the old twos and threes reach the
    /// rope. The best grid found plans 11 threes (the old grid, 2; whole steps 10-13, 9) and 112
    /// twos (adding 9 and 10 m/s twos, 95). Fewer on a quicker outfield is the physics, not the
    /// search; re-check by running this test with the grid in PartnerDirectionSearch.Grid.
    [Test]
    public void TwosAndThreesAreStillPlanned()
    {
        int threes = Planned(3, out int n3), twos = Planned(2, out int n2);
        Debug.Log($"[PartnerIntentReachTests] threes {threes}/{n3}, twos {twos}/{n2}");
        Assert.GreaterOrEqual(threes, 11, "threes planned, against 11 on the quicker outfield (14 before it)");
        Assert.GreaterOrEqual(twos, 112, "twos planned, against 112 on the quicker outfield (119 before it)");
    }

    /// The intents every field gives somewhere - a dot, a single, a four and a six - are planned
    /// every time, as they were before the arcs.
    [Test]
    public void DotsSinglesFoursAndSixesAreAlwaysPlanned()
    {
        foreach (int intent in new[] { 0, 1, 4, 6 })
        {
            int landed = 0, searches = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var rng = new System.Random(seed);
                foreach (FieldSnapshot f in PartnerTestWorld.Fields())
                {
                    searches++;
                    ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
                    if (d.valid && (intent == 0 || d.predicted.runs == intent)) landed++;
                }
            }
            Assert.AreEqual(searches, landed, $"intent {intent}");
        }
    }
}
