using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The career screens' layout (CareerDeckScreen): separate floating panels instead of one crowded
/// card. Each screen is a GameObject beside MenuRoot's card (under MenuRoot, so it is placed and
/// grabbed with the menu), holding its own world-space canvases:
///
///   * the main panel in the middle, CentreWidth wide;
///   * up to two side panels, SideWidth wide, angled SideYaw toward the player so they read
///     without turning the head far;
///   * the tab bar across the top - Home, Training, Season, Ratings, Inbox, Goals, Locker - the
///     current tab lit.
///
/// Every panel hangs from the same top line (pivot at its top edge), so panels of different heights
/// line up at the top and grow downward, never into the tab bar above them. 1 canvas px = 1 mm, as
/// on the menu card.
/// </summary>
public static class CareerDeckBuilder
{
    public const float CentreWidth = 760f, SideWidth = 560f, TabsWidth = 1640f;
    private const float Mm = 0.001f;
    /// The panels' top edge and the tab bar's bottom edge, metres above the menu's origin.
    private const float Top = 0.26f, TabsGap = 0.035f;
    /// Between the centre panel and a side panel (m), and how far a side panel turns in (deg).
    private const float Gap = 0.05f, SideYaw = 26f;
    private const float TabHeight = 60f;

    public enum Slot { Left = -1, Centre = 0, Right = 1 }

    /// The tabs, in bar order: label and the screen each opens (CareerDeckScreen's names).
    public static readonly (string label, string screen)[] Tabs =
    {
        ("Home", CareerDeckScreen.Hub), ("Training", CareerDeckScreen.Training), ("Season", CareerDeckScreen.SeasonTab),
        ("Ratings", CareerDeckScreen.RatingsTab), ("Inbox", CareerDeckScreen.InboxTab), ("Goals", CareerDeckScreen.GoalsTab),
        ("Locker", CareerDeckScreen.LockerTab),
    };

    /// The screen's GameObject, beside the menu card (`card` is the card MenuPrefabBuilder passes:
    /// its parent is MenuRoot).
    public static GameObject Screen(Transform card, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(card.parent, false);
        return go;
    }

    /// A screen of one panel (inbox, goals, locker, records): the screen's GameObject and its centre
    /// panel's column, for the screen's content as it was on the card. The caller adds the tabs.
    public static Transform CentreScreen(Transform card, string name, out GameObject go)
    {
        go = Screen(card, name);
        return Panel(go, name.Replace("Screen", ""), Slot.Centre);
    }

