using UnityEngine;

/// <summary>
/// The cricket nations a career player can come from (their national side is the one that can pick
/// them at the top of the ladder), and the appearance choices. Names only - no flags or crests (the
/// TMP default font is Latin-1 only, and national boards' marks are not ours to use).
/// </summary>
public static class Nations
{
    public struct Nation
    {
        public string code, name;
        public Nation(string code, string name) { this.code = code; this.name = name; }
    }

    public static readonly Nation[] All =
    {
        new Nation("AFG", "Afghanistan"), new Nation("AUS", "Australia"), new Nation("BAN", "Bangladesh"),
        new Nation("CAN", "Canada"), new Nation("ENG", "England"), new Nation("IND", "India"),
        new Nation("IRE", "Ireland"), new Nation("NEP", "Nepal"), new Nation("NED", "Netherlands"),
        new Nation("NZ", "New Zealand"), new Nation("PAK", "Pakistan"), new Nation("SCO", "Scotland"),
        new Nation("RSA", "South Africa"), new Nation("SL", "Sri Lanka"), new Nation("UAE", "UAE"),
        new Nation("USA", "USA"), new Nation("WI", "West Indies"), new Nation("ZIM", "Zimbabwe"),
    };

    public static int IndexOf(string code)
    {
        for (int i = 0; i < All.Length; i++)
            if (All[i].code == code) return i;
        return 0;
    }

    public static string Name(string code) => All[IndexOf(code)].name;
}

/// The appearance choices (CareerAppearance indexes these).
public static class Appearance
{
    public static readonly string[] SkinNames = { "Very light", "Light", "Medium", "Tan", "Brown", "Dark" };
    public static readonly Color[] SkinTones =
    {
        new Color32(0xF3, 0xD9, 0xC4, 0xFF), new Color32(0xE6, 0xBC, 0x98, 0xFF), new Color32(0xC9, 0x93, 0x6B, 0xFF),
        new Color32(0xA8, 0x72, 0x4C, 0xFF), new Color32(0x7C, 0x50, 0x33, 0xFF), new Color32(0x4E, 0x31, 0x22, 0xFF),
    };

    public static readonly string[] KitNames = { "Royal blue", "Navy", "Green", "Maroon", "Orange", "Yellow", "Black", "Sky blue" };
    public static readonly Color[] KitColours =
    {
        new Color32(0x1D, 0x4E, 0xD8, 0xFF), new Color32(0x1E, 0x29, 0x4B, 0xFF), new Color32(0x16, 0x7C, 0x3A, 0xFF),
        new Color32(0x7F, 0x1D, 0x2D, 0xFF), new Color32(0xEA, 0x6A, 0x10, 0xFF), new Color32(0xEA, 0xB3, 0x08, 0xFF),
        new Color32(0x1A, 0x1A, 0x1E, 0xFF), new Color32(0x38, 0xA8, 0xE8, 0xFF),
    };

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
