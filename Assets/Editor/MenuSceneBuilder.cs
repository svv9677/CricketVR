using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Menus In All Scenes. Puts the opening menu into the three scenes
/// that need it and sets the build list:
///   * Splash  - an XR rig, the laser pointer and event system, MenuRoot on the main menu with
///               the backdrop shown, and a fader. Splash had only ever held a camera and a light.
///   * CricketVR, Nets - the same MenuRoot prefab with PauseScreen as its root and no backdrop,
///               closed until B, plus a fader. The scene's existing SettingsPanel is attached as
///               the third screen, so Settings is reached the same way everywhere.
///
/// Splash must be build index 0 to be the launch scene.
///
/// Run Tools > CricketVR > Build Menu Prefabs first: this places that prefab, it does not build
/// it.
/// </summary>
public static class MenuSceneBuilder
{
    private const string SplashPath = "Assets/Scenes/Splash.unity";
    private const string CricketPath = "Assets/Scenes/CricketVR.unity";
    private const string NetsPath = "Assets/Scenes/Nets.unity";
    private const string MenuRootPrefab = "Assets/Resources/Prefabs/UI/MenuRoot.prefab";

    [MenuItem("Tools/CricketVR/Build Menus In All Scenes")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        BuildSplash();
        BuildMatchScene(CricketPath, confirmOnLeave: true);
        BuildMatchScene(NetsPath, confirmOnLeave: false);
        SetUpBuildList();

        AssetDatabase.SaveAssets();
        Debug.Log("[MenuSceneBuilder] Menus placed in Splash, CricketVR and Nets; " +
                  "build list set with Splash at index 0.");
    }

    // ---- Splash -----------------------------------------------------------------------------------

    private static void BuildSplash()
    {
        Scene scene = EditorSceneManager.OpenScene(SplashPath, OpenSceneMode.Single);

        SetUpSplashRig();
        CricketVRSceneBuilder.SetUpPointer();

        MenuRoot menu = PlaceMenuRoot(rootScreen: "main", showBackdrop: true);
        if (menu == null)
            return;
        if (menu.GetComponent<SplashMenuOpener>() == null)
            menu.gameObject.AddComponent<SplashMenuOpener>();

        StripMatchOnlyUI();
        AttachSettingsScreen(menu, FindOrInstantiateSettingsPanel());
        EnsureFader();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[MenuSceneBuilder] Splash built.");
    }

