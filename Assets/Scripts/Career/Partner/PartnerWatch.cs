using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Whether the career player is watching his partner bat (phase 3): the setting on, a partner built,
/// and the partner on strike with the player at the other end. Frozen per ball at InGame_DeliverBall
/// (ThisBall), because HUD changes the strike at the dead ball, before the reset - reading Live again
/// in the middle of a ball can name the wrong batter.
///
/// Off (CareerProfile.watchPartner false), or outside a career match, every member here is false and
/// the game is exactly as it was: the partners' balls simulated (CareerMatch), the player at the
/// striker's end.
/// </summary>
public static class PartnerWatch
{
    /// Watching is possible this match: a career match, the setting on, the partner built.
    public static bool Enabled
    {
        get
        {
            if (!CareerMatchSession.Active)
                return false;
            CareerProfile p = CareerSaves.Current;
            PartnerBatter partner = PartnerBatter.Instance;
            return p != null && p.watchPartner && partner != null && partner.Ready;
        }
    }

    /// Watching now: Enabled, the striker a partner and the runner the player.
    public static bool Live
    {
        get
        {
            HUD hud = HUD.Instance;
            return hud != null && LiveFor(Enabled, hud.CurrentBatsman, hud.CurrentRunner);
        }
    }

    /// This ball is the partner's (Live as it was at InGame_DeliverBall; cleared at ResetToReady).
    public static bool ThisBall { get; private set; }

    /// The partner's dice for this match (seeded from the plan; new each scene).
    public static System.Random Rng { get; private set; } = new System.Random(0);

    /// Seed mixed into the plan's for the partner's dice, so they never run in step with CareerMatch's
    /// (seed ^ 0x3B9F21) or anything else seeded from the plan.
    public const int RngSalt = 0x5A17;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Reset();
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Reset();

    /// A new scene (and tests): no ball frozen, the dice from the match plan if there is one.
    public static void Reset()
    {
        ThisBall = false;
        MatchPlan plan = CareerMatchSession.Plan;
        Rng = new System.Random((plan != null ? plan.seed : 0) ^ RngSalt);
    }

    /// Main's state, as PartnerDirector hears it: the ball is frozen as it is released, and let go at
    /// the reset.
    public static void OnState(eGameState s)
    {
        // Live is only read at the release: anything else would be wasted, and wrong mid-ball.
        if (s == eGameState.InGame_DeliverBall || s == eGameState.InGame_ResetToReady)
            ThisBall = Frozen(s, ThisBall, Live);
    }

    // ---- The rules, without a scene (tests) ----------------------------------------------------

    /// ThisBall after state `s`: what Live is at the release, false from the reset, and otherwise
    /// unchanged whatever the strike does in between.
    public static bool Frozen(eGameState s, bool current, bool live) =>
        s == eGameState.InGame_DeliverBall ? live : s == eGameState.InGame_ResetToReady ? false : current;

    /// Watching, given whether it is possible, the striker and the runner.
    public static bool LiveFor(bool enabled, Batsman striker, Batsman runner) =>
        enabled && striker != null && !striker.IsHuman && runner != null && runner.IsHuman;

    /// A run of simulated balls stops for the player's turn: on strike, as in phase 2; or, while
    /// watching, at the crease at either end (his partner's balls are then bowled for real).
    public static bool YourTurn(bool youOnStrike, bool youAtCrease, bool enabled) =>
        youOnStrike || (enabled && youAtCrease);

    /// A ball a partner faced, in a career match (`human` is the player's Batsman, null in any other
    /// match): not the player's, so it is kept out of his ratings and the fielding captain's book.
    public static bool PartnersBall(Batsman striker, Batsman human) =>
        human != null && striker != null && !striker.IsHuman;

    /// The game between balls: no ball in the air, so the strike HUD shows is the next ball's.
    public static bool BetweenBalls(eGameState s) =>
        s == eGameState.None || s == eGameState.InMenu || s == eGameState.InGame_Ready ||
        s == eGameState.InGame_SelectDelivery || s == eGameState.InGame_SelectDeliveryLoop ||
        s == eGameState.InGame_ResetToReady || s == eGameState.InGame_ResetToReadyLoop;
}

/// <summary>
/// The batter at the striker's end as the rules need him: the player himself, or (watching) the
/// partner - his head for the keeper and the over-the-head wide, where he takes guard for the wide
/// lines. Every reader passes its own answer for the player as the fallback, which is what comes
/// back whenever the partner is not the striker (always, outside a career match with watching on).
/// </summary>
public static class Striker
{
    /// The partner is the striker: this ball's (frozen at its release), or between balls the next.
    public static bool IsPartner
    {
        get
        {
            if (PartnerWatch.ThisBall)
                return PartnerBatter.Instance != null;
            Main main = Main.Instance;
            return main != null && PartnerWatch.BetweenBalls(main.gameState) && PartnerWatch.Live;
        }
    }

    /// The striker's eyes: the partner's head bone, or `fallback` (the player's headset).
    public static Vector3 HeadPosition(Vector3 fallback)
    {
        if (!IsPartner)
            return fallback;
        Transform head = PartnerBatter.Instance.Head;
        return head != null ? head.position : fallback;
    }

    /// The top of the striker's head for the over-the-head wide: the partner's in his stance (fixed,
    /// so a duck does not lower the line), or `fallback`.
    public static float HeadTopY(float fallback) => IsPartner ? PartnerBatter.Instance.StanceHeadTopY : fallback;

    /// Where across the crease the striker takes guard (world z), when it is the partner.
    public static bool TryStanceZ(out float z)
    {
        z = 0f;
        if (!IsPartner)
            return false;
        z = PartnerBatter.Instance.StanceZ;
        return true;
    }
}
