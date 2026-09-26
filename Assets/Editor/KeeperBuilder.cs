using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools/CricketVR/Build Keeper: builds Assets/Resources/Prefabs/Keeper.prefab - the fielders'
/// humanoid (model, avatar, Fielder Controller) as a wicketkeeper with keeping gloves, leg pads
/// and a helmet on its bones, a HumanoidReach body and a KeeperCatcher root - and swaps it in for
/// the scene's keeper placeholder (Main.theKeeper).
///
/// Idempotent: run it again and the prefab is rebuilt in place (same asset, so the scene instance
/// updates); the scene is only touched while Main.theKeeper is not yet a Keeper prefab instance.
/// The old placeholder is deactivated and renamed, not deleted, and its KeeperCollider (the
/// trigger behind him that calls a ball through to him "missed") is copied onto the new keeper.
///
/// The kit meshes are low-poly FBX made in Blender (see the Models/Keeper folder):
///   glove : origin at the wrist, +Z along the fingers, +Y the back of the hand.
///   pad   : origin at the ankle, +Y up the shin, +Z the front.
///   helmet: origin at the centre of the head, +Y up, +Z the grille.
/// Each piece is placed from the bones in the rest pose and sized to the rig (head height).
/// </summary>
public static class KeeperBuilder
{
    private const string PrefabPath = "Assets/Resources/Prefabs/Keeper.prefab";
    private const string FielderPrefab = "Assets/Resources/Prefabs/Animated Fielder.prefab";
    private const string KitFolder = "Assets/Resources/Models/Keeper";
    private const string KeeperName = "Fielder_Keeper";
    /// Head-bone height of a 1.8 m man; the kit was modelled for him.
    private const float ReferenceHeadHeight = 1.63f;

    [MenuItem("Tools/CricketVR/Build Keeper")]
    public static void Build()
    {
        ConfigureKitImporters();
        GameObject prefab = BuildPrefab();
        if (prefab == null)
            return;
        SwapIntoScene(prefab);
    }

    // ---- Kit import ----------------------------------------------------------------------------------

