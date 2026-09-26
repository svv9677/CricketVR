using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Editor-only controls in the UIStyle look, each wired with a saved listener: buttons, segmented
/// choices, on/off toggle buttons, labelled value sliders and range sliders. Every control is at
/// least 56 px (56 mm on a 1 px = 1 mm panel) tall so the laser can find it.
/// </summary>
public static class UIControls
{
    // ---- Buttons and toggles --------------------------------------------------------------------

    /// A button: `primary` in the accent colour (one per panel), otherwise the surface colour.
    public static Button ActionButton(Transform parent, string label, UnityAction onClick, bool primary,
                                      float height = UIStyle.Control, float width = 0f)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, width, height);
        var image = go.GetComponent<Image>();
        UIStyle.RoundedImage(image, Color.white, UIStyle.ControlCorners);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = UIStyle.Colors(primary);
        WorldPanelBuilder.NoNavigation(button);
        UnityEventTools.AddVoidPersistentListener(button.onClick, onClick);
        WorldPanelBuilder.FillLabel(go.transform, label, primary ? UIStyle.ButtonSize + 4f : UIStyle.ButtonSize, UIStyle.Text);
        return button;
    }

    /// A button's label text.
    public static TextMeshProUGUI LabelOf(Component control) => control.GetComponentInChildren<TextMeshProUGUI>(true);

    /// An on/off button that lights in the accent colour while on.
    public static Toggle ToggleButton(Transform parent, string label, UnityAction<bool> onChanged, float width = 0f)
    {
        Toggle toggle = MakeToggle(parent, label, UIStyle.Colors(false), UIStyle.ControlCorners);
        Size(toggle.gameObject, width, UIStyle.Control);
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, onChanged);
        return toggle;
    }

    /// Mutually exclusive options in a dark well; the chosen one lit in the accent colour.
    public static SegmentedControl Segmented(Transform parent, string[] labels, UnityAction<int> onChanged)
    {
        var go = new GameObject("Segmented", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup),
                                typeof(ToggleGroup), typeof(SegmentedControl), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, 0f, UIStyle.Control);
        var well = go.GetComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;
        var row = go.GetComponent<HorizontalLayoutGroup>();
        row.padding = new RectOffset(4, 4, 4, 4);
        row.spacing = 4f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        var group = go.GetComponent<ToggleGroup>();
        group.allowSwitchOff = false;
        var control = go.GetComponent<SegmentedControl>();

        var toggles = new Toggle[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            Toggle option = MakeToggle(go.transform, labels[i], UIStyle.OptionColors(), UIStyle.OptionCorners);
            option.GetComponent<LayoutElement>().flexibleWidth = 1f;
            option.group = group;
            UnityEventTools.AddIntPersistentListener(option.onValueChanged, control.OnOption, i);
            TextMeshProUGUI text = LabelOf(option);
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = UIStyle.ButtonSize;
            toggles[i] = option;
        }
        control.SetOptions(toggles);
        control.SetValueWithoutNotify(0);
        UnityEventTools.AddPersistentListener(control.onValueChanged, onChanged);
        return control;
    }

    private static Toggle MakeToggle(Transform parent, string label, ColorBlock colors, float corners)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var background = go.GetComponent<Image>();
        UIStyle.RoundedImage(background, Color.white, corners);
        Image on = WorldPanelBuilder.Overlay(go.transform, "On");
        UIStyle.RoundedImage(on, UIStyle.Accent, corners);
        var toggle = go.GetComponent<Toggle>();
        toggle.isOn = false;
        toggle.targetGraphic = background;
        toggle.graphic = on;
        toggle.toggleTransition = Toggle.ToggleTransition.Fade;
        toggle.colors = colors;
        WorldPanelBuilder.NoNavigation(toggle);
        on.canvasRenderer.SetAlpha(0f);
        WorldPanelBuilder.FillLabel(go.transform, label, UIStyle.ButtonSize, UIStyle.Text);
        return toggle;
    }

    // ---- Labels ---------------------------------------------------------------------------------

    /// "Label ............ value" on one line above a control. Returns the value text.
    public static TextMeshProUGUI LabelLine(Transform parent, string label, string value, out TextMeshProUGUI labelText)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, UIStyle.Gap);
        row.GetComponent<LayoutElement>().preferredHeight = UIStyle.LabelRow;
        WorldPanelBuilder.NoForceExpand(row);
        labelText = WorldPanelBuilder.Line(row, label, UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, UIStyle.LabelRow,
                                           TextAlignmentOptions.Left);
        // The label takes the width it needs; the value gets the rest, right-aligned.
        labelText.GetComponent<LayoutElement>().flexibleWidth = 0f;
        return WorldPanelBuilder.Line(row, value, UIStyle.ValueSize, FontStyles.Bold, UIStyle.AccentText, UIStyle.LabelRow,
                                      TextAlignmentOptions.Right);
    }

    public static void Divider(Transform parent)
    {
        var go = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 2f;
        var image = go.GetComponent<Image>();
        image.color = UIStyle.Divider;
        image.raycastTarget = false;
    }

    // ---- Sliders --------------------------------------------------------------------------------

    /// Label and value on one line, the slider under it. `mark` puts a tick at that value.
    public static SettingsPanel.SliderRow ValueSlider(Transform parent, string label, float min, float max, bool whole,
                                                      UnityAction<float> onChanged, float mark = float.NaN)
    {
        Transform block = WorldPanelBuilder.Column(parent, true, 0f);
        TextMeshProUGUI value = LabelLine(block, label, "", out _);
        Slider slider = BareSlider(block, min, max, whole, onChanged, mark);
        return new SettingsPanel.SliderRow { slider = slider, value = value };
    }

    /// Label and "low to high" on one line, a two-handled range slider under it.
    public static SettingsPanel.RangeRow Range(Transform parent, string label, float min, float max, float step,
                                               UnityAction<float, float> onChanged)
    {
        Transform block = WorldPanelBuilder.Column(parent, true, 0f);
        TextMeshProUGUI value = LabelLine(block, label, "", out TextMeshProUGUI labelText);
        GameObject go = HitArea(block, "Range", typeof(RangeSlider));
        RectTransform track = Strip(go.transform, "Track", UIStyle.TrackHeight);
        UIStyle.PillImage(track.gameObject.AddComponent<Image>(), UIStyle.Track);
        track.GetComponent<Image>().raycastTarget = false;
        RectTransform fill = WorldPanelBuilder.Overlay(track, "Fill").rectTransform;
        UIStyle.PillImage(fill.GetComponent<Image>(), UIStyle.Accent);
        RectTransform low = Knob(track, "Low");
        RectTransform high = Knob(track, "High");

        var range = go.GetComponent<RangeSlider>();
        range.SetParts(track, fill, low, high);
        range.Configure(min, max, step);
        range.SetValuesWithoutNotify(min, max);
        UnityEventTools.AddPersistentListener(range.onValueChanged, onChanged);
        return new SettingsPanel.RangeRow { slider = range, label = labelText, value = value };
    }

    /// A Unity Slider restyled: pill track, accent fill, white knob, a row-high hit area.
    public static Slider BareSlider(Transform parent, float min, float max, bool whole, UnityAction<float> onChanged, float mark)
    {
        GameObject go = HitArea(parent, "Slider", typeof(Slider));
        RectTransform background = Strip(go.transform, "Background", UIStyle.TrackHeight);
        UIStyle.PillImage(background.gameObject.AddComponent<Image>(), UIStyle.Track);
        background.GetComponent<Image>().raycastTarget = false;
        RectTransform fillArea = Strip(go.transform, "Fill Area", UIStyle.TrackHeight);
        Image fill = WorldPanelBuilder.Overlay(fillArea, "Fill");
        UIStyle.PillImage(fill, UIStyle.Accent);
        if (!float.IsNaN(mark) && max > min)
            Tick(Strip(go.transform, "Mark", UIStyle.TrackHeight), (mark - min) / (max - min));
        RectTransform handleArea = Strip(go.transform, "Handle Slide Area", UIStyle.Knob);
        RectTransform handle = Knob(handleArea, "Handle");
        handle.sizeDelta = new Vector2(UIStyle.Knob, 0f);   // the slider stretches it to the area's height

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = UIStyle.Text;
        colors.highlightedColor = Color.white;
        colors.pressedColor = UIStyle.AccentText;
        colors.selectedColor = UIStyle.Text;
        slider.colors = colors;
        WorldPanelBuilder.NoNavigation(slider);
        UnityEventTools.AddPersistentListener(slider.onValueChanged, onChanged);
        return slider;
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// A full-width, slider-high object whose see-through Image catches the laser.
    private static GameObject HitArea(Transform parent, string name, System.Type component)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), component, typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Size(go, 0f, UIStyle.SliderRow);
        go.GetComponent<Image>().color = UIStyle.Clear;
        return go;
    }

    /// A horizontal strip centred in its parent, inset half a knob at each end so the knob's
    /// centre can reach both ends without leaving the row.
    private static RectTransform Strip(Transform parent, string name, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(-UIStyle.Knob, height);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static RectTransform Knob(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(UIStyle.Knob, UIStyle.Knob);
        var image = go.GetComponent<Image>();
        UIStyle.PillImage(image, Color.white);
        image.raycastTarget = false;
        return rect;
    }

    private static void Tick(RectTransform strip, float t)
    {
        var go = new GameObject("Tick", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(strip, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01(t), 0.5f);
        rect.sizeDelta = new Vector2(4f, 28f);
        var image = go.GetComponent<Image>();
        image.color = UIStyle.TextMuted;
        image.raycastTarget = false;
    }

    private static void Size(GameObject go, float width, float height)
    {
        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        if (width > 0f)
        {
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }
        else
            layout.flexibleWidth = 1f;
    }
}
