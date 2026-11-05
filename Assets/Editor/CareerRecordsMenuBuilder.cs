using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career records screen, CareerRecordsScreen ("careerRecords", from Ratings' Records button),
/// built by MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs) in the drills screen's shape
/// so the card is no taller than it: the title, a 22 px header line (the career figures), six rows
/// of 56 px with 8 px gaps - each a bold name and value over a muted detail line, Watch on the
/// Longest six row only - the note on how a six is measured, a divider and Back. The screen fills
/// every line on show. No scroll view.
/// </summary>
public static class CareerRecordsMenuBuilder
{
    private const float HeaderHeight = 22f;
    private const float RowHeight = UIStyle.SliderRow;   // 56: the laser's minimum
    private const float RowGap = UIStyle.Unit;
    private const float NameHeight = 30f;
    private const float DetailHeight = 26f;
    private const float NoteHeight = 28f;
    private const float WatchWidth = 140f;
    /// The Longest six row (CareerRecordsScreen.RecordRows' order).
    private const int SixRow = 4;

    public static MenuScreen BuildRecords(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerRecordsScreen", out GameObject go);
        CareerRecordsScreen screen = go.AddComponent<CareerRecordsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerRecords");
        var so = new SerializedObject(screen);

        MenuPrefabBuilder.Title(column, "Records", "Your career bests.");
        so.FindProperty("header").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.AccentText, HeaderHeight,
                                   TextAlignmentOptions.Center);

        Transform list = WorldPanelBuilder.Column(column, true, RowGap);
        list.name = "Rows";
        Rows(list, screen, so);

        WorldPanelBuilder.Line(column, CareerRecordsScreen.NoteText, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted,
                               NoteHeight, TextAlignmentOptions.Center);
        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);
        so.ApplyModifiedProperties();
        return screen;
    }

    /// CareerRecordsScreen.Rows rows, named now (the screen fills the values on show); Watch on the
    /// Longest six row.
    private static void Rows(Transform list, CareerRecordsScreen screen, SerializedObject so)
    {
        int n = CareerRecordsScreen.Rows;
        SerializedProperty names = so.FindProperty("names");
        SerializedProperty details = so.FindProperty("details");
        names.arraySize = details.arraySize = n;
        for (int i = 0; i < n; i++)
        {
            Transform row = Row(list, $"Row{i + 1}", CareerRecordsScreen.NameLine(i, null), out TextMeshProUGUI name,
                                out TextMeshProUGUI detail);
            names.GetArrayElementAtIndex(i).objectReferenceValue = name;
            details.GetArrayElementAtIndex(i).objectReferenceValue = detail;
            if (i != SixRow)
                continue;
            Button watch = UIControls.ActionButton(row, "Watch", screen.OnWatch, false, RowHeight, WatchWidth);
            watch.GetComponent<LayoutElement>().minWidth = WatchWidth;
            watch.interactable = false;   // until the screen finds the clip
            so.FindProperty("watch").objectReferenceValue = watch;
        }
    }

    /// One row: the name and value over a muted detail line, taking what is left of the width.
    private static Transform Row(Transform list, string rowName, string label, out TextMeshProUGUI name,
                                 out TextMeshProUGUI detail)
    {
        Transform row = CareerMenuBuilder.Row(list, UIStyle.Gap);
        row.name = rowName;
        Transform words = WorldPanelBuilder.Column(row, true, 0f);
        words.GetComponent<LayoutElement>().preferredWidth = 0f;
        name = WorldPanelBuilder.Line(words, label, UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, NameHeight,
                                      TextAlignmentOptions.Left);
        detail = WorldPanelBuilder.Line(words, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, DetailHeight,
                                        TextAlignmentOptions.Left);
        return row;
    }
}
