using System.Globalization;
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
    /// The bat's colours, one per BatSubmesh (null: not a bat, or the bat as it is). An entry equal
    /// to CosmeticCatalog.Keep leaves that part as the bat model has it.
    public Color[] bat;
    public Color colour = Color.white;
    /// Use his side's kit colour instead of `colour`.
    public bool sideColour;

    /// The slot's Start item: wearing it changes nothing.
    public bool IsDefault => rule == eUnlock.Start;
}

/// The bat model's submeshes, in its material order.
public static class BatSubmesh
{
    public const int Grip = 0, Blade = 1, Face = 2, ToeGuard = 3, ToeSticker = 4, Label = 5, Count = 6;
}

/// <summary>
/// The locker's items (phase 7): seven bats, six gloves, six helmet trims, every one a tint of the
/// game's own models (no new assets) and earned from the record book. Each slot's Start item is the
/// look as it has always been. Names are the game's own; nothing here changes how he bats.
/// </summary>
public static class CosmeticCatalog
{
    /// A bat part left as the model has it (the willow, mostly).
    public static readonly Color Keep = new Color(0f, 0f, 0f, 0f);

    private static readonly Color Navy = new Color(0.12f, 0.16f, 0.33f), Gold = new Color(0.85f, 0.67f, 0.22f);
    private static readonly Color White = new Color(0.93f, 0.94f, 0.96f), Black = new Color(0.09f, 0.09f, 0.10f);
    private static readonly Color Ember = new Color(0.86f, 0.34f, 0.10f), Maroon = new Color(0.45f, 0.11f, 0.16f);
    private static readonly Color Teal = new Color(0.08f, 0.55f, 0.52f), Stained = new Color(0.55f, 0.36f, 0.20f);

    public static readonly CosmeticDef[] All =
    {
        Bat("bat.classic", "Classic", eUnlock.Start, 0, null),
        Bat("bat.ember", "Ember", eUnlock.Matches, 1, Parts(Ember, Keep, Keep, Black, Ember, Ember)),
        Bat("bat.state", "State navy", eUnlock.Rung, (int)eRung.State, Parts(Navy, Keep, Keep, Navy, White, Navy)),
        Bat("bat.pro", "Pro gold", eUnlock.Rung, (int)eRung.ProLeague, Parts(Black, Keep, Keep, Gold, Gold, Gold)),
        Bat("bat.cap", "Cap white", eUnlock.Caps, 1, Parts(White, Keep, Keep, White, Navy, Navy)),
        Bat("bat.ton", "Century", eUnlock.Hundreds, 1, Parts(Maroon, Stained, Stained, Black, Gold, Gold)),
        Bat("bat.coach", "Coach's pick", eUnlock.Goals, 3, Parts(Teal, Keep, Keep, White, Teal, Teal)),

        Item("gloves.standard", "Standard", eCosmeticSlot.Gloves, eUnlock.Start, 0, Color.white),
        Item("gloves.white", "White", eCosmeticSlot.Gloves, eUnlock.Fifties, 1, White),
        Side("gloves.side", eCosmeticSlot.Gloves, eUnlock.Goals, 1),
        Item("gloves.black", "Black", eCosmeticSlot.Gloves, eUnlock.Matches, 10, Black),
        Item("gloves.lime", "Lime", eCosmeticSlot.Gloves, eUnlock.DrillScore, 80, new Color(0.58f, 0.86f, 0.18f)),
        Item("gloves.gold", "Gold", eCosmeticSlot.Gloves, eUnlock.Runs, 1000, Gold),

        Item("trim.silver", "Silver", eCosmeticSlot.Trim, eUnlock.Start, 0, Color.white),
        Item("trim.white", "White", eCosmeticSlot.Trim, eUnlock.Seasons, 1, White),
        Side("trim.side", eCosmeticSlot.Trim, eUnlock.Rung, (int)eRung.State),
        Item("trim.red", "Red", eCosmeticSlot.Trim, eUnlock.Sixes, 25, new Color(0.80f, 0.12f, 0.12f)),
        Item("trim.gold", "Gold", eCosmeticSlot.Trim, eUnlock.Rung, (int)eRung.International, Gold),
        Item("trim.sky", "Sky", eCosmeticSlot.Trim, eUnlock.Goals, 6, new Color(0.35f, 0.70f, 0.93f)),
    };

    /// The item with `id`, or null.
    public static CosmeticDef For(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (CosmeticDef d in All)
            if (d.id == id)
                return d;
        return null;
    }

    /// What `s` wears when nothing is chosen: its Start item.
    public static CosmeticDef Default(eCosmeticSlot s)
    {
        foreach (CosmeticDef d in All)
            if (d.slot == s && d.IsDefault)
                return d;
        return null;
    }

    /// "Reach the Pro League", "Score your first fifty" ...
    public static string HowToUnlock(CosmeticDef d)
    {
        if (d == null)
            return "";
        int n = d.need;
        return d.rule switch
        {
            eUnlock.Start => "Yours from the start",
            eUnlock.Matches => n <= 1 ? "Play your first career match" : $"Play {n} career matches",
            eUnlock.Rung => RungLine((eRung)n),
            eUnlock.Caps => n <= 1 ? "Win your first cap" : $"Win {n} caps",
            eUnlock.Fifties => n <= 1 ? "Score your first fifty" : $"Score {n} fifties",
            eUnlock.Hundreds => n <= 1 ? "Score a hundred" : $"Score {n} hundreds",
            eUnlock.Runs => $"Score {n.ToString("N0", CultureInfo.InvariantCulture)} career runs",
            eUnlock.Sixes => $"Hit {n} sixes in your career",
            eUnlock.Seasons => n <= 1 ? "Finish a season" : $"Finish {n} seasons",
            eUnlock.Goals => n <= 1 ? "Reach a coach's goal" : $"Reach {n} coach's goals",
            _ => $"Score {n} or more in a drill",
        };
    }

    /// "Pro gold bat", "White gloves", "Gold helmet trim".
    public static string FullName(CosmeticDef d) => d == null ? "" : d.slot switch
    {
        eCosmeticSlot.Bat => d.name + " bat",
        eCosmeticSlot.Gloves => d.name + " gloves",
        _ => d.name + " helmet trim",
    };

    private static string RungLine(eRung r) => r switch
    {
        eRung.Club => "Play Club cricket",
        eRung.State => "Reach State cricket",
        eRung.ProLeague => "Reach the Pro League",
        _ => "Play International cricket",
    };

    private static Color[] Parts(Color grip, Color blade, Color face, Color toeGuard, Color toeSticker, Color label) =>
        new[] { grip, blade, face, toeGuard, toeSticker, label };

    private static CosmeticDef Bat(string id, string name, eUnlock rule, int need, Color[] parts) =>
        new CosmeticDef { id = id, name = name, slot = eCosmeticSlot.Bat, rule = rule, need = need, bat = parts };

    private static CosmeticDef Item(string id, string name, eCosmeticSlot slot, eUnlock rule, int need, Color c) =>
        new CosmeticDef { id = id, name = name, slot = slot, rule = rule, need = need, colour = c };

    private static CosmeticDef Side(string id, eCosmeticSlot slot, eUnlock rule, int need) =>
        new CosmeticDef { id = id, name = "Side colour", slot = slot, rule = rule, need = need, sideColour = true };
}
