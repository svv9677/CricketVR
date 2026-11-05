/// What a visit to the nets is for: free practice (nothing measured, the game as it always was),
/// the career player's 30-ball trial, or measured practice for the career player.
public enum eNetsMode { Free, Trial, Practice }

/// <summary>
/// The career's reason for being in the nets, set from the career hub before the Nets scene loads
/// and read there by NetsTrial (statics survive the scene load, like MatchSetup's). Free practice
/// from the main menu leaves Mode at Free and nothing career-related runs.
/// </summary>
public static class NetsSession
{
    public const string NetsScene = "Nets", SplashScene = "Splash";
    /// Deliveries in the trial (five overs: one of each kind of bowler, then a mix).
    public const int TrialBalls = 30;

    public static eNetsMode Mode { get; private set; } = eNetsMode.Free;

    /// A career session is running in the nets (a slot is active and the mode is not Free).
    public static bool Career => Mode != eNetsMode.Free && CareerSaves.Current != null;

    /// Counting deliveries faced so far in this trial (kept across a restart of the scene).
    public static int TrialIndex;

    /// Coming back to Splash, open the career hub (set when a career session ends, however it ends).
    public static bool ReturnToHub;
    /// The trial just finished (the hub shows its result once), or null.
    public static RatingSet TrialResult;

    public static void BeginTrial()
    {
        Mode = eNetsMode.Trial;
        TrialIndex = 0;
        TrialResult = null;
        SceneFader.Load(NetsScene);
    }

    public static void BeginPractice()
    {
        Mode = eNetsMode.Practice;
        SceneFader.Load(NetsScene);
    }

    /// The session is over (finished or left): back to free nets for next time, hub on return.
    public static void End(RatingSet trialResult = null)
    {
        if (Mode != eNetsMode.Free)
            ReturnToHub = true;
        if (trialResult != null)
            TrialResult = trialResult;
        Mode = eNetsMode.Free;
        TrialIndex = 0;
    }

    /// Tests.
    public static void ResetForTests()
    {
        Mode = eNetsMode.Free;
        TrialIndex = 0;
        ReturnToHub = false;
        TrialResult = null;
    }
}
