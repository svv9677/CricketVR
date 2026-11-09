using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Quick play's matches, one slot per length - 5, 10, 20 and 50 overs and a Test (2026-11-09: "I
/// should be able to have a slot for each type of match ... It should show if there's a game, and
/// I should be able to continue that game, restart, or new game"). A slot keeps the match's setup
/// (the two steps of New Game: QuickPlaySetup) and, while one is being played, the match itself as
/// it stood after the last ball (MatchSnapshot, written by QuickPlayAutosave).
///
///   Continue   the setup back on, the saved match put back once the ground has loaded;
///   Restart    the same setup, from the first ball;
///   New Game   the two setup steps (MatchSetupScreen, CustomiseScreen), then Start, which makes
///              that the slot's setup and plays it from the first ball.
///
/// One JSON file per slot under persistentDataPath/quickplay, written through a .tmp file so a
/// write cut short never leaves half a file (as CareerSaves).
/// </summary>
public static class QuickPlaySlots
{
    public const string Folder = "quickplay";

    /// The saved match to put back when the match scene loads (Continue); taken by
    /// QuickPlayAutosave, so it is used once.
    public static MatchSnapshot PendingResume;

    private static string Directory => Path.Combine(Application.persistentDataPath, Folder);
    private static string PathFor(eMatchFormat f) => Path.Combine(Directory, f + ".json");

    public static QuickPlaySlot Load(eMatchFormat f)
    {
        try
        {
            string path = PathFor(f);
            if (File.Exists(path))
            {
                QuickPlaySlot s = JsonUtility.FromJson<QuickPlaySlot>(File.ReadAllText(path));
                if (s != null)
                {
                    s.format = (int)f;
                    if (s.inProgress && (s.match == null || s.match.version != MatchSnapshot.Version))
                        s.inProgress = false;     // from another version of the save: not continued
                    return s;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuickPlay] could not read the {MatchFormat.Label(f)} slot: {e.Message}");
        }
        return new QuickPlaySlot { format = (int)f };
    }

    public static void Save(QuickPlaySlot slot)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = PathFor((eMatchFormat)slot.format), tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(slot));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[QuickPlay] could not save the {MatchFormat.Label((eMatchFormat)slot.format)} slot: {e.Message}");
        }
    }

    // ---- The Quick Play screen's three choices ----------------------------------------------

    public static void Continue(eMatchFormat f)
    {
        QuickPlaySlot slot = Load(f);
        if (!slot.inProgress)
        {
            Restart(f);
            return;
        }
        Use(f, slot);
        PendingResume = slot.match;
        SceneFader.Load(MainMenuScreen.CricketScene);
    }

    public static void Restart(eMatchFormat f)
    {
        QuickPlaySlot slot = Load(f);
        Use(f, slot);
        slot.inProgress = false;
        slot.match = null;
        Save(slot);
        PendingResume = null;
        SceneFader.Load(MainMenuScreen.CricketScene);
    }

    /// New Game's first step: this length, its last setup (if it has one) to start from.
    public static void BeginNew(eMatchFormat f) => Use(f, Load(f));

    /// New Game's Start: the setup as the two steps left it is this slot's, from the first ball.
    public static void StartNew()
    {
        eMatchFormat f = MatchFormat.Current;
        QuickPlaySlot slot = Load(f);
        slot.setup = QuickPlaySetup.FromCurrent();
        slot.hasSetup = true;
        slot.inProgress = false;
        slot.match = null;
        Save(slot);
        PendingResume = null;
        SceneFader.Load(MainMenuScreen.CricketScene);
    }

    /// The length and its slot's setup on for the match (the setup screens and the match read them).
    private static void Use(eMatchFormat f, QuickPlaySlot slot)
    {
        MatchFormat.Current = f;
        if (slot.hasSetup)
            slot.setup.Apply();
    }

    // ---- What the screen says --------------------------------------------------------------

    /// "45/2 (6.3 ov) · need 76 off 22", "Your second innings · 245 & 45/2 v 380".
    public static string Progress(QuickPlaySlot slot)
    {
        if (!slot.inProgress || slot.match == null)
            return slot.lastResult;
        MatchSnapshot m = slot.match;
        if (m.test)
        {
            string yours = "", theirs = "";
            foreach (MatchSnapshot.InningsLine i in m.innings)
            {
                string t = i.declared ? $"{i.runs}/{i.wickets}d" : i.wickets >= 10 ? $"{i.runs}" : $"{i.runs}/{i.wickets}";
                if (i.yours) yours += (yours.Length > 0 ? " & " : "") + t;
                else theirs += (theirs.Length > 0 ? " & " : "") + t;
            }
            if (!m.betweenInnings)
                yours += (yours.Length > 0 ? " & " : "") + $"{m.runs}/{m.wickets}";
            int day = 1 + (m.matchBalls + m.overs * 6 + m.balls) / (MatchFormat.OversPerDay * 6);
            return $"Day {day}  ·  You {yours}" + (theirs.Length > 0 ? $"  ·  AI {theirs}" : "");
        }
        string score = m.ScoreText;
        if (m.target > 0)
        {
            int need = Mathf.Max(0, m.target - m.runs), left = Mathf.Max(0, m.totalOvers * 6 - m.overs * 6 - m.balls);
            return $"{score}  ·  need {need} off {left}";
        }
        return $"{m.runs}/{m.wickets}  ·  {m.overs}.{m.balls} of {m.totalOvers} overs";
    }
}

