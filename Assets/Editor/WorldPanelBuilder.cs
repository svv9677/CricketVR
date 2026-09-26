using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Editor-only building blocks shared by every world-space panel: the canvas, layout rows and
/// columns, and text, all in the UIStyle look. The panels' controls (buttons, segmented choices,
/// sliders, range sliders) are in UIControls; the panels themselves in UIPrefabBuilder
/// (Tools > CricketVR > Build Player UI). Every control is wired with a persistent (saved)
/// listener, so the prefabs work with nothing created or hooked up at runtime.
///
/// The "Legacy" section keeps the old API that CricketVRSceneBuilder (Tools > CricketVR > Build UI
/// Prefabs) still calls, drawn in the new style; it can go once that builder calls
/// UIPrefabBuilder.BuildPanels instead.
/// </summary>
public static class WorldPanelBuilder
{
    /// A world-space canvas child ("Panel") of `root`, `metresPerPixel` metres per canvas px. With
    /// `interactive` it carries the raycasters the laser pointer needs.
    public static GameObject Canvas(GameObject root, float width, float metresPerPixel, bool interactive)
    {
        var go = new GameObject("Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(root.transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        if (interactive)
        {
            go.AddComponent<GraphicRaycaster>();
            var tracked = go.AddComponent<TrackedDeviceRaycaster>();
            tracked.maxDistance = 10f;
            tracked.checkFor3DOcclusion = false;
            tracked.checkFor2DOcclusion = false;
        }
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 100f);
        rect.localScale = Vector3.one * metresPerPixel;
        return go;
    }

    /// Make `go` a card: rounded dark background, vertical layout with the standard padding.
    public static VerticalLayoutGroup MakeCard(GameObject go, float padding = UIStyle.Pad, float spacing = UIStyle.Gap)
    {
        // (TryGetComponent, not `GetComponent ?? Add`: in the editor a missing component comes back
        // as a fake-null object that `??` does not see as null.)
        if (!go.TryGetComponent(out Image image))
            image = go.AddComponent<Image>();
        UIStyle.RoundedImage(image, UIStyle.Card, UIStyle.CardCorners);
        image.raycastTarget = true;   // the laser stops on the card rather than passing through
        if (!go.TryGetComponent(out VerticalLayoutGroup column))
            column = go.AddComponent<VerticalLayoutGroup>();
        int p = Mathf.RoundToInt(padding);
        column.padding = new RectOffset(p, p, p, p);
        column.spacing = spacing;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        return column;
    }

