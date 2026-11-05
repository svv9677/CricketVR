/// <summary>
/// Whether the career player is watching his partner bat (phase 3): the setting on, a partner built,
/// and the partner on strike with the player at the other end. Frozen per ball at InGame_DeliverBall.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these members.
/// </summary>
public static class PartnerWatch
{
    /// Watching is possible this match: a career match, the setting on, the partner built.
    public static bool Enabled => false;
    /// Watching now: Enabled, the striker a partner and the runner the player.
    public static bool Live => false;
    /// This ball is the partner's (Live as it was at InGame_DeliverBall; cleared at ResetToReady).
    public static bool ThisBall => false;
    /// The partner's dice for this match (seeded from the plan; new each scene).
    public static System.Random Rng { get; private set; } = new System.Random(0);
}

/// <summary>
/// The batter at the striker's end as the rules need him: the player himself, or (watching) the
/// partner - his head for the keeper and the over-the-head wide, where he takes guard for the wide
/// lines. Every reader passes its own answer for the player as the fallback. CONTRACT STUB.
/// </summary>
public static class Striker
{
    public static bool IsPartner => false;
    public static UnityEngine.Vector3 HeadPosition(UnityEngine.Vector3 fallback) => fallback;
    public static float HeadTopY(float fallback) => fallback;
    public static bool TryStanceZ(out float z) { z = 0f; return false; }
}
