using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build UI Prefabs. Does in the editor, once, everything the game used to
/// create when it started, so the build creates nothing at runtime:
///   * builds the SettingsPanel, NextBallMenu and GripCalibrationPanel prefabs (controls wired
///     with saved listeners) and places them in the scene under "UI";
///   * places the menu pointer: the UIHelpers EventSystem + laser, the UI input module with a saved
///     input-actions asset, and the OpenXRMenuInputModule driver with its aim transform;
///   * retires the Oculus DebugUIBuilder canvas that built the old menu at runtime;
///   * puts the controller trackers and the hand models under the hand anchors, the keyboard
///     movement controller on the rig, and an AudioSource on the crowd;
///   * puts BatGripCalibration and a fitted BatGeometry on the Bat prefab;
///   * makes the replay's RenderTexture assets and points the replay camera and screen at them.
/// Re-run it after changing any of this; it replaces what it made last time.
/// </summary>
public static class CricketVRSceneBuilder
{
    private const string UiFolder = "Assets/Resources/Prefabs/UI";
    private const string ActionsPath = "Assets/Resources/Input/MenuUIActions.inputactions";

    [MenuItem("Tools/CricketVR/Build UI Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(UiFolder);
        Transform uiRoot = FindOrCreateRoot("UI");

        SettingsPanel settings = BuildSettingsPanel(uiRoot);
        BuildNextBallMenu(uiRoot);
        BuildGripCalibrationPanel(uiRoot);

        var main = Object.FindFirstObjectByType<Main>();
        var mainSo = new SerializedObject(main);
        mainSo.FindProperty("settingsPanel").objectReferenceValue = settings;
        mainSo.ApplyModifiedProperties();

        SetUpPointer();
        RetireDebugUIBuilder();
        SetUpPlayer(main);
        SetUpBatPrefab();
        SetUpReplay();

        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        EditorSceneManager.SaveScene(main.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CricketVRSceneBuilder] UI prefabs, pointer, player, bat and replay set up. Nothing is created at runtime.");
    }

    // ---- Panels ------------------------------------------------------------------------------

    private static SettingsPanel BuildSettingsPanel(Transform uiRoot)
    {
        var root = new GameObject("SettingsPanel");
        var c = root.AddComponent<SettingsPanel>();
        var panel = WorldPanelBuilder.Panel(root, 1000f, null);

        Transform header = WorldPanelBuilder.Column(panel.transform, false);
        header.GetComponent<LayoutElement>().preferredHeight = 60f;
        var title = WorldPanelBuilder.Text(header, "Settings", 40f, TMPro.FontStyles.Bold, Color.white, 60f, TMPro.TextAlignmentOptions.Left);
        title.GetComponent<LayoutElement>().flexibleWidth = 1f;
        var close = WorldPanelBuilder.Button(header, "Close  <size=70%>(B)</size>", c.OnClose, WorldPanelBuilder.Neutral, 56f);
        close.transform.parent.GetComponent<LayoutElement>().preferredWidth = 220f;
        close.transform.parent.GetComponent<LayoutElement>().flexibleWidth = 0f;

        Transform body = WorldPanelBuilder.Column(panel.transform, false, 28f);
        Transform left = WorldPanelBuilder.Column(body, true);
        Transform right = WorldPanelBuilder.Column(body, true);

        WorldPanelBuilder.Section(left, "Batting");
        Transform hands = WorldPanelBuilder.Column(left, false);
        var handGroup = hands.gameObject.AddComponent<ToggleGroup>();
        c.leftHanded = WorldPanelBuilder.Choice(hands, "Left-handed", c.OnLeftHanded, handGroup);
        c.rightHanded = WorldPanelBuilder.Choice(hands, "Right-handed", c.OnRightHanded, handGroup);
        WorldPanelBuilder.Button(left, "Calibrate Bat Grip", c.OnCalibrateGrip, WorldPanelBuilder.Primary);
        WorldPanelBuilder.Button(left, "Reset Bat Grip", c.OnResetGrip, WorldPanelBuilder.Neutral, 52f);
        WorldPanelBuilder.Section(left, "Difficulty");
        Transform levels = WorldPanelBuilder.Column(left, false);
        var levelGroup = levels.gameObject.AddComponent<ToggleGroup>();
        c.easy = WorldPanelBuilder.Choice(levels, "Easy", c.OnEasy, levelGroup);
        c.medium = WorldPanelBuilder.Choice(levels, "Medium", c.OnMedium, levelGroup);
        c.hard = WorldPanelBuilder.Choice(levels, "Hard", c.OnHard, levelGroup);
        WorldPanelBuilder.Section(left, "Display");
        c.overlay = WorldPanelBuilder.Choice(left, "Show Debug Overlay", c.OnOverlay, null);

        WorldPanelBuilder.Section(right, "Game");
        c.resetDelay = WorldPanelBuilder.Slider(right, "Reset delay", 0f, 5f, c.OnResetDelay);
        c.fielderSpeed = WorldPanelBuilder.Slider(right, "Fielder speed", 0.5f, 2.5f, c.OnFielderSpeed);
        c.batPower = WorldPanelBuilder.Slider(right, "Bat power", 50f, 200f, c.OnBatPower);
        c.ampMin = WorldPanelBuilder.Slider(right, "Min amplifier", 0f, 15f, c.OnAmpMin);
        c.ampMax = WorldPanelBuilder.Slider(right, "Max amplifier", 0f, 25f, c.OnAmpMax);
        c.bowlingType = WorldPanelBuilder.Section(right, "Bowling");
        c.minSpeed = WorldPanelBuilder.Slider(right, "Min speed", 0f, 10f, c.OnMinSpeed);
        c.maxSpeed = WorldPanelBuilder.Slider(right, "Max speed", 0f, 10f, c.OnMaxSpeed);
        c.minLine = WorldPanelBuilder.Slider(right, "Min line", -0.75f, 0.75f, c.OnMinLine);
        c.maxLine = WorldPanelBuilder.Slider(right, "Max line", -0.75f, 0.75f, c.OnMaxLine);
        c.minSwing = WorldPanelBuilder.Slider(right, "Min swing", 0f, 1f, c.OnMinSwing);
        c.maxSwing = WorldPanelBuilder.Slider(right, "Max swing", 0f, 1f, c.OnMaxSwing);
        c.minTurn = WorldPanelBuilder.Slider(right, "Min turn", -0.1f, 1f, c.OnMinTurn);
        c.maxTurn = WorldPanelBuilder.Slider(right, "Max turn", -0.1f, 1f, c.OnMaxTurn);

        SetField(c, "panel", panel);
        Save(root, uiRoot);
        return c;   // the scene instance (SaveAsPrefabAssetAndConnect returns the asset, not this)
    }

    private static void BuildNextBallMenu(Transform uiRoot)
    {
        var root = new GameObject("NextBallMenu");
        var c = root.AddComponent<NextBallMenu>();
        var panel = WorldPanelBuilder.Panel(root, 460f, null);
        WorldPanelBuilder.Button(panel.transform, "Next Ball  <size=70%>(A)</size>", c.OnNextBall, WorldPanelBuilder.Primary, 70f);
        var bowler = WorldPanelBuilder.Button(panel.transform, "Change Bowler", c.OnChangeBowler, WorldPanelBuilder.Accent);
        WorldPanelBuilder.Button(panel.transform, "Settings", c.OnSettings, WorldPanelBuilder.Neutral);
        WorldPanelBuilder.Button(panel.transform, "Calibrate Grip", c.OnCalibrateGrip, WorldPanelBuilder.Neutral);
        SetField(c, "panel", panel);
        SetField(c, "bowlerLabel", bowler);
        Save(root, uiRoot);
    }

    private static void BuildGripCalibrationPanel(Transform uiRoot)
    {
        var root = new GameObject("GripCalibrationPanel");
        var c = root.AddComponent<GripCalibrationPanel>();
        var panel = WorldPanelBuilder.Panel(root, 440f, "Calibrate Grip",
            "Hold the see-through handle the way you bat, then lock it. Stand the bat upright or lay it flat - whichever is easier to line up.");
        Transform pair = WorldPanelBuilder.Column(panel.transform, false);
        pair.GetComponent<LayoutElement>().preferredHeight = 62f;
        WorldPanelBuilder.Button(pair, "Upright", c.OnUpright, WorldPanelBuilder.Accent, 62f, out Image upright);
        WorldPanelBuilder.Button(pair, "Flat", c.OnFlat, WorldPanelBuilder.Neutral, 62f, out Image flat);
        WorldPanelBuilder.Button(panel.transform, "Lock Grip  <size=70%>(A)</size>", c.OnLock, WorldPanelBuilder.Primary, 68f);
        WorldPanelBuilder.Button(panel.transform, "Cancel  <size=70%>(B)</size>", c.OnCancel, WorldPanelBuilder.Danger);
        WorldPanelBuilder.Text(panel.transform, "X / Y also switches upright / flat", 20f, TMPro.FontStyles.Italic, WorldPanelBuilder.Muted, 30f);
        SetField(c, "panel", panel);
        SetField(c, "uprightButton", upright);
        SetField(c, "flatButton", flat);
        SetField(c, "selected", WorldPanelBuilder.Accent);
        SetField(c, "unselected", WorldPanelBuilder.Neutral);
        Save(root, uiRoot);
    }

    /// Save as a prefab under Resources/Prefabs/UI and leave `root` in the scene as a connected
    /// instance, replacing any previous instance. Returns the prefab asset.
    private static GameObject Save(GameObject root, Transform uiRoot)
    {
        Transform old = uiRoot.Find(root.name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        root.transform.SetParent(uiRoot, false);
        string path = $"{UiFolder}/{root.name}.prefab";
        return PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
    }

    // ---- Pointer / input ---------------------------------------------------------------------

    private static void SetUpPointer()
    {
        LaserPointer laser = Object.FindFirstObjectByType<LaserPointer>(FindObjectsInactive.Include);
        if (laser == null)
        {
            var helpers = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Oculus/SampleFramework/Core/DebugUI/Prefabs/UIHelpers.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(helpers);
            instance.name = "UIHelpers";
            laser = instance.GetComponentInChildren<LaserPointer>(true);
        }
        EventSystem system = laser.transform.root.GetComponentInChildren<EventSystem>(true) ?? Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        foreach (var old in system.GetComponents<BaseInputModule>())
            if (!(old is InputSystemUIInputModule)) old.enabled = false;

        var module = system.GetComponent<InputSystemUIInputModule>() ?? system.gameObject.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = MenuActions();
        var driver = system.GetComponent<OpenXRMenuInputModule>() ?? system.gameObject.AddComponent<OpenXRMenuInputModule>();
        Transform aim = system.transform.Find("OpenXR Menu Aim");
        if (aim == null)
        {
            aim = new GameObject("OpenXR Menu Aim").transform;
            aim.SetParent(system.transform, false);
        }

        var beam = laser.GetComponent<LineRenderer>();
        beam.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/XRPointer.mat");
        beam.useWorldSpace = true;
        beam.widthMultiplier = 0.004f;
        beam.numCapVertices = 4;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.receiveShadows = false;
        if (laser.cursorVisual != null && laser.cursorVisual.GetComponent<Renderer>() != null)
            laser.cursorVisual.GetComponent<Renderer>().sharedMaterial = beam.sharedMaterial;

        SetField(driver, "inputModule", module);
        SetField(driver, "menuActions", module.actionsAsset);
        SetField(driver, "laser", laser);
        SetField(driver, "beam", beam);
        SetField(driver, "aim", aim);
    }

    /// The default UI actions, with the tracked-device pointer bound to the XR controllers' aim
    /// pose, saved as an asset (previously cloned and rebound at runtime).
    private static InputActionAsset MenuActions()
    {
        var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (existing != null)
            return existing;
        var temp = new GameObject("temp");
        var module = temp.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        var actions = Object.Instantiate(module.actionsAsset);
        Object.DestroyImmediate(temp);
        Rebind(actions.FindAction("UI/TrackedDevicePosition"), "<XRController>/pointerPosition");
        Rebind(actions.FindAction("UI/TrackedDeviceOrientation"), "<XRController>/pointerRotation");
        Directory.CreateDirectory(Path.GetDirectoryName(ActionsPath));
        File.WriteAllText(ActionsPath, actions.ToJson());
        AssetDatabase.ImportAsset(ActionsPath);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
    }

    private static void Rebind(InputAction action, string path)
    {
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
            action.ChangeBinding(i).WithPath(path);
    }

    private static void RetireDebugUIBuilder()
    {
        foreach (var d in Object.FindObjectsByType<DebugUIBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            d.gameObject.SetActive(false);
    }

    // ---- Player, crowd, bat, replay ------------------------------------------------------------

    private static void SetUpPlayer(Main main)
    {
        var bat = main.theBat.GetComponent<Bat>();
        foreach (bool left in new[] { true, false })
        {
            Transform anchor = (left ? bat.leftHandParent : bat.rightHandParent).parent;
            var tracker = anchor.GetComponent<XRControllerTracker>() ?? anchor.gameObject.AddComponent<XRControllerTracker>();
            tracker.isLeftHand = left;
            var visual = anchor.GetComponentInChildren<XRHandVisual>(true);
            if (visual == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(left ? "Assets/Resources/Prefabs/XRLeftHand.prefab" : "Assets/Resources/Prefabs/XRRightHand.prefab");
                visual = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor)).GetComponent<XRHandVisual>();
            }
            SetField(tracker, "visual", visual);
        }
        Transform rig = bat.leftHandParent.parent.parent.parent;
        if (rig.GetComponent<SimplePlayerController>() == null)
            rig.gameObject.AddComponent<SimplePlayerController>();

        var crowd = Object.FindFirstObjectByType<CrowdController>();
        if (crowd != null && crowd.GetComponent<AudioSource>() == null)
        {
            var audio = crowd.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
        }
    }

    private static void SetUpBatPrefab()
    {
        const string path = "Assets/Resources/Prefabs/Bat.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<BatGripCalibration>() == null)
            root.AddComponent<BatGripCalibration>();
        var geometry = root.GetComponent<BatGeometry>() ?? root.AddComponent<BatGeometry>();
        if (!geometry.IsFitted)
            geometry.Fit();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void SetUpReplay()
    {
        RenderTexture view = RenderTextureAsset("Assets/Resources/Textures/ReplayView.renderTexture", false, 24);
        RenderTexture frames = RenderTextureAsset("Assets/Resources/Textures/ReplayFrames.renderTexture", true, 0);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/Large Displays/ReplayDisplay.mat");
        material.shader = Shader.Find("CricketVR/ReplayScreen");
        material.SetTexture("_Frames", frames);
        material.SetFloat("_Slice", -1f);
        EditorUtility.SetDirty(material);

        var replay = Object.FindFirstObjectByType<CameraReplay>();
        var camera = replay.GetComponent<Camera>();
        camera.enabled = false;
        camera.targetTexture = view;
        camera.farClipPlane = 250f;
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            data.renderShadows = false;
            data.renderPostProcessing = false;
        }
        SetField(replay, "targetMat", material);
        SetField(replay, "view", view);
        SetField(replay, "frames", frames);
    }

    /// 320x240 is all a 16 m screen 80 m away can show on a Quest 2.
    private static RenderTexture RenderTextureAsset(string path, bool array, int depth)
    {
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
        if (existing != null)
            return existing;
        var rt = new RenderTexture(320, 240, depth, RenderTextureFormat.ARGB32)
        {
            dimension = array ? UnityEngine.Rendering.TextureDimension.Tex2DArray : UnityEngine.Rendering.TextureDimension.Tex2D,
            volumeDepth = array ? 64 : 1,
            useMipMap = false,
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
        };
        AssetDatabase.CreateAsset(rt, path);
        return rt;
    }

    // ---- Helpers -------------------------------------------------------------------------------

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
            Debug.LogError($"[CricketVRSceneBuilder] {target.GetType().Name} has no field '{field}'");
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
