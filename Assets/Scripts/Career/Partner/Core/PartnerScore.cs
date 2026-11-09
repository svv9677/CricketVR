/// <summary>
/// What a watched partner's ball is scored as, so watching him bat scores like not watching
/// (2026-11-06: "when I'm watching the non-striker play, he ends up scoring a lot more than what
/// is actually simulated... make sure that algorithm... is the same").
///
/// Both draw the ball from CareerSim at his rating, against the same attack, as hard as the match
/// says (PartnerIntent; CareerMatch.NextCode). Simulated, that draw is the ball. Watched, it only
/// picked his shot, and the live game - the fielding clock, a wicket the delivery could not give -
/// decided the runs: a single became two, a dismissal the ball could not produce became a dot, and
/// he batted on. Now the draw is what the ball is scored as wherever the picture allows it:
///
///   ball fielded after bouncing   what the fielding clock gives, up to the runs he meant (he
///                                 never takes on a run the ball does not allow, nor greeds an
///                                 extra one); a wicket he meant, run out
///   ball through to the keeper    a wicket he meant: the faint nick he played at, caught behind;
///                                 anything else a dot (no wide: those stay the umpire's)
///   over the rope, a catch,       as the game saw them - they are in plain view
///   bowled
///
/// HUD asks (its fielded and missed branches) only while Armed: the partner's ball, from his
/// release to the dead ball (PartnerDirector). Pure: PartnerScoreTests.
/// </summary>
public static class PartnerScore
{
    /// The partner's ball is live and this is what he meant (an intent code: CareerSim.Wicket,
    /// runs, or PartnerIntent.LeaveWide).
    public static bool Armed { get; private set; }
    public static int Intent { get; private set; }

    /// How the last pinned wicket fell ("run out", "c wk"), for the scorecard line; null if none.
    public static string LastHow { get; private set; }

    public static void Arm(int intent)
    {
        Armed = true;
        Intent = intent;
        LastHow = null;
    }

    public static void Disarm() => Armed = false;

    /// The ball was fielded after it bounced; `timed` is what the fielding clock gave. A HUD code:
    /// -1 a wicket, else runs.
    public static int Fielded(int timed)
    {
        if (!Armed || Intent == PartnerIntent.LeaveWide)
            return timed;
        if (Intent == CareerSim.Wicket)
        {
            LastHow = "run out";
            return -1;
        }
        // He runs what the fielding gives him, never more than he set out to: a ball fielded in
        // two seconds is no run even if he meant two (2026-11-06: "it looks like a one, it's a
        // two... be sensible"), and a single he meant stays a single however slow the fielder.
        return Intent >= 4 ? timed : UnityEngine.Mathf.Min(timed, UnityEngine.Mathf.Max(0, Intent));
    }

    /// The ball went through to the keeper (not a wide).
    public static int Missed()
    {
        if (!Armed || Intent != CareerSim.Wicket)
            return 0;
        LastHow = "c wk";
        return -1;
    }
}
