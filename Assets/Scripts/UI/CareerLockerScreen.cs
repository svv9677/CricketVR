using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The locker ("careerLocker", from the hub and the Goals screen, phase 7): the bat, gloves and
/// helmet trim, each a ‹ › stepper over every item in its slot with a swatch of its colour. An item
/// he has earned is put on (and saved) as soon as it is stepped to; a locked one is shown dimmed,
/// marked locked, and the status line says how to earn it. Cosmetic only: the kit shows in career
/// matches, career nets and their replays, and nothing here changes how he bats. Built by Tools >
/// CricketVR > Build Menu Prefabs (CareerLockerMenuBuilder); each stepper is wired with -1 and +1.
/// </summary>
public class CareerLockerScreen : CareerDeckScreen
{
    public const string Muted = "#98A2B3";
    /// A locked item's swatch: its colour, faded.
    public const float LockedAlpha = 0.3f;
    /// The swatch for an item that leaves the model as it is (Classic bat, Standard gloves, Silver trim).
    public static readonly Color[] DefaultSwatch =
    {
        new Color(0.86f, 0.73f, 0.52f), new Color(0.72f, 0.78f, 0.86f), new Color(0.6f, 0.6f, 0.62f),
    };

    private const int Slots = 3;

    [SerializeField] private TextMeshProUGUI[] names = new TextMeshProUGUI[0];
    [SerializeField] private Image[] swatches = new Image[0];
    [SerializeField] private TextMeshProUGUI status;

    // The item each stepper shows (an index into Cosmetics.Items), which may be a locked one.
    private readonly int[] shown = new int[Slots];
    private CosmeticDef lastLocked;

    public override void OnShow()
    {
        base.OnShow();
        CareerProfile p = CareerSaves.Current;
        if (p != null && CareerUpkeep.Run(p))   // kit earned since the hub last looked
            CareerSaves.SaveCurrent();
        lastLocked = null;
        for (int s = 0; s < Slots; s++)
            shown[s] = Cosmetics.IndexOf(Cosmetics.Equipped(p, (eCosmeticSlot)s));
        Refresh();
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    public void OnBat(int dir) => Step(eCosmeticSlot.Bat, dir);
    public void OnGloves(int dir) => Step(eCosmeticSlot.Gloves, dir);
    public void OnTrim(int dir) => Step(eCosmeticSlot.Trim, dir);

    private void Step(eCosmeticSlot slot, int dir)
    {
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        int s = (int)slot;
        shown[s] = Cosmetics.Step(slot, shown[s], dir);
        CosmeticDef d = Cosmetics.Items(slot)[shown[s]];
        if (Cosmetics.Equip(p, d.id))
        {
            lastLocked = null;
            CareerSaves.SaveCurrent();
        }
        else
            lastLocked = d;
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        CosmeticLook look = p == null ? null : new CosmeticLook { side = CosmeticLook.SideColour(p, null) };
        for (int s = 0; s < Slots; s++)
        {
            var items = Cosmetics.Items((eCosmeticSlot)s);
            CosmeticDef d = items[Mathf.Clamp(shown[s], 0, items.Count - 1)];
            bool owned = Cosmetics.Unlocked(p, d);
            if (s < names.Length && names[s] != null)
                names[s].text = NameLine(d, owned);
            if (s < swatches.Length && swatches[s] != null)
                swatches[s].color = Swatch(d, look, owned);
        }
        if (status != null)
            status.text = StatusLine(p, lastLocked);
    }

    /// "Pro gold", or "Pro gold locked" with the mark small and muted: the stepper leaves the name
    /// about 250 px of a line that does not wrap, so a full-size mark would cut "Coach's pick" short.
    public static string NameLine(CosmeticDef d, bool owned) =>
        d == null ? "" : owned ? d.name : $"{d.name} <size=70%><color={Muted}>locked</color></size>";

    /// "Locked: Pro gold bat - reach the Pro League. 7 of 19 unlocked."; with nothing locked shown,
    /// the count and where the kit is worn.
    public static string StatusLine(CareerProfile p, CosmeticDef locked)
    {
        if (p == null)
            return "No career loaded.";
        string count = $"{Cosmetics.UnlockedCount(p)} of {CosmeticCatalog.All.Length} unlocked.";
        if (locked != null)
            return $"Locked: {CosmeticCatalog.FullName(locked)} - {LowerFirst(CosmeticCatalog.HowToUnlock(locked))}. {count}";
        return $"{count} Worn in career matches and the career nets.";
    }

    /// The swatch's colour: the item's (a bat's label or grip), the model's own for a default, faded
    /// when locked.
    public static Color Swatch(CosmeticDef d, CosmeticLook look, bool owned)
    {
        if (d == null)
            return Color.clear;
        Color c = d.IsDefault ? DefaultSwatch[Mathf.Clamp((int)d.slot, 0, DefaultSwatch.Length - 1)]
                : d.bat != null ? BatColour(d.bat)
                : d.sideColour ? (look != null ? look.side : Color.white)
                : d.colour;
        c.a = owned ? 1f : LockedAlpha;
        return c;
    }

    private static Color BatColour(Color[] parts)
    {
        foreach (int i in new[] { BatSubmesh.Label, BatSubmesh.Grip })
            if (i < parts.Length && parts[i] != CosmeticCatalog.Keep)
                return parts[i];
        return DefaultSwatch[0];
    }

    private static string LowerFirst(string s) =>
        string.IsNullOrEmpty(s) ? "" : char.ToLowerInvariant(s[0]) + s.Substring(1);
}