    /// The camera under a rig root at y = 0 with XRRigSetup, so the player gets their own eye
    /// height from a floor-relative tracking origin - the same arrangement as the match scenes,
    /// so the menu sits at the right height.
    private static void SetUpSplashRig()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener))
            {
                tag = "MainCamera"
            };
            camera = go.GetComponent<Camera>();
        }
        Transform rig = camera.transform.parent;
        if (rig == null)
        {
            var rigGo = GameObject.Find("XRRig") ?? new GameObject("XRRig");
            rigGo.transform.position = Vector3.zero;
            camera.transform.SetParent(rigGo.transform, false);
            camera.transform.localPosition = Vector3.zero;
            rig = rigGo.transform;
        }
        if (rig.GetComponent<XRRigSetup>() == null)
            rig.gameObject.AddComponent<XRRigSetup>();

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.043f, 0.024f, 0.094f);   // matches the grade's top
        camera.nearClipPlane = 0.05f;
    }

    /// The between-balls dock, grip calibration and the last-shot card are match UI. They have
    /// no meaning in the menu scene, and the shot card in particular renders as a panel floating
    /// out in space beside the menu.
    private static void StripMatchOnlyUI()
    {
        foreach (string name in new[] { "NextBallMenu", "ShotCard", "GripCalibrationPanel" })
        {
            GameObject go = GameObject.Find("UI/" + name) ?? GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
                Debug.Log($"[MenuSceneBuilder] Removed match-only {name} from Splash.");
            }
        }
    }

    /// Splash has no Main, so Build Player UI cannot run there. Take the prefab straight.
    ///
    /// Re-running this tool must not stack up copies, so any extras are removed first: an earlier
    /// version left two SettingsPanels under MenuRoot, both registered as the "settings" screen.
    private static SettingsPanel FindOrInstantiateSettingsPanel()
    {
        SettingsPanel[] all = Object.FindObjectsByType<SettingsPanel>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 1; i < all.Length; i++)
        {
            Debug.Log("[MenuSceneBuilder] Removed a duplicate SettingsPanel.");
            Object.DestroyImmediate(all[i].gameObject);
        }
        if (all.Length > 0 && all[0] != null)
            return all[0];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/UI/SettingsPanel.prefab");
        if (prefab == null)
        {
            Debug.LogError("[MenuSceneBuilder] No SettingsPanel prefab - run " +
                           "Tools > CricketVR > Build Player UI in CricketVR first.");
            return null;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "SettingsPanel";
        return instance.GetComponent<SettingsPanel>();
    }

    // ---- Match scenes -----------------------------------------------------------------------------

    private static void BuildMatchScene(string path, bool confirmOnLeave)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        MenuRoot menu = PlaceMenuRoot(rootScreen: "pause", showBackdrop: false);
        if (menu == null)
            return;

        var pause = menu.GetComponentInChildren<PauseScreen>(true);
        if (pause != null)
        {
            var pso = new SerializedObject(pause);
            pso.FindProperty("confirmOnLeave").boolValue = confirmOnLeave;
            pso.ApplyModifiedProperties();
        }

        AttachSettingsScreen(menu, Object.FindFirstObjectByType<SettingsPanel>(
            FindObjectsInactive.Include));
        EnsureFader();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MenuSceneBuilder] {System.IO.Path.GetFileNameWithoutExtension(path)} built " +
                  $"(confirm on leave: {confirmOnLeave}).");
    }

    // ---- Shared -------------------------------------------------------------------------------------

    private static MenuRoot PlaceMenuRoot(string rootScreen, bool showBackdrop)
    {
        MenuRoot menu = Object.FindFirstObjectByType<MenuRoot>(FindObjectsInactive.Include);
        if (menu == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuRootPrefab);
            if (prefab == null)
            {
                Debug.LogError("[MenuSceneBuilder] No MenuRoot prefab - run " +
                               "Tools > CricketVR > Build Menu Prefabs first.");
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "MenuRoot";
            Transform uiRoot = FindOrCreateRoot("UI");
            instance.transform.SetParent(uiRoot, false);
            menu = instance.GetComponent<MenuRoot>();
        }
        // Re-pose even an instance that already existed: a prefab instance keeps its own
        // transform, so a fix to the prefab's default pose does not reach scenes already built.
        menu.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.45f), Quaternion.identity);

        var so = new SerializedObject(menu);
        so.FindProperty("rootScreen").stringValue = rootScreen;
        so.FindProperty("showBackdrop").boolValue = showBackdrop;
        so.ApplyModifiedProperties();
        return menu;
    }

    /// Put the scene's settings panel under MenuRoot as the "settings" screen, so Back returns
    /// to whoever opened it.
    private static void AttachSettingsScreen(MenuRoot menu, SettingsPanel panel)
    {
        if (menu == null || panel == null)
            return;

        var screen = panel.GetComponent<SettingsScreen>()
                     ?? panel.gameObject.AddComponent<SettingsScreen>();
        var sso = new SerializedObject(screen);
        sso.FindProperty("screenName").stringValue = "settings";
        sso.ApplyModifiedProperties();

        if (panel.transform.parent != menu.transform)
            panel.transform.SetParent(menu.transform, false);

        // Rebuild the screens list so re-running cannot leave stale or duplicate entries: the
        // two screens from the prefab, then this settings screen.
        var so = new SerializedObject(menu);
        SerializedProperty screens = so.FindProperty("screens");
        var keep = new List<Object>();
        for (int i = 0; i < screens.arraySize; i++)
        {
            Object entry = screens.GetArrayElementAtIndex(i).objectReferenceValue;
            if (entry != null && entry != screen && !keep.Contains(entry))
                keep.Add(entry);
        }
        keep.Add(screen);
        screens.arraySize = keep.Count;
        for (int i = 0; i < keep.Count; i++)
            screens.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
        so.ApplyModifiedProperties();
    }

    /// A screen-space overlay canvas with a full-screen black image, sorting above everything.
    private static void EnsureFader()
    {
        if (Object.FindFirstObjectByType<SceneFader>(FindObjectsInactive.Include) != null)
            return;
        var go = new GameObject("Fader", typeof(Canvas), typeof(CanvasScaler),
                                typeof(SceneFader));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var imageGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(go.transform, false);
        WorldPanelBuilder.Stretch(imageGo.GetComponent<RectTransform>());
        var image = imageGo.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        var so = new SerializedObject(go.GetComponent<SceneFader>());
        so.FindProperty("black").objectReferenceValue = image;
        so.ApplyModifiedProperties();
    }

    private static Transform FindOrCreateRoot(string name)
    {
        GameObject go = GameObject.Find(name) ?? new GameObject(name);
        return go.transform;
    }

    private static void SetUpBuildList()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(SplashPath, true),
            new EditorBuildSettingsScene(CricketPath, true),
            new EditorBuildSettingsScene(NetsPath, true),
        };
        // Keep the existing disabled Oculus sample entries after ours, still disabled.
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.path == SplashPath || s.path == CricketPath || s.path == NetsPath)
                continue;
            scenes.Add(new EditorBuildSettingsScene(s.path, false));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
