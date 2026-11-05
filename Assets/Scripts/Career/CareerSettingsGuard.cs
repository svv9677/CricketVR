using UnityEngine;

/// <summary>
/// The career plays with its own batting hand and bowling level, but both are the player's saved
/// preferences (GameSettings.BattingStyle and MatchSetup.Bowling write PlayerPrefs at once). So the
/// player's own values are written down before the career's go in, and put back when the session
/// ends - or, if it never ended (the app quit from the Quest menu, the battery died, a crash: OnDestroy
/// is not promised on Android), at the next launch, before any scene loads. Quick Match and free nets
/// therefore always come back as the player left them.
/// </summary>
public static class CareerSettingsGuard
{
    private const string KeyPending = "career_restore_pending", KeyHand = "career_restore_hand",
                         KeyBowling = "career_restore_bowling";

    /// The career's settings are in force (and the player's own are waiting to go back).
    public static bool Pending => PlayerPrefs.GetInt(KeyPending, 0) == 1;

    /// Put the career's hand and level in, keeping the player's own. Applying again before a restore
    /// (the scene restarted mid-session) keeps the first originals, not the career's.
    public static void Apply(eBattingStyle hand, eBowlingLevel level)
    {
        if (!Pending)
        {
            PlayerPrefs.SetInt(KeyHand, (int)GameSettings.BattingStyle);
            PlayerPrefs.SetInt(KeyBowling, (int)MatchSetup.Bowling);
            PlayerPrefs.SetInt(KeyPending, 1);
            PlayerPrefs.Save();
        }
        MatchSetup.Bowling = level;
        GameSettings.BattingStyle = hand;
    }

    /// The player's own back, once. The hand last: changing it re-applies the settings into Main,
    /// which may be mid-teardown on a scene change.
    public static void Restore()
    {
        if (!Pending)
            return;
        var hand = (eBattingStyle)PlayerPrefs.GetInt(KeyHand, (int)eBattingStyle.RightHanded);
        var level = (eBowlingLevel)PlayerPrefs.GetInt(KeyBowling, (int)eBowlingLevel.Medium);
        PlayerPrefs.SetInt(KeyPending, 0);
        PlayerPrefs.Save();
        MatchSetup.Bowling = level;
        try
        {
            GameSettings.BattingStyle = hand;
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
