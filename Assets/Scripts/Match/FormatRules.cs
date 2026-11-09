using UnityEngine;

public enum eGamePhase { Powerplay, Middle, Death, NewBall, OldBall, Reverse }

/// <summary>
/// How each match length is bowled and fielded (2026-11-06: "research how people bowl in the 10-over
/// matches or 20-over matches versus 50-over matches versus test match... the bowling strategies
/// and the field sets"). From the innings' overs alone (a Test innings is MatchFormat.TestInningsOvers),
/// so a career match of any length gets the rules of the format it is closest to. Pure: FormatRulesTests.
///
/// The playing conditions:
///   Powerplay (two outside the circle): 6 overs of a T20, 10 of an ODI, 3 of a T10 (the ECN T10
///                 conditions), 2 of 5 (30%, as ICC reduced-overs tables give); none in a Test.
///   Outside the circle after it: 5; 4 through an ODI's middle overs (11-40); a Test has no limit -
///                 only two behind square on the leg side, as in every format (FieldSets).
///   Bowler's overs: a fifth of the innings (1 of 5, 2 of 10, 4 of 20, 10 of 50); none in a Test.
///
/// How it is played (BowlingAttack, FieldCaptain):
///   T10/T20       seam with the new ball in the powerplay, spin through the middle, the best quicks
///                 at the death (last 2 of 10, 4 of 20) on yorkers, wide yorkers, slower balls and the
///                 bouncer as a change-up, with the field back. Short spells: 2-3 overs.
///   ODI           the same three phases, longer: the death is the last 10 (41-50); the middle overs
///                 are spin and hard lengths to keep it to ones, spells of up to 5 overs for a quick
///                 and 8 for a spinner.
///   Test          no containment: the new ball (first 15 overs) in the corridor outside off - "the
///                 fourth stump" - with slips and a gully waiting; the old ball, spin with close
///                 catchers and the quicks' short-ball spells; reverse swing at the stumps from about
///                 55 overs; the second new ball at 80. Spells of 6 for a quick, 10 for a spinner.
///                 Fewer variations - the stock ball, moved off the seam and in the air, is the weapon.
/// </summary>
public static class FormatRules
{
    public static bool IsTest(int totalOvers) => totalOvers >= MatchFormat.TestInningsOvers;

    public static int PowerplayOvers(int total)
    {
        if (IsTest(total)) return 0;
        if (total >= 50) return 10;
        if (total >= 20) return 6;
        if (total >= 10) return 3;
        return total >= 2 ? Mathf.Max(1, Mathf.RoundToInt(total * 0.3f)) : 0;
    }

    /// Most fielders allowed outside the circle in over `over` (0-based).
    public static int MaxOutside(int over, int total)
    {
        if (IsTest(total)) return 9;
        if (over < PowerplayOvers(total)) return 2;
        if (total > 20 && over < total - 10) return 4;
        return FieldSets.MaxOutside;
    }

    /// Most overs one bowler may bowl (no limit in a Test).
    public static int Quota(int total) => IsTest(total) ? int.MaxValue : Mathf.Max(1, Mathf.CeilToInt(total / 5f));

    /// Overs at the end of the innings that are its death overs.
    public static int DeathOvers(int total)
    {
        if (IsTest(total) || total < 3) return 0;
        if (total >= 50) return 10;
        if (total >= 20) return 4;
        if (total >= 10) return 2;
        return 1;
    }

    /// Where the innings is in over `over` (0-based). A Test's by the age of the ball (a new one at 80).
    public static eGamePhase Phase(int over, int total)
    {
        if (IsTest(total))
        {
            int age = over % 80;
            return age < 15 ? eGamePhase.NewBall : age < 55 ? eGamePhase.OldBall : eGamePhase.Reverse;
        }
        if (over < PowerplayOvers(total)) return eGamePhase.Powerplay;
        if (over >= total - DeathOvers(total)) return eGamePhase.Death;
        return eGamePhase.Middle;
    }

    /// The seamers' phases, when the captain wants pace on.
    public static bool SeamPhase(eGamePhase p) =>
        p == eGamePhase.Powerplay || p == eGamePhase.Death || p == eGamePhase.NewBall || p == eGamePhase.Reverse;

    /// The longest spell from one end before he is rested.
    public static int MaxSpell(bool spinner, int total)
    {
        if (IsTest(total)) return spinner ? 10 : 6;
        if (total >= 50) return spinner ? 8 : 5;
        if (total >= 20) return spinner ? 4 : 3;
        return 2;
    }

