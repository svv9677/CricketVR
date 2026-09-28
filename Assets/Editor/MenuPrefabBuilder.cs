using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Menu Prefabs. Builds the opening-menu prefab under
/// Assets/Resources/Prefabs/UI, in the UIStyle look with the vibrant menu accents:
///   * MenuRoot - the world-space host with the screen stack, used in all three scenes, holding
///     MainMenuScreen (Quick Match with overs steppers, Practice in Nets, Settings) and
///     PauseScreen (Resume, Settings, Main Menu, plus the leave-match confirm row).
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
    private const float BackdropWidth = 2600f;
    private const float BackdropHeight = 1300f;
    private const float StepperWidth = 88f;
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
        MenuRoot menu = root.AddComponent<MenuRoot>();
        root.AddComponent<GrabbablePanel>();

        GameObject panel = WorldPanelBuilder.Canvas(root, MenuWidth, Mm, true);

        // Backdrop first so it sorts behind the card, and outside the card's layout group.
        MenuBackdrop backdrop = BuildBackdrop(root, panel);

        WorldPanelBuilder.MakeCard(panel, UIStyle.Pad, UIStyle.SectionGap);
        WorldPanelBuilder.FitContent(panel, false);

        MainMenuScreen main = BuildMainMenu(panel.transform);
        PauseScreen pause = BuildPause(panel.transform);

        var so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panel;
        so.FindProperty("rootScreen").stringValue = "main";
        so.FindProperty("backdrop").objectReferenceValue = backdrop;
        so.FindProperty("showBackdrop").boolValue = true;
        SerializedProperty screens = so.FindProperty("screens");
        screens.arraySize = 2;
        screens.GetArrayElementAtIndex(0).objectReferenceValue = main;
        screens.GetArrayElementAtIndex(1).objectReferenceValue = pause;
        so.ApplyModifiedProperties();

        return root;
    }

    /// The backdrop is its own canvas under the root, not a child of the card: the card's
    /// vertical layout would otherwise stretch it into the button stack.
    private static MenuBackdrop BuildBackdrop(GameObject root, GameObject cardPanel)
    {
        GameObject canvas = WorldPanelBuilder.Canvas(root, BackdropWidth, Mm, false);
        canvas.name = "Backdrop";
        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(BackdropWidth, BackdropHeight);
        // Just behind the card, and centred on it.
        canvas.transform.localPosition = new Vector3(0f, 0f, 0.02f);

        var image = canvas.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.color = Color.white;

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

        UIControls.ActionButton(column, "Practice in Nets", screen.OnPracticeInNets, false);
        UIControls.ActionButton(column, "Settings", screen.OnSettings, false);

        var so = new SerializedObject(screen);
        so.FindProperty("oversLabel").objectReferenceValue = overs;
        so.ApplyModifiedProperties();
        return screen;
    }

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
    private static Transform ScreenColumn(Transform parent, string name, out GameObject go)
    {
        Transform column = WorldPanelBuilder.Column(parent, true, UIStyle.Gap);
        column.name = name;
        go = column.gameObject;
        return column;
    }

    private static void SetScreenName(MenuScreen screen, string name)
    {
        var so = new SerializedObject(screen);
        so.FindProperty("screenName").stringValue = name;
        so.ApplyModifiedProperties();
    }
}
