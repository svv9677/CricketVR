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
///   * SettingsPanel - a list of categories, each its own page (SettingsPanelBuilder);
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
        GameObject panel = WorldPanelBuilder.Canvas(root, SettingsPanelBuilder.Width, Mm, true);
        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.SectionGap);
        WorldPanelBuilder.FitContent(panel, false);

        SettingsPanelBuilder.BuildContent(panel.transform, c);

        SetField(c, "panel", panel);
        Grabbable(root, panel, "settings");
        Save(root, uiRoot);
        return c;   // the scene instance (SaveAsPrefabAssetAndConnect returns the asset, not this)
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
        Grabbable(root, panel, "next_ball");
        Save(root, uiRoot);
    }

    /// Grip while pointing at it moves the panel, and it opens where it was left (GrabbablePanel).
    private static void Grabbable(GameObject root, GameObject panel, string key)
    {
        var grab = root.AddComponent<GrabbablePanel>();
        SetField(grab, "target", panel.transform);
        SetField(grab, "saveKey", key);
    }

    private static TextMeshProUGUI PlayCard(Transform parent, NextBallMenu c)
    {
        // The bowler on a line of his own (name, and how he bowls under it - they were squeezed
        // onto one line with the Change button and cut off: "Bhuvneshwar Medi"), the one big
        // action, then the three small ones in an even row.
        Transform card = NestedCard(parent, "Play", 520f);
        WorldPanelBuilder.Section(card, "Bowler");
        TextMeshProUGUI label = WorldPanelBuilder.Line(card, "", UIStyle.LabelSize + 6f, FontStyles.Bold, UIStyle.Text,
                                                       78f, TextAlignmentOptions.Left);
        UIControls.ActionButton(card, "Bowl" + A, c.OnNextBall, true, 88f);
        Transform more = WorldPanelBuilder.Column(card, false, UIStyle.Gap);
        UIControls.ActionButton(more, "Change bowler", c.OnChangeBowler, false);
        UIControls.ActionButton(more, "Settings" + B, c.OnSettings, false);
        UIControls.ActionButton(more, "Bat grip", c.OnCalibrateGrip, false);
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
        // Read from ~3.5 m, so 2 mm per px: the 20 px captions are 40 mm tall. Three tiers, nothing
        // else (2026-11-05, "not so cluttered"): how it came off the bat, what was bowled, and the
        // three numbers. The score lives on the stadium scoreboard, not here.
        GameObject panel = WorldPanelBuilder.Canvas(root, 540f, 0.002f, false);
        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.Gap);
        WorldPanelBuilder.FitContent(panel, false);
        Transform t = panel.transform;

        WorldPanelBuilder.Section(t, "Last shot");
        TextMeshProUGUI contact = WorldPanelBuilder.Line(t, "No shot yet", 56f, FontStyles.Bold, UIStyle.Text, 66f, TextAlignmentOptions.Left);
        RectTransform quality = QualityBar(t);
        TextMeshProUGUI delivery = WorldPanelBuilder.Line(t, " ", 24f, FontStyles.Normal, UIStyle.TextMuted, 34f, TextAlignmentOptions.Left);
        UIControls.Divider(t);
        Transform stats = WorldPanelBuilder.Column(t, false, UIStyle.Gap);
        TextMeshProUGUI bat = Stat(stats, "Bat speed", out _);
        TextMeshProUGUI exit = Stat(stats, "Off the bat", out _);
        TextMeshProUGUI distance = Stat(stats, "Carry", out TextMeshProUGUI distanceCaption);

        SetField(c, "panel", panel);
        SetField(c, "contact", contact);
        SetField(c, "qualityFill", quality);
        SetField(c, "delivery", delivery);
        SetField(c, "batSpeed", bat);
        SetField(c, "exitSpeed", exit);
        SetField(c, "distance", distance);
        SetField(c, "distanceCaption", distanceCaption);
        SetField(c, "goodColor", UIStyle.AccentText);
        SetField(c, "okColor", UIStyle.Text);
        SetField(c, "poorColor", UIStyle.TextMuted);
        Save(root, uiRoot);
    }

    private static TextMeshProUGUI Stat(Transform row, string caption, out TextMeshProUGUI captionText)
    {
        Transform cell = WorldPanelBuilder.Column(row, true, 2f);
        captionText = WorldPanelBuilder.Line(cell, caption, 17f, FontStyles.Bold | FontStyles.UpperCase, UIStyle.TextMuted, 24f,
                                             TextAlignmentOptions.Left);
        captionText.characterSpacing = 3f;
        return WorldPanelBuilder.Line(cell, "–", 38f, FontStyles.Bold, UIStyle.Text, 46f, TextAlignmentOptions.Left);
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
            case string s: p.stringValue = s; break;
        }
        so.ApplyModifiedProperties();
    }
}
