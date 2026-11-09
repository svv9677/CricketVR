using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The coach's goals screen, CareerGoalsScreen ("careerGoals", from the hub), built by
/// MenuPrefabBuilder (Tools > CricketVR > Build Menu Prefabs): the title over a subtitle the screen
/// fills (the season), the "why there are none" line, three goal rows in the ratings screen's cell
/// (CareerMenuBuilder2.RatingCell: the goal, its value, a bar), the career's count of goals reached,
/// then Back | Locker. About 520 px tall with every row showing, inside the 640 px budget.
/// </summary>
public static class CareerGoalsMenuBuilder
{
    public static MenuScreen BuildGoals(Transform parent)
    {
        Transform column = CareerDeckBuilder.CentreScreen(parent, "CareerGoalsScreen", out GameObject go);
        CareerGoalsScreen screen = go.AddComponent<CareerGoalsScreen>();
        CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.GoalsTab);
        MenuPrefabBuilder.SetScreenName(screen, "careerGoals");
        var so = new SerializedObject(screen);

        WorldPanelBuilder.Text(column, "Coach's goals", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 58f);
        TextMeshProUGUI subtitle = WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f,
                                                          TextAlignmentOptions.Center);
        UIControls.Divider(column);

        TextMeshProUGUI empty = WorldPanelBuilder.Text(column, CoachGoalsText.NoTrial, UIStyle.LabelSize - 2f, FontStyles.Normal,
                                                       UIStyle.TextMuted, 60f, TextAlignmentOptions.Center);
        empty.gameObject.SetActive(false);
        GoalRows(column, so);
        TextMeshProUGUI career = WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f,
                                                        TextAlignmentOptions.Left);

        UIControls.Divider(column);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Back", screen.OnBack, false);
        UIControls.ActionButton(actions, "Locker", screen.OnLocker, false);

        so.FindProperty("subtitle").objectReferenceValue = subtitle;
        so.FindProperty("empty").objectReferenceValue = empty;
        so.FindProperty("career").objectReferenceValue = career;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// CareerGoalsScreen.Rows cells, 20 px apart. RatingCell keeps its label to itself, so it is
    /// found where RatingCell puts it: the cell's first child (the line), whose first child it is.
    private static void GoalRows(Transform column, SerializedObject so)
    {
        Transform list = WorldPanelBuilder.Column(column, true, 20f);
        list.name = "Goals";
        SerializedProperty rows = so.FindProperty("rows"), labels = so.FindProperty("labels");
        SerializedProperty values = so.FindProperty("values"), bars = so.FindProperty("bars");
        rows.arraySize = labels.arraySize = values.arraySize = bars.arraySize = CareerGoalsScreen.Rows;
        for (int i = 0; i < CareerGoalsScreen.Rows; i++)
        {
            CareerMenuBuilder2.RatingCell(list, "", false, out Image bar, out TextMeshProUGUI value);
            Transform cell = bar.transform.parent.parent;
            cell.name = $"Goal {i + 1}";
            TextMeshProUGUI label = cell.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>();
            rows.GetArrayElementAtIndex(i).objectReferenceValue = cell.gameObject;
            labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
            values.GetArrayElementAtIndex(i).objectReferenceValue = value;
            bars.GetArrayElementAtIndex(i).objectReferenceValue = bar;
        }
    }
}
