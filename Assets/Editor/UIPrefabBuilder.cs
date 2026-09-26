using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Player UI. Builds the player-facing panels as prefabs under
/// Assets/Resources/Prefabs/UI, all in the UIStyle look, and places them in the open scene under
/// "UI" (replacing what the last run - or Build UI Prefabs - put there):
///   * SettingsPanel - batting (hand, difficulty, bat power, grip), fielding, bowling (type, speed,
///     swing/drift, turn/seam, reset) and a collapsed Advanced section (line, debug overlay);
///   * NextBallMenu - the between-balls dock (bowler + Change, Bowl, Settings, Calibrate grip)
///     with the replay card (ReplayControls) beside it;
///   * GripCalibrationPanel - Upright | Flat, Lock, Cancel;
///   * ShotCard - the last-shot card on the batter's off side (display only).
/// It then points Main at the new SettingsPanel and saves the scene. Run it after
/// Tools > CricketVR > Build UI Prefabs, which still builds the old-style panels and sets up the
/// pointer, player, bat and replay.
/// </summary>
public static class UIPrefabBuilder
{
    private const string PrefabFolder = "Assets/Resources/Prefabs/UI";
    private const float Mm = 0.001f;                       // 1 canvas px = 1 mm
    private const float SettingsWidth = 1120f;
    private const float LeftWidth = 448f;                  // (1120 - 2 * 32 padding - 48 gap) split
    private const float RightWidth = 560f;
    private const float ColumnGap = 48f;
    private const float InlineLabel = 150f;
    private const string A = "  <size=75%>(A)</size>";
    private const string B = "  <size=75%>(B)</size>";

    [MenuItem("Tools/CricketVR/Build Player UI")]
    public static void Build()
    {
        var main = Object.FindFirstObjectByType<Main>();
        if (main == null)
        {
            Debug.LogError("[UIPrefabBuilder] No Main in the open scene - open CricketVR.unity (or Nets.unity) first.");
            return;
        }
        SettingsPanel settings = BuildPanels(FindOrCreateRoot("UI"));
        var mainSo = new SerializedObject(main);
        mainSo.FindProperty("settingsPanel").objectReferenceValue = settings;
        mainSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        EditorSceneManager.SaveScene(main.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[UIPrefabBuilder] Settings, next-ball + replay, grip calibration and shot card built and placed under UI.");
    }

    /// Build every player panel under `uiRoot`; returns the scene's SettingsPanel for Main.
    public static SettingsPanel BuildPanels(Transform uiRoot)
    {
        Directory.CreateDirectory(PrefabFolder);
        UIStyle.EnsureSprites();
        SettingsPanel settings = BuildSettingsPanel(uiRoot);
        BuildNextBallMenu(uiRoot);
        BuildGripCalibrationPanel(uiRoot);
        BuildShotCard(uiRoot);
        return settings;
    }

    // ---- Settings -------------------------------------------------------------------------------

    private static SettingsPanel BuildSettingsPanel(Transform uiRoot)
    {
        var root = new GameObject("SettingsPanel");
        var c = root.AddComponent<SettingsPanel>();
        GameObject panel = WorldPanelBuilder.Canvas(root, SettingsWidth, Mm, true);
        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.SectionGap);
        WorldPanelBuilder.FitContent(panel, false);

        Header(panel.transform, "Settings", "Changes apply from the next ball", "Done" + B, c.OnClose);
        TwoColumns(panel.transform, out Transform left, out Transform right);
        BattingSection(left, c);
        FieldingSection(left, c);
        BowlingSection(right, c);
        UIControls.Divider(panel.transform);
        AdvancedSection(panel.transform, c);

        SetField(c, "panel", panel);
        Save(root, uiRoot);
        return c;   // the scene instance (SaveAsPrefabAssetAndConnect returns the asset, not this)
    }

