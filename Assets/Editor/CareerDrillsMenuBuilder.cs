using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The career nets screen, CareerDrillsScreen ("careerDrills", from the hub's Nets button), built
/// by MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs) beside the other career screens in
/// the same house style: the title, the header line (training slot, coach's pick), then six rows -
/// Measured practice and CareerDrillsScreen.Rows drills - each a name over a muted detail line with
/// a Start button on the right, the locked line, and Back. The drills' Start buttons share
/// CareerDrillsScreen.OnStart, so each is wired with a saved listener that carries its row. There
/// is no scroll view: the rows are the laser's minimum height so the card stays about the hub's.
/// </summary>
public static class CareerDrillsMenuBuilder
{
    private const float RowHeight = UIStyle.SliderRow;   // 56: the laser's minimum
    private const float RowGap = UIStyle.Unit;
    private const float NameHeight = 30f;
    private const float DetailHeight = 26f;
    private const float StartWidth = 140f;

    public static MenuScreen BuildDrills(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerDrillsScreen", out GameObject go);
        CareerDrillsScreen screen = go.AddComponent<CareerDrillsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerDrills");
        var so = new SerializedObject(screen);

        MenuPrefabBuilder.Title(column, "Nets", "Measured practice, or a 12-ball drill the coach scores.");
        so.FindProperty("header").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.AccentText, 30f,
                                   TextAlignmentOptions.Center);

        Transform list = WorldPanelBuilder.Column(column, true, RowGap);
        list.name = "Rows";
        Transform practiceRow = Row(list, "Practice", $"<b>{CareerDrillsScreen.PracticeName}</b>",
                                    CareerDrillsScreen.PracticeDetail, out _, out _);
        Button practice = UIControls.ActionButton(practiceRow, "Start", screen.OnPractice, false, RowHeight, StartWidth);
        practice.GetComponent<LayoutElement>().minWidth = StartWidth;
        UIControls.Divider(list);
        DrillRows(list, screen, so);

        TextMeshProUGUI locked = WorldPanelBuilder.Line(column, CareerDrillsScreen.LockedText, UIStyle.HintSize, FontStyles.Normal,
                                                        UIStyle.TextMuted, 28f, TextAlignmentOptions.Center);
        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        so.FindProperty("locked").objectReferenceValue = locked;
        so.FindProperty("practice").objectReferenceValue = practice;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// CareerDrillsScreen.Rows drill rows, named from DrillDefs.All where it has them (the screen
    /// fills them in again on show), each Start wired to OnStart with its index.
    private static void DrillRows(Transform list, CareerDrillsScreen screen, SerializedObject so)
    {
        int n = CareerDrillsScreen.Rows;
        SerializedProperty names = so.FindProperty("names");
        SerializedProperty details = so.FindProperty("details");
        SerializedProperty starts = so.FindProperty("starts");
        SerializedProperty rows = so.FindProperty("rows");
        names.arraySize = details.arraySize = starts.arraySize = rows.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            DrillDef d = CareerDrillsScreen.DrillAt(i);
            string label = d != null ? CareerDrillsScreen.RowName(d, false) : $"Drill {i + 1}";
            Transform row = Row(list, $"Drill{i + 1}", label, "", out TextMeshProUGUI name, out TextMeshProUGUI detail);
            Button start = IntButton(row, "Start", screen.OnStart, i, RowHeight, StartWidth);
            names.GetArrayElementAtIndex(i).objectReferenceValue = name;
            details.GetArrayElementAtIndex(i).objectReferenceValue = detail;
            starts.GetArrayElementAtIndex(i).objectReferenceValue = start;
            rows.GetArrayElementAtIndex(i).objectReferenceValue = row.gameObject;
        }
    }

    /// One row: the name over a muted detail line, taking what is left; the caller puts its Start
    /// button on the right.
    private static Transform Row(Transform list, string rowName, string label, string detailText,
                                 out TextMeshProUGUI name, out TextMeshProUGUI detail)
    {
        Transform row = CareerMenuBuilder.Row(list, UIStyle.Gap);
        row.name = rowName;
        Transform words = WorldPanelBuilder.Column(row, true, 0f);
        words.GetComponent<LayoutElement>().preferredWidth = 0f;
        name = WorldPanelBuilder.Line(words, label, UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, NameHeight,
                                      TextAlignmentOptions.Left);
        detail = WorldPanelBuilder.Line(words, detailText, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, DetailHeight,
                                        TextAlignmentOptions.Left);
        return row;
    }

    /// UIControls.ActionButton's look (surface colour, rounded, no navigation) with a saved listener
    /// that passes `argument` (the row). A fixed width that is also the minimum, so a long name
    /// squeezes its text rather than the button.
    private static Button IntButton(Transform parent, string label, UnityAction<int> onClick, int argument, float height,
                                    float width)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        layout.minWidth = layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        Image image = go.GetComponent<Image>();
        UIStyle.RoundedImage(image, Color.white, UIStyle.ControlCorners);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = UIStyle.Colors(false);
        WorldPanelBuilder.NoNavigation(button);
        UnityEventTools.AddIntPersistentListener(button.onClick, onClick, argument);
        WorldPanelBuilder.FillLabel(go.transform, label, UIStyle.ButtonSize, UIStyle.Text);
        return button;
    }
}
