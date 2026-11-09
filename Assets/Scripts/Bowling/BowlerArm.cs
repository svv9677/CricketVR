using UnityEngine;

/// <summary>
/// Which arm a bowler bowls with. A left-armer is the right-armer's world mirror image across the
/// pitch (AnimatedBowlerArm: the humanoid's mirrored action, his mark on the other side, the ball in
/// his left hand). The line, length and swing of a pace ball are still the delivery's, judged
/// against the batter - an outswinger leaves a right-hander whoever bowls it - but a spinner's turn
/// and drift are his fingers': a left-arm finger spinner's stock ball turns away from the
/// right-hander (left-arm orthodox), a left-arm wrist spinner's into him (EffectSign).
///
/// As many left-armers as right-armers, pace and spin alike (2026-11-06: "just as many lefty
/// bowlers as there are righty bowlers... it should also be there for spin"). Which arm bowls is
/// the player's choice (Settings > Bowling > Arm: Both, Right, Left) in quick play and the free
/// nets. With Both, a match bowler keeps his arm (every other bowler in the side, IsLeftArm), and
/// in the nets each change of bowler swaps it, so Change bowler gives a
/// left-armer every other time. A career is always Both: its sides are its own.
/// </summary>
public static class BowlerArm
{
    public enum eSetting { Both, Right, Left }

    public const string PrefKey = "bowler_arm";

    /// The player's choice, saved.
    public static eSetting Setting
    {
        get => (eSetting)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)eSetting.Both), 0, 2);
        set
        {
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// Every ball from this arm while set (tests, and checking a left-armer in the editor).
    public static bool? Force;

    /// A match bowler's arm by his place in the attack: every other one left-arm, so a side has as
    /// many left-armers as right-armers (by name alone, 7 of the quick match's 8 came out left).
    /// Which half is the side's own (its first bowler's name), so sides differ.
    public static bool IsLeftArm(int index, string firstBowler) =>
        index >= 0 && ((index + (int)(Hash(firstBowler ?? "") & 1u)) & 1) == 1;

    /// The batter's off-side sign as a delivery's turn and drift should see it (BowlingParams.Effects):
    /// a left-arm spinner's are his fingers' mirror image; pace is judged against the batter alone.
    public static float EffectSign(eSwingType family, bool leftArm) =>
        leftArm && (family == eSwingType.OffSpin || family == eSwingType.LegSpin) ? -1f : 1f;

    /// "Left-arm fast", "Left-arm orthodox", "Left-arm wrist spin"; a right-armer's plain kind.
    public static string KindName(eBowlerKind k, bool leftArm)
    {
        if (!leftArm)
            return BowlerRepertoire.KindName(k);
        switch (k)
        {
            case eBowlerKind.Fast: return "Left-arm fast";
            case eBowlerKind.Medium: return "Left-arm medium";
            case eBowlerKind.OffSpin: return "Left-arm orthodox";
            case eBowlerKind.LegSpin: return "Left-arm wrist spin";
            default: return "Left-arm mystery";
        }
    }

    /// This ball's arm: the setting, and with Both the bowler's own in a match (`matchLeft`,
    /// IsLeftArm) or the spell's in the nets (`spellLeft`, swapped each change).
    public static bool Decide(eSetting setting, bool nets, bool spellLeft, bool matchLeft)
    {
        switch (setting)
        {
            case eSetting.Right: return false;
            case eSetting.Left: return true;
            default: return nets ? spellLeft : matchLeft;
        }
    }

    /// FNV-1a: stable on every platform (string.GetHashCode is not).
    public static uint Hash(string s)
    {
        uint h = 2166136261u;
        foreach (char c in s)
        {
            h ^= c;
            h *= 16777619u;
        }
        // Murmur3's finaliser: FNV's low bits barely mix, so "% 3" alone put every name in one bin.
        h ^= h >> 16;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        h *= 0xc2b2ae35u;
        h ^= h >> 16;
        return h;
    }

    /// The left-armer's mark: the right-armer's mirrored across the middle of the pitch.
    public static Vector3 Mirror(Vector3 p) => new Vector3(p.x, p.y, -p.z);
}
