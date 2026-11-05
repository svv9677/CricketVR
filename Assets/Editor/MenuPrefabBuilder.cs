using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Menu Prefabs. Builds the opening-menu prefab under
/// Assets/Resources/Prefabs/UI, in the UIStyle look with the vibrant menu accents:
///   * MenuRoot - the world-space host with the screen stack, used in all three scenes, holding
///     MainMenuScreen (Quick Match with overs steppers, Settings, and - MainMenuExtras - Match
///     Setup, Customise, Career, Results and the last match's score), PauseScreen (Resume,
///     Settings, Main Menu, plus the leave-match confirm row), MatchSetupScreen, CustomiseScreen,
///     the career screens (CareerMenuBuilder: the save slots, Create Player, Create Look and the
///     career hub) and MatchResultScreen.
///
/// The settings screen is NOT built here: it is the existing SettingsPanel prefab from
/// Tools > CricketVR > Build Player UI, attached as a third screen by the scene builders.
///
/// Every control is wired with a persistent (saved) listener through UIControls.ActionButton,
/// so the prefab works with nothing created or hooked up at runtime.
/// </summary>
public static class MenuPrefabBuilder
{
    private const string PrefabFolder = "Assets/Resources/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/MenuRoot.prefab";
    private const float Mm = 0.001f;              // 1 canvas px = 1 mm, as elsewhere
    private const float MenuWidth = 760f;
    // The backdrop has to fill the view, not hang in front of the player like a picture. At
    // 3 m behind the card it needs to be enormous: 20 x 10 m covers roughly 145 degrees across,
    // which is wider than a Quest's field of view, so the camera's clear colour never shows at
    // the edges.
    private const float BackdropWidth = 3200f;
    private const float BackdropHeight = 1600f;
    private const float BackdropMetresPerPixel = 0.00625f;   // 3200 px -> 20 m
    private const float BackdropBehind = 3.0f;
    internal const float StepperWidth = 88f;
    private const float OversWidth = 170f;

    [MenuItem("Tools/CricketVR/Build Menu Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(PrefabFolder);
        UIStyle.EnsureSprites();

        GameObject root = BuildMenuRoot();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log($"[MenuPrefabBuilder] MenuRoot built at {PrefabPath} " +
                  "(MainMenuScreen + PauseScreen; settings is attached per scene).");
    }

    private static GameObject BuildMenuRoot()
    {
        var root = new GameObject("MenuRoot");
        // A default pose in front of an un-lifted camera, so the menu is visible in the Scene and
        // Game views without entering play mode. MenuRoot.Place overwrites this at runtime.
        root.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.45f), Quaternion.identity);
        MenuRoot menu = root.AddComponent<MenuRoot>();

        // Grab-to-move works on the whole menu, card and backdrop together.
        var grab = root.AddComponent<GrabbablePanel>();
        var gso = new SerializedObject(grab);
        gso.FindProperty("target").objectReferenceValue = root.transform;
        gso.FindProperty("saveKey").stringValue = "menu";
        gso.ApplyModifiedProperties();

        GameObject panel = WorldPanelBuilder.Canvas(root, MenuWidth, Mm, true);