    private static void Header(Transform parent, string title, string subtitle, string closeLabel, UnityAction close)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(row);
        Transform titles = WorldPanelBuilder.Column(row, true, 0f);
        WorldPanelBuilder.Line(titles, title, UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 48f, TextAlignmentOptions.Left);
        WorldPanelBuilder.Line(titles, subtitle, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f, TextAlignmentOptions.Left);
        UIControls.ActionButton(row, closeLabel, close, true, UIStyle.Control, 200f);
    }

    private static Transform TwoColumns(Transform parent, out Transform left, out Transform right)
    {
        Transform body = WorldPanelBuilder.Column(parent, false, ColumnGap);
        WorldPanelBuilder.NoForceExpand(body);
        body.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
        left = WorldPanelBuilder.FixedColumn(body, LeftWidth, UIStyle.Gap);
        right = WorldPanelBuilder.FixedColumn(body, RightWidth, UIStyle.Gap);
        return body;
    }

    private static void BattingSection(Transform column, SettingsPanel c)
    {
        WorldPanelBuilder.Section(column, "Batting");
        c.hand = UIControls.Segmented(InlineField(column, "Hand"), new[] { "Left", "Right" }, c.OnHand);

        Transform difficulty = Group(column);
        c.difficultyHint = UIControls.LabelLine(difficulty, "Difficulty", "", out _);
        c.difficultyLevel = UIControls.Segmented(difficulty, new[] { "Easy", "Medium", "Hard" }, c.OnDifficulty);

        c.batPowerSlider = UIControls.ValueSlider(column, "Bat power", 25f, 200f, true, c.OnBatPower, Constants.BatPowerRealistic);

        Transform grip = InlineField(column, "Grip");
        UIControls.ActionButton(grip, "Calibrate", c.OnCalibrateGrip, false);
        UIControls.ActionButton(grip, "Reset", c.OnResetGrip, false, UIStyle.Control, 120f);
    }

    private static void FieldingSection(Transform column, SettingsPanel c)
    {
        WorldPanelBuilder.Section(column, "Fielding");
        c.fielderSpeedSlider = UIControls.ValueSlider(column, "Fielder speed", 0.5f, 2.5f, false, c.OnFielderSpeed);
    }

    private static void BowlingSection(Transform column, SettingsPanel c)
    {
        Transform head = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(head);
        WorldPanelBuilder.Section(head, "Bowling");
        UIControls.ActionButton(head, "Reset", c.OnResetBowling, false, UIStyle.Control, 120f);

        var labels = new string[UIFormat.BowlerTypes.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = UIFormat.BowlerType(UIFormat.BowlerTypes[i]);
        Transform bowler = Group(column);
        Label(bowler, "Bowler");
        c.bowler = UIControls.Segmented(bowler, labels, c.OnBowler);

        c.speedRange = UIControls.Range(column, "Speed", 60f, 165f, 1f, c.OnSpeedRange);
        c.swingRange = UIControls.Range(column, "Swing", 0f, 1f, 0.05f, c.OnSwingRange);
        c.turnRange = UIControls.Range(column, "Turn", -0.1f, 1f, 0.01f, c.OnTurnRange);
    }

    private static void AdvancedSection(Transform parent, SettingsPanel c)
    {
        Button toggle = UIControls.ActionButton(parent, "Advanced", c.OnToggleAdvanced, false);
        c.advancedLabel = UIControls.LabelOf(toggle);

        Transform body = TwoColumns(parent, out Transform left, out Transform right);
        WorldPanelBuilder.Section(left, "Display");
        c.overlayToggle = UIControls.ToggleButton(left, "Debug overlay", c.OnOverlay);
        WorldPanelBuilder.Section(right, "Bowling line");
        c.lineRange = UIControls.Range(right, "At the stumps", -0.75f, 0.75f, 0.01f, c.OnLineRange);
        c.advanced = body.gameObject;
        body.gameObject.SetActive(false);   // collapsed until opened
    }

    // ---- Next ball + replay ---------------------------------------------------------------------

    private static void BuildNextBallMenu(Transform uiRoot)
    {
        var root = new GameObject("NextBallMenu");
        var c = root.AddComponent<NextBallMenu>();
        GameObject panel = WorldPanelBuilder.Canvas(root, 100f, 0.0009f, true);
        var row = panel.AddComponent<HorizontalLayoutGroup>();
        row.spacing = UIStyle.Gap;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.UpperCenter;
        WorldPanelBuilder.FitContent(panel, true);

        TextMeshProUGUI bowler = PlayCard(panel.transform, c);
        ReplayCard(panel.transform);

        SetField(c, "panel", panel);
        SetField(c, "bowlerLabel", bowler);
        Save(root, uiRoot);
    }

    private static TextMeshProUGUI PlayCard(Transform parent, NextBallMenu c)
    {
        Transform card = NestedCard(parent, "Play", 460f);
        WorldPanelBuilder.Section(card, "Bowler");
        Transform who = WorldPanelBuilder.Column(card, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(who);
        TextMeshProUGUI label = WorldPanelBuilder.Line(who, "", UIStyle.LabelSize + 2f, FontStyles.Bold, UIStyle.Text,
                                                       UIStyle.Control, TextAlignmentOptions.Left);
        UIControls.ActionButton(who, "Change", c.OnChangeBowler, false, UIStyle.Control, 140f);

        UIControls.ActionButton(card, "Bowl" + A, c.OnNextBall, true, 88f);
        Transform more = WorldPanelBuilder.Column(card, false, UIStyle.Gap);
        UIControls.ActionButton(more, "Settings" + B, c.OnSettings, false);
        UIControls.ActionButton(more, "Calibrate grip", c.OnCalibrateGrip, false);
        return label;
    }

    private static void ReplayCard(Transform parent)
    {
        Transform card = NestedCard(parent, "Replay", 400f);
        var c = card.gameObject.AddComponent<ReplayControls>();
        WorldPanelBuilder.Section(card, "Replay");

        Transform play = WorldPanelBuilder.Column(card, false, UIStyle.Unit);
        Button replay = UIControls.ActionButton(play, "Replay", c.OnReplay, false);
        Button pause = UIControls.ActionButton(play, "Pause", c.OnPause, false);
        Button stop = UIControls.ActionButton(play, "Stop", c.OnStop, false);

        Transform speed = WorldPanelBuilder.Column(card, false, UIStyle.Unit);
        WorldPanelBuilder.NoForceExpand(speed);
        Button slower = UIControls.ActionButton(speed, "-", c.OnSlower, false, UIStyle.Control, 88f);
        TextMeshProUGUI speedLabel = WorldPanelBuilder.Line(speed, "1×", UIStyle.ValueSize + 4f, FontStyles.Bold,
                                                            UIStyle.AccentText, UIStyle.Control, TextAlignmentOptions.Center);
        Button faster = UIControls.ActionButton(speed, "+", c.OnFaster, false, UIStyle.Control, 88f);

        Toggle front = UIControls.ToggleButton(card, "Screen in front", c.OnScreenInFront);

        SetField(c, "replayButton", replay);
        SetField(c, "pauseButton", pause);
        SetField(c, "stopButton", stop);
        SetField(c, "slowerButton", slower);
        SetField(c, "fasterButton", faster);
        SetField(c, "speedLabel", speedLabel);
        SetField(c, "screenInFront", front);
    }

    // ---- Grip calibration -----------------------------------------------------------------------

    private static void BuildGripCalibrationPanel(Transform uiRoot)
    {
        var root = new GameObject("GripCalibrationPanel");
        var c = root.AddComponent<GripCalibrationPanel>();
        GameObject panel = WorldPanelBuilder.Canvas(root, 480f, Mm, true);
        WorldPanelBuilder.MakeCard(panel);
        WorldPanelBuilder.FitContent(panel, false);
        Transform t = panel.transform;
        WorldPanelBuilder.Line(t, "Calibrate grip", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 48f, TextAlignmentOptions.Left);
        WorldPanelBuilder.Text(t, "Hold the see-through handle the way you bat, then lock it. Stand the bat upright or lay it flat - whichever is easier to line up.",
                               UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 0f, TextAlignmentOptions.Left);
        SegmentedControl orientation = UIControls.Segmented(t, new[] { "Upright", "Flat" }, c.OnOrientation);
        UIControls.ActionButton(t, "Lock grip" + A, c.OnLock, true, 80f);
        UIControls.ActionButton(t, "Cancel" + B, c.OnCancel, false);
        WorldPanelBuilder.Line(t, "X / Y also switches upright / flat", UIStyle.HintSize, FontStyles.Italic, UIStyle.TextMuted, 28f,
                               TextAlignmentOptions.Center);
        SetField(c, "panel", panel);
        SetField(c, "orientation", orientation);
        Save(root, uiRoot);
    }

    // ---- Shot card ------------------------------------------------------------------------------

    private static void BuildShotCard(Transform uiRoot)
    {
        var root = new GameObject("ShotCard");
        var c = root.AddComponent<ShotCard>();
        // Read from ~3.5 m, so 2 mm per px: the 24 px captions are 48 mm tall.
        GameObject panel = WorldPanelBuilder.Canvas(root, 440f, 0.002f, false);
        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.Gap);
        WorldPanelBuilder.FitContent(panel, false);
        Transform t = panel.transform;

        WorldPanelBuilder.Section(t, "Last shot");
        TextMeshProUGUI contact = WorldPanelBuilder.Line(t, "Ready", 52f, FontStyles.Bold, UIStyle.Text, 64f, TextAlignmentOptions.Left);
        RectTransform quality = QualityBar(t);
        Transform speeds = WorldPanelBuilder.Column(t, false, UIStyle.Gap);
        TextMeshProUGUI ball = Stat(speeds, "Ball", out _);
        TextMeshProUGUI bat = Stat(speeds, "Bat", out _);
        Transform result = WorldPanelBuilder.Column(t, false, UIStyle.Gap);
        TextMeshProUGUI exit = Stat(result, "Exit", out _);
        TextMeshProUGUI distance = Stat(result, "Carry", out TextMeshProUGUI distanceCaption);
        UIControls.Divider(t);

        Transform scoreRow = WorldPanelBuilder.Column(t, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(scoreRow);
        TextMeshProUGUI score = WorldPanelBuilder.Line(scoreRow, "0/0", 44f, FontStyles.Bold, UIStyle.Text, 56f, TextAlignmentOptions.Left);
        TextMeshProUGUI overs = WorldPanelBuilder.Line(scoreRow, "0.0 ov", 24f, FontStyles.Normal, UIStyle.TextMuted, 56f,
                                                       TextAlignmentOptions.Right, 160f);
        TextMeshProUGUI recent = WorldPanelBuilder.Line(t, " ", 24f, FontStyles.Bold, UIStyle.TextMuted, 32f, TextAlignmentOptions.Left);

        SetField(c, "panel", panel);
        SetField(c, "contact", contact);
        SetField(c, "qualityFill", quality);
        SetField(c, "ballSpeed", ball);
        SetField(c, "batSpeed", bat);
        SetField(c, "exitSpeed", exit);
        SetField(c, "distance", distance);
        SetField(c, "distanceCaption", distanceCaption);
        SetField(c, "score", score);
        SetField(c, "overs", overs);
        SetField(c, "recent", recent);
        SetField(c, "goodColor", UIStyle.AccentText);
        SetField(c, "okColor", UIStyle.Text);
        SetField(c, "poorColor", UIStyle.TextMuted);
        Save(root, uiRoot);
    }

    private static TextMeshProUGUI Stat(Transform row, string caption, out TextMeshProUGUI captionText)
    {
        Transform cell = WorldPanelBuilder.Column(row, true, 0f);
        captionText = WorldPanelBuilder.Line(cell, caption, 18f, FontStyles.Bold | FontStyles.UpperCase, UIStyle.TextMuted, 24f,
                                             TextAlignmentOptions.Left);
        captionText.characterSpacing = 4f;
        return WorldPanelBuilder.Line(cell, "-", 40f, FontStyles.Bold, UIStyle.Text, 48f, TextAlignmentOptions.Left);
    }

    private static RectTransform QualityBar(Transform parent)
    {
        var go = new GameObject("Quality", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<LayoutElement>().preferredHeight = 8f;
        var track = go.GetComponent<Image>();
        UIStyle.PillImage(track, UIStyle.Track);
        track.raycastTarget = false;
        Image fill = WorldPanelBuilder.Overlay(go.transform, "Fill");
        UIStyle.PillImage(fill, UIStyle.Accent);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        return fill.rectTransform;
    }

    // ---- Layout helpers -------------------------------------------------------------------------

    /// A label and its control, 8 px apart.
    private static Transform Group(Transform parent) => WorldPanelBuilder.Column(parent, true, UIStyle.Unit);

    private static void Label(Transform parent, string text) =>
        WorldPanelBuilder.Line(parent, text, UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, UIStyle.LabelRow, TextAlignmentOptions.Left);

    /// "Label  [control .......]" on one row; returns the row for the control(s).
    private static Transform InlineField(Transform parent, string label)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(row);
        WorldPanelBuilder.Line(row, label, UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, UIStyle.Control,
                               TextAlignmentOptions.Left, InlineLabel);
        return row;
    }

    private static Transform NestedCard(Transform parent, string name, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        WorldPanelBuilder.MakeCard(go);
        go.GetComponent<LayoutElement>().preferredWidth = width;
        return go.transform;
    }

    // ---- Scene / prefab helpers -----------------------------------------------------------------

    /// Save as a prefab under Resources/Prefabs/UI and leave `root` in the scene as a connected
    /// instance, replacing any previous instance. Returns the prefab asset.
    private static GameObject Save(GameObject root, Transform uiRoot)
    {
        Transform old = uiRoot.Find(root.name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        root.transform.SetParent(uiRoot, false);
        string path = $"{PrefabFolder}/{root.name}.prefab";
        return PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
    }

    private static Transform FindOrCreateRoot(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name)
                return go.transform;
        return new GameObject(name).transform;
    }

    private static void SetField(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[UIPrefabBuilder] {target.GetType().Name} has no field '{field}'");
            return;
        }
        switch (value)
        {
            case Object o: p.objectReferenceValue = o; break;
            case Color col: p.colorValue = col; break;
        }
        so.ApplyModifiedProperties();
    }
}
