using UnityEngine;

public enum eMatchFormat { Five, Ten, Twenty, Fifty, Test }

/// <summary>
/// The quick-play match length (2026-11-06: "for overs, i should only be able to select these
/// options: five overs, 10 overs, 20 overs, 50 overs, or test match"). The four limited-overs
/// lengths are GameSettings.Overs as before (HUD reads it); a Test is two innings a side with no
/// over limit (MatchFlowTest), played as innings of TestInningsOvers - a number nothing reaches, so
/// every "overs left" sum in the game keeps working - and FormatRules knows a Test by it.
///
/// The choice is saved on its own; GameSettings.Overs is set to match whenever it changes (Sync),
/// only from the menus, so a career match's overs (CareerSettingsGuard) are never touched here.
/// </summary>
public static class MatchFormat
{
    public const string PrefKey = "match_format";
    /// A Test innings, in overs: five days of 90, which no innings reaches.
    public const int TestInningsOvers = 450;
    /// A Test's overs in all (five days): past them, a draw.
    public const int TestMatchOvers = 450, OversPerDay = 90;

    public static readonly eMatchFormat[] Order = { eMatchFormat.Five, eMatchFormat.Ten, eMatchFormat.Twenty, eMatchFormat.Fifty, eMatchFormat.Test };

    public static eMatchFormat Current
    {
        get => PlayerPrefs.HasKey(PrefKey) ? (eMatchFormat)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, Order.Length - 1)
                                           : Nearest(GameSettings.Overs);
        set
        {
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            Sync();
        }
    }

    public static bool IsTest => Current == eMatchFormat.Test;

    /// GameSettings.Overs to the format's (a Test leaves it: its innings are set by MatchFlowTest).
    public static void Sync()
    {
        eMatchFormat f = Current;
        if (f != eMatchFormat.Test && GameSettings.Overs != OversOf(f))
            GameSettings.Overs = OversOf(f);
    }

    public static void Next() => Current = Order[Mathf.Min(Order.Length - 1, (int)Current + 1)];
    public static void Previous() => Current = Order[Mathf.Max(0, (int)Current - 1)];

    public static int OversOf(eMatchFormat f) => f switch
    {
        eMatchFormat.Five => 5,
        eMatchFormat.Ten => 10,
        eMatchFormat.Twenty => 20,
        eMatchFormat.Fifty => 50,
        _ => TestInningsOvers,
    };

    /// The listed length nearest a number of overs (a save from before the formats).
    public static eMatchFormat Nearest(int overs) =>
        overs <= 7 ? eMatchFormat.Five : overs <= 14 ? eMatchFormat.Ten : overs <= 35 ? eMatchFormat.Twenty : eMatchFormat.Fifty;

    /// "5 overs", "20 overs", "Test match".
    public static string Label(eMatchFormat f) => f == eMatchFormat.Test ? "Test match" : $"{OversOf(f)} overs";

    public static string Label() => Label(Current);
}
