using System;
using UnityEngine;

/// <summary>
/// The settings the player chooses, and the only thing the settings UI edits. Backed by
/// PlayerPrefs under the existing Constants.PP_* keys, so settings saved by older builds are
/// picked up unchanged.
///
/// Main used to read these straight from PlayerPrefs in Start and own them from then on, which
/// meant the settings panel could only work in a scene that had a Main. Now Main reads this on
/// Start and re-applies whenever Changed fires, so the same settings screen works in Splash -
/// where there is no Main at all - as in a match.
///
/// Every default here is the one Main already used, so a fresh install behaves exactly as
/// before: difficulty Easy, batting right-handed, fielder speed 1.5, bat power realistic.
///
/// Plain static C# on purpose: no MonoBehaviour, no scene, so it is unit-testable.
/// </summary>
public static class GameSettings
{
    public const int MinOvers = 1;
    public const int MaxOvers = 20;
    public const int DefaultOvers = 5;

    /// Main's pre-existing defaults - see Main.Start's "Read Settings" block.
    private const float DefaultFielderSpeed = 1.5f;

    /// Raised after any value actually changes. Never raised for a set to the same value:
    /// Main re-applies everything on this, and doing so mid-over is visible to the player.
    public static event Action Changed;

    private static bool loaded;
    private static int overs;
    private static float batPower;
    private static float fielderSpeed;
    private static eDifficulty difficulty;
    private static eBattingStyle battingStyle;
    private static eSwingType bowlerType;
    private static bool overlayVisible;

    // ---- Values -------------------------------------------------------------------------------

    public static int Overs
    {
        get { Load(); return overs; }
        set
        {
            Load();
            int clamped = Mathf.Clamp(value, MinOvers, MaxOvers);
            if (clamped == overs) return;
            overs = clamped;
            PlayerPrefs.SetInt(Constants.PP_Overs, clamped);
            Save();
        }
    }

    public static float BatPower
    {
        get { Load(); return batPower; }
        set
        {
            Load();
            if (Mathf.Approximately(value, batPower)) return;
            batPower = value;
            PlayerPrefs.SetFloat(Constants.PP_BatPower, value);
            Save();
        }
    }

    public static float FielderSpeed
    {
        get { Load(); return fielderSpeed; }
        set
        {
            Load();
            if (Mathf.Approximately(value, fielderSpeed)) return;
            fielderSpeed = value;
            PlayerPrefs.SetFloat(Constants.PP_FielderSpeed, value);
            Save();
        }
    }

    public static eDifficulty Difficulty
    {
        get { Load(); return difficulty; }
        set
        {
            Load();
            if (value == difficulty) return;
            difficulty = value;
            PlayerPrefs.SetInt(Constants.PP_Difficulty, (int)value);
            Save();
        }
    }

    public static eBattingStyle BattingStyle
    {
        get { Load(); return battingStyle; }
        set
        {
            Load();
            if (value == battingStyle) return;
            battingStyle = value;
            PlayerPrefs.SetInt(Constants.PP_BattingStyle, (int)value);
            Save();
        }
    }

    public static eSwingType BowlerType
    {
        get { Load(); return bowlerType; }
        set
        {
            Load();
            if (value == bowlerType) return;
            bowlerType = value;
            PlayerPrefs.SetInt(Constants.PP_BowlerType, (int)value);
            Save();
        }
    }

    public static bool OverlayVisible
    {
        get { Load(); return overlayVisible; }
        set
        {
            Load();
            if (value == overlayVisible) return;
            overlayVisible = value;
            PlayerPrefs.SetInt(Constants.PP_Overlay, value ? 1 : 0);
            Save();
        }
    }

    // ---- Persistence --------------------------------------------------------------------------

    private static void Load()
    {
        if (loaded) return;
        loaded = true;   // set first: the reads below must not recurse
        overs = Mathf.Clamp(PlayerPrefs.GetInt(Constants.PP_Overs, DefaultOvers), MinOvers, MaxOvers);
        batPower = PlayerPrefs.GetFloat(Constants.PP_BatPower, Constants.BatPowerRealistic);
        fielderSpeed = PlayerPrefs.GetFloat(Constants.PP_FielderSpeed, DefaultFielderSpeed);
        difficulty = (eDifficulty)PlayerPrefs.GetInt(Constants.PP_Difficulty, (int)eDifficulty.Easy);
        battingStyle = (eBattingStyle)PlayerPrefs.GetInt(Constants.PP_BattingStyle, (int)eBattingStyle.RightHanded);
        bowlerType = (eSwingType)PlayerPrefs.GetInt(Constants.PP_BowlerType, (int)eSwingType.Pace);
        overlayVisible = PlayerPrefs.GetInt(Constants.PP_Overlay, 0) == 1;
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// Drop the cache and re-read PlayerPrefs. For tests, and for anything that changes prefs
    /// behind this class's back.
    public static void ReloadForTests()
    {
        loaded = false;
        Load();
    }
}
