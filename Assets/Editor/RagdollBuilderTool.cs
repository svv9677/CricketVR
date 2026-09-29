using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > CricketVR > Build Fielder Ragdolls. Gives the fielder prefab (David) a ragdoll: a
/// Rigidbody and collider on eleven bones - hips, chest, head, upper and lower arms and legs -
/// joined by CharacterJoints with a person's limits, the same layout and limits as Unity's own
/// Ragdoll Wizard (which cannot be driven from a script). All kinematic, on the Body layer (which
/// the ball ignores), until FielderRagdoll lets it fall.
///
/// Re-running removes the old ragdoll first.
/// </summary>
public static class RagdollBuilderTool
{
    public const string FielderPrefab = "Assets/Resources/Prefabs/David.prefab";
    private const float TotalMass = 70f;

    [MenuItem("Tools/CricketVR/Build Fielder Ragdolls")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(FielderPrefab);
        try
        {
            Animator a = root.GetComponentInChildren<Animator>();
            BuildOn(a);
            if (root.GetComponent<FielderRagdoll>() == null)
                root.AddComponent<FielderRagdoll>();
            PrefabUtility.SaveAsPrefabAsset(root, FielderPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[RagdollBuilderTool] Ragdoll built on " + FielderPrefab);
    }

    public const string KeeperPrefab = "Assets/Resources/Prefabs/Keeper.prefab";

    /// The keeper too (his Body: the same avatar, controller and get-up clips as the fielders).
    [MenuItem("Tools/CricketVR/Build Keeper Ragdoll")]
    public static void BuildKeeper()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(KeeperPrefab);
        try
        {
            Animator a = root.GetComponentInChildren<HumanoidReach>(true).GetComponent<Animator>();
            BuildOn(a);
            if (a.GetComponent<FielderRagdoll>() == null)
                a.gameObject.AddComponent<FielderRagdoll>();
            PrefabUtility.SaveAsPrefabAsset(root, KeeperPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("[RagdollBuilderTool] Ragdoll built on " + KeeperPrefab);
    }

    public static void BuildOn(Animator a)
    {
        Transform Bone(HumanBodyBones b) => a.GetBoneTransform(b);
        Transform root = a.transform;
        Clear(Bone(HumanBodyBones.Hips));

        // The character's own frame, taken from its rest pose: right, up, forward.
        Vector3 right = root.right, up = root.up, forward = root.forward;
        int layer = LayerMask.NameToLayer("Body");

        Transform hips = Bone(HumanBodyBones.Hips), chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
        Transform head = Bone(HumanBodyBones.Head);
        Transform lUpLeg = Bone(HumanBodyBones.LeftUpperLeg), lLeg = Bone(HumanBodyBones.LeftLowerLeg), lFoot = Bone(HumanBodyBones.LeftFoot);
        Transform rUpLeg = Bone(HumanBodyBones.RightUpperLeg), rLeg = Bone(HumanBodyBones.RightLowerLeg), rFoot = Bone(HumanBodyBones.RightFoot);
        Transform lArm = Bone(HumanBodyBones.LeftUpperArm), lFore = Bone(HumanBodyBones.LeftLowerArm), lHand = Bone(HumanBodyBones.LeftHand);
        Transform rArm = Bone(HumanBodyBones.RightUpperArm), rFore = Bone(HumanBodyBones.RightLowerArm), rHand = Bone(HumanBodyBones.RightHand);

        // Colliders. Hips and chest are boxes spanning their part of the torso; limbs capsules
        // bone-to-bone; the head a sphere.
        float hipWidth = Vector3.Distance(lUpLeg.position, rUpLeg.position);
        TorsoBox(hips, hips.position - up * 0.08f, chest.position, hipWidth * 1.6f, 0.22f, layer);
        TorsoBox(chest, chest.position, Vector3.Lerp(chest.position, head.position, 0.7f), Vector3.Distance(lArm.position, rArm.position) * 1.1f, 0.22f, layer);
        Sphere(head, 0.11f, layer);
        Capsule(lUpLeg, lLeg.position, 0.075f, layer); Capsule(lLeg, lFoot.position, 0.06f, layer);
        Capsule(rUpLeg, rLeg.position, 0.075f, layer); Capsule(rLeg, rFoot.position, 0.06f, layer);
        Capsule(lArm, lFore.position, 0.05f, layer); Capsule(lFore, lHand.position, 0.045f, layer);
        Capsule(rArm, rFore.position, 0.05f, layer); Capsule(rFore, rHand.position, 0.045f, layer);

        // Bodies (mass shares of a 70 kg man) and joints: twist axis, swing axis, twist limits,
        // swing limit - as the Ragdoll Wizard sets them.
        Body(hips, 0.25f);
        Body(chest, 0.23f); Joint(chest, hips, right, forward, -20f, 20f, 10f);
        Body(head, 0.07f); Joint(head, chest, right, forward, -40f, 25f, 25f);
        Body(lUpLeg, 0.1f); Joint(lUpLeg, hips, right, forward, -20f, 70f, 30f);
        Body(rUpLeg, 0.1f); Joint(rUpLeg, hips, right, forward, -20f, 70f, 30f);
        Body(lLeg, 0.05f); Joint(lLeg, lUpLeg, right, forward, -80f, 0f, 0f);
        Body(rLeg, 0.05f); Joint(rLeg, rUpLeg, right, forward, -80f, 0f, 0f);
        Body(lArm, 0.04f); Joint(lArm, chest, up, forward, -70f, 10f, 50f);
        Body(rArm, 0.04f); Joint(rArm, chest, up, forward, -70f, 10f, 50f);
        Body(lFore, 0.035f); Joint(lFore, lArm, forward, up, -90f, 0f, 0f);
        Body(rFore, 0.035f); Joint(rFore, rArm, forward, up, -90f, 0f, 0f);
    }

    /// Remove a ragdoll built before, from these bones down.
    private static void Clear(Transform hips)
    {
        foreach (CharacterJoint j in hips.GetComponentsInChildren<CharacterJoint>(true)) Object.DestroyImmediate(j);
        foreach (Rigidbody r in hips.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
        foreach (Collider c in hips.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
    }

    private static void Body(Transform bone, float share)
    {
        var rb = bone.gameObject.AddComponent<Rigidbody>();
        rb.mass = TotalMass * share;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;   // FielderRagdoll turns it on for the fall
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.5f;
    }

    private static void Joint(Transform bone, Transform parent, Vector3 worldTwist, Vector3 worldSwing, float low, float high, float swing)
    {
        var j = bone.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = parent.GetComponent<Rigidbody>();
        j.axis = LocalAxis(bone, worldTwist);
        j.swingAxis = LocalAxis(bone, worldSwing);
        j.lowTwistLimit = new SoftJointLimit { limit = low };
        j.highTwistLimit = new SoftJointLimit { limit = high };
        j.swing1Limit = new SoftJointLimit { limit = swing };
        j.swing2Limit = new SoftJointLimit { limit = swing };
        j.enableProjection = true;
        j.enablePreprocessing = false;
    }

    /// The bone's local axis nearest a world direction (as the Wizard snaps them).
    private static Vector3 LocalAxis(Transform bone, Vector3 world)
    {
        Vector3 l = bone.InverseTransformDirection(world);
        Vector3 a = new Vector3(Mathf.Abs(l.x), Mathf.Abs(l.y), Mathf.Abs(l.z));
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(l.x), 0f, 0f);
        if (a.y >= a.z) return new Vector3(0f, Mathf.Sign(l.y), 0f);
        return new Vector3(0f, 0f, Mathf.Sign(l.z));
    }

    private static void Capsule(Transform bone, Vector3 end, float radius, int layer)
    {
        var c = bone.gameObject.AddComponent<CapsuleCollider>();
        Vector3 local = bone.InverseTransformPoint(end);
        Vector3 axis = LocalAxis(bone, bone.TransformDirection(local));
        c.direction = axis.x != 0f ? 0 : axis.y != 0f ? 1 : 2;
        float scale = bone.lossyScale.x;
        c.center = local * 0.5f;
        c.height = local.magnitude + 2f * radius / scale;
        c.radius = radius / scale;
        bone.gameObject.layer = layer;
    }

    private static void Sphere(Transform bone, float radius, int layer)
    {
        var c = bone.gameObject.AddComponent<SphereCollider>();
        float scale = bone.lossyScale.x;
        c.radius = radius / scale;
        c.center = bone.InverseTransformDirection(bone.root.up) * (0.08f / scale);
        bone.gameObject.layer = layer;
    }

    private static void TorsoBox(Transform bone, Vector3 from, Vector3 to, float width, float depth, int layer)
    {
        var c = bone.gameObject.AddComponent<BoxCollider>();
        Vector3 a = bone.InverseTransformPoint(from), b = bone.InverseTransformPoint(to);
        Vector3 up = LocalAxis(bone, bone.TransformDirection(b - a));
        Vector3 side = LocalAxis(bone, bone.root.right);
        Vector3 front = Vector3.Cross(up, side);
        float scale = bone.lossyScale.x;
        Vector3 size = Abs(up) * (b - a).magnitude + Abs(side) * (width / scale) + Abs(front) * (depth / scale);
        c.center = (a + b) * 0.5f;
        c.size = size;
        bone.gameObject.layer = layer;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    // ---- Get-up clips (Mixamo, Assets/Animations/GetUp) ----------------------------------------

    private const string ClipFolder = "Assets/Animations/GetUp/";
    private const string FielderController = "Assets/Resources/Animations/Fielder Controller.controller";

    /// FBX file, the Animator state it becomes (FielderRagdoll plays these by name).
    public static readonly (string file, string state)[] GetUpClips =
    {
        ("getup_face_down.fbx", FielderRagdoll.FaceDownState),
        ("getup_face_up.fbx", FielderRagdoll.FaceUpState),
        ("getup_side.fbx", FielderRagdoll.SideState),
    };

    /// Tools > CricketVR > Set Up Get-Up Clips: imports the Mixamo get-ups as Humanoid clips (not
    /// looping; rotation and height baked into the pose so he stays upright and on the ground,
    /// the walk across the ground left as root motion) and adds a state for each to the fielders'
    /// controller. No transitions: FielderRagdoll plays them and crossfades back to Idle.
    [MenuItem("Tools/CricketVR/Set Up Get-Up Clips")]
    public static void SetUpClips()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(FielderController);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var (file, stateName) in GetUpClips)
        {
            string path = ClipFolder + file;
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError("[RagdollBuilderTool] Missing " + path);
                continue;
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation c in clips)
            {
                c.name = stateName;
                c.loopTime = false;
                c.lockRootRotation = true;             // bake rotation into the pose
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;              // bake height: feet stay on the ground
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = false;          // his walk across the ground stays root motion
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            AnimationClip clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview"))
                    clip = ac;
            UnityEditor.Animations.AnimatorState state = null;
            foreach (var s in sm.states)
                if (s.state.name == stateName)
                    state = s.state;
            if (state == null)
                state = sm.AddState(stateName, new Vector3(600f, 60f * System.Array.FindIndex(GetUpClips, g => g.state == stateName), 0f));
            state.motion = clip;
            state.iKOnFeet = false;
            state.writeDefaultValues = true;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("[RagdollBuilderTool] Get-up clips set up in " + FielderController);
    }
}
