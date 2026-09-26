using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds Assets/Scenes/BatOffsetDebug.unity - a bare scene holding nothing but the two hand
/// models, the two grip points and a bat for each, so the grab offsets can be judged in the Editor
/// instead of over a device build.
///
/// The rig mirrors the real hierarchy exactly. In CricketVR.unity the hand anchor carries two
/// children at identity: the grip transform the bat attaches to (Bat.leftHandParent /
/// rightHandParent, the objects formerly showing as "Missing Prefab") and the hand visual prefab.
/// Getting that nesting right is the whole point - it is what makes an offset found here correct in
/// the game.
///
/// Menu: Tools/CricketVR/Build Bat Offset Debug Scene
/// </summary>
public static class BuildBatOffsetDebugScene
{
    private const string ScenePath = "Assets/Scenes/BatOffsetDebug.unity";
    private const float RigSeparation = 1.6f;

    [MenuItem("Tools/CricketVR/Build Bat Offset Debug Scene")]
    public static void Build()
    {
        var batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Bat.prefab");
        var leftHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/XRLeftHand.prefab");
        var rightHandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/XRRightHand.prefab");
        if (batPrefab == null || leftHandPrefab == null || rightHandPrefab == null)
        {
            Debug.LogError("[BatOffsetDebug] missing Bat/XRLeftHand/XRRightHand prefab - run " +
                           "Tools/CricketVR/Rebuild OpenXR Hand Visuals first.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // A light, or the hands read as flat silhouettes and the grip is impossible to judge.
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        var cameraGO = new GameObject("Preview Camera");
        var camera = cameraGO.AddComponent<Camera>();
        camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraGO.transform.position = new Vector3(0f, 0.35f, -1.5f);
        cameraGO.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        cameraGO.tag = "MainCamera";

        var previewGO = new GameObject("BatGripPreview");
        var preview = previewGO.AddComponent<BatGripPreview>();

        BuildRig(true, leftHandPrefab, batPrefab, preview, -RigSeparation * 0.5f);
        BuildRig(false, rightHandPrefab, batPrefab, preview, RigSeparation * 0.5f);

        // Seed from whatever the Bat prefab currently holds, so tuning starts from the real values.
        var bat = batPrefab.GetComponent<Bat>();
        preview.leftGrabOffsetPosition = bat.leftGrabOffsetPosition;
        preview.leftGrabOffsetEuler = bat.leftGrabOffsetEuler;
        preview.rightGrabOffsetPosition = bat.rightGrabOffsetPosition;
        preview.rightGrabOffsetEuler = bat.rightGrabOffsetEuler;
        preview.Apply();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[BatOffsetDebug] built " + ScenePath +
                  "\n  seeded L=" + preview.leftGrabOffsetPosition + " / " + preview.leftGrabOffsetEuler +
                  "\n         R=" + preview.rightGrabOffsetPosition + " / " + preview.rightGrabOffsetEuler);
    }

    private static void BuildRig(bool left, GameObject handPrefab, GameObject batPrefab,
                                 BatGripPreview preview, float x)
    {
        string side = left ? "Left" : "Right";

        var rig = new GameObject(side + "Rig");
        rig.transform.position = new Vector3(x, 0f, 0f);

        // The hand anchor is the controller pose. Everything below it sits at identity, exactly as
        // it does under LeftHandAnchor / RightHandAnchor in CricketVR.unity.
        var anchor = new GameObject(side + "HandAnchor");
        anchor.transform.SetParent(rig.transform, false);

        var grip = new GameObject(side + "HandGrip");
        grip.transform.SetParent(anchor.transform, false);

        var hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab);
        hand.transform.SetParent(anchor.transform, false);
        // XRHandVisual hides the renderers until its controller reports tracking, which never
        // happens here, so take it off and leave the mesh visible.
        var visual = hand.GetComponent<XRHandVisual>();
        if (visual != null) visual.enabled = false;
        foreach (var r in hand.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

        var batGO = (GameObject)PrefabUtility.InstantiatePrefab(batPrefab);
        batGO.name = "Bat (" + side + ")";
        batGO.transform.SetParent(rig.transform, false);
        // Keep the bat inert: the preview drives its transform, and a live Bat/Rigidbody would
        // fight it or throw on entering play mode in a scene with no Main.
        var batScript = batGO.GetComponent<Bat>();
        if (batScript != null) batScript.enabled = false;
        var body = batGO.GetComponent<Rigidbody>();
        if (body != null) { body.isKinematic = true; body.useGravity = false; }
        foreach (var c in batGO.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        var animator = hand.GetComponentInChildren<Animator>(true);
        if (left)
        {
            preview.leftGrip = grip.transform;
            preview.leftBat = batGO.transform;
            preview.leftHand = animator;
        }
        else
        {
            preview.rightGrip = grip.transform;
            preview.rightBat = batGO.transform;
            preview.rightHand = animator;
        }
    }
}
