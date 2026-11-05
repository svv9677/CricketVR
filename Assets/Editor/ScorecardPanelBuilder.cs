using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Scorecard Panel. Builds the match's batting card (ScorecardPanel) as
/// Assets/Resources/Prefabs/UI/ScorecardPanel.prefab, in the UIStyle look of the other floating
/// panels, then opens CricketVR.unity and puts one instance of it under "UI", just after the
/// ShotCard it stands in for, and saves the scene. Run again, it rebuilds the prefab and replaces
/// the instance (never a second one). The nets (Nets.unity) are left alone: they keep the
/// last-shot card.
///
/// Read from about 3.5 m, like the shot card, so the same 2 mm per canvas px: 24 px batter lines
/// are 48 mm tall. Eleven lines, the heading and the foot come to about 590 px (1.2 m), under the
/// 700 px the card must stay within to sit beside the batter without towering over him.
/// </summary>
public static class ScorecardPanelBuilder
{
    public const string PrefabPath = "Assets/Resources/Prefabs/UI/ScorecardPanel.prefab";
    private const string ScenePath = "Assets/Scenes/CricketVR.unity";
    private const string RootName = "ScorecardPanel";
    private const float MetresPerPixel = 0.002f;
    public const float Width = 700f;
    public const int Rows = 11;

    // Line sizes (canvas px). The cells beside the name and the dismissal are fixed, so the
    // columns line up from row to row; the dismissal takes the rest.
    public const float RowHeight = 32f, RowGap = 2f, RowFont = 24f;
    private const float NameWidth = 200f, RunsWidth = 56f, BallsWidth = 72f;
    private const float HeadingHeight = 48f, FootHeight = 34f;

