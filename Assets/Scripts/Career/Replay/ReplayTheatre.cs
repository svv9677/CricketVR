using System;
using UnityEngine.SceneManagement;

/// <summary>
/// The record replay theatre (phase 7): a saved record clip played back from the Records screen at
/// the ground it was hit at, in the match scene with nothing bowled (ReplayTheatreStage holds every
/// ball back and loops the clip). Begin reads the clip's header, puts the record's ground, light,
/// crowd, pitch and hand in for the scene (CareerSettingsGuard: the player's own come back on the way
/// home, CareerMatchSession.OnSceneLoaded) and loads it. Leaving is the pause menu's Main Menu, like
/// any match; back at Splash, ReturnToRecords asks MainMenuExtras for the Records screen again.
///
/// Never during a career match. The theatre plays no match: no history, no career, no ratings.
/// </summary>
public static class ReplayTheatre
{
    public static bool Active { get; private set; }

    public static eVenue Venue { get; private set; } = eVenue.Stadium;

    /// Back at Splash from the theatre: open Records (MainMenuExtras clears it once pushed).
    public static bool ReturnToRecords;

    /// The clip being shown: its header and its bytes (the stage decodes them off the main thread).
    public static ClipHeader Header { get; private set; }
    public static byte[] Clip { get; private set; }

    private static bool leftPending;

    /// Play `which`'s clip from `slot` (the active career's). False when it cannot: a career match is
    /// on, the theatre already is, or there is no readable clip for that record.
    public static bool Begin(int slot, eRecord which)
    {
        if (!Ready(slot, which, out ClipHeader h, out byte[] data))
            return false;
        Header = h;
        Clip = data;
        Venue = h.venue;
        Active = true;
        leftPending = false;
        CareerSettingsGuard.ApplyMatch(h.hand, h.bowling, Math.Max(1, h.overs), eMatchType.SetTotal, h.crowd, h.pitch, h.time);
        SceneFader.Load(CareerMatchSession.MatchScene);
        return true;
    }

    /// The clip `Begin` would play, read and checked: the file's header must carry the record's stamp.
    public static bool Ready(int slot, eRecord which, out ClipHeader header, out byte[] data)
    {
        header = null;
        data = null;
        CareerProfile p = CareerSaves.Current;
        if (Active || CareerMatchSession.Active || p?.recordBook == null || slot < 0 || slot != CareerSaves.Active)
            return false;
        string stamp = p.recordBook.Get(which)?.clipStamp;
        if (!ReplayVault.SafeStamp(stamp) || !ReplayVault.Has(slot, stamp))
            return false;
        data = ReplayVault.Read(ReplayVault.PathFor(slot, stamp));
        header = ClipCodec.ReadHeader(data);
        if (header != null && header.stamp == stamp)
            return true;
        header = null;
        data = null;
        return false;
    }

    /// The caption's heading: "Longest six: 104 m".
    public static string Heading(ClipHeader h)
    {
        if (h == null)
            return "Replay";
        return h.record switch
        {
            eRecord.LongestSix => $"Longest six: {h.value:0} m",
            eRecord.HighScore => "Highest score",
            eRecord.FastestFifty => "Fastest fifty",
            eRecord.BestStrikeRate => "Best strike rate",
            _ => "Most sixes",
        };
    }

    /// The caption's body: where and when, and how to leave (no replay controls here).
    public static string Body(ClipHeader h)
    {
        string against = h == null || string.IsNullOrEmpty(h.opponent) ? "" : $"vs {h.opponent}";
        string when = h == null ? "" : Ladder.Name(h.rung) + (string.IsNullOrEmpty(h.date) ? "" : ", " + h.date);
        string line = against.Length > 0 && when.Length > 0 ? against + "\n" + when : against + when;
        return (line.Length > 0 ? line + "\n" : "") + "<size=80%><color=#98A2B3>Looping. Press B, then Main Menu, to leave.</color></size>";
    }

    /// The theatre's scene is going (ReplayTheatreStage). Records is wanted back at Splash, and is set
    /// now, as Splash's menu may read it before its sceneLoaded comes round; a reload of the match
    /// scene (a restart) carries on with the theatre instead.
    public static void Left()
    {
        if (!Active)
            return;
        ReturnToRecords = true;
        leftPending = true;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetForTests();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!leftPending)
            return;
        leftPending = false;
        if (scene.name == CareerMatchSession.MatchScene)
            ReturnToRecords = false;      // the match scene again: still in the theatre
        else
            End();
    }

    private static void End()
    {
        Active = false;
        Header = null;
        Clip = null;
        Venue = eVenue.Stadium;
    }

    /// Tests (and each run of the game).
    public static void ResetForTests()
    {
        End();
        ReturnToRecords = false;
        leftPending = false;
    }
}