    private static void ConfigureKitImporters()
    {
        foreach (string file in new[] { "KeeperGlove_L", "KeeperGlove_R", "KeeperPad", "KeeperHelmet" })
        {
            var importer = AssetImporter.GetAtPath(KitFolder + "/" + file + ".fbx") as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[KeeperBuilder] Missing " + KitFolder + "/" + file + ".fbx");
                continue;
            }
            bool dirty = importer.importAnimation || importer.animationType != ModelImporterAnimationType.None ||
                         !importer.bakeAxisConversion || importer.importCameras || importer.importLights ||
                         !Mathf.Approximately(importer.globalScale, 1f);
            if (!dirty)
                continue;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.SaveAndReimport();
        }
    }

    private static Dictionary<string, Material> KitMaterials()
    {
        return new Dictionary<string, Material>
        {
            { "KeeperWhite", MaterialAt("KeeperWhite", new Color(0.92f, 0.92f, 0.9f), 0.25f) },
            { "KeeperAccent", MaterialAt("KeeperAccent", new Color(0.05f, 0.35f, 0.2f), 0.3f) },
            { "KeeperHelmet", MaterialAt("KeeperHelmet", new Color(0.05f, 0.08f, 0.2f), 0.6f) },
            { "KeeperGrille", MaterialAt("KeeperGrille", new Color(0.6f, 0.6f, 0.62f), 0.7f) },
        };
    }

    private static Material MaterialAt(string name, Color color, float smoothness)
    {
        string path = KitFolder + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- Prefab ------------------------------------------------------------------------------------

    private static GameObject BuildPrefab()
    {
        var fielder = AssetDatabase.LoadAssetAtPath<GameObject>(FielderPrefab);
        if (fielder == null)
        {
            Debug.LogError("[KeeperBuilder] Missing " + FielderPrefab);
            return null;
        }
        var root = new GameObject("Keeper");
        try
        {
            GameObject bodyGo = MakeBody(fielder, root.transform);
            Animator animator = bodyGo.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError("[KeeperBuilder] The fielder model is not a Humanoid rig; cannot build the keeper.");
                return null;
            }
            animator.Rebind();
            DressKeeper(animator, root.transform);
            var reach = bodyGo.GetComponent<HumanoidReach>() ?? bodyGo.AddComponent<HumanoidReach>();
            var catcher = root.AddComponent<KeeperCatcher>();
            var so = new SerializedObject(catcher);
            so.FindProperty("body").objectReferenceValue = reach;
            so.FindProperty("bodyRoot").objectReferenceValue = bodyGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
            if (!ok)
                Debug.LogError("[KeeperBuilder] Could not save " + PrefabPath);
            AssetDatabase.SaveAssets();
            return ok ? saved : null;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// The fielder's humanoid, unpacked, without the fielder behaviour.
    private static GameObject MakeBody(GameObject fielder, Transform parent)
    {
        var bodyGo = (GameObject)PrefabUtility.InstantiatePrefab(fielder);
        PrefabUtility.UnpackPrefabInstance(bodyGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        bodyGo.name = "Body";
        bodyGo.transform.SetParent(parent, false);
        bodyGo.transform.localPosition = Vector3.zero;
        bodyGo.transform.localRotation = Quaternion.identity;
        foreach (var f in bodyGo.GetComponentsInChildren<AnimatedFielder>(true))
            Object.DestroyImmediate(f);
        foreach (var rb in bodyGo.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(rb);
        foreach (var c in bodyGo.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        Animator animator = bodyGo.GetComponent<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            // He is behind the batter, off camera most of the time, and his gloves are only where
            // the IK put them if the animator keeps running: never cull him.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        return bodyGo;
    }

    // ---- Kit on the bones -----------------------------------------------------------------------------

    private static void DressKeeper(Animator animator, Transform root)
    {
        Dictionary<string, Material> materials = KitMaterials();
        Transform head = Bone(animator, HumanBodyBones.Head);
        float height = head != null ? head.position.y - root.position.y : ReferenceHeadHeight;
        float scale = Mathf.Clamp(height / ReferenceHeadHeight, 0.75f, 1.3f);
        Vector3 fwd = root.forward, up = root.up;

        AttachGlove(animator, true, scale, materials);
        AttachGlove(animator, false, scale, materials);
        AttachPad(animator, true, fwd, scale, materials);
        AttachPad(animator, false, fwd, scale, materials);
        if (head != null)
        {
            GameObject helmet = LoadKit("KeeperHelmet");
            // The head bone sits at the top of the neck; the middle of the skull is ~7 cm above.
            Vector3 at = head.position + up * (0.07f * scale) + fwd * (0.015f * scale);
            Attach(helmet, head, "Helmet", at, Quaternion.LookRotation(fwd, up), scale, materials);
        }
    }

    private static void AttachGlove(Animator animator, bool left, float scale, Dictionary<string, Material> materials)
    {
        Transform hand = Bone(animator, left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        Transform middle = Bone(animator, left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
        Transform index = Bone(animator, left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
        Transform little = Bone(animator, left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
        if (hand == null || middle == null || index == null || little == null)
        {
            Debug.LogWarning("[KeeperBuilder] Hand bones missing; no " + (left ? "left" : "right") + " glove.");
            return;
        }
        Vector3 finger = (middle.position - hand.position).normalized;
        Vector3 palm = HumanoidReach.PalmNormal(finger, index.position - little.position, left);
        // Glove frame: +Z along the fingers, +Y the back of the hand (away from the palm).
        Quaternion rotation = Quaternion.LookRotation(finger, -palm);
        Attach(LoadKit(left ? "KeeperGlove_L" : "KeeperGlove_R"), hand, left ? "Glove_L" : "Glove_R",
               hand.position, rotation, scale, materials);
    }

    private static void AttachPad(Animator animator, bool left, Vector3 fwd, float scale, Dictionary<string, Material> materials)
    {
        Transform shin = Bone(animator, left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
        Transform foot = Bone(animator, left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
        if (shin == null || foot == null)
        {
            Debug.LogWarning("[KeeperBuilder] Leg bones missing; no " + (left ? "left" : "right") + " pad.");
            return;
        }
        Vector3 along = (shin.position - foot.position).normalized;   // ankle to knee
        Vector3 front = Vector3.ProjectOnPlane(fwd, along).normalized;
        Attach(LoadKit("KeeperPad"), shin, left ? "Pad_L" : "Pad_R", foot.position,
               Quaternion.LookRotation(front, along), scale, materials);
    }

    private static GameObject LoadKit(string file)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitFolder + "/" + file + ".fbx");
        if (model == null)
            Debug.LogError("[KeeperBuilder] Missing " + KitFolder + "/" + file + ".fbx");
        return model;
    }

    /// A holder on the bone at the kit piece's frame, with a copy of the model inside it.
    private static void Attach(GameObject model, Transform bone, string name, Vector3 position, Quaternion rotation,
                               float scale, Dictionary<string, Material> materials)
    {
        if (model == null)
            return;
        Transform old = bone.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject(name);
        holder.transform.SetParent(bone, false);
        holder.transform.SetPositionAndRotation(position, rotation);
        Vector3 boneScale = bone.lossyScale;
        holder.transform.localScale = new Vector3(scale / boneScale.x, scale / boneScale.y, scale / boneScale.z);
        var copy = (GameObject)Object.Instantiate(model, holder.transform, false);
        copy.name = "Mesh";
        foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string key = mats[i] != null ? mats[i].name.Replace(" (Instance)", "") : "KeeperWhite";
                mats[i] = materials.TryGetValue(key, out Material m) ? m : materials["KeeperWhite"];
            }
            r.sharedMaterials = mats;
            // Small pieces: not worth a shadow caster on a mobile GPU.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        CheckOrientation(copy, holder.transform, name);
    }

    /// The pieces are longest along a known axis in their frame (glove Z, pad Y); if the import
    /// turned them, say so rather than leave a keeper with gloves on sideways.
    private static void CheckOrientation(GameObject copy, Transform frame, string name)
    {
        var filter = copy.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || name == "Helmet")
            return;
        Bounds b = filter.sharedMesh.bounds;
        Matrix4x4 toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 p = toFrame.MultiplyPoint3x4(corner);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        Vector3 size = max - min;
        int want = name.StartsWith("Pad") ? 1 : 2;
        int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        if (longest != want)
            Debug.LogWarning($"[KeeperBuilder] {name}: longest axis is {"XYZ"[longest]}, expected {"XYZ"[want]} - check the FBX axis settings. Size in its frame {size}.");
    }

    /// A humanoid bone, from the animator or, if it has not bound in edit mode, by the avatar's
    /// bone-name mapping.
    private static Transform Bone(Animator animator, HumanBodyBones bone)
    {
        Transform t = animator.GetBoneTransform(bone);
        if (t != null || animator.avatar == null)
            return t;
        string human = HumanTrait.BoneName[(int)bone];
        foreach (HumanBone hb in animator.avatar.humanDescription.human)
        {
            if (hb.humanName != human)
                continue;
            foreach (Transform c in animator.GetComponentsInChildren<Transform>(true))
                if (c.name == hb.boneName) return c;
        }
        return null;
    }

    // ---- Scene ------------------------------------------------------------------------------------------

    private static void SwapIntoScene(GameObject prefab)
    {
        var main = Object.FindFirstObjectByType<Main>(FindObjectsInactive.Include);
        if (main == null)
        {
            Debug.LogWarning("[KeeperBuilder] Built " + PrefabPath + ". No Main in the open scene - open CricketVR.unity and run again to swap it in.");
            return;
        }
        GameObject old = main.theKeeper;
        if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old) == prefab)
        {
            Debug.Log("[KeeperBuilder] Rebuilt " + PrefabPath + "; the scene keeper is already this prefab.");
            return;
        }
        var keeper = (GameObject)PrefabUtility.InstantiatePrefab(prefab, main.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(keeper, "Build Keeper");
        if (old != null)
        {
            keeper.transform.SetParent(old.transform.parent, false);
            keeper.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
            keeper.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            SetLayer(keeper, old.layer);
            foreach (var trigger in old.GetComponentsInChildren<KeeperCollider>(true))
            {
                var copy = (GameObject)Object.Instantiate(trigger.gameObject, keeper.transform, true);
                copy.name = trigger.gameObject.name;
                Undo.RegisterCreatedObjectUndo(copy, "Build Keeper");
            }
            Undo.RecordObject(old, "Build Keeper");
            old.name = old.name + " (replaced by Keeper prefab)";
            old.SetActive(false);
        }
        else
        {
            var fielders = Object.FindFirstObjectByType<AnimatedFielderManagement>(FindObjectsInactive.Include);
            if (fielders != null)
                keeper.transform.SetParent(fielders.transform, false);
            // Facing up the pitch toward the bowler (-X), as the placeholder did.
            keeper.transform.SetPositionAndRotation(Constants.KeeperPositionMedium, Quaternion.Euler(0f, -90f, 0f));
        }
        keeper.name = KeeperName;
        var so = new SerializedObject(main);
        so.FindProperty("theKeeper").objectReferenceValue = keeper;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        Selection.activeGameObject = keeper;
        Debug.Log("[KeeperBuilder] Swapped the keeper placeholder for " + PrefabPath + ". Save the scene to keep it.");
    }

    private static void SetLayer(GameObject go, int layer)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