/// A Quick Play slot (QuickPlaySlots): its setup, and the match while one is on.
[Serializable]
public class QuickPlaySlot
{
    public int format;
    public bool hasSetup;
    public QuickPlaySetup setup = new QuickPlaySetup();
    public bool inProgress;
    public MatchSnapshot match;
    /// The last finished match's result line, for the screen once it is over.
    public string lastResult = "";
}

/// New Game's two steps, kept with the slot: who bats first, the bowling, the bat shield and the
/// batting hand; then the ground, the crowd, the pitch and the light.
[Serializable]
public class QuickPlaySetup
{
    public int bowling = (int)eBowlingLevel.Medium, type = (int)eMatchType.SetTotal;
    public int difficulty = (int)eDifficulty.Medium, hand = (int)eBattingStyle.RightHanded;
    public int venue = (int)eVenue.Stadium, crowd = (int)eCrowd.Packed, pitch = (int)ePitch.Flat, time = (int)eTimeOfDay.Afternoon;

    public static QuickPlaySetup FromCurrent() => new QuickPlaySetup
    {
        bowling = (int)MatchSetup.Bowling, type = (int)MatchSetup.Type,
        difficulty = (int)GameSettings.Difficulty, hand = (int)GameSettings.BattingStyle,
        venue = (int)MatchSetup.Venue, crowd = (int)MatchSetup.Crowd, pitch = (int)MatchSetup.Pitch, time = (int)MatchSetup.TimeOfDay,
    };

    public void Apply()
    {
        MatchSetup.Bowling = (eBowlingLevel)bowling;
        MatchSetup.Type = (eMatchType)type;
        if (difficulty != (int)eDifficulty.None) GameSettings.Difficulty = (eDifficulty)difficulty;
        if (hand != (int)eBattingStyle.None) GameSettings.BattingStyle = (eBattingStyle)hand;
        MatchSetup.Venue = (eVenue)venue;
        MatchSetup.Crowd = (eCrowd)crowd;
        MatchSetup.Pitch = (ePitch)pitch;
        MatchSetup.TimeOfDay = (eTimeOfDay)time;
    }

    /// "Medium bowling · Flat pitch · Stadium · you bat first".
    public string Summary => $"{MatchSetup.Name((eBowlingLevel)bowling)} bowling  ·  {MatchSetup.Name((ePitch)pitch)} pitch  ·  " +
                             $"{MatchSetup.Name((eVenue)venue)}  ·  {((eMatchType)type == eMatchType.Chase ? "you chase" : "you bat first")}";
}