    /// How many more (or fewer) variations than the bowling level's: few in a Test, most at the death.
    public static float VariationScale(eGamePhase p) => p switch
    {
        eGamePhase.NewBall => 0.4f,
        eGamePhase.OldBall => 0.6f,
        eGamePhase.Reverse => 0.5f,
        eGamePhase.Powerplay => 0.8f,
        eGamePhase.Death => 1.4f,
        _ => 1f,
    };

    /// How much a plan suits the phase, as a change to its cost (runs a ball: negative is likelier).
    public static float PlanBias(BowlingPlan plan, eGamePhase p)
    {
        if (plan == null) return 0f;
        switch (p)
        {
            case eGamePhase.Powerplay:
                return Pick(plan, offStump: -0.12f, stumps: -0.1f, offTheory: -0.12f, shortBall: 0.05f, yorkers: 0.25f, wideOff: 0.3f,
                            flight: 0f, darts: -0.1f, wideSpin: 0.05f);
            case eGamePhase.Middle:
                return Pick(plan, offStump: -0.08f, stumps: 0f, offTheory: 0.1f, shortBall: -0.06f, yorkers: 0.2f, wideOff: 0.15f,
                            flight: 0f, darts: -0.08f, wideSpin: -0.08f);
            case eGamePhase.Death:
                return Pick(plan, offStump: 0.15f, stumps: 0.05f, offTheory: 0.35f, shortBall: -0.05f, yorkers: -0.3f, wideOff: -0.28f,
                            flight: 0.25f, darts: -0.2f, wideSpin: -0.15f);
            case eGamePhase.NewBall:
                return Pick(plan, offStump: -0.25f, stumps: -0.05f, offTheory: -0.2f, shortBall: 0.1f, yorkers: 0.7f, wideOff: 0.7f,
                            flight: -0.1f, darts: 0.2f, wideSpin: 0.2f);
            case eGamePhase.OldBall:
                return Pick(plan, offStump: -0.15f, stumps: 0f, offTheory: -0.05f, shortBall: -0.12f, yorkers: 0.6f, wideOff: 0.7f,
                            flight: -0.25f, darts: 0.1f, wideSpin: 0f);
            default:   // Reverse
                return Pick(plan, offStump: -0.05f, stumps: -0.25f, offTheory: 0.1f, shortBall: 0f, yorkers: -0.1f, wideOff: 0.6f,
                            flight: -0.2f, darts: 0.05f, wideSpin: 0.05f);
        }
    }

    private static float Pick(BowlingPlan plan, float offStump, float stumps, float offTheory, float shortBall, float yorkers,
                              float wideOff, float flight, float darts, float wideSpin)
    {
        if (plan == BowlingPlans.OffStump) return offStump;
        if (plan == BowlingPlans.AttackStumps) return stumps;
        if (plan == BowlingPlans.OffTheory) return offTheory;
        if (plan == BowlingPlans.ShortBall) return shortBall;
        if (plan == BowlingPlans.Yorkers) return yorkers;
        if (plan == BowlingPlans.WideOff) return wideOff;
        if (plan == BowlingPlans.Flight) return flight;
        if (plan == BowlingPlans.Darts) return darts;
        if (plan == BowlingPlans.WideOfOffSpin) return wideSpin;
        return 0f;
    }

    /// How the field is set: a Test attacks (catchers in) unless the batter is getting away - and
    /// even then never sends everyone back; a limited-overs death defends.
    public static FieldSets.Plan Aggression(eGamePhase p, bool newBatter, int dotsInARow, int recentBalls, float strikeRate, int boundaries)
    {
        bool test = p == eGamePhase.NewBall || p == eGamePhase.OldBall || p == eGamePhase.Reverse;
        if (test)
            return recentBalls >= 12 && strikeRate >= 90f && !newBatter ? FieldSets.Plan.Standard : FieldSets.Plan.Attack;
        if (newBatter || dotsInARow >= 6 || (recentBalls >= 6 && strikeRate < 60f))
            return FieldSets.Plan.Attack;
        if (p == eGamePhase.Death || (recentBalls >= 6 && (strikeRate >= 150f || boundaries >= 2)))
            return FieldSets.Plan.Defend;
        return FieldSets.Plan.Standard;
    }

    /// Catchers an attacking field may add: up to four in a Test with the new ball (three slips and
    /// a gully), three with the old; two in a limited-overs match.
    public static int MaxCatchers(eGamePhase p) =>
        p == eGamePhase.NewBall ? 4 : p == eGamePhase.OldBall || p == eGamePhase.Reverse ? 3 : 2;
}
