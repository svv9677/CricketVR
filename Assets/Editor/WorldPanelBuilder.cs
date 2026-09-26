using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Editor-only building blocks for the game's world-space panels: a card with a title, help text,
/// buttons, choice pairs, radio rows and labelled sliders, all in one consistent style. Every
/// control is wired with a persistent (saved) listener, so the prefabs work with nothing created
/// or hooked up at runtime. Used by CricketVRSceneBuilder.
/// </summary>
public static class WorldPanelBuilder
{
    public static readonly Color Card = new Color(0.07f, 0.09f, 0.13f, 0.95f);
    public static readonly Color Primary = new Color(0.16f, 0.58f, 0.3f);
    public static readonly Color Accent = new Color(0.2f, 0.36f, 0.62f);
    public static readonly Color Neutral = new Color(0.24f, 0.26f, 0.32f);
    public static readonly Color Danger = new Color(0.55f, 0.2f, 0.2f);
    public static readonly Color Muted = new Color(0.7f, 0.75f, 0.82f);

    /// A panel: an inactive-able canvas child ("Panel") of `root`, 1 px = 1 mm.
    public static GameObject Panel(GameObject root, float width, string title, string help = null)
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                   typeof(GraphicRaycaster), typeof(TrackedDeviceRaycaster), typeof(Image),
                                   typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);
        panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        panel.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        var tracked = panel.GetComponent<TrackedDeviceRaycaster>();
        tracked.maxDistance = 10f;
        tracked.checkFor3DOcclusion = false;
        tracked.checkFor2DOcclusion = false;
        var rect = panel.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 100f);
        rect.localScale = Vector3.one * 0.001f;
        panel.GetComponent<Image>().color = Card;
        var column = panel.GetComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(24, 24, 18, 24);
        column.spacing = 10f;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (!string.IsNullOrEmpty(title))
            Text(panel.transform, title, 36f, FontStyles.Bold, Color.white, 50f);
        if (!string.IsNullOrEmpty(help))
            Text(panel.transform, help, 22f, FontStyles.Normal, Muted, 0f);
        return panel;
    }

    public static Transform Column(Transform parent, bool vertical, float spacing = 10f)
    {
        var go = new GameObject(vertical ? "Column" : "Row", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        HorizontalOrVerticalLayoutGroup layout = vertical ? go.AddComponent<VerticalLayoutGroup>() : (HorizontalOrVerticalLayoutGroup)go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        go.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return go.transform;
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

    public static TextMeshProUGUI Section(Transform parent, string text) =>
        Text(parent, text, 24f, FontStyles.Bold | FontStyles.UpperCase, Muted, 34f, TextAlignmentOptions.Left);

    public static TextMeshProUGUI Button(Transform parent, string label, UnityAction onClick, Color color, float height = 62f)
    {
        return Button(parent, label, onClick, color, height, out _);
    }

    public static TextMeshProUGUI Button(Transform parent, string label, UnityAction onClick, Color color, float height, out Image background)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        background = go.GetComponent<Image>();
        background.color = color;
        var button = go.GetComponent<UnityEngine.UI.Button>();
        button.colors = Tint(button.colors);
        UnityEventTools.AddVoidPersistentListener(button.onClick, onClick);
        return Label(go.transform, label, 28f);
    }

    /// A radio/toggle button: lit in the accent colour while on. Grouped when `group` is set.
    public static Toggle Choice(Transform parent, string label, UnityAction<bool> onChanged, ToggleGroup group, float height = 58f)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var background = go.GetComponent<Image>();
        background.color = Neutral;
        var on = new GameObject("On", typeof(RectTransform), typeof(Image));
        on.transform.SetParent(go.transform, false);
        Stretch(on.GetComponent<RectTransform>());
        on.GetComponent<Image>().color = Accent;
        on.GetComponent<Image>().raycastTarget = false;
        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = on.GetComponent<Image>();
        toggle.group = group;
        toggle.colors = Tint(toggle.colors);
        toggle.isOn = false;
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, onChanged);
        Label(go.transform, label, 26f);
        return toggle;
    }

    /// Label, slider, value readout on one line.
    public static SettingsPanel.SliderRow Slider(Transform parent, string label, float min, float max, UnityAction<float> onChanged)
    {
        Transform row = Column(parent, false, 12f);
        row.GetComponent<LayoutElement>().preferredHeight = 44f;
        var name = Text(row, label, 22f, FontStyles.Normal, Color.white, 44f, TextAlignmentOptions.Left);
        name.GetComponent<LayoutElement>().preferredWidth = 190f;
        name.GetComponent<LayoutElement>().flexibleWidth = 0f;

        GameObject sliderGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderGo.transform.SetParent(row, false);
        var layout = sliderGo.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.preferredHeight = 44f;
        var slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        foreach (var image in sliderGo.GetComponentsInChildren<Image>())
        {
            if (image.name == "Background") image.color = Neutral;
            else if (image.name == "Fill") image.color = Accent;
            else if (image.name == "Handle") image.color = Color.white;
        }
        UnityEventTools.AddPersistentListener(slider.onValueChanged, onChanged);

        var value = Text(row, "0", 22f, FontStyles.Bold, Color.white, 44f, TextAlignmentOptions.Right);
        value.GetComponent<LayoutElement>().preferredWidth = 80f;
        value.GetComponent<LayoutElement>().flexibleWidth = 0f;
        return new SettingsPanel.SliderRow { slider = slider, value = value };
    }

    private static TextMeshProUGUI Label(Transform parent, string text, float size)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        rect.offsetMin = new Vector2(8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
