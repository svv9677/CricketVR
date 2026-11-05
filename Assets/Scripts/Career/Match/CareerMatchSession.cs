/// <summary>
/// A career match about to be played or being played: set from the career hub before the match
/// scene loads (the plan is static, so it survives the load, like NetsSession), read there by
/// CareerMatch, and finished by it with the result - which goes into the career
/// (CareerProgress.RecordMatch) and is saved before the scene goes home.
/// CONTRACT STUB - the implementation replaces the bodies, keeping these signatures.
/// </summary>
public static class CareerMatchSession
{
    public const string MatchScene = "CricketVR";

    /// A career match is set up or in progress.
    public static bool Active => Plan != null;
    public static MatchPlan Plan { get; private set; }

    /// Coming back to Splash, open the career hub; the match just finished (shown once).
    public static bool ReturnToHub;
    public static CareerMatchResult LastResult;
    public static MatchPlan LastPlan;

    /// Apply the plan's settings for the match (CareerSettingsGuard, so the player's own come back)
    /// and load the match scene.
    public static void Begin(MatchPlan plan) { Plan = plan; }

    /// The match is over (or abandoned): record it in the career, save, put the player's settings
    /// back, and ask for the hub on the way home. Safe to call once; later calls do nothing.
    public static void Finish(CareerMatchResult result) { Plan = null; }

    /// Tests.
    public static void ResetForTests() { Plan = null; ReturnToHub = false; LastResult = null; LastPlan = null; }
}
