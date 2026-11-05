using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The other side's innings, simulated ball by ball: the total the player chases (MatchSetup
/// "Chase"), or the AI chasing what the player made (MatchSetup "Bat first"). Pure, seeded, so a
/// result can be replayed over by over on the result screen and tested.
///
/// Each legal ball is a dot, 1, 2, 3, 4, 6 or a wicket, with a wide now and then. The odds come
/// from how strong the side is (its level, the same as the bowling the player picked: a Pro attack
/// comes with Pro batters) and how hard it is going at it (aggression): more boundaries and more
/// wickets the harder it goes. Setting a total it starts steady and goes hard at the end; chasing,
/// it goes as hard as the asking rate needs, and steadies when wickets fall. Tuned to T20-style
/// scoring: about 7 an over for a club side, 9 for a professional one (EditMode-tested).
/// </summary>
public static class AIInnings
{
    public class Result
    {
        public int runs, wickets, balls, overs, target;
        /// Runs and wickets in each over bowled, for the over-by-over replay.
        public readonly List<int> overRuns = new List<int>(), overWickets = new List<int>();
        public bool Chasing => target > 0;
        public bool ReachedTarget => target > 0 && runs >= target;
        public string Score => $"{runs}/{wickets}";
        public string OversText => $"{balls / 6}.{balls % 6}";
    }

    /// Batting strength by level (Easy .. Pro).
    private static readonly float[] Strength = { 0.15f, 0.45f, 0.7f, 0.95f };
    private const float WideChance = 0.03f;

    /// The innings: `overs` overs, chasing `target` (0 = setting a total).
    public static Result Play(int overs, int target, eBowlingLevel level, System.Random rng)
    {
        var r = new Result { overs = overs, target = target };
        float k = Strength[Mathf.Clamp((int)level, 0, Strength.Length - 1)];
        int total = overs * 6;
        for (int over = 0; over < overs && r.wickets < 10 && !r.ReachedTarget; over++)
        {
            int startRuns = r.runs, startWickets = r.wickets;
            int legal = 0;
            while (legal < 6 && r.wickets < 10 && !r.ReachedTarget)
            {
                if (rng.NextDouble() < WideChance)
                {
                    r.runs++;
                    continue;
                }
                float a = Aggression(r, total, k);
                Ball(r, a, k, rng);
                legal++;
                r.balls++;
            }
            r.overRuns.Add(r.runs - startRuns);
            r.overWickets.Add(r.wickets - startWickets);
        }
        return r;
    }

    /// How hard they go at it now, 0 (blocking) .. 1 (slogging).
    private static float Aggression(Result r, int total, float k)
    {
        int left = total - r.balls;
        float a;
        if (r.Chasing)
        {
            float need = r.target - r.runs;
            float perBall = need / Mathf.Max(1, left);
            // Par for this side is ~1.15-1.5 a ball: above it they push, well below they knock it about.
            float par = 1.15f + 0.35f * k;
            a = Mathf.Clamp01(0.35f + (perBall - par) * 0.9f);
        }
        else
        {
            float through = total > 0 ? (float)r.balls / total : 1f;
            a = through < 0.3f ? 0.45f : through < 0.75f ? 0.38f : 0.85f;
        }
        // Wickets down: protect what is left (the tail cannot slog for long).
        if (r.wickets >= 7) a *= 0.65f;
        else if (r.wickets >= 5) a *= 0.85f;
        return a;
    }

    private static void Ball(Result r, float a, float k, System.Random rng)
    {
        float wicket = 0.032f + 0.05f * a - 0.012f * k;
        float six = 0.012f + 0.06f * a + 0.03f * k;
        float four = 0.06f + 0.07f * a + 0.04f * k;
        float three = 0.008f;
        float two = 0.065f + 0.02f * k;
        float one = 0.36f - 0.06f * a;
        double x = rng.NextDouble();
        if ((x -= wicket) < 0) { r.wickets++; return; }
        if ((x -= six) < 0) { r.runs += 6; return; }
        if ((x -= four) < 0) { r.runs += 4; return; }
        if ((x -= three) < 0) { r.runs += 3; return; }
        if ((x -= two) < 0) { r.runs += 2; return; }
        if ((x -= one) < 0) { r.runs += 1; return; }
        // a dot
    }

    /// What this side usually makes in `overs` (the middle of 40 innings, always the same 40).
    public static int Typical(int overs, eBowlingLevel level)
    {
        var rng = new System.Random(12345);
        var totals = new List<int>(40);
        for (int i = 0; i < 40; i++) totals.Add(Play(overs, 0, level, rng).runs);
        totals.Sort();
        return totals[totals.Count / 2];
    }

    /// "AI won by 4 wickets (7 balls left)" / "You won by 12 runs" / "Match tied", for the AI
    /// chasing `r.target` after the player made target - 1.
    public static string ChaseResult(Result r, out bool playerWon, out bool tied, string them = "AI", string us = "You")
    {
        tied = !r.ReachedTarget && r.runs == r.target - 1;
        playerWon = !r.ReachedTarget && !tied;
        if (tied) return "Match tied";
        if (r.ReachedTarget)
        {
            int left = 10 - r.wickets, balls = r.overs * 6 - r.balls;
            return $"{them} won by {left} wicket{(left == 1 ? "" : "s")} ({balls} ball{(balls == 1 ? "" : "s")} left)";
        }
        int by = r.target - 1 - r.runs;
        return $"{us} won by {by} run{(by == 1 ? "" : "s")}";
    }
}
