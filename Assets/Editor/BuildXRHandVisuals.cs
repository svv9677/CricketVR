using UnityEngine;
using UnityEditor;

/// <summary>
/// Builds the two hand prefabs that <see cref="XRControllerTracker"/> loads at runtime from
/// Resources/Prefabs, plus the two materials the hands and the menu pointer use.
///
/// The models are the Oculus CustomHands sample hands (l/r_hand_skeletal_lowres), restored from
/// this repository's history under Assets/Hands together with their pose clips and animator
/// controllers. Those are the hands authored to be held on a Touch controller, which is what this
/// game has; the OculusHand_* meshes under Assets/Oculus/VR are the optical hand-tracking meshes
/// and expect a tracked 24-bone skeleton streamed per frame, which the project does not have.
///
/// Menu: Tools/CricketVR/Rebuild OpenXR Hand Visuals
/// </summary>
public static class BuildXRHandVisuals
{
    private const string ModelFolder = "Assets/Hands/Models";
    private const string PrefabFolder = "Assets/Resources/Prefabs";
    private const string MaterialFolder = "Assets/Resources/Materials";

    // Where the wrist sits relative to the controller's grip pose. Taken from the sample's own
    // CustomHandLeft/CustomHandRight prefabs, which parented the model to the hand anchor under a
    // root rotated +/-90 degrees about Z with a child "Offset" at z = -0.0298. OpenXR's
    // devicePosition/deviceRotation is the same grip pose OVRInput reported, so the numbers carry
    // over unchanged.
    private const float WristOffsetZ = -0.0298f;
    private const float WristRollZ = 90f;

    [MenuItem("Tools/CricketVR/Rebuild OpenXR Hand Visuals")]
    public static void Build()
    {
        var log = new System.Text.StringBuilder();

        Material handMaterial = MaterialAt(MaterialFolder + "/XRHands.mat", "Universal Render Pipeline/Lit");
        handMaterial.SetColor("_BaseColor", new Color(0.72f, 0.78f, 0.86f));
        handMaterial.SetFloat("_Smoothness", 0.25f);
        // A little self-illumination so the hands stay readable when they pass through the
        // stadium's shadowed areas, rather than going to near-black right in front of the eyes.
        // URP's material validation recomputes the keyword set when the asset is saved and drops
        // a bare EnableKeyword("_EMISSION"), so the GI flag has to be set as well - that is what
        // the validation reads to decide emission is actually in use.
        handMaterial.SetColor("_EmissionColor", new Color(0.09f, 0.10f, 0.12f));
        handMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        handMaterial.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(handMaterial);

        // The menu pointer beam and cursor read this material back through Resources at runtime
        // (see OpenXRMenuInputModule), so it is built here alongside the hands.
        Material pointerMaterial = MaterialAt(MaterialFolder + "/XRPointer.mat", "Universal Render Pipeline/Unlit");
        pointerMaterial.SetColor("_BaseColor", new Color(0.2f, 0.9f, 1f));
        EditorUtility.SetDirty(pointerMaterial);

        for (int i = 0; i < 2; i++)
        {
            bool left = i == 0;
            BuildHand(left, handMaterial, log);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BuildXRHandVisuals]\n" + log);
    }

    private static void BuildHand(bool left, Material handMaterial, System.Text.StringBuilder log)
    {
        string side = left ? "l" : "r";
        string modelPath = ModelFolder + "/" + side + "_hand_skeletal_lowres.fbx";
        string controllerPath = ModelFolder + "/" + (left ? "Left" : "Right") + "HandAnimator.controller";
        string prefabName = left ? "XRLeftHand" : "XRRightHand";

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null) { log.AppendLine("MISSING " + modelPath); return; }
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
        if (controller == null) { log.AppendLine("MISSING " + controllerPath); return; }

        FillControllerHoldPose(controller, left, log);

        var root = new GameObject(prefabName);
        try
        {
            GameObject mesh = Object.Instantiate(source, root.transform, false);
            mesh.name = "Hand Mesh";
            mesh.transform.localPosition = new Vector3(0f, 0f, WristOffsetZ);
            mesh.transform.localRotation = Quaternion.Euler(0f, 0f, left ? WristRollZ : -WristRollZ);

            var animator = mesh.GetComponent<Animator>();
            if (animator == null) animator = mesh.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            // The hands sit at the edge of the view frustum constantly; without this the animator
            // stops evaluating whenever the bounds fall offscreen and the pose freezes mid-grip.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var renderer in mesh.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.sharedMaterial = handMaterial;
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            foreach (var collider in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            var visual = root.AddComponent<XRHandVisual>();
            visual.isLeftHand = left;
            // Serialised now so the runtime does not have to search for it.
            var so = new SerializedObject(visual);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/" + prefabName + ".prefab");
            log.AppendLine(prefabName + ": model=" + source.name + " controller=" + controller.name
                           + " bones=" + mesh.GetComponentInChildren<SkinnedMeshRenderer>(true).bones.Length
                           + " offset=" + mesh.transform.localPosition
                           + " roll=" + mesh.transform.localRotation.eulerAngles.z.ToString("F0"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// <summary>
    /// The restored controllers have one empty state: "Controller Hold", the Pose = Controller
    /// branch. Its clip reference was already broken before the SDK folder was removed - the GUID
    /// it points at exists nowhere in this repository's history - so fill it from the clip that
    /// matches by name. Nothing here selects that pose, but an empty state in a live controller is
    /// a trap for whoever sets Pose next.
    /// </summary>
    private static void FillControllerHoldPose(UnityEditor.Animations.AnimatorController controller,
                                               bool left, System.Text.StringBuilder log)
    {
        foreach (var layer in controller.layers)
        {
            foreach (var child in layer.stateMachine.states)
            {
                // The two controllers spell this state differently - the left calls it
                // "Controller Hold", the right "Hold Controller" - and their first layer is
                // "Flex Layer" vs "Base Layer". Match on either spelling.
                bool isControllerHold = child.state.name == "Controller Hold" || child.state.name == "Hold Controller";
                if (!isControllerHold || child.state.motion != null) continue;

                string clipPath = "Assets/Hands/Animations/" + (left ? "l_hand_hold_l_controller_anim" : "r_hand_hold_r_controller_anim") + ".fbx";
                AnimationClip clip = null;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(clipPath))
                    if (o is AnimationClip c && !c.name.StartsWith("__")) clip = c;

                if (clip == null) { log.AppendLine("  (no clip at " + clipPath + " for Controller Hold)"); continue; }
                child.state.motion = clip;
                EditorUtility.SetDirty(controller);
                log.AppendLine("  filled empty 'Controller Hold' state with " + clip.name);
            }
        }
    }

    private static Material MaterialAt(string path, string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
}