    /// A floating panel: a card-styled world-space canvas in `slot`, `width` canvas px wide, fitting
    /// its content's height. Returns the card's column for content.
    public static Transform Panel(GameObject screen, string name, Slot slot, float width = 0f)
    {
        if (width <= 0f)
            width = slot == Slot.Centre ? CentreWidth : SideWidth;
        GameObject canvas = WorldPanelBuilder.Canvas(screen, width, Mm, true);
        canvas.name = name;
        WorldPanelBuilder.MakeCard(canvas, UIStyle.Pad, UIStyle.Gap);
        WorldPanelBuilder.FitContent(canvas, false);
        var rect = canvas.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 1f);
        float half = width * 0.5f * Mm;
        if (slot == Slot.Centre)
        {
            canvas.transform.localPosition = new Vector3(0f, Top, 0f);
            return canvas.transform;
        }
        float side = (int)slot;
        float yaw = SideYaw * Mathf.Deg2Rad;
        float x = CentreWidth * 0.5f * Mm + Gap + half * Mathf.Cos(yaw);
        // Toward the player (local -Z: the canvas faces -Z) by the turn, so the inner edges meet.
        canvas.transform.localPosition = new Vector3(side * x, Top, -half * Mathf.Sin(yaw));
        canvas.transform.localRotation = Quaternion.Euler(0f, side * SideYaw, 0f);
        return canvas.transform;
    }

    /// The tab bar over the panels, `active` lit; returns each tab's button by screen name (the hub
    /// writes the unread count and the goals onto Inbox and Goals).
    public static Dictionary<string, Button> TabBar(GameObject screen, CareerDeckScreen deck, string active)
    {
        GameObject canvas = WorldPanelBuilder.Canvas(screen, TabsWidth, Mm, true);
        canvas.name = "Tabs";
        WorldPanelBuilder.MakeCard(canvas, UIStyle.Unit * 1.5f, 0f);
        WorldPanelBuilder.FitContent(canvas, false);
        var rect = canvas.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0.5f, 0f);
        canvas.transform.localPosition = new Vector3(0f, Top + TabsGap, 0f);
        Transform row = WorldPanelBuilder.Column(canvas.transform, false, UIStyle.Unit);
        var buttons = new Dictionary<string, Button>();
        foreach ((string label, string target) in Tabs)
        {
            Button b = UIControls.ActionButton(row, label, TabAction(deck, target), target == active, TabHeight);
            if (target == active)
                b.colors = UIStyle.Colors(true);
            buttons[target] = b;
        }
        return buttons;
    }

    private static UnityAction TabAction(CareerDeckScreen deck, string target) => target switch
    {
        CareerDeckScreen.Hub => deck.OnTabHome,
        CareerDeckScreen.Training => deck.OnTabTraining,
        CareerDeckScreen.SeasonTab => deck.OnTabSeason,
        CareerDeckScreen.RatingsTab => deck.OnTabRatings,
        CareerDeckScreen.InboxTab => deck.OnTabInbox,
        CareerDeckScreen.GoalsTab => deck.OnTabGoals,
        _ => deck.OnTabLocker,
    };

    /// A panel's heading: a small accent caps label and, unless `title` is null, a big title line
    /// under it (empty for the screen to fill in).
    public static TextMeshProUGUI Heading(Transform panel, string caps, string title = null)
    {
        WorldPanelBuilder.Line(panel, caps.ToUpperInvariant(), UIStyle.SectionSize, FontStyles.Bold, UIStyle.AccentText, 28f,
                               TextAlignmentOptions.Left);
        return title == null
            ? null
            : WorldPanelBuilder.Line(panel, title, UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 56f, TextAlignmentOptions.Left);
    }

    /// A line of body text at a readable size (bigger than the old card's hints).
    public static TextMeshProUGUI Body(Transform panel, string text, Color color, float height = 36f, float size = 0f,
                                       FontStyles style = FontStyles.Normal)
    {
        TextMeshProUGUI t = WorldPanelBuilder.Text(panel, text, size > 0f ? size : UIStyle.LabelSize, style, color, height,
                                                   TextAlignmentOptions.Left);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    /// A big number with a caption under it ("36" over "OVERALL"), for a stat tile row.
    public static TextMeshProUGUI StatTile(Transform row, string caption, out TextMeshProUGUI captionText)
    {
        Transform tile = WorldPanelBuilder.Column(row, true, 0f);
        tile.name = caption;
        LayoutElement share = tile.GetComponent<LayoutElement>();
        share.preferredWidth = 0f;          // equal shares of the row, whatever the numbers
        share.flexibleWidth = 1f;
        tile.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 10, 10);
        Image well = tile.gameObject.AddComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;
        TextMeshProUGUI value = WorldPanelBuilder.Line(tile, "--", 56f, FontStyles.Bold, UIStyle.Text, 64f, TextAlignmentOptions.Center);
        captionText = WorldPanelBuilder.Line(tile, caption, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 26f,
                                             TextAlignmentOptions.Center);
        captionText.enableAutoSizing = true;     // "OVERALL · Developing" in half a side panel
        captionText.fontSizeMin = 13f;
        captionText.fontSizeMax = UIStyle.HintSize;
        return value;
    }

    /// A button with an int argument (a drill tile's index), as CareerMenuBuilder wires its slots.
    public static Button IndexButton(Transform parent, string label, UnityAction<int> onClick, int index, float height)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
        Image image = go.GetComponent<Image>();
        UIStyle.RoundedImage(image, Color.white, UIStyle.ControlCorners);
        Button b = go.GetComponent<Button>();
        b.targetGraphic = image;
        b.colors = UIStyle.Colors(false);
        WorldPanelBuilder.NoNavigation(b);
        UnityEventTools.AddIntPersistentListener(b.onClick, onClick, index);
        return b;
    }

    public static void Bind(SerializedObject so, string field, Object value) =>
        so.FindProperty(field).objectReferenceValue = value;
}
