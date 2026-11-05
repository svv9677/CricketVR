using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The human bat's part in watching the partner bat (phase 3): while the player stands at the
/// non-striker's end his bat must not touch the ball, and the partner's contact (PartnerContact)
/// reports its shot through the same ShotStruck event a real hit does, so the shot card, the ball's
/// look, the fielding captain and the ledger all hear of it as they would of the player's.
/// </summary>
public partial class Bat
{
    /// The player's bat makes no contact (he is at the non-striker's end; NonStrikerView sets it).
    /// Static, so it is put back to false on every scene load: a Quick Match after a career match
    /// must never start with a dead bat.
    public static bool ContactSuspended;

    /// The partner's contact raising the same event as a real hit.
    internal static void ReportShot(ShotInfo i) => ShotStruck?.Invoke(i);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPartnerStatics()
    {
        ContactSuspended = false;
        SceneManager.sceneLoaded -= OnSceneLoadedResetPartner;
        SceneManager.sceneLoaded += OnSceneLoadedResetPartner;
    }

    private static void OnSceneLoadedResetPartner(Scene scene, LoadSceneMode mode) => ContactSuspended = false;
}
