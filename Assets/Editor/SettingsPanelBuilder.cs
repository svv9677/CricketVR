using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// The settings panel's contents: a list of categories, each opening its own page with a Back.
/// The categories are the ones settings will keep growing into - a new setting goes on the page
/// it belongs to, a new kind of setting gets a new page on the list:
///
///   Batting        hand, difficulty, bat power, grip
///   Bowling        bowler type, speed, swing/drift, turn/seam, line, reset  (in a match only)
///   Fielding       fielder speed
///   Match          overs
///   View & Comfort camera height (this session), recentre at the crease
///   Audio          volumes 0-200%: master, bat and ball, crowd, stumps, fielders' calls
///   Developer      debug overlay, restart the scene
///
/// Used by UIPrefabBuilder for a fresh build, and by Tools > CricketVR > Rebuild Settings Pages,
/// which rebuilds just the contents inside the existing prefab: its root and card keep their
/// identity, so everything pointing at them - Main, MenuRoot's screen list, the scene instances -
/// stays connected.
/// </summary>
public static class SettingsPanelBuilder
{
    public const string PrefabPath = "Assets/Resources/Prefabs/UI/SettingsPanel.prefab";
    public const float Width = 820f;
    private const float CategoryHeight = 92f;
    private const float InlineLabel = 170f;

