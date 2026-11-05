using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career screens around matches, built by MenuPrefabBuilder (Tools > CricketVR > Build Menu
/// Prefabs) beside CareerMenuBuilder's slots and Create Player, in the same house style:
///   * CareerHubScreen ("careerHub") - the player card, the overall and form chips, the next
///     match, Play match (the nets trial first), and Ratings, Season and Nets.
///   * CareerRatingsScreen ("careerRatings") - the measured ratings, the coach, the trial's.
///   * SeasonScreen ("careerSeason") - the league table and the side's fixtures.
///   * CareerResultScreen ("careerResult") - how the career match just played went.
/// There is no scroll view, so every screen is kept to what fits on the card. Its own file only to
/// keep CareerMenuBuilder under the 500-line cap; the helpers are CareerMenuBuilder's.
/// </summary>
public static class CareerMenuBuilder2
{
    private const float ChipHeight = 48f;
    private const float ListRow = 40f;
    private const float ListGap = 4f;

    // ---- Career hub -------------------------------------------------------------------------------

    public static MenuScreen BuildHub(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerHubScreen", out GameObject go);
        CareerHubScreen screen = go.AddComponent<CareerHubScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerHub");
        var so = new SerializedObject(screen);

        PlayerCard(column, so);
        Transform chips = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        so.FindProperty("overallChip").objectReferenceValue = Chip(chips, "Overall  --");
        so.FindProperty("formChip").objectReferenceValue = Chip(chips, "Form  --");
        UIControls.Divider(column);

        WorldPanelBuilder.Section(column, "Next match");
        NextMatch(column, so);
        TextMeshProUGUI banner = WorldPanelBuilder.Line(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, 34f,
                                                        TextAlignmentOptions.Center);
        banner.gameObject.SetActive(false);

        Button primary = UIControls.ActionButton(column, $"Take your nets trial · {NetsSession.TrialBalls} balls", screen.OnPrimary, true);
        primary.colors = UIStyle.MenuColors(true);
        // The trial's label is the longest on the card: let it shrink a little rather than clip.
        TextMeshProUGUI primaryLabel = UIControls.LabelOf(primary);
        primaryLabel.enableAutoSizing = true;
        primaryLabel.fontSizeMin = UIStyle.HintSize;
        primaryLabel.fontSizeMax = UIStyle.ButtonSize + 4f;
        TextMeshProUGUI locked = WorldPanelBuilder.Line(column, "The trial sets your ratings before your first match.",
                                                        UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f,
                                                        TextAlignmentOptions.Center);

        // Equal thirds.
        Transform more = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(more, "Ratings", screen.OnRatings, false);
        UIControls.ActionButton(more, "Season", screen.OnSeason, false);
        Button nets = UIControls.ActionButton(more, "Nets", screen.OnNets, false);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        so.FindProperty("banner").objectReferenceValue = banner;
        so.FindProperty("primary").objectReferenceValue = primary;
        so.FindProperty("primaryLabel").objectReferenceValue = primaryLabel;
        so.FindProperty("locked").objectReferenceValue = locked;
        so.FindProperty("nets").objectReferenceValue = nets;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// The kit-colour bar, the name over "#18 · Nation · hand", and the rung and season on the right.
    private static void PlayerCard(Transform column, SerializedObject so)
    {
        Transform card = CareerMenuBuilder.Row(column, UIStyle.Gap);
        Image kitBar = CareerMenuBuilder.Swatch(card, 12f, 84f, 2f);
        Transform who = WorldPanelBuilder.Column(card, true, 2f);
        who.GetComponent<LayoutElement>().preferredWidth = 0f;
        TextMeshProUGUI name = WorldPanelBuilder.Line(who, "Player", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 52f,
                                                      TextAlignmentOptions.Left);
        TextMeshProUGUI detail = WorldPanelBuilder.Line(who, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.TextMuted, 30f,
                                                        TextAlignmentOptions.Left);
        Transform where = WorldPanelBuilder.FixedColumn(card, 200f, 2f);
        TextMeshProUGUI stage = WorldPanelBuilder.Line(where, "Club cricket", UIStyle.LabelSize, FontStyles.Bold, UIStyle.AccentText,
                                                       34f, TextAlignmentOptions.Right);
        TextMeshProUGUI season = WorldPanelBuilder.Line(where, "Season 1", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted,
                                                        28f, TextAlignmentOptions.Right);

        so.FindProperty("playerName").objectReferenceValue = name;
        so.FindProperty("playerDetail").objectReferenceValue = detail;
        so.FindProperty("kitBar").objectReferenceValue = kitBar;
        so.FindProperty("stage").objectReferenceValue = stage;
        so.FindProperty("season").objectReferenceValue = season;
    }

    /// "vs Old Mill CC" bold, the round and format muted, "You bat at 7" in the accent - in a dark
    /// inset tile, so the match reads as one thing.
    private static void NextMatch(Transform column, SerializedObject so)
    {
        Transform tile = Tile(column);
        so.FindProperty("nextTitle").objectReferenceValue =
            WorldPanelBuilder.Line(tile, "", UIStyle.LabelSize + 2f, FontStyles.Bold, UIStyle.Text, 36f, TextAlignmentOptions.Left);
        so.FindProperty("nextDetail").objectReferenceValue =
            WorldPanelBuilder.Line(tile, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.TextMuted, 30f, TextAlignmentOptions.Left);
        so.FindProperty("nextSlot").objectReferenceValue =
            WorldPanelBuilder.Line(tile, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.AccentText, 30f, TextAlignmentOptions.Left);
    }

    /// A rounded dark chip holding one line ("Overall 54 Solid"), sharing its row equally.
    private static TextMeshProUGUI Chip(Transform row, string text)
    {
        var go = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(row, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = ChipHeight;
        layout.flexibleWidth = 1f;
        Image well = go.GetComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;
        TextMeshProUGUI label = WorldPanelBuilder.FillLabel(go.transform, text, UIStyle.LabelSize - 2f, UIStyle.Text);
        label.fontStyle = FontStyles.Normal;   // the number is bold in the text itself
        return label;
    }

    /// A dark inset column with padding (the next match).
    private static Transform Tile(Transform column)
    {
        Transform tile = WorldPanelBuilder.Column(column, true, 2f);
        tile.name = "Tile";
        tile.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(20, 20, 12, 12);
        Image well = tile.gameObject.AddComponent<Image>();
        UIStyle.RoundedImage(well, UIStyle.Well, UIStyle.ControlCorners);
        well.raycastTarget = false;
        return tile;
    }

    // ---- Ratings ----------------------------------------------------------------------------------

    public static MenuScreen BuildRatings(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerRatingsScreen", out GameObject go);
        CareerRatingsScreen screen = go.AddComponent<CareerRatingsScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerRatings");
        var so = new SerializedObject(screen);

        TextMeshProUGUI subtitle = TitleWithLine(column, "Ratings");
        RatingsGrid(column, so);
        TextMeshProUGUI verdict = WorldPanelBuilder.Text(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, 64f,
                                                         TextAlignmentOptions.Left);
        TextMeshProUGUI trial = WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f,
                                                       TextAlignmentOptions.Left);
        WorldPanelBuilder.Line(column, "Your ratings describe how you bat. They never change how the game plays.",
                               UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f, TextAlignmentOptions.Left);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        so.FindProperty("subtitle").objectReferenceValue = subtitle;
        so.FindProperty("verdict").objectReferenceValue = verdict;
        so.FindProperty("trial").objectReferenceValue = trial;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// Two columns of four: Timing, Power, vs Pace, vs Spin | Defence, Judgement, Temperament,
    /// Overall - the cells in CareerRatingsScreen's order (RatingSet's, then the overall).
    private static void RatingsGrid(Transform column, SerializedObject so)
    {
        Transform grid = WorldPanelBuilder.Column(column, false, UIStyle.SectionGap);
        Transform left = WorldPanelBuilder.Column(grid, true, 10f);
        Transform right = WorldPanelBuilder.Column(grid, true, 10f);
        // Equal halves, whatever the labels' widths.
        left.GetComponent<LayoutElement>().preferredWidth = 0f;
        right.GetComponent<LayoutElement>().preferredWidth = 0f;

        int cells = CareerHubScreen.Cells, half = (cells + 1) / 2;
        SerializedProperty bars = so.FindProperty("bars");
        SerializedProperty values = so.FindProperty("values");
        bars.arraySize = cells;
        values.arraySize = cells;
        for (int i = 0; i < cells; i++)
        {
            bool overall = i >= RatingSet.Count;
            string label = overall ? "Overall" : RatingSet.Names[i];
            RatingCell(i < half ? left : right, label, overall, out Image bar, out TextMeshProUGUI value);
            bars.GetArrayElementAtIndex(i).objectReferenceValue = bar;
            values.GetArrayElementAtIndex(i).objectReferenceValue = value;
        }
    }

    /// "Label ......... 54 Solid" over a thin bar whose fill CareerRatingsScreen sizes to rating / 99.
    private static void RatingCell(Transform parent, string label, bool bold, out Image fill, out TextMeshProUGUI value)
    {
        Transform cell = WorldPanelBuilder.Column(parent, true, 6f);
        Transform line = CareerMenuBuilder.Row(cell, UIStyle.Unit);
        WorldPanelBuilder.Line(line, label, UIStyle.LabelSize - 2f, bold ? FontStyles.Bold : FontStyles.Normal, UIStyle.Text,
                               UIStyle.LabelRow, TextAlignmentOptions.Left);
        value = WorldPanelBuilder.Line(line, "--", UIStyle.ValueSize - 2f, FontStyles.Normal, UIStyle.Text, UIStyle.LabelRow,
                                       TextAlignmentOptions.Right, 170f);

        var track = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        track.transform.SetParent(cell, false);
        LayoutElement layout = track.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 8f;
        layout.flexibleWidth = 1f;
        Image back = track.GetComponent<Image>();
        UIStyle.PillImage(back, UIStyle.Track);
        back.raycastTarget = false;
        fill = WorldPanelBuilder.Overlay(track.transform, "Fill");
        UIStyle.PillImage(fill, UIStyle.Accent);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.enabled = false;
    }

    // ---- Season -----------------------------------------------------------------------------------

    public static MenuScreen BuildSeason(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "SeasonScreen", out GameObject go);
        SeasonScreen screen = go.AddComponent<SeasonScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerSeason");
        var so = new SerializedObject(screen);

        TextMeshProUGUI subtitle = TitleWithLine(column, "Season");
        SegmentedControl view = UIControls.Segmented(column, new[] { "Table", "Fixtures" }, screen.OnView);
        // Both lists are a header and SeasonScreen.Rows rows, so the card keeps its height as the
        // view changes.
        Transform table = List(column, "Table", SeasonScreen.TableHeader(), so.FindProperty("tableRows"));
        Transform fixtures = List(column, "Fixtures", SeasonScreen.FixturesHeader(), so.FindProperty("fixtureRows"));
        fixtures.gameObject.SetActive(false);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        so.FindProperty("subtitle").objectReferenceValue = subtitle;
        so.FindProperty("view").objectReferenceValue = view;
        so.FindProperty("tablePanel").objectReferenceValue = table.gameObject;
        so.FindProperty("fixturesPanel").objectReferenceValue = fixtures.gameObject;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// A muted header line over SeasonScreen.Rows single-line rows, put into `rows`.
    private static Transform List(Transform column, string name, string header, SerializedProperty rows)
    {
        Transform list = WorldPanelBuilder.Column(column, true, ListGap);
        list.name = name;
        WorldPanelBuilder.Line(list, header, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f, TextAlignmentOptions.Left);
        rows.arraySize = SeasonScreen.Rows;
        for (int i = 0; i < SeasonScreen.Rows; i++)
            rows.GetArrayElementAtIndex(i).objectReferenceValue =
                WorldPanelBuilder.Line(list, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, ListRow,
                                       TextAlignmentOptions.Left);
        return list;
    }

    // ---- Match result -----------------------------------------------------------------------------

    public static MenuScreen BuildResult(Transform parent)
    {
        Transform column = MenuPrefabBuilder.ScreenColumn(parent, "CareerResultScreen", out GameObject go);
        CareerResultScreen screen = go.AddComponent<CareerResultScreen>();
        MenuPrefabBuilder.SetScreenName(screen, "careerResult");
        var so = new SerializedObject(screen);

        so.FindProperty("headline").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.TitleSize + 4f, FontStyles.Bold, UIStyle.Text, 60f, TextAlignmentOptions.Center);
        so.FindProperty("scores").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, 34f, TextAlignmentOptions.Center);
        UIControls.Divider(column);

        WorldPanelBuilder.Section(column, "Your innings");
        so.FindProperty("innings").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", 64f, FontStyles.Bold, UIStyle.Text, 76f, TextAlignmentOptions.Center);
        so.FindProperty("inningsDetail").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.TextMuted, 32f,
                                   TextAlignmentOptions.Center);
        UIControls.Divider(column);

        so.FindProperty("ratings").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, 34f, TextAlignmentOptions.Center);
        so.FindProperty("position").objectReferenceValue =
            WorldPanelBuilder.Line(column, "", UIStyle.LabelSize - 2f, FontStyles.Normal, UIStyle.Text, 34f, TextAlignmentOptions.Center);

        UIControls.Divider(column);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Season", screen.OnSeason, false);
        Button next = UIControls.ActionButton(actions, "Continue", screen.OnContinue, true);
        next.colors = UIStyle.MenuColors(true);
        so.ApplyModifiedProperties();
        return screen;
    }

    // ---- Helpers ----------------------------------------------------------------------------------

    /// MenuPrefabBuilder.Title's look with the subtitle left empty for the screen to fill in.
    private static TextMeshProUGUI TitleWithLine(Transform column, string title)
    {
        WorldPanelBuilder.Text(column, title, UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 58f);
        TextMeshProUGUI subtitle = WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f,
                                                          TextAlignmentOptions.Center);
        UIControls.Divider(column);
        return subtitle;
    }
}