    /// Size a canvas to its content (height only, or both).
    public static void FitContent(GameObject go, bool width)
    {
        if (!go.TryGetComponent(out ContentSizeFitter fitter))
            fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (width)
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// A row (horizontal) or column (vertical) that fills its parent's width.
    public static Transform Column(Transform parent, bool vertical, float spacing = UIStyle.Gap)
    {
        var go = new GameObject(vertical ? "Column" : "Row", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        HorizontalOrVerticalLayoutGroup layout = vertical ? go.AddComponent<VerticalLayoutGroup>() : (HorizontalOrVerticalLayoutGroup)go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = vertical ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return go.transform;
    }

    /// A column of fixed width (0 = flexible), for side-by-side sections.
    public static Transform FixedColumn(Transform parent, float width, float spacing = UIStyle.Gap)
    {
        Transform column = Column(parent, true, spacing);
        var layout = column.GetComponent<LayoutElement>();
        if (width > 0f)
        {
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }
        return column;
    }

    /// Children keep their own widths rather than being stretched to share the row.
    public static void NoForceExpand(Transform row)
    {
        row.GetComponent<HorizontalOrVerticalLayoutGroup>().childForceExpandWidth = false;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, FontStyles style, Color color, float height,
                                       TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        if (height > 0f)
            go.GetComponent<LayoutElement>().preferredHeight = height;
        return tmp;
    }

    /// A single line that stays one line: ellipsis rather than wrapping.
    public static TextMeshProUGUI Line(Transform parent, string text, float size, FontStyles style, Color color, float height,
                                       TextAlignmentOptions align, float width = 0f)
    {
        var tmp = Text(parent, text, size, style, color, height, align);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var layout = tmp.GetComponent<LayoutElement>();
        if (width > 0f)
        {
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }
        else
            layout.flexibleWidth = 1f;
        return tmp;
    }

    /// Small, spaced capitals: the label over a group of controls.
    public static TextMeshProUGUI Section(Transform parent, string text)
    {
        var tmp = Line(parent, text, UIStyle.SectionSize, FontStyles.Bold | FontStyles.UpperCase, UIStyle.TextMuted, 28f, TextAlignmentOptions.Left);
        tmp.characterSpacing = 6f;
        return tmp;
    }

    /// A text filling its parent (button and option labels).
    public static TextMeshProUGUI FillLabel(Transform parent, string text, float size, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        rect.offsetMin = new Vector2(UIStyle.Unit, 0f);
        rect.offsetMax = new Vector2(-UIStyle.Unit, 0f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// A child image stretched over its parent (a toggle's lit state, a fill).
    public static Image Overlay(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void NoNavigation(Selectable selectable)
    {
        selectable.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    // ---- Legacy (CricketVRSceneBuilder) ----------------------------------------------------------

    public static Color Card => UIStyle.Card;
    public static Color Primary => UIStyle.Accent;
    public static Color Accent => UIStyle.Accent;
    public static Color Neutral => UIStyle.Surface;
    public static Color Danger => UIStyle.Surface;
    public static Color Muted => UIStyle.TextMuted;

    /// LEGACY. A card canvas ("Panel") of `root`, 1 px = 1 mm, with an optional title and help.
    public static GameObject Panel(GameObject root, float width, string title, string help = null)
    {
        var panel = Canvas(root, width, 0.001f, true);
        MakeCard(panel);
        FitContent(panel, false);
        if (!string.IsNullOrEmpty(title))
            Text(panel.transform, title, UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 52f, TextAlignmentOptions.Left);
        if (!string.IsNullOrEmpty(help))
            Text(panel.transform, help, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 0f, TextAlignmentOptions.Left);
        return panel;
    }

    /// LEGACY. A button whose Image carries `color` (tinted brighter on hover).
    public static TextMeshProUGUI Button(Transform parent, string label, UnityAction onClick, Color color, float height = UIStyle.Control)
    {
        return Button(parent, label, onClick, color, height, out _);
    }

    public static TextMeshProUGUI Button(Transform parent, string label, UnityAction onClick, Color color, float height, out Image background)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        background = go.GetComponent<Image>();
        UIStyle.RoundedImage(background, color, UIStyle.ControlCorners);
        var button = go.GetComponent<UnityEngine.UI.Button>();
        button.colors = Tint(button.colors);
        NoNavigation(button);
        UnityEventTools.AddVoidPersistentListener(button.onClick, onClick);
        return FillLabel(go.transform, label, UIStyle.ButtonSize, UIStyle.Text);
    }

    /// LEGACY. A radio/toggle button: lit in the accent colour while on. Grouped when `group` is set.
    public static Toggle Choice(Transform parent, string label, UnityAction<bool> onChanged, ToggleGroup group, float height = UIStyle.Control)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var background = go.GetComponent<Image>();
        UIStyle.RoundedImage(background, Color.white, UIStyle.ControlCorners);
        var on = Overlay(go.transform, "On");
        UIStyle.RoundedImage(on, UIStyle.Accent, UIStyle.ControlCorners);
        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = on;
        toggle.group = group;
        toggle.colors = UIStyle.Colors(false);
        toggle.isOn = false;
        NoNavigation(toggle);
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, onChanged);
        FillLabel(go.transform, label, UIStyle.ButtonSize, UIStyle.Text);
        return toggle;
    }

    /// LEGACY. Label, slider, value readout on one line.
    public static SettingsPanel.SliderRow Slider(Transform parent, string label, float min, float max, UnityAction<float> onChanged)
    {
        Transform row = Column(parent, false, UIStyle.Gap);
        row.GetComponent<LayoutElement>().preferredHeight = UIStyle.SliderRow;
        NoForceExpand(row);
        Line(row, label, UIStyle.HintSize, FontStyles.Normal, UIStyle.Text, UIStyle.SliderRow, TextAlignmentOptions.Left, 190f);
        UnityEngine.UI.Slider slider = UIControls.BareSlider(row, min, max, false, onChanged, float.NaN);
        slider.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var value = Line(row, "0", UIStyle.HintSize, FontStyles.Bold, UIStyle.AccentText, UIStyle.SliderRow, TextAlignmentOptions.Right, 80f);
        return new SettingsPanel.SliderRow { slider = slider, value = value };
    }

    private static ColorBlock Tint(ColorBlock colors)
    {
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
        colors.selectedColor = Color.white;
        return colors;
    }
}