        // Backdrop first so it sorts behind the card, and outside the card's layout group.
        MenuBackdrop backdrop = BuildBackdrop(root, panel);

        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.SectionGap);
        WorldPanelBuilder.FitContent(panel, false);

        MainMenuScreen main = BuildMainMenu(panel.transform);
        PauseScreen pause = BuildPause(panel.transform);
        MenuScreen[] more =
        {
            BuildMatchSetup(panel.transform), BuildCustomise(panel.transform),
            CareerMenuBuilder.BuildSlots(panel.transform), CareerMenuBuilder.BuildCreatePlayer(panel.transform),
            CareerMenuBuilder.BuildCreateLook(panel.transform), CareerMenuBuilder2.BuildHub(panel.transform),
            CareerMenuBuilder2.BuildRatings(panel.transform), CareerMenuBuilder2.BuildSeason(panel.transform),
            CareerMenuBuilder2.BuildResult(panel.transform),
            BuildResult(panel.transform),
        };

        // MenuRoot.Awake switches screens off at runtime, but Awake does not run in the editor,
        // so leave only the main menu on: otherwise the prefab shows every screen stacked.
        pause.gameObject.SetActive(false);
        foreach (MenuScreen m in more)
            m.gameObject.SetActive(false);

        var so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("rootScreen").stringValue = "main";
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("showBackdrop").boolValue = true;
        SerializedProperty screens = so.FindProperty("screens");
        screens.arraySize = 2 + more.Length;
        screens.GetArrayElementAtIndex(0).objectReferenceValue = main;
        screens.GetArrayElementAtIndex(1).objectReferenceValue = pause;
        for (int i = 0; i < more.Length; i++)
            screens.GetArrayElementAtIndex(2 + i).objectReferenceValue = more[i];
        so.ApplyModifiedProperties();

        return root;
    }

    /// The backdrop is its own canvas under the root, not a child of the card: the card's
    /// vertical layout would otherwise stretch it into the button stack.
    private static MenuBackdrop BuildBackdrop(GameObject root, GameObject cardPanel)
    {
        GameObject canvas = WorldPanelBuilder.Canvas(root, BackdropWidth, BackdropMetresPerPixel, false);
        canvas.name = "Backdrop";
        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(BackdropWidth, BackdropHeight);
        // Behind the card in local +Z (the canvas faces -Z, so +Z is away from the player), far
        // enough back to read as scenery with real parallax rather than a halo around the
        // buttons. Lifted slightly so the horizon sits above the card rather than behind it.
        canvas.transform.localPosition = new Vector3(0f, 0.55f, BackdropBehind);

        var image = canvas.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;
        // Assign the plate now as well as at runtime, so the backdrop previews in the editor
        // instead of showing as a white box before play mode.
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Resources/UI/Backdrops/Backdrop-BowlersEnd.png");

        var backdrop = canvas.AddComponent<MenuBackdrop>();
        var so = new SerializedObject(backdrop);
        so.FindProperty("image").objectReferenceValue = image;
        so.FindProperty("defaultPlate").stringValue = "Backdrop-BowlersEnd";
        so.ApplyModifiedProperties();
        _ = cardPanel;
        return backdrop;
    }

    private static MainMenuScreen BuildMainMenu(Transform parent)
    {
        Transform column = ScreenColumn(parent, "MainMenuScreen", out GameObject go);
        MainMenuScreen screen = go.AddComponent<MainMenuScreen>();
        SetScreenName(screen, "main");

        WorldPanelBuilder.Text(column, "CricketVR", UIStyle.TitleSize, FontStyles.Bold,
                               UIStyle.Text, 58f);
        WorldPanelBuilder.Text(column, "Step in and face the over", UIStyle.HintSize,
                               FontStyles.Normal, UIStyle.TextMuted, 30f);
        UIControls.Divider(column);

        // Quick Match, with the overs stepper on the row itself.
        Transform row = WorldPanelBuilder.Column(column, false, UIStyle.Unit);
        WorldPanelBuilder.NoForceExpand(row);

        Button play = UIControls.ActionButton(row, "Quick Match", screen.OnQuickMatch, true);
        play.colors = UIStyle.MenuColors(true);
        play.GetComponent<LayoutElement>().flexibleWidth = 1f;

        Button fewer = UIControls.ActionButton(row, "‹", screen.OnFewerOvers, false,
                                               UIStyle.Control, StepperWidth);
        TextMeshProUGUI overs = WorldPanelBuilder.Line(row, "5 overs", UIStyle.LabelSize,
                                                       FontStyles.Bold, UIStyle.Text,
                                                       UIStyle.Control,
                                                       TextAlignmentOptions.Center, OversWidth);
        Button more = UIControls.ActionButton(row, "›", screen.OnMoreOvers, false,
                                              UIStyle.Control, StepperWidth);
        _ = fewer;
        _ = more;

        // The rest of the game, beside Rao's screen rather than in it (MainMenuExtras): the match
        // Quick Match starts, then the other screens, then the last match's scorecard.
        MainMenuExtras extras = go.AddComponent<MainMenuExtras>();
        TextMeshProUGUI setupLine = WorldPanelBuilder.Line(column, "5 overs · Medium bowling", UIStyle.HintSize, FontStyles.Normal,
                                                           UIStyle.TextMuted, 28f, TextAlignmentOptions.Center);
        Transform pair = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(pair, "Match Setup", extras.OnMatchSetup, false);
        UIControls.ActionButton(pair, "Career", extras.OnCareer, false);
        pair = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(pair, "Customise", extras.OnCustomise, false);
        UIControls.ActionButton(pair, "Results", extras.OnResults, false);

        UIControls.ActionButton(column, "Settings", screen.OnSettings, false);
        UIControls.Divider(column);
        TextMeshProUGUI last = WorldPanelBuilder.Line(column, "No matches played yet", UIStyle.LabelSize, FontStyles.Bold,
                                                      UIStyle.Text, 34f, TextAlignmentOptions.Center);
        TextMeshProUGUI lastDetail = WorldPanelBuilder.Line(column, "", UIStyle.HintSize, FontStyles.Normal,
                                                            UIStyle.Text, 28f, TextAlignmentOptions.Center);

        var so = new SerializedObject(screen);
        so.FindProperty("oversLabel").objectReferenceValue = overs;
        so.ApplyModifiedProperties();
        var eso = new SerializedObject(extras);
        eso.FindProperty("lastMatch").objectReferenceValue = last;
        eso.FindProperty("lastDetail").objectReferenceValue = lastDetail;
        eso.FindProperty("setup").objectReferenceValue = setupLine;
        eso.ApplyModifiedProperties();
        return screen;
    }

    // ---- Match Setup, Customise, Result (the career screens: CareerMenuBuilder) ------------------

    private static MenuScreen BuildMatchSetup(Transform parent)
    {
        Transform column = ScreenColumn(parent, "MatchSetupScreen", out GameObject go);
        MatchSetupScreen screen = go.AddComponent<MatchSetupScreen>();
        SetScreenName(screen, "setup");
        Title(column, "Match Setup", "Your innings, your way");

        WorldPanelBuilder.Section(column, "Overs");
        Transform row = WorldPanelBuilder.Column(column, false, UIStyle.Unit);
        WorldPanelBuilder.NoForceExpand(row);
        UIControls.ActionButton(row, "‹", screen.OnFewerOvers, false, UIStyle.Control, StepperWidth);
        TextMeshProUGUI overs = WorldPanelBuilder.Line(row, "5 overs", UIStyle.LabelSize, FontStyles.Bold, UIStyle.Text,
                                                       UIStyle.Control, TextAlignmentOptions.Center);
        UIControls.ActionButton(row, "›", screen.OnMoreOvers, false, UIStyle.Control, StepperWidth);

        WorldPanelBuilder.Section(column, "Match");
        SegmentedControl type = UIControls.Segmented(column, new[] { "Bat first", "Chase" }, screen.OnMatchType);
        TextMeshProUGUI typeHint = Hint(column);
        WorldPanelBuilder.Section(column, "Bowling difficulty");
        SegmentedControl bowling = UIControls.Segmented(column, new[] { "Easy", "Medium", "Hard", "Pro" }, screen.OnBowling);
        TextMeshProUGUI bowlingHint = Hint(column);
        WorldPanelBuilder.Section(column, "Bat shield");
        SegmentedControl shield = UIControls.Segmented(column, new[] { "Wide", "Medium", "Off" }, screen.OnShield);
        TextMeshProUGUI shieldHint = Hint(column);

        UIControls.Divider(column);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Back", screen.OnBack, false);
        Button start = UIControls.ActionButton(actions, "Start Match", screen.OnStartMatch, true);
        start.colors = UIStyle.MenuColors(true);

        var so = new SerializedObject(screen);
        so.FindProperty("oversLabel").objectReferenceValue = overs;
        so.FindProperty("matchType").objectReferenceValue = type;
        so.FindProperty("matchTypeHint").objectReferenceValue = typeHint;
        so.FindProperty("bowling").objectReferenceValue = bowling;
        so.FindProperty("bowlingHint").objectReferenceValue = bowlingHint;
        so.FindProperty("shield").objectReferenceValue = shield;
        so.FindProperty("shieldHint").objectReferenceValue = shieldHint;
        so.ApplyModifiedProperties();
        return screen;
    }

    private static MenuScreen BuildCustomise(Transform parent)
    {
        Transform column = ScreenColumn(parent, "CustomiseScreen", out GameObject go);
        CustomiseScreen screen = go.AddComponent<CustomiseScreen>();
        SetScreenName(screen, "customise");
        Title(column, "Customise", "The ground for your next match");

        WorldPanelBuilder.Section(column, "Crowd");
        SegmentedControl crowd = UIControls.Segmented(column, new[] { "Packed", "Half full", "Empty" }, screen.OnCrowd);
        WorldPanelBuilder.Section(column, "Pitch");
        SegmentedControl pitch = UIControls.Segmented(column, new[] { "Flat", "Green", "Dusty" }, screen.OnPitch);
        TextMeshProUGUI pitchHint = Hint(column);
        WorldPanelBuilder.Section(column, "Time of day");
        SegmentedControl time = UIControls.Segmented(column, new[] { "Afternoon", "Golden hour", "Night" }, screen.OnTimeOfDay);
        TextMeshProUGUI timeHint = Hint(column);

        UIControls.Divider(column);
        UIControls.ActionButton(column, "Back", screen.OnBack, false);

        var so = new SerializedObject(screen);
        so.FindProperty("crowd").objectReferenceValue = crowd;
        so.FindProperty("pitch").objectReferenceValue = pitch;
        so.FindProperty("pitchHint").objectReferenceValue = pitchHint;
        so.FindProperty("timeOfDay").objectReferenceValue = time;
        so.FindProperty("timeHint").objectReferenceValue = timeHint;
        so.ApplyModifiedProperties();
        return screen;
    }

    private static MenuScreen BuildResult(Transform parent)
    {
        Transform column = ScreenColumn(parent, "MatchResultScreen", out GameObject go);
        MatchResultScreen screen = go.AddComponent<MatchResultScreen>();
        SetScreenName(screen, "result");
        TextMeshProUGUI heading = WorldPanelBuilder.Text(column, "MATCH RESULT", UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 58f);
        TextMeshProUGUI score = WorldPanelBuilder.Text(column, "0/0", UIStyle.TitleSize * 1.6f, FontStyles.Bold, UIStyle.Text, 84f);
        TextMeshProUGUI result = WorldPanelBuilder.Text(column, "", UIStyle.LabelSize, FontStyles.Bold, UIStyle.AccentText, 40f);
        TextMeshProUGUI details = WorldPanelBuilder.Text(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.Text, 64f);
        UIControls.Divider(column);
        TextMeshProUGUI ai = WorldPanelBuilder.Text(column, "", UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, 70f);
        UIControls.Divider(column);
        TextMeshProUGUI history = WorldPanelBuilder.Text(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.Text, 96f);
        Transform actions = WorldPanelBuilder.Column(column, false, UIStyle.Gap);
        UIControls.ActionButton(actions, "Home", screen.OnBack, false);
        Button again = UIControls.ActionButton(actions, "Play Again", screen.OnPlayAgain, true);
        again.colors = UIStyle.MenuColors(true);

        var so = new SerializedObject(screen);
        so.FindProperty("heading").objectReferenceValue = heading;
        so.FindProperty("score").objectReferenceValue = score;
        so.FindProperty("result").objectReferenceValue = result;
        so.FindProperty("details").objectReferenceValue = details;
        so.FindProperty("aiInnings").objectReferenceValue = ai;
        so.FindProperty("history").objectReferenceValue = history;
        so.ApplyModifiedProperties();
        return screen;
    }

    internal static void Title(Transform column, string title, string subtitle)
    {
        WorldPanelBuilder.Text(column, title, UIStyle.TitleSize, FontStyles.Bold, UIStyle.Text, 58f);
        WorldPanelBuilder.Text(column, subtitle, UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f);
        UIControls.Divider(column);
    }

    internal static TextMeshProUGUI Hint(Transform column) =>
        WorldPanelBuilder.Text(column, "", UIStyle.HintSize, FontStyles.Normal, UIStyle.TextMuted, 30f, TextAlignmentOptions.Left);

    private static PauseScreen BuildPause(Transform parent)
    {
        Transform column = ScreenColumn(parent, "PauseScreen", out GameObject go);
        PauseScreen screen = go.AddComponent<PauseScreen>();
        SetScreenName(screen, "pause");

        WorldPanelBuilder.Text(column, "Paused", UIStyle.TitleSize, FontStyles.Bold,
                               UIStyle.Text, 58f);
        UIControls.Divider(column);

        Transform actions = WorldPanelBuilder.Column(column, true, UIStyle.Gap);
        Button resume = UIControls.ActionButton(actions, "Resume", screen.OnResume, true);
        resume.colors = UIStyle.MenuColors(true);
        UIControls.ActionButton(actions, "Settings", screen.OnSettings, false);
        UIControls.ActionButton(actions, "Main Menu", screen.OnMainMenu, false);

        // The confirm row, hidden until Main Menu is pressed.
        Transform confirm = WorldPanelBuilder.Column(column, true, UIStyle.Gap);
        WorldPanelBuilder.Text(confirm, "Leave match? Your score will be lost.",
                               UIStyle.LabelSize, FontStyles.Normal, UIStyle.Text, 44f);
        Transform confirmRow = WorldPanelBuilder.Column(confirm, false, UIStyle.Gap);
        Button leave = UIControls.ActionButton(confirmRow, "Leave", screen.OnConfirmLeave, true);
        leave.colors = UIStyle.MenuColors(false);
        UIControls.ActionButton(confirmRow, "Stay", screen.OnCancelLeave, false);
        confirm.gameObject.SetActive(false);

        var so = new SerializedObject(screen);
        so.FindProperty("confirmOnLeave").boolValue = true;
        so.FindProperty("confirmRow").objectReferenceValue = confirm.gameObject;
        so.FindProperty("actionRow").objectReferenceValue = actions.gameObject;
        so.ApplyModifiedProperties();
        return screen;
    }

    /// A screen: a full-width column under the card, switched on and off by MenuRoot.
    internal static Transform ScreenColumn(Transform parent, string name, out GameObject go)
    {
        Transform column = WorldPanelBuilder.Column(parent, true, UIStyle.Gap);
        column.name = name;
        go = column.gameObject;
        return column;
    }

    internal static void SetScreenName(MenuScreen screen, string name)
    {
        var so = new SerializedObject(screen);
        so.FindProperty("screenName").stringValue = name;
        so.ApplyModifiedProperties();
    }
}
