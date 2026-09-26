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

        // The player UI (settings, between-balls menu with the replay card, grip calibration, shot
        // card) - see UIPrefabBuilder.
        SettingsPanel settings = UIPrefabBuilder.BuildPanels(uiRoot);

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
