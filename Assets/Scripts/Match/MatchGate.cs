using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the next ball back while something else has the floor between balls - in a career match,
/// the partners' simulated balls and the scoreboard catch-up card that shows them. While Blocking
/// is set, Main.StartNextBall does nothing (A and the panel's Next Ball alike) and the between-balls
/// panel (NextBallMenu) stays hidden.
///
/// Nothing in a normal match sets it, so it is inert there. It goes back to false on every scene
/// load, so a match left with it set cannot carry it into the next one; set it from Start or later,
/// not Awake (Unity raises sceneLoaded after the scene's Awake and OnEnable calls).
/// </summary>
public static class MatchGate
{
    /// The next ball may not be started.
    public static bool Blocking;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Reset();
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Reset();

    /// Let balls be bowled again (scene loads, and tests).
    public static void Reset() => Blocking = false;
}
