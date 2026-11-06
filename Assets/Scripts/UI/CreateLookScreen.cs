using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Create Look, after Create Player: where the player is from (Nations.All), which way they bat,
/// their skin tone and kit colour (Appearance), and their shirt number, with a one-line preview of
/// the player card. "Create player" saves the career in its slot (CareerSaves.Create) and opens the
/// career hub over the slots screen, so Back from the hub goes to the slots, not back in here.
/// Built by Tools > CricketVR > Build Menu Prefabs; each ‹ › stepper is wired to its handler with
/// -1 or +1, and every stepper wraps round at its ends.
/// </summary>
public class CreateLookScreen : MenuScreen
{
    public const int MinNumber = 1, MaxNumber = 99;

    [SerializeField] private TextMeshProUGUI nation;
    [SerializeField] private SegmentedControl hand;
    [Tooltip("Easy / Medium / Hard: the bat's width for the whole career.")]
    [SerializeField] private SegmentedControl difficulty;
    [SerializeField] private TextMeshProUGUI skin;
    [SerializeField] private Image skinSwatch;
    [SerializeField] private TextMeshProUGUI kit;
    [SerializeField] private Image kitSwatch;
    [SerializeField] private TextMeshProUGUI number;
    [SerializeField] private TextMeshProUGUI summary;

    /// `value` moved by `delta`, wrapping round inside min..max (both included).
    public static int Wrap(int value, int delta, int min, int max)
    {
        int span = max - min + 1;
        if (span <= 0)
            return min;
        int i = ((value - min + delta) % span + span) % span;
        return min + i;
    }

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null)
            return;
        int skinIndex = Mathf.Clamp(p.look.skin, 0, Appearance.SkinTones.Length - 1);
        int kitIndex = Mathf.Clamp(p.look.kit, 0, Appearance.KitColours.Length - 1);
        string nationName = Nations.Name(p.nation);
        if (nation != null) nation.text = nationName;
        if (hand != null) hand.SetValueWithoutNotify(p.hand == eBattingStyle.LeftHanded ? 1 : 0);
        if (difficulty != null) difficulty.SetValueWithoutNotify(DifficultyIndex(p.difficulty));
        if (skin != null) skin.text = Appearance.SkinNames[skinIndex];
        if (skinSwatch != null) skinSwatch.color = Appearance.SkinTones[skinIndex];
        if (kit != null) kit.text = Appearance.KitNames[kitIndex];
        if (kitSwatch != null) kitSwatch.color = Appearance.KitColours[kitIndex];
        if (number != null) number.text = $"#{p.look.number}";
        if (summary != null)
            summary.text = $"<b>{p.name}</b>   <color=#98A2B3>#{p.look.number}  ·  {nationName}  ·  {p.HandName}</color>";
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    public void OnNation(int delta)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.nation = Nations.All[Wrap(Nations.IndexOf(p.nation), delta, 0, Nations.All.Length - 1)].code;
        Refresh();
    }

    public void OnHand(int i)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.hand = i == 1 ? eBattingStyle.LeftHanded : eBattingStyle.RightHanded;
        Refresh();
    }

    /// Easy, Medium, Hard (0..2) for the segmented control; anything else reads as Medium.
    public static int DifficultyIndex(eDifficulty d) => d == eDifficulty.Easy ? 0 : d == eDifficulty.Hard ? 2 : 1;
    public static eDifficulty DifficultyAt(int i) => i == 0 ? eDifficulty.Easy : i == 2 ? eDifficulty.Hard : eDifficulty.Medium;

    /// The career's difficulty: chosen here, fixed for the career (CareerSettingsGuard).
    public void OnDifficulty(int i)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.difficulty = DifficultyAt(i);
        Refresh();
    }

    public void OnSkin(int delta)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.look.skin = Wrap(p.look.skin, delta, 0, Appearance.SkinTones.Length - 1);
        Refresh();
    }

    public void OnKit(int delta)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.look.kit = Wrap(p.look.kit, delta, 0, Appearance.KitColours.Length - 1);
        Refresh();
    }

    public void OnNumber(int delta)
    {
        CareerProfile p = CareerDraft.Profile;
        if (p == null) return;
        p.look.number = Wrap(p.look.number, delta, MinNumber, MaxNumber);
        Refresh();
    }

    public void OnCreate()
    {
        if (!CareerDraft.Active || !OnScreenKeyboard.IsValid(CareerDraft.Profile.name))
        {
            OnBack();
            return;
        }
        CareerSaves.Create(CareerDraft.Slot, CareerDraft.Profile);
        // The stack is main > career > createPlayer > createLook: two Backs land on the slots, and
        // the hub goes over them (MenuStack has no replace, and MenuRoot is not ours to change).
        if (Root != null)
        {
            Root.Back();
            Root.Back();
            Root.Push("careerHub");
        }
        CareerDraft.Clear();
    }
}
