using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ratings and Season as floating panels (CareerDeckBuilder):
///
///   Ratings  centre: the seven ratings and the overall, one per line, each over its bar (what the
///            last balls measured); right: the coach's verdict, the trial's ratings, what the
///            ratings are and are not, and Records.
///   Season   centre: the league table; right: your fixtures, round by round - both at once, where
///            the card had them behind a Table | Fixtures switch.
/// </summary>
public static class CareerDeckStats
{
    private const float ListRow = 44f, ListGap = 4f, FixturesWidth = 640f;

    public static MenuScreen BuildRatings(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "CareerRatingsScreen");
        CareerRatingsScreen screen = go.AddComponent<CareerRatingsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerDeckScreen.RatingsTab);
        var so = new SerializedObject(screen);

        Transform main = CareerDeckBuilder.Panel(go, "Ratings", CareerDeckBuilder.Slot.Centre);
        CareerDeckBuilder.Heading(main, "Ratings");
        CareerDeckBuilder.Bind(so, "subtitle", CareerDeckBuilder.Body(main, "", UIStyle.TextMuted));
        UIControls.Divider(main);
        int cells = CareerHubScreen.Cells;
        SerializedProperty bars = so.FindProperty("bars"), values = so.FindProperty("values");
        bars.arraySize = values.arraySize = cells;
        for (int i = 0; i < cells; i++)
        {
            bool overall = i >= RatingSet.Count;
            if (overall)
                UIControls.Divider(main);
            CareerMenuBuilder2.RatingCell(main, overall ? "Overall" : RatingSet.Names[i], overall, out Image bar, out TextMeshProUGUI value);
            bars.GetArrayElementAtIndex(i).objectReferenceValue = bar;
            values.GetArrayElementAtIndex(i).objectReferenceValue = value;
        }

        Transform side = CareerDeckBuilder.Panel(go, "Coach", CareerDeckBuilder.Slot.Right);
        CareerDeckBuilder.Heading(side, "Coach");
        TextMeshProUGUI verdict = CareerDeckBuilder.Body(side, "", UIStyle.Text, 150f);
        verdict.alignment = TextAlignmentOptions.TopLeft;
        CareerDeckBuilder.Bind(so, "verdict", verdict);
        CareerDeckBuilder.Bind(so, "trial", CareerDeckBuilder.Body(side, "", UIStyle.TextMuted, 64f, UIStyle.HintSize + 2f));
        CareerDeckBuilder.Body(side, "Your ratings describe how you bat. They never change how the game plays.", UIStyle.TextMuted, 64f,
                               UIStyle.HintSize + 2f);
        UIControls.ActionButton(side, "Records", screen.OnRecords, false);

        CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.RatingsTab);
        so.ApplyModifiedProperties();
        return screen;
    }

    public static MenuScreen BuildSeason(Transform card)
    {
        GameObject go = CareerDeckBuilder.Screen(card, "SeasonScreen");
        SeasonScreen screen = go.AddComponent<SeasonScreen>();
        MenuPrefabBuilder.SetScreenName(screen, CareerDeckScreen.SeasonTab);
        var so = new SerializedObject(screen);

        Transform table = CareerDeckBuilder.Panel(go, "Table", CareerDeckBuilder.Slot.Centre);
        CareerDeckBuilder.Heading(table, "League table");
        CareerDeckBuilder.Bind(so, "subtitle", CareerDeckBuilder.Body(table, "", UIStyle.TextMuted));
        UIControls.Divider(table);
        CareerDeckBuilder.Bind(so, "tablePanel", List(table, SeasonScreen.TableHeader(), so.FindProperty("tableRows")).gameObject);

        Transform fixtures = CareerDeckBuilder.Panel(go, "Fixtures", CareerDeckBuilder.Slot.Right, FixturesWidth);
        CareerDeckBuilder.Heading(fixtures, "Your fixtures");
        UIControls.Divider(fixtures);
        CareerDeckBuilder.Bind(so, "fixturesPanel", List(fixtures, SeasonScreen.FixturesHeader(), so.FindProperty("fixtureRows")).gameObject);

        CareerDeckBuilder.TabBar(go, screen, CareerDeckScreen.SeasonTab);
        so.ApplyModifiedProperties();
        return screen;
    }

    /// A muted header line over SeasonScreen.Rows rows, put into `rows`.
    private static Transform List(Transform panel, string header, SerializedProperty rows)
    {
        Transform list = WorldPanelBuilder.Column(panel, true, ListGap);
        WorldPanelBuilder.Line(list, header, UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.TextMuted, 32f, TextAlignmentOptions.Left);
        rows.arraySize = SeasonScreen.Rows;
        for (int i = 0; i < SeasonScreen.Rows; i++)
            rows.GetArrayElementAtIndex(i).objectReferenceValue =
                WorldPanelBuilder.Line(list, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, ListRow, TextAlignmentOptions.Left);
        return list;
    }
}