    [MenuItem("Tools/CricketVR/Rebuild Settings Pages")]
    public static void RebuildInPlace()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var c = root.GetComponent<SettingsPanel>();
            GameObject panel = c.Panel;
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(panel.transform.GetChild(i).gameObject);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(Width, rect.sizeDelta.y);
            BuildContent(panel.transform, c);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[SettingsPanelBuilder] Settings pages rebuilt in " + PrefabPath);
    }

    /// Everything inside the card: the header, the category list and one page per category.
    public static void BuildContent(Transform card, SettingsPanel c)
    {
        c.pageTitle = Header(card, c);
        var pages = new List<GameObject>();
        var names = new List<string>();
        var matchOnly = new List<GameObject>();

        Transform home = Page(card, SettingsPanel.HomePage, pages, names);
        Category(home, "Batting", "Hand, difficulty, bat power, grip", c.OnPageBatting);
        matchOnly.Add(Category(home, "Bowling", "Bowler, speed, swing, turn, line", c.OnPageBowling).gameObject);
        Category(home, "Fielding", "Fielder speed", c.OnPageFielding);
        Category(home, "Match", "Overs", c.OnPageMatch);
        Category(home, "View & Comfort", "Camera height, recentre", c.OnPageView);
        Category(home, "Audio", "Master, bat, crowd, stumps, fielders", c.OnPageAudio);
        Category(home, "Developer", "Debug overlay, restart", c.OnPageDeveloper);

        Transform batting = Page(card, "batting", pages, names);
        c.hand = UIControls.Segmented(InlineField(batting, "Hand"), new[] { "Left", "Right" }, c.OnHand);
        Transform difficulty = Group(batting);
        c.difficultyHint = UIControls.LabelLine(difficulty, "Difficulty", "", out _);
        c.difficultyLevel = UIControls.Segmented(difficulty, new[] { "Easy", "Medium", "Hard" }, c.OnDifficulty);
        // 50%-150% of a real swing: BatContact.SwingScale clamps to exactly this.
        c.batPowerSlider = UIControls.ValueSlider(batting, "Bat power", 38f, 112f, true, c.OnBatPower, Constants.BatPowerRealistic);
        Transform grip = InlineField(batting, "Grip");
        UIControls.ActionButton(grip, "Calibrate", c.OnCalibrateGrip, false);
        UIControls.ActionButton(grip, "Reset", c.OnResetGrip, false, UIStyle.Control, 140f);
        matchOnly.Add(grip.gameObject);

        Transform bowling = Page(card, "bowling", pages, names);
        var labels = new string[UIFormat.BowlerTypes.Length];
        for (int i = 0; i < labels.Length; i++)
            labels[i] = UIFormat.BowlerType(UIFormat.BowlerTypes[i]);
        Transform bowler = Group(bowling);
        Label(bowler, "Bowler");
        c.bowler = UIControls.Segmented(bowler, labels, c.OnBowler);
        c.speedRange = UIControls.Range(bowling, "Speed", 60f, 165f, 1f, c.OnSpeedRange);
        c.swingRange = UIControls.Range(bowling, "Swing", 0f, 1f, 0.05f, c.OnSwingRange);
        c.turnRange = UIControls.Range(bowling, "Turn", -0.1f, 1f, 0.01f, c.OnTurnRange);
        c.lineRange = UIControls.Range(bowling, "Line at the stumps", -0.75f, 0.75f, 0.01f, c.OnLineRange);
        UIControls.ActionButton(bowling, "Reset bowling", c.OnResetBowling, false);

        Transform fielding = Page(card, "fielding", pages, names);
        c.fielderSpeedSlider = UIControls.ValueSlider(fielding, "Fielder speed", 0.5f, 2.5f, false, c.OnFielderSpeed);

        Transform match = Page(card, "match", pages, names);
        Transform overs = InlineField(match, "Overs");
        UIControls.ActionButton(overs, "‹", c.OnFewerOvers, false, UIStyle.Control, 88f);
        c.oversLabel = WorldPanelBuilder.Line(overs, "5 overs", UIStyle.LabelSize, FontStyles.Bold, UIStyle.Text,
                                              UIStyle.Control, TextAlignmentOptions.Center, 200f);
        UIControls.ActionButton(overs, "›", c.OnMoreOvers, false, UIStyle.Control, 88f);
        Hint(match, "Applies to the next match.");

        Transform view = Page(card, "view", pages, names);
        c.cameraHeightSlider = UIControls.ValueSlider(view, "Camera height", GameSettings.MinCameraHeight,
                                                      GameSettings.MaxCameraHeight, false, c.OnCameraHeight, 0f);
        Hint(view, "For this session only - every session starts from your headset's own height.");
        Button recentre = UIControls.ActionButton(view, "Recentre at the crease", c.OnRecentre, false);
        matchOnly.Add(recentre.gameObject);

        Transform audio = Page(card, "audio", pages, names);
        // 0-200%, the tick at 100% (as recorded).
        float max = GameAudio.MaxVolume;
        c.masterVolume = UIControls.ValueSlider(audio, "Master", 0f, max, false, c.OnMasterVolume, 1f);
        c.batVolume = UIControls.ValueSlider(audio, "Bat and ball", 0f, max, false, c.OnBatVolume, 1f);
        c.crowdVolume = UIControls.ValueSlider(audio, "Crowd", 0f, max, false, c.OnCrowdVolume, 1f);
        c.stumpsVolume = UIControls.ValueSlider(audio, "Stumps", 0f, max, false, c.OnStumpsVolume, 1f);
        c.fieldersVolume = UIControls.ValueSlider(audio, "Fielders' calls", 0f, max, false, c.OnFieldersVolume, 1f);

        Transform developer = Page(card, "developer", pages, names);
        c.overlayToggle = UIControls.ToggleButton(developer, "Debug overlay", c.OnOverlay);
        Button restart = UIControls.ActionButton(developer, "Restart the scene", c.OnJustRestart, false);
        matchOnly.Add(restart.gameObject);

        c.pages = pages.ToArray();
        c.pageNames = names.ToArray();
        c.matchOnly = matchOnly.ToArray();
        // The old collapsed "Advanced" block is gone: its contents live on Bowling and Developer.
        c.advanced = null;
        c.advancedLabel = null;
        for (int i = 1; i < pages.Count; i++)
            pages[i].SetActive(false);   // the list shows first
        EditorUtility.SetDirty(c);
    }

    /// Title on the left, Back and Done on the right. Back goes from a page to the list (and
    /// from the list closes Settings); Done always closes.
    private static TMP_Text Header(Transform card, SettingsPanel c)
    {
        Transform row = WorldPanelBuilder.Column(card, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(row);
        UIControls.ActionButton(row, "‹ Back", c.OnBackOrClose, false, UIStyle.Control, 150f);
        TextMeshProUGUI title = WorldPanelBuilder.Line(row, "Settings", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text,
                                                       56f, TextAlignmentOptions.Left);
        UIControls.ActionButton(row, "Done  <size=75%>(B)</size>", c.OnClose, true, UIStyle.Control, 200f);
        UIControls.Divider(card);
        return title;
    }

    private static Transform Page(Transform card, string name, List<GameObject> pages, List<string> names)
    {
        Transform page = WorldPanelBuilder.Column(card, true, UIStyle.Gap);
        page.name = "Page_" + name;
        pages.Add(page.gameObject);
        names.Add(name);
        return page;
    }

    /// A category on the list: its name, and what is in it underneath.
    private static Button Category(Transform list, string title, string contents, UnityAction open)
    {
        string label = $"<b>{title}</b>   <size=75%><color=#98A2B3>{contents}</color></size>";
        Button button = UIControls.ActionButton(list, label, open, false, CategoryHeight);
        TextMeshProUGUI text = UIControls.LabelOf(button);
        text.alignment = TextAlignmentOptions.Left;
        text.margin = new Vector4(28f, 0f, 28f, 0f);
        return button;
    }

    private static Transform Group(Transform parent) => WorldPanelBuilder.Column(parent, true, UIStyle.Unit);

    private static void Label(Transform parent, string text) =>
        WorldPanelBuilder.Line(parent, text, UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, UIStyle.LabelRow,
                               TextAlignmentOptions.Left);

    private static void Hint(Transform parent, string text) =>
        WorldPanelBuilder.Text(parent, text, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 28f,
                               TextAlignmentOptions.Left);

    /// "Label  [control .......]" on one row; returns the row for the control(s).
    private static Transform InlineField(Transform parent, string label)
    {
        Transform row = WorldPanelBuilder.Column(parent, false, UIStyle.Gap);
        WorldPanelBuilder.NoForceExpand(row);
        WorldPanelBuilder.Line(row, label, UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, UIStyle.Control,
                               TextAlignmentOptions.Left, InlineLabel);
        return row;
    }
}
