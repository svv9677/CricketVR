using UnityEngine;

/// <summary>
/// The ground the match in the scene is being played at, decided once per scene load by
/// StadiumConditions (VenueRules.Resolve: the career plan's venue, else the Quick Match setting)
/// and read by whatever sounds or looks different at a club ground (MatchSounds, the lights).
///
/// Stadium unless set: the stadium is the scene as built, so anything that runs before (or
/// without) StadiumConditions sees the ground it is actually in.
/// </summary>
public static class MatchVenue
{
    public static eVenue Current { get; private set; } = eVenue.Stadium;

    public static bool IsClub => Current == eVenue.ClubGround;

    /// Public, not internal: the EditMode tests (another assembly) set it too.
    public static void Set(eVenue v) => Current = v;

    /// Statics survive an editor play session with domain reload off: start each one in the stadium.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => Current = eVenue.Stadium;
}
