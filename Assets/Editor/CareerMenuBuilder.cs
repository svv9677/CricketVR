using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The career screens of the menu prefab, built by MenuPrefabBuilder (Tools > CricketVR > Build
/// Menu Prefabs) beside the others and in the same house style:
///   * CareerScreen ("career") - the three save slots, each with Create player, or Play and a
///     Delete that asks first in the slot's own row.
///   * CreatePlayerScreen ("createPlayer") - the name, on an in-world QWERTY keyboard.
///   * CreateLookScreen ("createLook") - nation, batting hand, skin tone, kit colour, shirt number.
///   * the career hub, ratings, season and match result screens - CareerMenuBuilder2.
///
/// Here, unlike the rest of the menu, one handler serves many buttons (a slot, a key, a stepper's
/// two arrows), so those buttons are wired with saved listeners that carry an argument - the slot
/// index, the key's character, -1 or +1. There is no scroll view, so every screen fits on the card.
/// </summary>
public static class CareerMenuBuilder
{
    private const float KeyGap = UIStyle.Unit;
    private const float KeyWidth = 62f;              // ten keys and nine gaps fill the 696 px column
    private const float BackspaceWidth = 180f;
    private const float NameHeight = 76f;
    private const float RandomWidth = 160f;
    private const float RowLabelWidth = 200f;
    private const float SwatchSize = 34f;

    // ---- Slots ------------------------------------------------------------------------------------

    public static MenuScreen BuildSlots(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerScreen", out GameObject go);
        CareerScreen screen = go.AddComponent<CareerScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "career");
        MenuPrefabBuilder.Title(column, "Career", "Three save slots. Your ratings come from how you really bat.");

        var so = new SerializedObject(screen);
        SerializedProperty slots = so.FindProperty("slots");
        slots.arraySize = CareerSaves.Slots;
        for (int i = 0; i < CareerSaves.Slots; i++)
            SlotTile(column, screen, i, slots.GetArrayElementAtIndex(i));
        so.ApplyModifiedProperties();

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        return screen;
    }

    /// One slot: a dark inset tile holding the normal row and, hidden, the delete-confirm row.
    private static void SlotTile(Transform column, CareerScreen screen, int i, SerializedProperty slot)
    {
        Transform tile = WorldPanelBuilder.Column(column, true, 0f);
        tile.name = $"Slot{i + 1}";
        tile.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 12, 12, 12);
        Image well = tile.gameObject.AddComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;

        Transform normal = Row(tile, UIStyle.Unit);
        Transform info = WorldPanelBuilder.Column(normal, true, 0f);
        info.GetComponent<LayoutElement>().preferredWidth = 0f;   // the buttons first, the text the rest
        TextMeshProUGUI title = WorldPanelBuilder.Line(info, $"Slot {i + 1}  ·  Empty", UIStyle.LabelSize + 2f, FontStyles.Normal,
                                                       UIStyle.Text, 34f, TextAlignmentOptions.Left);
        TextMeshProUGUI detail = WorldPanelBuilder.Line(info, "Start a new career here.", UIStyle.HintSize, FontStyles.Normal,
                                                        UIStyle.TextMuted, 28f, TextAlignmentOptions.Left);
        Button create = IntButton(normal, "Create player", screen.OnCreate, i, 220f);
        Button play = IntButton(normal, "Play", screen.OnPlay, i, 150f);
        Button delete = IntButton(normal, "Delete", screen.OnDelete, i, 116f);
        TextMeshProUGUI deleteLabel = UIControls.LabelOf(delete);
        deleteLabel.fontSize = UIStyle.HintSize + 2f;
        deleteLabel.color = UIStyle.TextMuted;

        Transform confirm = Row(tile, UIStyle.Unit);
        TextMeshProUGUI question = WorldPanelBuilder.Text(confirm, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text,
                                                          UIStyle.Control, TextAlignmentOptions.Left);
        // A 16-letter name makes the question wider than the room left by the two buttons:
        // shrink it to fit the row rather than spill out of the tile.
        question.enableAutoSizing = true;
        question.fontSizeMin = UIStyle.HintSize;
        question.fontSizeMax = UIStyle.LabelSize - 2f;
        LayoutElement questionLayout = question.GetComponent<LayoutElement>();
        questionLayout.preferredWidth = 0f;
        questionLayout.flexibleWidth = 1f;
        Button yes = IntButton(confirm, "Delete", screen.OnConfirmDelete, i, 140f);
        yes.colors = UIStyle.MenuColors(false);
        IntButton(confirm, "Keep", screen.OnKeep, i, 120f);
        confirm.gameObject.SetActive(false);
        play.gameObject.SetActive(false);
        delete.gameObject.SetActive(false);

        slot.FindPropertyRelative("title").objectReferenceValue = title;
        slot.FindPropertyRelative("detail").objectReferenceValue = detail;
        slot.FindPropertyRelative("create").objectReferenceValue = create.gameObject;
        slot.FindPropertyRelative("play").objectReferenceValue = play.gameObject;
        slot.FindPropertyRelative("delete").objectReferenceValue = delete.gameObject;
        slot.FindPropertyRelative("normal").objectReferenceValue = normal.gameObject;
        slot.FindPropertyRelative("confirm").objectReferenceValue = confirm.gameObject;
        slot.FindPropertyRelative("question").objectReferenceValue = question;
    }

    // ---- Create Player: the name ----------------------------------------------------------------

    public static MenuScreen BuildCreatePlayer(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CreatePlayerScreen", out GameObject go);
        CreatePlayerScreen screen = go.AddComponent<CreatePlayerScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "createPlayer");
        MenuPrefabBuilder.Title(column, "Create your player", "Step 1 of 2: your name, as the scoreboard will show it.");

        // The name in a dark field, with Random beside it.
        Transform field = Row(column, UIStyle.Gap);
        var box = new GameObject("Name", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        box.transform.SetParent(field, false);
        LayoutElement boxLayout = box.GetComponent<LayoutElement>();
        boxLayout.minHeight = boxLayout.preferredHeight = NameHeight;
        boxLayout.flexibleWidth = 1f;
        Image well = box.GetComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;
        TextMeshProUGUI nameLine = WorldPanelBuilder.FillLabel(box.transform, "", UIStyle.TitleSize - 2f, UIStyle.Text);
        UIControls.ActionButton(field, "Random", screen.OnRandom, false, NameHeight, RandomWidth);
        TextMeshProUGUI hint = MenuPrefabBuilder.Hint(column);

        Keyboard(column, screen);

        UIControls.Divider(column);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Back", screen.OnBack, false);
        Button next = UIControls.ActionButton(actions, "Next", screen.OnNext, true);
        next.colors = UIStyle.MenuColors(true);

        var so = new SerializedObject(screen);
        so.FindProperty("nameLine").objectReferenceValue = nameLine;
        so.FindProperty("hint").objectReferenceValue = hint;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// QWERTY, three letter rows, then hyphen, apostrophe, Space and Backspace. Every key is wired
    /// to OnKey with its own character, so the keyboard is one handler, not thirty.
    private static void Keyboard(Transform column, CreatePlayerScreen screen)
    {
        Transform keys = WorldPanelBuilder.Column(column, true, KeyGap);
        keys.name = "Keyboard";
        foreach (string letters in OnScreenKeyboard.Rows)
        {
            Transform row = KeyRow(keys);
            foreach (char c in letters)
                StringButton(row, c.ToString(), screen.OnKey, c.ToString(), KeyWidth);
        }
        Transform last = KeyRow(keys);
        StringButton(last, OnScreenKeyboard.Hyphen, screen.OnKey, OnScreenKeyboard.Hyphen, KeyWidth);
        StringButton(last, OnScreenKeyboard.Apostrophe, screen.OnKey, OnScreenKeyboard.Apostrophe, KeyWidth);
        StringButton(last, "Space", screen.OnKey, OnScreenKeyboard.Space, 0f);
        UIControls.ActionButton(last, "Backspace", screen.OnBackspace, false, UIStyle.Control, BackspaceWidth);
    }

    /// A row of keys at their own widths, centred (the shorter rows sit in the middle, as on a
    /// real keyboard).
    private static Transform KeyRow(Transform parent)
    {
        Transform row = Row(parent, KeyGap);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        return row;
    }

    // ---- Create Look: everything else -----------------------------------------------------------

    public static MenuScreen BuildCreateLook(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CreateLookScreen", out GameObject go);
        CreateLookScreen screen = go.AddComponent<CreateLookScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "createLook");
        MenuPrefabBuilder.Title(column, "Create your player", "Step 2 of 2: where you are from, how you bat, what you wear.");

        TextMeshProUGUI nation = StepperRow(column, "Nationality", screen.OnNation, false, out _);
        Transform handRow = LabelledRow(column, "Batting");
        SegmentedControl hand = UIControls.Segmented(handRow, new[] { "Right", "Left" }, screen.OnHand);
        Transform difficultyRow = LabelledRow(column, "Difficulty");
        SegmentedControl difficulty = UIControls.Segmented(difficultyRow, new[] { "Easy", "Medium", "Hard" }, screen.OnDifficulty);
        WorldPanelBuilder.Line(column, "How wide your bat is. Fixed for this career.", UIStyle.LabelSize - 4f, FontStyles.Normal,
                               UIStyle.TextMuted, 30f, TextAlignmentOptions.Center);
        TextMeshProUGUI skin = StepperRow(column, "Skin tone", screen.OnSkin, true, out Image skinSwatch);
        TextMeshProUGUI kit = StepperRow(column, "Kit colour", screen.OnKit, true, out Image kitSwatch);
        TextMeshProUGUI number = StepperRow(column, "Shirt number", screen.OnNumber, false, out _);

        UIControls.Divider(column);
        TextMeshProUGUI summary = WorldPanelBuilder.Line(column, "", UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, 36f,
                                                         TextAlignmentOptions.Center);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Back", screen.OnBack, false);
        Button create = UIControls.ActionButton(actions, "Create player", screen.OnCreate, true);
        create.colors = UIStyle.MenuColors(true);

        var so = new SerializedObject(screen);
        so.FindProperty("nation").objectReferenceValue = nation;
        so.FindProperty("hand").objectReferenceValue = hand;
        so.FindProperty("difficulty").objectReferenceValue = difficulty;
        so.FindProperty("skin").objectReferenceValue = skin;
        so.FindProperty("skinSwatch").objectReferenceValue = skinSwatch;
        so.FindProperty("kit").objectReferenceValue = kit;
        so.FindProperty("kitSwatch").objectReferenceValue = kitSwatch;
        so.FindProperty("number").objectReferenceValue = number;
        so.FindProperty("summary").objectReferenceValue = summary;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// "Label   ‹  [swatch] value  ›": the arrows wired to `onStep` with -1 and +1.
    internal static TextMeshProUGUI StepperRow(Transform column, string label, UnityAction<int> onStep, bool swatch,
                                              out Image swatchFill)
    {
        Transform row = LabelledRow(column, label);
        IntButton(row, "‹", onStep, -1, MenuPrefabBuilder.StepperWidth);
        Transform value = Row(row, UIStyle.Unit + 4f);
        value.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        value.GetComponent<LayoutElement>().flexibleWidth = 1f;
        swatchFill = swatch ? Swatch(value, SwatchSize, SwatchSize, 3f) : null;
        TextMeshProUGUI text = WorldPanelBuilder.Line(value, "", UIStyle.LabelSize, FontStyles.Bold, UIStyle.Text, UIStyle.Control,
                                                      TextAlignmentOptions.Center);
        // The text's own width, so the swatch and the name sit together in the middle.
        text.GetComponent<LayoutElement>().flexibleWidth = -1f;
        IntButton(row, "›", onStep, 1, MenuPrefabBuilder.StepperWidth);
        return text;
    }

    private static Transform LabelledRow(Transform column, string label)
    {
        Transform row = Row(column, UIStyle.Unit);
        WorldPanelBuilder.Line(row, label, UIStyle.LabelSize, FontStyles.Normal, UIStyle.TextMuted, UIStyle.Control,
                               TextAlignmentOptions.Left, RowLabelWidth);
        return row;
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// A row whose children keep their own widths (a flexible child takes what is left).
    internal static Transform Row(Transform parent, float spacing)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, spacing);
        WorldPanelBuilder.NoForceExpand(row);
        return row;
    }

    /// A colour disc or bar inside a faint ring, so the darkest kits still show on the dark card.
    /// Returns the inner image, whose colour the screen sets.
    internal static Image Swatch(Transform parent, float width, float height, float ring)
    {
        var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = width;
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = 0f;
        Image outer = go.GetComponent<Image>();
        UIStyle.PillImage(outer, new Color(1f, 1f, 1f, 0.22f));
        outer.raycastTarget = false;
        Image inner = WorldPanelBuilder.Overlay(go.transform, "Fill");
        inner.rectTransform.offsetMin = new Vector2(ring, ring);
        inner.rectTransform.offsetMax = new Vector2(-ring, -ring);
        UIStyle.PillImage(inner, Color.white);
        return inner;
    }

    /// A button in UIControls.ActionButton's look whose click passes `argument` (a slot, a step).
    internal static Button IntButton(Transform parent, string label, UnityAction<int> onClick, int argument, float width)
    {
        Button button = PlainButton(parent, label, width);
        UnityEventTools.AddIntPersistentListener(button.onClick, onClick, argument);
        return button;
    }

    /// A button in UIControls.ActionButton's look whose click passes `argument` (a key).
    private static Button StringButton(Transform parent, string label, UnityAction<string> onClick, string argument, float width)
    {
        Button button = PlainButton(parent, label, width);
        UnityEventTools.AddStringPersistentListener(button.onClick, onClick, argument);
        return button;
    }

    /// ActionButton's look (surface colour, rounded, no navigation) with no listener yet: its own
    /// listener takes no argument. `width` 0 = flexible; a fixed width is also the minimum, so a
    /// full row squeezes the flexible text rather than the buttons.
    private static Button PlainButton(Transform parent, string label, float width)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = UIStyle.Control;
        if (width > 0f)
        {
            layout.minWidth = layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }
        else
            layout.flexibleWidth = 1f;
        Image image = go.GetComponent<Image>();
        UIStyle.RoundedImage(image, Color.white, UIStyle.ControlCorners);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = UIStyle.Colors(false);
        WorldPanelBuilder.NoNavigation(button);
        WorldPanelBuilder.FillLabel(go.transform, label, UIStyle.ButtonSize, UIStyle.Text);
        return button;
    }
}
