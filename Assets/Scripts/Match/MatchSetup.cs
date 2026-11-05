using UnityEngine;

public enum eBowlingLevel { Easy, Medium, Hard, Pro }
public enum eMatchType { SetTotal, Chase }
public enum eCrowd { Packed, Half, Empty }
public enum ePitch { Flat, Green, Dusty }

/// <summary>
/// The match the player has set up in the Splash menu (Match Setup and Customise), kept in
/// PlayerPrefs so it is there next session. The overs and the bat shield (bat width,
/// eDifficulty) live in GameSettings already and are read from there, not copied.
///
///   bowling   how good the attack is: pace, movement, loose balls, how cleverly the captain
///             plans (BowlingLevels);
///   type      set a total in the overs, or chase a target set from the overs and the bowling;
///   stadium   the crowd (StadiumConditions); the light is always the scene's own;
///   pitch     flat, green (bounce and seam for the quicks) or dusty (low, slow, turning).
/// </summary>
public static class MatchSetup
{
    private const string KeyBowling = "match_bowling", KeyType = "match_type", KeyCrowd = "match_crowd", KeyPitch = "match_pitch",
                         KeyTime = "match_time";

    public static eBowlingLevel Bowling
    {
        get => (eBowlingLevel)PlayerPrefs.GetInt(KeyBowling, (int)eBowlingLevel.Medium);
        set { PlayerPrefs.SetInt(KeyBowling, (int)value); PlayerPrefs.Save(); }
    }

    public static eMatchType Type
    {
        get => (eMatchType)PlayerPrefs.GetInt(KeyType, (int)eMatchType.SetTotal);
        set { PlayerPrefs.SetInt(KeyType, (int)value); PlayerPrefs.Save(); }
    }

    public static eCrowd Crowd
    {
        get => (eCrowd)PlayerPrefs.GetInt(KeyCrowd, (int)eCrowd.Packed);
        set { PlayerPrefs.SetInt(KeyCrowd, (int)value); PlayerPrefs.Save(); }
    }

    /// The light the match is played in (StadiumLighting). Afternoon is the ground as built.
    public static eTimeOfDay TimeOfDay
    {
        get => (eTimeOfDay)PlayerPrefs.GetInt(KeyTime, (int)eTimeOfDay.Afternoon);
        set { PlayerPrefs.SetInt(KeyTime, (int)value); PlayerPrefs.Save(); }
    }

    public static ePitch Pitch
    {
        get => (ePitch)PlayerPrefs.GetInt(KeyPitch, (int)ePitch.Flat);
        set { PlayerPrefs.SetInt(KeyPitch, (int)value); PlayerPrefs.Save(); }
    }

    public static string Name(eBowlingLevel l) => l == eBowlingLevel.Easy ? "Easy" : l == eBowlingLevel.Medium ? "Medium"
                                               : l == eBowlingLevel.Hard ? "Hard" : "Pro";
    public static string Name(eCrowd c) => c == eCrowd.Packed ? "Packed" : c == eCrowd.Half ? "Half full" : "Empty";
    public static string Name(ePitch p) => p == ePitch.Flat ? "Flat" : p == ePitch.Green ? "Green" : "Dusty";

    public static string Hint(eBowlingLevel l)
    {
        switch (l)
        {
            case eBowlingLevel.Easy: return "Club bowlers: slower, little movement, plenty of loose balls.";
            case eBowlingLevel.Medium: return "County standard: decent pace and some plans.";
            case eBowlingLevel.Hard: return "International pace and movement; the captain plans for you.";
            default: return "The best in the world: quicker, more movement, very few bad balls.";
        }
    }

    public static string Hint(ePitch p)
    {
        switch (p)
        {
            case ePitch.Green: return "Grass on it: more bounce, the seamers move it.";
            case ePitch.Dusty: return "Dry and worn: keeps low, slows up, spinners turn it.";
            default: return "A true, hard surface: good for batting.";
        }
    }
}
