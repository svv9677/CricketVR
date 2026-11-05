using System;
using System.Collections.Generic;

/// <summary>
/// The career's locker (phase 7): which items he has earned, granting new ones, and the ones he has
/// on. Cosmetic only - nothing here changes how he bats. Earning reads only facts already saved (the
/// stats, caps, seasons, goals reached, drill bests, the highest rung he has played), so an old save
/// gets its kit the first time the hub opens, and dropping a rung never takes kit away. Each slot's
/// Start item is always his and is never listed in `unlocked`.
/// </summary>
public static class Cosmetics
{
    private const string From = "Locker";

    /// Has the career earned `d`?
    public static bool Qualifies(CareerProfile p, CosmeticDef d)
    {
        if (p == null || d == null)
            return false;
        CareerStats st = p.stats ?? new CareerStats();
        return d.rule switch
        {
            eUnlock.Start => true,
            eUnlock.Matches => st.matches >= d.need,
            eUnlock.Rung => (int)HighestRung(p) >= d.need,
            eUnlock.Caps => p.caps >= d.need,
            // A hundred passed fifty on the way (the stats count it as a hundred only).
            eUnlock.Fifties => st.fifties + st.hundreds >= d.need,
            eUnlock.Hundreds => st.hundreds >= d.need,
            eUnlock.Runs => st.runs >= d.need,
            eUnlock.Sixes => st.sixes >= d.need,
            eUnlock.Seasons => p.seasonsPlayed >= d.need,
            eUnlock.Goals => p.goalsDone >= d.need,
            _ => BestDrill(p) >= d.need,
        };
    }

    /// The highest rung he has played at: where he is, or any innings in the record book.
    public static eRung HighestRung(CareerProfile p)
    {
        eRung top = p?.rung ?? eRung.Club;
        if (p?.innings != null)
            foreach (InningsRecord r in p.innings)
                if (r != null && r.rung > top)
                    top = r.rung;
        return top;
    }

    /// His best score in any drill (0 with none).
    public static int BestDrill(CareerProfile p)
    {
        int best = 0;
        if (p?.drills?.records != null)
            foreach (DrillRecord d in p.drills.records)
                if (d != null)
                    best = Math.Max(best, d.best);
        return best;
    }

    /// Owned: a Start item, or one in `unlocked`.
    public static bool Unlocked(CareerProfile p, CosmeticDef d) =>
        d != null && (d.IsDefault || (p?.unlocked != null && p.unlocked.Contains(d.id)));

    /// Every item earned and not yet unlocked, unlocked now; returns them. One Locker message for
    /// the batch, so a career that earns three at once hears about them once.
    public static List<CosmeticDef> Grant(CareerProfile p)
    {
        var granted = new List<CosmeticDef>();
        if (p == null)
            return granted;
        p.unlocked ??= new List<string>();
        foreach (CosmeticDef d in CosmeticCatalog.All)
            if (!d.IsDefault && !p.unlocked.Contains(d.id) && Qualifies(p, d))
            {
                p.unlocked.Add(d.id);
                granted.Add(d);
            }
        if (granted.Count > 0)
            CareerProgress.Post(p, eMessageKind.Locker, From, "New in your Locker", NewsLine(granted));
        return granted;
    }

    /// The item he has on in `s`: the one saved for it, or the default when that is empty, unknown,
    /// another slot's or not his.
    public static CosmeticDef Equipped(CareerProfile p, eCosmeticSlot s)
    {
        CosmeticDef d = CosmeticCatalog.For(SavedId(p, s));
        return d != null && d.slot == s && Unlocked(p, d) ? d : CosmeticCatalog.Default(s);
    }

    /// Put on `id`. False when it is locked or unknown (nothing changes). The default is saved as "".
    public static bool Equip(CareerProfile p, string id)
    {
        CosmeticDef d = CosmeticCatalog.For(id);
        if (p == null || d == null || !Unlocked(p, d))
            return false;
        string saved = d.IsDefault ? "" : d.id;
        switch (d.slot)
        {
            case eCosmeticSlot.Bat: p.batFinish = saved; break;
            case eCosmeticSlot.Gloves: p.gloves = saved; break;
            default: p.kitTrim = saved; break;
        }
        return true;
    }

    /// `s`'s items, in the catalogue's order (the default first).
    public static List<CosmeticDef> Items(eCosmeticSlot s)
    {
        var l = new List<CosmeticDef>();
        foreach (CosmeticDef d in CosmeticCatalog.All)
            if (d.slot == s)
                l.Add(d);
        return l;
    }

    /// The index `dir` steps from `index` among `s`'s items, wrapping round at both ends.
    public static int Step(eCosmeticSlot s, int index, int dir)
    {
        int n = Items(s).Count;
        if (n == 0)
            return 0;
        return ((index + dir) % n + n) % n;
    }

    /// Where `d` is among its slot's items (0 when it is not there).
    public static int IndexOf(CosmeticDef d) => d == null ? 0 : Math.Max(0, Items(d.slot).IndexOf(d));

    /// Items he owns, the defaults included.
    public static int UnlockedCount(CareerProfile p)
    {
        int n = 0;
        foreach (CosmeticDef d in CosmeticCatalog.All)
            if (Unlocked(p, d))
                n++;
        return n;
    }

    /// The inbox line for newly unlocked items: "Pro gold bat and White gloves are in your Locker
    /// now - put them on from the career hub."
    public static string NewsLine(List<CosmeticDef> l)
    {
        if (l == null || l.Count == 0)
            return "";
        var names = new List<string>();
        foreach (CosmeticDef d in l)
            if (d != null)
                names.Add(CosmeticCatalog.FullName(d));
        if (names.Count == 0)
            return "";
        string list = names.Count == 1 ? names[0]
            : string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        return names.Count == 1
            ? $"{list} is in your Locker now - put it on from the career hub. It only changes how you look."
            : $"{list} are in your Locker now - put them on from the career hub. They only change how you look.";
    }

    private static string SavedId(CareerProfile p, eCosmeticSlot s) => p == null ? "" : s switch
    {
        eCosmeticSlot.Bat => p.batFinish,
        eCosmeticSlot.Gloves => p.gloves,
        _ => p.kitTrim,
    };
}
