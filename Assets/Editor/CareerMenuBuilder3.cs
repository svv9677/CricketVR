using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The career inbox screens, built by MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs)
/// beside CareerMenuBuilder2's hub, in the same house style:
///   * InboxScreen ("careerInbox") - InboxScreen.PerPage message rows, newest first, and a
///     "‹ Page 1 of 3 ›" stepper.
///   * MessageScreen ("careerMessage") - one message, and up to MessageScreen.MaxOptions choices.
/// The rows, the pager's arrows and the choices share one handler each, so they are wired with
/// saved listeners that carry the row, the step or the option's index. There is no scroll view:
/// the inbox pages and the message's body is kept to about eight lines.
/// </summary>
public static class CareerMenuBuilder3
{
    private const float RowHeight = UIStyle.SliderRow;   // 56: the laser's minimum
    private const float RowGap = UIStyle.Unit;
    private const float DotSize = 12f;
    private const float DateWidth = 150f;
    private const float PageWidth = 260f;
    private const float BodyHeight = 224f;              // about eight lines of 22 px text
    private const float BodySize = UIStyle.LabelSize - 4f;

    // ---- Inbox ------------------------------------------------------------------------------------

    public static MenuScreen BuildInbox(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "InboxScreen", out GameObject go);
        InboxScreen screen = go.AddComponent<InboxScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerInbox");
        var so = new SerializedObject(screen);

