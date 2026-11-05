using System.Collections.Generic;

/// <summary>
/// The career's locker (phase 7): which items he has earned, granting new ones, and the ones he has
/// on. Cosmetic only - nothing here changes how he bats.
/// </summary>
public static class Cosmetics
{
    /// Has the career earned `d`?
    public static bool Qualifies(CareerProfile p, CosmeticDef d) => false;

    /// Every item earned and not yet unlocked, unlocked now; returns them.
    public static List<CosmeticDef> Grant(CareerProfile p) => new List<CosmeticDef>();

    /// The item he has on in `s`.
    public static CosmeticDef Equipped(CareerProfile p, eCosmeticSlot s) => CosmeticCatalog.Default(s);

    /// Put on `id`. False when it is locked or unknown.
    public static bool Equip(CareerProfile p, string id) => false;

    /// The index `dir` steps from `index` among `s`'s items.
    public static int Step(eCosmeticSlot s, int index, int dir) => index;

    /// The inbox line for newly unlocked items.
    public static string NewsLine(List<CosmeticDef> l) => "";
}
