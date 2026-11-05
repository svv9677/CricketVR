using TMPro;
using UnityEngine;

/// <summary>
/// The career being made, between the slots screen and CareerSaves.Create: which slot it goes in
/// and the profile filled in over Create Player (the name) and Create Look (everything else).
/// Nothing is saved until Create Look's "Create player", so backing out leaves the slot empty.
/// </summary>
public static class CareerDraft
{
    public static int Slot { get; private set; } = -1;
    public static CareerProfile Profile { get; private set; }

    public static bool Active => Profile != null && Slot >= 0 && Slot < CareerSaves.Slots;

    /// A fresh profile for `slot`, batting the way the player already has the game set up.
    public static void Begin(int slot)
    {
        Slot = slot;
        Profile = new CareerProfile
        {
            hand = GameSettings.BattingStyle == eBattingStyle.LeftHanded ? eBattingStyle.LeftHanded : eBattingStyle.RightHanded,
        };
    }

    public static void Clear()
    {
        Slot = -1;
        Profile = null;
    }

    // Fictional names: first names and surnames from across the cricket nations, paired at random.
    // Pairs that would make a well-known player's name are left out by leaving out the first name.
    private static readonly string[] First =
    {
        "Arjun", "Rohan", "Kabir", "Dev", "Nikhil", "Tariq", "Hasan", "Faisal", "Tom", "Jack", "Liam",
        "Ollie", "Callum", "Theo", "Aiden", "Zane", "Kyle", "Marlon", "Andile", "Tendai", "Ravi", "Sami",
        "Asha", "Priya", "Meera", "Zara", "Amelia", "Grace", "Leah", "Nadia", "Ella", "Ruby",
    };

    private static readonly string[] Last =
    {
        "Mehta", "Rao", "Kapoor", "Iyer", "Nair", "Shah", "Malik", "Ahmed", "Hossain", "Qureshi",
        "Clarke", "Hughes", "Walsh", "Fletcher", "Barnes", "Morgan", "Pereira", "Fernando", "Silva",
        "Joseph", "Charles", "Botha", "Naidoo", "Moyo", "Doyle", "Brennan", "Reid", "Campbell",
        "O'Neill", "Lewis-Hart",
    };

    /// A plausible made-up name that fits the keyboard's rules (OnScreenKeyboard.MaxLength).
    public static string RandomName(System.Random random)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string name = First[random.Next(First.Length)] + " " + Last[random.Next(Last.Length)];
            if (name.Length <= OnScreenKeyboard.MaxLength)
                return name;
        }
        return First[0] + " " + Last[0];
    }
}

/// <summary>
/// Create Player, from an empty career slot: the player's name, typed on an in-world QWERTY
/// keyboard (there is no system keyboard under OpenXR here), or a Random one. Next goes on to Create
/// Look; nothing is saved yet (CareerDraft). Built by Tools > CricketVR > Build Menu Prefabs, every
/// key wired to OnKey with its own character.
/// </summary>
public class CreatePlayerScreen : MenuScreen
{
    private const float BlinkSeconds = 0.53f;
    private const string Caret = "#8AB8FF", Muted = "#98A2B3", Bad = "#FF6B4A";
    private const string DefaultHint = "Up to 16 letters. Capitals are added for you.";

    [SerializeField] private TextMeshProUGUI nameLine;
    [SerializeField] private TextMeshProUGUI hint;

    private static readonly System.Random random = new System.Random();
    private bool caretOn = true;
    private float nextBlink;

    private string Name
    {
        get => CareerDraft.Profile != null ? CareerDraft.Profile.name ?? "" : "";
        set { if (CareerDraft.Profile != null) CareerDraft.Profile.name = value; }
    }

    public override void OnShow()
    {
        SetHint(DefaultHint);
        Show();
    }

    private void Update()
    {
        // The caret blinks; the text is only rebuilt when it does, not every frame.
        if (Time.unscaledTime < nextBlink)
            return;
        caretOn = !caretOn;
        nextBlink = Time.unscaledTime + BlinkSeconds;
        ShowName();
    }

    private void Show()
    {
        caretOn = true;
        nextBlink = Time.unscaledTime + BlinkSeconds;
        ShowName();
    }

    private void ShowName()
    {
        if (nameLine == null)
            return;
        // A hidden caret is drawn see-through rather than left out, so the centred name does not
        // shuffle sideways as it blinks.
        string caret = caretOn ? $"<color={Caret}>|</color>" : $"<color={Caret}00>|</color>";
        string name = Name;
        nameLine.text = name.Length == 0 ? $"{caret}<color={Muted}>Your name</color>" : name + caret;
    }

    private void SetHint(string text)
    {
        if (hint != null)
            hint.text = text;
    }

    private void Edit(string text)
    {
        Name = text;
        SetHint(text.Length >= OnScreenKeyboard.MaxLength ? "That is as long as a name can be." : DefaultHint);
        Show();
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// A letter, Space, hyphen or apostrophe key.
    public void OnKey(string key) => Edit(OnScreenKeyboard.Type(Name, key));

    public void OnBackspace() => Edit(OnScreenKeyboard.Backspace(Name));

    public void OnRandom() => Edit(CareerDraft.RandomName(random));

    public void OnNext()
    {
        if (!CareerDraft.Active)
        {
            OnBack();
            return;
        }
        if (!OnScreenKeyboard.IsValid(Name))
        {
            SetHint($"<color={Bad}>Type at least two letters, or press Random.</color>");
            return;
        }
        Name = OnScreenKeyboard.Finish(Name);
        if (Root != null)
            Root.Push("createLook");
    }
}
