using System;

/// <summary>
/// The arithmetic of the match situations (phase 7), with no scene: how much of an innings' scoring
/// a number of overs holds, the target rain leaves a chaser, how many overs rain leaves, and the
/// runs a chaser had to pass.
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
}
