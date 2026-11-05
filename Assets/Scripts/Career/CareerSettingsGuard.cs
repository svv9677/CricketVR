using UnityEngine;

/// <summary>
/// The career plays with its own batting hand, bowling level and - for a career match - its own
/// overs, match type, crowd, pitch and light; but all of these are the player's saved preferences
/// (GameSettings and MatchSetup write PlayerPrefs at once, and the match scene reads them as it
/// loads). So the player's own values are written down before the career's go in, and put back
/// when the session ends - or, if it never ended (the app quit from the Quest menu, the battery
/// died, a crash: OnDestroy is not promised on Android), at the next launch, before any scene
/// loads. Quick Match and free nets therefore always come back as the player left them.
/// </summary>
public static class CareerSettingsGuard
{
    private const string KeyPending = "career_restore_pending", KeyHand = "career_restore_hand",
                         KeyBowling = "career_restore_bowling", KeyOvers = "career_restore_overs",
                         KeyType = "career_restore_type", KeyCrowd = "career_restore_crowd",
                         KeyPitch = "career_restore_pitch", KeyTime = "career_restore_time";

    /// The career's settings are in force (and the player's own are waiting to go back).
    public static bool Pending => PlayerPrefs.GetInt(KeyPending, 0) == 1;

    /// Put the career's hand and level in (the nets), keeping the player's own.
    public static void Apply(eBattingStyle hand, eBowlingLevel level)
    {
        Remember();
        MatchSetup.Bowling = level;
        GameSettings.BattingStyle = hand;
    }

    /// Everything a career match sets (before its scene loads, so every reader sees it).
    public static void ApplyMatch(eBattingStyle hand, eBowlingLevel level, int overs, eMatchType type,
                                  eCrowd crowd, ePitch pitch, eTimeOfDay time)
    {
        Remember();
        MatchSetup.Bowling = level;
        MatchSetup.Type = type;
        MatchSetup.Crowd = crowd;
        MatchSetup.Pitch = pitch;
        MatchSetup.TimeOfDay = time;
        GameSettings.Overs = overs;
        GameSettings.BattingStyle = hand;
    }

    /// The player's own, written down once: applying again before a restore (the scene restarted
    /// mid-session) keeps the first originals, not the career's.
    private static void Remember()
    {
        if (Pending)
            return;
        PlayerPrefs.SetInt(KeyHand, (int)GameSettings.BattingStyle);
        PlayerPrefs.SetInt(KeyBowling, (int)MatchSetup.Bowling);
        PlayerPrefs.SetInt(KeyOvers, GameSettings.Overs);
        PlayerPrefs.SetInt(KeyType, (int)MatchSetup.Type);
        PlayerPrefs.SetInt(KeyCrowd, (int)MatchSetup.Crowd);
        PlayerPrefs.SetInt(KeyPitch, (int)MatchSetup.Pitch);
        PlayerPrefs.SetInt(KeyTime, (int)MatchSetup.TimeOfDay);
        PlayerPrefs.SetInt(KeyPending, 1);
        PlayerPrefs.Save();
    }

    /// The player's own back, once. The hand last: changing it re-applies the settings into Main,
    /// which may be mid-teardown on a scene change.
    public static void Restore()
    {
        if (!Pending)
            return;
        PlayerPrefs.SetInt(KeyPending, 0);
        PlayerPrefs.Save();
        MatchSetup.Bowling = (eBowlingLevel)PlayerPrefs.GetInt(KeyBowling, (int)MatchSetup.Bowling);
        MatchSetup.Type = (eMatchType)PlayerPrefs.GetInt(KeyType, (int)MatchSetup.Type);
        MatchSetup.Crowd = (eCrowd)PlayerPrefs.GetInt(KeyCrowd, (int)MatchSetup.Crowd);
        MatchSetup.Pitch = (ePitch)PlayerPrefs.GetInt(KeyPitch, (int)MatchSetup.Pitch);
        MatchSetup.TimeOfDay = (eTimeOfDay)PlayerPrefs.GetInt(KeyTime, (int)MatchSetup.TimeOfDay);
        GameSettings.Overs = PlayerPrefs.GetInt(KeyOvers, GameSettings.Overs);
        try
        {
            GameSettings.BattingStyle = (eBattingStyle)PlayerPrefs.GetInt(KeyHand, (int)eBattingStyle.RightHanded);
        }
        catch (System.Exception e)
        {
            // Saved before Changed is raised, so the preference is back even if a listener failed.
            Debug.LogWarning($"[Career] restoring the batting hand: {e.Message}");
        }
    }

    /// A session that never ended: put the player's settings back before anything reads them.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreAfterAnUnfinishedSession() => Restore();
}
