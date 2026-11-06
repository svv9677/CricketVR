using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career screens around matches, built by MenuPrefabBuilder (Tools > CricketVR > Build Menu
/// Prefabs) beside CareerMenuBuilder's slots and Create Player, in the same house style:
///   * CareerHubScreen ("careerHub") - the player card, the overall and form chips, the next
///     match, Play match (the nets trial first), Ratings, Season, Nets and Inbox, and the partner's
///     innings toggle (Watch | Skip).
///   * CareerRatingsScreen ("careerRatings") - the measured ratings, the coach, the trial's.
///   * SeasonScreen ("careerSeason") - the league table and the side's fixtures.
///   * CareerResultScreen ("careerResult") - how the career match just played went.
/// The hub, ratings and season are floating panels since 2026-11-06 (CareerDeckHub, CareerDeckStats);
/// their entry points stay here so MenuPrefabBuilder's list of screens is unchanged.
/// </summary>
public static class CareerMenuBuilder2
{

    // ---- Career hub -------------------------------------------------------------------------------

    /// The hub as floating panels since 2026-11-06 (CareerDeckHub).
    public static MenuScreen BuildHub(Transform parent) => CareerDeckHub.Build(parent);

    // ---- Ratings ----------------------------------------------------------------------------------

    /// Ratings as floating panels since 2026-11-06 (CareerDeckStats).
    public static MenuScreen BuildRatings(Transform parent) => CareerDeckStats.BuildRatings(parent);

    /// "Label ......... 54 Solid" over a thin bar whose fill CareerRatingsScreen sizes to rating / 99.
    internal static void RatingCell(Transform parent, string label, bool bold, out Image fill, out TextMeshProUGUI value)
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

    /// Season as floating panels since 2026-11-06 (CareerDeckStats).
    public static MenuScreen BuildSeason(Transform parent) => CareerDeckStats.BuildSeason(parent);

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
        // Phase 7: records broken, a coach's goal reached - each hidden unless there is one.
        TextMeshProUGUI best = WorldPanelBuilder.Line(column, "", UIStyle.HintSize + 2f, FontStyles.Bold, UIStyle.AccentText, 30f, TextAlignmentOptions.Center);
        best.gameObject.SetActive(false);
        so.FindProperty("best").objectReferenceValue = best;
        TextMeshProUGUI goal = WorldPanelBuilder.Line(column, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.Text, 30f, TextAlignmentOptions.Center);
        goal.gameObject.SetActive(false);
        so.FindProperty("goal").objectReferenceValue = goal;
        // The scout's report, three lines at most (a scout's runs to about 160 characters, its
        // verdict last), shown only when one was watching.
        TextMeshProUGUI scout = WorldPanelBuilder.Text(column, "", UIStyle.HintSize + 2f, FontStyles.Normal, UIStyle.Text, 84f);
        scout.overflowMode = TextOverflowModes.Ellipsis;
        scout.gameObject.SetActive(false);
        so.FindProperty("scout").objectReferenceValue = scout;

        UIControls.Divider(column);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Season", screen.OnSeason, false);
        Button next = UIControls.ActionButton(actions, "Continue", screen.OnContinue, true);
        next.colors = UIStyle.MenuColors(true);
        so.ApplyModifiedProperties();
        return screen;
    }
}