    [MenuItem("Tools/CricketVR/Build Scorecard Panel")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        GameObject prefab = BuildPrefab();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform ui = FindRoot(scene, "UI");
        if (ui == null)
        {
            Debug.LogError("[ScorecardPanelBuilder] CricketVR.unity has no UI root: run Tools > CricketVR > Build Player UI first.");
            return;
        }
        Place(prefab, ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[ScorecardPanelBuilder] {PrefabPath} built and placed under UI in CricketVR.unity.");
    }

    /// One instance under `ui`, just after the ShotCard: any earlier one goes first.
    private static void Place(GameObject prefab, Transform ui)
    {
        for (int i = ui.childCount - 1; i >= 0; i--)
            if (ui.GetChild(i).name == RootName)
                Object.DestroyImmediate(ui.GetChild(i).gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ui);
        instance.name = RootName;
        Transform shot = ui.Find("ShotCard");
        if (shot != null)
            instance.transform.SetSiblingIndex(shot.GetSiblingIndex() + 1);
    }

    /// The prefab, built in a temporary object that is gone again afterwards.
    public static GameObject BuildPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        UIStyle.EnsureSprites();
        var root = new GameObject(RootName);
        try
        {
            Fill(root);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Fill(GameObject root)
    {
        var c = root.AddComponent<ScorecardPanel>();
        GameObject panel = WorldPanelBuilder.Canvas(root, Width, MetresPerPixel, false);
        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.Gap);
        WorldPanelBuilder.FitContent(panel, false);
        Transform t = panel.transform;

        Transform heading = Line(t, HeadingHeight, UIStyle.Gap);
        TextMeshProUGUI team = Fit(WorldPanelBuilder.Line(heading, "Batting", UIStyle.LabelSize, FontStyles.Bold | FontStyles.UpperCase,
                                                          UIStyle.Text, HeadingHeight, TextAlignmentOptions.Left), 0f);
        team.characterSpacing = 4f;
        TextMeshProUGUI score = Fit(WorldPanelBuilder.Line(heading, "0/0", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text,
                                                           HeadingHeight, TextAlignmentOptions.Right), 150f);   // "142/10"
        TextMeshProUGUI overs = Fit(WorldPanelBuilder.Line(heading, "0.0 ov", UIStyle.LabelSize, FontStyles.Normal, UIStyle.TextMuted,
                                                           HeadingHeight, TextAlignmentOptions.Right), 100f);
        UIControls.Divider(t);

        Transform list = WorldPanelBuilder.Column(t, true, RowGap);
        var views = new ScorecardPanel.RowView[Rows];
        for (int i = 0; i < Rows; i++)
            views[i] = Row(list, i);
        UIControls.Divider(t);

        Transform foot = Line(t, FootHeight, UIStyle.Gap);
        TextMeshProUGUI extras = Fit(WorldPanelBuilder.Line(foot, "Extras 0", UIStyle.LabelSize, FontStyles.Normal, UIStyle.TextMuted,
                                                            FootHeight, TextAlignmentOptions.Left), 0f);
        TextMeshProUGUI total = Fit(WorldPanelBuilder.Line(foot, "Total 0/0", UIStyle.LabelSize, FontStyles.Bold, UIStyle.Text,
                                                           FootHeight, TextAlignmentOptions.Right), 220f);

        Wire(c, panel, team, score, overs, views, extras, total);
    }

    /// "A. Shah | c Jones b Khan | 4 | (6)": four cells on one line, each cut with an ellipsis.
    private static ScorecardPanel.RowView Row(Transform list, int index)
    {
        Transform row = Line(list, RowHeight, UIStyle.Unit);
        row.name = $"Batter {index + 1}";
        return new ScorecardPanel.RowView
        {
            root = row.gameObject,
            name = Cell(row, "", FontStyles.Normal, UIStyle.Text, TextAlignmentOptions.Left, NameWidth),
            howOut = Cell(row, "yet to bat", FontStyles.Normal, UIStyle.TextMuted, TextAlignmentOptions.Left, 0f),
            runs = Cell(row, "", FontStyles.Normal, UIStyle.Text, TextAlignmentOptions.Right, RunsWidth),
            balls = Cell(row, "", FontStyles.Normal, UIStyle.TextMuted, TextAlignmentOptions.Right, BallsWidth),
        };
    }

    private static TextMeshProUGUI Cell(Transform row, string text, FontStyles style, Color color, TextAlignmentOptions align, float width) =>
        Fit(WorldPanelBuilder.Line(row, text, RowFont, style, color, RowHeight, align), width);

    /// A row of cells `height` px tall whose fixed cells keep their widths (no force-expand).
    private static Transform Line(Transform parent, float height, float spacing)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, spacing);
        row.GetComponent<LayoutElement>().preferredHeight = height;
        WorldPanelBuilder.NoForceExpand(row);
        return row;
    }

    /// A cell exactly `width` px wide (min = preferred, so a long text in another cell cannot
    /// squeeze it and the columns stay in line), or with 0 the one that takes what is left: no
    /// width of its own (the TMP text would ask for its full length) and all the slack.
    private static TextMeshProUGUI Fit(TextMeshProUGUI text, float width)
    {
        var layout = text.GetComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = width > 0f ? 0f : 1f;
        return text;
    }

    private static void Wire(ScorecardPanel c, GameObject panel, TMP_Text team, TMP_Text score, TMP_Text overs,
                             ScorecardPanel.RowView[] views, TMP_Text extras, TMP_Text total)
    {
        var so = new SerializedObject(c);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("team").objectReferenceValue = team;
        so.FindProperty("score").objectReferenceValue = score;
        so.FindProperty("overs").objectReferenceValue = overs;
        so.FindProperty("extras").objectReferenceValue = extras;
        so.FindProperty("total").objectReferenceValue = total;
        so.FindProperty("textColor").colorValue = UIStyle.Text;
        so.FindProperty("mutedColor").colorValue = UIStyle.TextMuted;
        so.FindProperty("youColor").colorValue = UIStyle.AccentText;
        SerializedProperty rows = so.FindProperty("rows");
        rows.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++)
        {
            SerializedProperty r = rows.GetArrayElementAtIndex(i);
            r.FindPropertyRelative("root").objectReferenceValue = views[i].root;
            r.FindPropertyRelative("name").objectReferenceValue = views[i].name;
            r.FindPropertyRelative("howOut").objectReferenceValue = views[i].howOut;
            r.FindPropertyRelative("runs").objectReferenceValue = views[i].runs;
            r.FindPropertyRelative("balls").objectReferenceValue = views[i].balls;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == name)
                return go.transform;
        return null;
    }

    /// The card's height in canvas px, from the sizes above: padding, the heading, two dividers,
    /// the eleven lines and the foot, with the card's gaps between. For the size test.
    public static float Height()
    {
        float list = Rows * RowHeight + (Rows - 1) * RowGap;
        float items = HeadingHeight + 2f + list + 2f + FootHeight;
        return 2f * UIStyle.Pad + items + 4f * UIStyle.Gap;
    }
}
