using UnityEngine;

/// What a cosmetic dresses.
public enum eCosmeticSlot { Bat, Gloves, Trim }

/// What unlocks a cosmetic (a fact already in the career save).
public enum eUnlock { Start, Matches, Rung, Caps, Fifties, Hundreds, Runs, Sixes, Seasons, Goals, DrillScore }

/// <summary>One locker item: its id and name, what it dresses, what unlocks it, and its colours (the
/// bat's per submesh, or one colour). Looks only - nothing here changes how he bats.</summary>
public sealed class CosmeticDef
{
    public string id = "", name = "";
    public eCosmeticSlot slot;
    public eUnlock rule;
    public int need;
    /// The bat's colours, one per BatSubmesh (null: not a bat).
    public Color[] bat;
    public Color colour = Color.white;
    /// Use his side's kit colour instead of `colour`.
    public bool sideColour;
}

/// The bat model's submeshes, in its material order.
public static class BatSubmesh
{
    public const int Grip = 0, Blade = 1, Face = 2, ToeGuard = 3, ToeSticker = 4, Label = 5, Count = 6;
}

/// <summary>The locker's items (phase 7). Empty for now.</summary>
public static class CosmeticCatalog
{
    public static readonly CosmeticDef[] All = new CosmeticDef[0];

    /// The item with `id`, or null.
    public static CosmeticDef For(string id) => null;

    /// What `s` wears when nothing is chosen.
    public static CosmeticDef Default(eCosmeticSlot s) => null;

    /// "Reach the Pro League", "Score 3 fifties" ...
    public static string HowToUnlock(CosmeticDef d) => "";
}
