using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > CricketVR > Build Menu Layout.
///   * Builds the MenuLayout prefab: a root at standing eye height facing +Z, and one child per
///     screen at its fixed pose in front of the player (see MenuLayout).
///   * Places it in CricketVR at the crease, facing the bowler (-X), and in Splash at the origin.
///   * Splash: gives its camera head tracking (a TrackedPoseDriver on the headset's centre eye,
///     as in CricketVR). Without one the camera never moved - the menu scene was drawn as a flat,
///     head-locked picture that could not be looked around.
///   * Splash: removes Practice in Nets from the main menu.
/// </summary>
public static class MenuLayoutBuilder
{
    public const string PrefabPath = "Assets/Resources/Prefabs/UI/MenuLayout.prefab";
    private const string SplashPath = "Assets/Scenes/Splash.unity";
    private const string CricketPath = "Assets/Scenes/CricketVR.unity";
    private const string MenuRootPrefab = "Assets/Resources/Prefabs/UI/MenuRoot.prefab";

    /// Standing eye height above the floor the spots are laid out from.
    private const float EyeHeight = 1.6f;

    [MenuItem("Tools/CricketVR/Build Menu Layout")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        BuildPrefab();
        RemoveNetsButton();

        Scene cricket = EditorSceneManager.OpenScene(CricketPath, OpenSceneMode.Single);
        XRRigSetup rig = Object.FindFirstObjectByType<XRRigSetup>();
        float floor = rig != null ? rig.transform.position.y : 0f;
        Place(new Vector3(BallDelivery.BatsmanStumpsX - 0.95f, floor + EyeHeight, 0f),
              Quaternion.LookRotation(Vector3.left, Vector3.up));
        EditorSceneManager.SaveScene(cricket);

        Scene splash = EditorSceneManager.OpenScene(SplashPath, OpenSceneMode.Single);
        Place(new Vector3(0f, EyeHeight, 0f), Quaternion.identity);
        TrackHead(Camera.main);
        EditorSceneManager.SaveScene(splash);

        Debug.Log("[MenuLayoutBuilder] MenuLayout built and placed in CricketVR and Splash.");
    }

    private static void BuildPrefab()
    {
        var root = new GameObject("MenuLayout", typeof(MenuLayout));
        try
        {
            // Local to the eye point, +Z ahead. Canvases face -Z, so each spot's +Z points away
            // from the player; the pitch tilts the card up toward the eyes.
            Spot(root, MenuLayout.Menu, new Vector3(0f, -0.1f, 1.45f), 8f);
            Spot(root, MenuLayout.Settings, new Vector3(0f, -0.1f, 1.45f), 8f);
            // Lower, so the pitch stays in view over the top of it.
            Spot(root, MenuLayout.NextBall, new Vector3(0f, -0.38f, 1.1f), 20f);
            // Beside the free hand during grip calibration, clear of both bat shells.
            Spot(root, MenuLayout.GripPanelLeftHanded, new Vector3(0.45f, -0.2f, 0.7f), 0f);
            Spot(root, MenuLayout.GripPanelRightHanded, new Vector3(-0.45f, -0.2f, 0.7f), 0f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Spot(GameObject root, string name, Vector3 local, float tiltDegrees)
    {
        var spot = new GameObject(name);
        spot.transform.SetParent(root.transform, false);
        spot.transform.localPosition = local;
        spot.transform.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f);
    }

    /// One instance per scene, re-posed if it is already there.
    private static void Place(Vector3 position, Quaternion rotation)
    {
        MenuLayout layout = Object.FindFirstObjectByType<MenuLayout>(FindObjectsInactive.Include);
        if (layout == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject ui = GameObject.Find("UI");
            if (ui != null)
                instance.transform.SetParent(ui.transform, false);
            layout = instance.GetComponent<MenuLayout>();
        }
        layout.transform.SetPositionAndRotation(position, rotation);
        EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
    }

    /// The headset's centre eye drives the camera, before rendering as well as in Update.
    public static void TrackHead(Camera camera)
    {
        if (camera == null || camera.GetComponent<TrackedPoseDriver>() != null)
            return;
        var driver = camera.gameObject.AddComponent<TrackedPoseDriver>();
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        driver.positionInput = new InputActionProperty(
            new InputAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        driver.rotationInput = new InputActionProperty(
            new InputAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        EditorUtility.SetDirty(driver);
    }

    /// Practice in Nets is off the main menu for now. The button is taken out of the prefab
    /// (MenuPrefabBuilder no longer makes it either); MainMenuScreen.OnPracticeInNets stays.
    private static void RemoveNetsButton()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MenuRootPrefab);
        try
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Practice in Nets")
                {
                    Object.DestroyImmediate(t.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, MenuRootPrefab);
                    break;
                }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