        WorldPanelBuilder.Text(column, "Inbox", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 58f);
        so.FindProperty("subtitle").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f, TextAlignmentOptions.Center);
        UIControls.Divider(column);

        Transform list = WorldPanelBuilder.Column(column, true, RowGap);
        list.name = "Messages";
        SerializedProperty rows = so.FindProperty("rows");
        rows.arraySize = InboxScreen.PerPage;
        for (int i = 0; i < InboxScreen.PerPage; i++)
            MessageRow(list, screen, i, rows.GetArrayElementAtIndex(i));

        // "‹  Page 1 of 3  ›", centred.
        Transform pager = CareerMenuBuilder.Row(column, UIStyle.Unit);
        pager.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        Button prev = IntButton(pager, "‹", screen.OnPage, -1, false, UIStyle.Control, MenuPrefabBuilder.StepperWidth);
        so.FindProperty("pageText").objectReferenceValue =
            WorldPanelBuilder.Line(pager, "Page 1 of 1", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, UIStyle.Control,
                                   TextAlignmentOptions.Center, PageWidth);
        Button next = IntButton(pager, "›", screen.OnPage, 1, false, UIStyle.Control, MenuPrefabBuilder.StepperWidth);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        so.FindProperty("prevPage").objectReferenceValue = prev;
        so.FindProperty("nextPage").objectReferenceValue = next;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// One row: a surface-coloured button holding the unread dot, the tag-and-title line and the
    /// date, wired to InboxScreen.OnRow with its index.
    private static void MessageRow(Transform list, InboxScreen screen, int i, SerializedProperty row)
    {
        var go = new GameObject($"Message{i + 1}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement),
                                typeof(HorizontalLayoutGroup));
        go.transform.SetParent(list, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = RowHeight;
        layout.flexibleWidth = 1f;
        HorizontalLayoutGroup h = go.GetComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(18, 18, 0, 0);
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        Image background = go.GetComponent<Image>();
        UIStyle.RoundedImage(background, Color.white, UIStyle.ControlCorners);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        button.colors = UIStyle.Colors(false);
        WorldPanelBuilder.NoNavigation(button);
        UnityEventTools.AddIntPersistentListener(button.onClick, screen.OnRow, i);

        Image dot = Dot(go.transform);
        TextMeshProUGUI text = WorldPanelBuilder.Line(go.transform, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text,
                                                      RowHeight, TextAlignmentOptions.Left);
        TextMeshProUGUI date = WorldPanelBuilder.Line(go.transform, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted,
                                                      RowHeight, TextAlignmentOptions.Right, DateWidth);
        // The date keeps its column; the title takes what is left and ends in an ellipsis.
        text.GetComponent<LayoutElement>().preferredWidth = 0f;
        date.GetComponent<LayoutElement>().minWidth = DateWidth;

        row.FindPropertyRelative("button").objectReferenceValue = button;
        row.FindPropertyRelative("background").objectReferenceValue = background;
        row.FindPropertyRelative("dot").objectReferenceValue = dot;
        row.FindPropertyRelative("text").objectReferenceValue = text;
        row.FindPropertyRelative("date").objectReferenceValue = date;
    }

    /// The unread dot: a small pink disc (InboxScreen switches it off once the message is read).
    private static Image Dot(Transform parent)
    {
        var go = new GameObject("Unread", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = DotSize;
        layout.minHeight = layout.preferredHeight = DotSize;
        layout.flexibleWidth = 0f;
        Image dot = go.GetComponent<Image>();
        UIStyle.PillImage(dot, UIStyle.MenuPrimary);
        dot.raycastTarget = false;
        return dot;
    }

    // ---- Message ----------------------------------------------------------------------------------

    public static MenuScreen BuildMessage(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "MessageScreen", out GameObject go);
        MessageScreen screen = go.AddComponent<MessageScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerMessage");
        var so = new SerializedObject(screen);

        so.FindProperty("meta").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f, TextAlignmentOptions.Left);
        // The title wraps to two lines: "You're left out for the match against <side>" names the
        // side only at its end, and the body does not repeat it.
        TextMeshProUGUI title = WorldPanelBuilder.Text(column, "", UIStyle.TitleSize - 8f, FontStyles.Bold, UIStyle.Text, 76f,
                                                       TextAlignmentOptions.TopLeft);
        title.overflowMode = TextOverflowModes.Ellipsis;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("from").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.AccentText, 30f, TextAlignmentOptions.Left);
        UIControls.Divider(column);

        // The body from the top, wrapping; a body longer than the box ends in an ellipsis rather
        // than running over the choices.
        TextMeshProUGUI body = WorldPanelBuilder.Text(column, "", BodySize, FontStyles.Normal, UIStyle.Text, BodyHeight,
                                                      TextAlignmentOptions.TopLeft);
        body.overflowMode = TextOverflowModes.Ellipsis;
        so.FindProperty("body").objectReferenceValue = body;

        Options(column, screen, so);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        so.ApplyModifiedProperties();
        return screen;
    }

    /// MaxOptions full-width buttons, one under the other (an offer's side names are long), the
    /// first the pink call-to-action; MessageScreen shows as many as the message has.
    private static void Options(Transform column, MessageScreen screen, SerializedObject so)
    {
        Transform stack = WorldPanelBuilder.Column(column, true, UIStyle.Unit);
        stack.name = "Options";
        SerializedProperty buttons = so.FindProperty("options");
        SerializedProperty labels = so.FindProperty("optionLabels");
        buttons.arraySize = labels.arraySize = MessageScreen.MaxOptions;
        for (int i = 0; i < MessageScreen.MaxOptions; i++)
        {
            Button b = IntButton(stack, "OK", screen.OnOption, i, i == 0, RowHeight, 0f);
            TextMeshProUGUI label = UIControls.LabelOf(b);
            label.enableAutoSizing = true;
            label.fontSizeMin = UIStyle.HintSize;
            label.fontSizeMax = label.fontSize;
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = b;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
        }
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// UIControls.ActionButton's look (`primary`: the pink call-to-action) with a saved listener
    /// that passes `argument`. `width` 0 = flexible.
    private static Button IntButton(Transform parent, string label, UnityAction<int> onClick, int argument, bool primary,
                                    float height, float width)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
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
        button.colors = primary ? UIStyle.MenuColors(true) : UIStyle.Colors(false);
        WorldPanelBuilder.NoNavigation(button);
        UnityEventTools.AddIntPersistentListener(button.onClick, onClick, argument);
        WorldPanelBuilder.FillLabel(go.transform, label, primary ? UIStyle.ButtonSize + 4f : UIStyle.ButtonSize, UIStyle.Text);
        return button;
    }
}
