using System;
using System.Collections.Generic;

/// <summary>
/// The arithmetic of the match situations (phase 7), with no scene: how much of an innings' scoring
/// a number of overs holds, the target rain leaves a chaser, how many overs rain leaves, the runs a
/// chaser had to pass, and the collapse a rescue starts from.
/// </summary>
public static class CareerSituationMath
{
    /// How fast the share of an innings' runs grows with its overs (a resource curve, DLS-like).
    public const float ResourceDecay = 0.027f;

    /// The share of a full innings' scoring `overs` hold (0 at none, rising ever slower).
    public static float Resource(int overs) => overs <= 0 ? 0f : 1f - (float)Math.Exp(-ResourceDecay * overs);

    /// The chaser's target when rain cuts his innings from `fullOvers` to `overs`: the setter's runs
    /// scaled by the two innings' resources, plus one (never more than the full target, never under 1).
    public static int RevisedTarget(int theirRuns, int fullOvers, int overs)
    {
        float full = Resource(fullOvers);
        if (full <= 0f || overs >= fullOvers)
            return theirRuns + 1;
        int t = (int)Math.Floor(theirRuns * Resource(overs) / full) + 1;
        return Math.Max(1, Math.Min(theirRuns + 1, t));
    }

    /// The overs rain leaves of `full`: a fifth to two fifths of them lost, at least one lost and one left.
    public static int RainOvers(int full, Random rng)
    {
        double u = 0.2 + 0.2 * (rng?.NextDouble() ?? 0.5);
        return Math.Max(1, full - Math.Max(1, (int)Math.Round(full * u)));
    }

    /// Runs the chaser had to pass: revised target - 1 when rain cut it, else the setter's runs.
    /// NRR in a rain chase still counts the setter's full overs (follow-up: DLS convention).
    public static int Par(InningsCard setter, InningsCard chaser) =>
        chaser != null && chaser.revised && chaser.target > 0 ? chaser.target - 1 : setter?.runs ?? 0;

    /// The runs each side of a played fixture is judged on: the chaser (the card with a target) on
    /// its own runs, the side that set the total on the runs the chaser had to pass (Par). With no
    /// rain these are simply both sides' runs.
    public static void Judged(InningsCard home, InningsCard away, out int homeRuns, out int awayRuns)
    {
        bool homeChased = home != null && home.target > 0 && (away == null || away.target <= 0);
        int par = homeChased ? Par(away, home) : Par(home, away);
        homeRuns = homeChased ? home.runs : par;
        awayRuns = homeChased ? par : away?.runs ?? 0;
    }

    // ---- A collapse to rescue ---------------------------------------------------------------------

    /// The share of the innings' balls a collapse may take; the top order's rating drop and intent
    /// while it lasts (a nervy, edgy start rather than a slog).
    public const float CollapseShare = 0.3f, CollapseDrop = 15f, CollapseIntent = 0.3f;

    /// The top order's collapse, ball by ball, as CareerSim codes for the match to replay before he
    /// comes in: `wickets` fall at seeded places among the first B legal balls (B = the larger of
    /// 3 x wickets and `budget`, never the whole innings); every other ball is CareerSim.Ball for the
    /// striker at CollapseDrop below his rating and CollapseIntent, a wicket there played as a dot,
    /// and a chase is never won in it. Each is scored as the match will score it
    /// (CareerMatchLogic.Score) and the script ends on the last of the wickets, so `end` is the
    /// score he comes in at, having faced no ball. Empty when the plan has too few batters or balls,
    /// or has him in the top order.
    public static List<int> Collapse(MatchPlan plan, int wickets, int budget, Random rng, out MatchState end)
    {
        var codes = new List<int>();
        end = CareerMatchLogic.NewState(plan, plan != null ? plan.overs * 6 : 0, plan?.Target ?? 0);
        int total = end.totalBalls;
        int b = Math.Min(Math.Max(3 * wickets, budget), total - 1);
        // He must come in on the last wicket, never face a scripted ball: he bats after the top order.
        if (plan == null || rng == null || wickets <= 0 || b < wickets || end.batters.Count <= wickets + 1
            || plan.slot < wickets + 1)
            return codes;
        HashSet<int> falls = WicketBalls(wickets, b, rng);
        int legal = 0;
        for (int guard = 0; end.wickets < wickets && guard < b * 4 + 30; guard++)
        {
            int code = falls.Contains(legal) ? CareerSim.Wicket : NotOut(end, plan.bowling, rng);
            CareerMatchLogic.Score(end, code, "");
            codes.Add(code);
            if (code != CareerSim.Wide)
                legal++;
            if (CareerMatchLogic.InningsDone(end))
                break;
        }
        return codes;
    }

    /// `wickets` distinct legal-ball places out of the first `balls`.
    private static HashSet<int> WicketBalls(int wickets, int balls, Random rng)
    {
        var places = new List<int>();
        for (int i = 0; i < balls; i++)
            places.Add(i);
        var falls = new HashSet<int>();
        for (int i = 0; i < wickets; i++)
        {
            int j = i + rng.Next(balls - i);
            (places[i], places[j]) = (places[j], places[i]);
            falls.Add(places[i]);
        }
        return falls;
    }

    /// A collapse ball that is not one of its wickets: CareerSim's ball for the struggling striker,
    /// a wicket played as a dot, and nothing that would reach a chase's target.
    private static int NotOut(MatchState s, eBowlingLevel level, Random rng)
    {
        float rating = s.Striker != null ? s.Striker.rating : 40f;
        int code = CareerSim.Ball(rating - CollapseDrop, level, CollapseIntent, rng);
        int runs = code == CareerSim.Wide ? 1 : Math.Max(0, code);
        if (code == CareerSim.Wicket || (s.target > 0 && s.runs + runs >= s.target))
            return 0;
        return code;
    }
}
