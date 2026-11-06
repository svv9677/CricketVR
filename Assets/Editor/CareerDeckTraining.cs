using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career nets as floating panels (CareerDeckBuilder; CareerDrillsScreen's fields):
///
///   Left    COACH - the training slot and the coach's pick (DrillText.Header), Measured practice
///           with what it is, and the locked line before the trial.
///   Centre  DRILLS - every drill as a tile in two columns: its name (a pink PICK on the coach's)
///           over its best. A tile chooses the drill.
///   Right   the chosen drill - its name, the coach's pick badge, what it is, what it feeds, last and
///           best, and Start.
///   Top     the tabs, Training lit.
/// </summary>
public static class CareerDeckTraining
{
    private const float TileHeight = 92f, TileNameHeight = 34f, TileDetailHeight = 26f;

    public static MenuScreen Build(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "CareerDrillsScreen");
        CareerDrillsScreen screen = go.AddComponent<CareerDrillsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerDeckScreen.Training);
        var so = new SerializedObject(screen);

        Coach(CareerDeckBuilder.Panel(go, "Coach", CareerDeckBuilder.Slot.Left), screen, so);
        Tiles(CareerDeckBuilder.Panel(go, "Drills", CareerDeckBuilder.Slot.Centre), screen, so);
        Chosen(CareerDeckBuilder.Panel(go, "Chosen Drill", CareerDeckBuilder.Slot.Right), screen, so);
        CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.Training);
        so.ApplyModifiedProperties();
        return screen;
    }

    private static void Coach(Transform panel, CareerDrillsScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Coach");
        TextMeshProUGUI header = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 110f);
        header.alignment = TextAlignmentOptions.TopLeft;
        CareerDeckBuilder.Bind(so, "header", header);
        UIControls.Divider(panel);
        CareerDeckBuilder.Heading(panel, "Free practice");
        CareerDeckBuilder.Body(panel, CareerDrillsScreen.PracticeDetail, UIStyle.TextMuted, 64f);
        Button practice = UIControls.ActionButton(panel, CareerDrillsScreen.PracticeName, screen.OnPractice, false);
        CareerDeckBuilder.Bind(so, "practice", practice);
        TextMeshProUGUI locked = CareerDeckBuilder.Body(panel, CareerDrillsScreen.LockedText, UIStyle.MenuSecondary, 64f);
        CareerDeckBuilder.Bind(so, "locked", locked);
    }

    /// The drills in two columns of tiles, each a button wired to OnChoose with its index.
    private static void Tiles(Transform panel, CareerDrillsScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Heading(panel, "Drills", "Pick a drill");
        int n = CareerDrillsScreen.Rows;
        SerializedProperty names = so.FindProperty("names"), details = so.FindProperty("details");
        SerializedProperty starts = so.FindProperty("starts"), rows = so.FindProperty("rows");
        names.arraySize = details.arraySize = starts.arraySize = rows.arraySize = n;
        Transform grid = null;
        for (int i = 0; i < n; i++)
        {
            if (i % 2 == 0)
                grid = WorldPanelBuilder.Column(panel, false, UIStyle.Gap);
            DrillDef d = CareerDrillsScreen.DrillAt(i);
            Button tile = CareerDeckBuilder.IndexButton(grid, d != null ? d.name : "Drill", screen.OnChoose, i, TileHeight);
            Transform inside = WorldPanelBuilder.Column(tile.transform, true, 0f);
            WorldPanelBuilder.Stretch((RectTransform)inside);
            inside.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 16, 12, 10);
            inside.GetComponent<LayoutElement>().ignoreLayout = true;
            TextMeshProUGUI name = WorldPanelBuilder.Line(inside, d != null ? $"<b>{d.name}</b>" : "", UIStyle.LabelSize, FontStyles.Normal,
                                                          UIStyle.Text, TileNameHeight, TextAlignmentOptions.Left);
            name.raycastTarget = false;
            TextMeshProUGUI detail = WorldPanelBuilder.Line(inside, "New", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted,
                                                            TileDetailHeight, TextAlignmentOptions.Left);
            detail.raycastTarget = false;
            names.GetArrayElementAtIndex(i).objectReferenceValue = name;
            details.GetArrayElementAtIndex(i).objectReferenceValue = detail;
            starts.GetArrayElementAtIndex(i).objectReferenceValue = tile;
            rows.GetArrayElementAtIndex(i).objectReferenceValue = tile.gameObject;
        }
    }

    private static void Chosen(Transform panel, CareerDrillsScreen screen, SerializedObject so)
    {
        CareerDeckBuilder.Bind(so, "chosenTitle", CareerDeckBuilder.Heading(panel, "Drill", ""));
        TextMeshProUGUI pick = WorldPanelBuilder.Line(panel, "COACH'S PICK", UIStyle.HintSize, FontStyles.Bold, UIStyle.MenuPrimary, 28f,
                                                      TextAlignmentOptions.Left);
        CareerDeckBuilder.Bind(so, "chosenPick", pick);
        TextMeshProUGUI blurb = CareerDeckBuilder.Body(panel, "", UIStyle.Text, 140f);
        blurb.alignment = TextAlignmentOptions.TopLeft;
        CareerDeckBuilder.Bind(so, "chosenBlurb", blurb);
        TextMeshProUGUI stats = CareerDeckBuilder.Body(panel, "", UIStyle.TextMuted, 110f, UIStyle.HintSize + 2f);
        stats.alignment = TextAlignmentOptions.TopLeft;
        CareerDeckBuilder.Bind(so, "chosenStats", stats);
        Button start = UIControls.ActionButton(panel, "Start drill", screen.OnStartChosen, true, 96f);
        start.colors = UIStyle.MenuColors(true);
        CareerDeckBuilder.Bind(so, "chosenStart", start);
    }
}
