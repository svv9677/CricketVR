using UnityEngine;

/// <summary>
/// Front-foot no-balls and free hits, as the Laws and playing conditions have them:
///   * A no-ball: the bowler's front foot lands with no part of it behind the popping crease. The
///     umpire calls it as the ball is bowled. It costs one run (to the bowler), plus whatever is
///     scored off it; it is not one of the over's six; and the batter cannot be out bowled or
///     caught off it (only run out, which the game does not have).
///   * A free hit: the ball after a no-ball (in all limited-overs cricket now). The batter cannot
///     be out bowled or caught off it either. If the free hit is itself a wide or a no-ball, the
///     next ball is still a free hit.
///
/// How often a bowler oversteps goes with the standard of the attack (club bowlers more, a
/// professional rarely). FrontFootNoBall moves the bowler's run-up forward for the ball he
/// oversteps on, so his foot really does land over the line. HUD scores it (ScoreNoBall), the
/// umpire signals it (UmpireSignals), and the between-balls panel shows FREE HIT.
/// </summary>
public static class NoBalls
{
    /// Chance of overstepping, by bowling level (Easy .. Pro): about one an over for a club attack,
    /// one every five or six overs for a professional one.
    public static readonly float[] OverstepChance = { 0.05f, 0.035f, 0.022f, 0.012f };

    /// How far past the usual mark the run-up starts on an overstep (m): the marks put the front
    /// toes just over the popping crease with the heel behind it; this puts the heel over too.
    public const float OverstepBy = 0.32f;

    /// This delivery will be (has been) a no-ball.
    public static bool Overstep { get; private set; }
    /// The umpire has called it (at the release).
    public static bool Called { get; private set; }
    /// This delivery is a free hit.
    public static bool FreeHit { get; private set; }
    /// The next delivery will be a free hit (for the between-balls panel).
    public static bool NextIsFreeHit { get; private set; }

    public static event System.Action OnCalled;

    /// Checks and debug: the next delivery is a no-ball.
    public static bool ForceNext;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Overstep = Called = FreeHit = NextIsFreeHit = false;
        OnCalled = null;
    }

    public static void NewInnings() => Overstep = Called = FreeHit = NextIsFreeHit = false;

    public static float ChanceFor(eBowlingLevel level) =>
        OverstepChance[Mathf.Clamp((int)level, 0, OverstepChance.Length - 1)];

    /// A new delivery is being set up: decide whether he oversteps, and make it a free hit if the
    /// last one called for it.
    public static void NewDelivery(System.Func<float> rand01)
    {
        FreeHit = NextIsFreeHit;
        NextIsFreeHit = false;
        Called = false;
        Overstep = ForceNext || rand01() < ChanceFor(MatchSetup.Bowling);
        ForceNext = false;
    }

    /// The ball has left his hand.
    public static void Released()
    {
        if (!Overstep || Called) return;
        Called = true;
        OnCalled?.Invoke();
    }

    /// The ball is over and scored. wasWideOrNoBall: it does not count in the over.
    public static void Scored(bool wasWideOrNoBall)
    {
        // A no-ball earns a free hit; a free hit that was a wide or no-ball passes it on.
        NextIsFreeHit = Called || (FreeHit && wasWideOrNoBall);
        Overstep = false;
        Called = false;
    }

    /// Can the batter be out bowled or caught off this ball?
    public static bool Protected => Called || FreeHit;
}
