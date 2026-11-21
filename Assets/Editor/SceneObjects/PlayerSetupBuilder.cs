using UnityEditor;
using UnityEngine;

/// <summary>
/// CricketVR > Scene Objects > Bake Player Setup. Saves on the players, in the prefabs, what the
/// game used to add or overwrite on them as the match started (until 2026-11-15):
///
///   David, Keeper and Bowler prefabs   hand and foot colliders, Body layer and continuous
///                                      collision on the ragdoll, kinematic root body, Animator
///                                      update mode Normal (RagdollEndColliders).
///   Main.prefab > Fielders             ThrowBack, CatchJudge, FieldCaptain.
///   Main.prefab > each active fielder  HumanoidReach, BodySelfCollision, PlayerClap, Footsteps;
///                                      root body kinematic; update mode Normal.
///   Main.prefab > Bowler               BowlerSnap, BowlerFielding, BowlerGrip, FrontFoot, and (off
///                                      until he fields) HumanoidReach and BodySelfCollision,
///                                      Footsteps; the fielders' look (FielderLookBuilder).
///   Main.prefab > Fielder_Keeper       Footsteps; his other colliders switched off; the fielders'
///                                      look; update mode Normal.
///
/// Run it again and it changes nothing that is already there. Leave Play mode first.
/// </summary>
public static class PlayerSetupBuilder
{
    private const string MainPrefab = "Assets/Prefabs/Scene/Main.prefab";
    private static readonly string[] Sources =
    {
        "Assets/Resources/Prefabs/David.prefab",
        "Assets/Resources/Prefabs/Keeper.prefab",
        "Assets/Resources/Prefabs/Bowler.prefab",
    };

    [MenuItem("CricketVR/Scene Objects/Bake Player Setup")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[PlayerSetupBuilder] Leave Play mode first.");
            return;
        }
        foreach (string path in Sources)
            BakeSource(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();   // Main.prefab reads the saved sources
        GameObject root = PrefabUtility.LoadPrefabContents(MainPrefab);
        try
        {
            Transform fielders = root.transform.Find("Fielders");
            Transform bowler = root.transform.Find("Bowler");
            if (fielders == null || bowler == null)
            {
                Debug.LogError("[PlayerSetupBuilder] Main.prefab has no Fielders or Bowler: the prefab has changed shape.");
                return;
            }
            PlaceFieldingSystems(fielders);
            int n = PlaceOnFielders(fielders);
            PlaceOnBowler(bowler);
            PlaceOnKeeper(fielders.Find("Fielder_Keeper"));
            PrefabUtility.SaveAsPrefabAsset(root, MainPrefab);
            Debug.Log($"[PlayerSetupBuilder] Player setup saved in {MainPrefab} ({n} fielders, the bowler and the keeper).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// The ragdoll's saved setup, and the Animator update mode HumanoidReach used to set.
    private static void BakeSource(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (FielderRagdoll r in root.GetComponentsInChildren<FielderRagdoll>(true))
            {
                Animator a = r.GetComponent<Animator>();
                if (a == null || !a.isHuman)
                    continue;
                RagdollEndColliders.Bake(a);
                a.updateMode = AnimatorUpdateMode.Normal;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PlaceFieldingSystems(Transform fielders)
    {
        Ensure<ThrowBack>(fielders.gameObject);
        Ensure<CatchJudge>(fielders.gameObject);
        Ensure<FieldCaptain>(fielders.gameObject);
    }

    /// Every active fielder: the nine David instances (the Animated Fielder copies beside them are
    /// switched off and never played).
    private static int PlaceOnFielders(Transform fielders)
    {
        int n = 0;
        foreach (AnimatedFielder f in fielders.GetComponentsInChildren<AnimatedFielder>(false))
        {
            Animator a = new SerializedObject(f).FindProperty("myAnimator").objectReferenceValue as Animator;
            if (a == null)
                a = f.GetComponent<Animator>();
            if (a == null || !a.isHuman)
                continue;
            Ensure<HumanoidReach>(a.gameObject);
            Ensure<BodySelfCollision>(a.gameObject);
            Ensure<PlayerClap>(a.gameObject);
            Ensure<Footsteps>(a.gameObject);
            NormalUpdate(a);
            RagdollOnInstance(a);
            n++;
        }
        return n;
    }

    private static void PlaceOnBowler(Transform bowler)
    {
        GameObject go = bowler.gameObject;
        Ensure<BowlerSnap>(go);
        Ensure<BowlerFielding>(go);
        Ensure<BowlerGrip>(go);
        Ensure<FrontFoot>(go);
        // Off until he fields (BowlerFielding switches them on and off, and leaves them off in the nets).
        HumanoidReach reach = Ensure<HumanoidReach>(go);
        BodySelfCollision guard = Ensure<BodySelfCollision>(go);
        Ensure<Footsteps>(go);
        Animator a = go.GetComponent<Animator>();
        NormalUpdate(a);
        RagdollOnInstance(a);
        FielderLookBuilder.Place(bowler, bowler.parent);
        reach.enabled = false;
        Touch(reach);
        Touch(guard);
    }

    private static void PlaceOnKeeper(Transform keeper)
    {
        if (keeper == null)
        {
            Debug.LogError("[PlayerSetupBuilder] Main.prefab > Fielders has no Fielder_Keeper.");
            return;
        }
        Transform body = keeper.Find("Body");
        Animator a = body != null ? body.GetComponent<Animator>() : null;
        if (a == null)
        {
            Debug.LogError("[PlayerSetupBuilder] Fielder_Keeper has no Body with an Animator.");
            return;
        }
        Ensure<Footsteps>(body.gameObject);
        NormalUpdate(a);
        RagdollOnInstance(a);
        SwitchOffOtherColliders(keeper, a);
        FielderLookBuilder.Place(body, keeper);
    }

    /// Nothing solid on the keeper: a missed ball must go through to KeeperCollider, not bounce back
    /// up the pitch off a collider. His ragdoll's colliders are off until he dives (FielderRagdoll);
    /// anything else under him is saved off here. (KeeperCatcher used to do this as it started.)
    private static void SwitchOffOtherColliders(Transform keeper, Animator a)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        foreach (Collider c in keeper.GetComponentsInChildren<Collider>(false))
        {
            if (c.isTrigger || !c.enabled || (hips != null && c.transform.IsChildOf(hips)))
                continue;
            c.enabled = false;
            Touch(c);
        }
    }

    /// The layer, collision mode and kinematic root body again on the instance, in case Main.prefab
    /// overrides them (the sources are baked first).
    private static void RagdollOnInstance(Animator a)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null || hips.GetComponentsInChildren<Rigidbody>(true).Length == 0)
            return;
        RagdollEndColliders.SetLayersAndCollision(a);
        Rigidbody own = a.GetComponent<Rigidbody>();
        if (own != null)
            RagdollEndColliders.MakeKinematic(own);
    }

    private static void NormalUpdate(Animator a)
    {
        a.updateMode = AnimatorUpdateMode.Normal;
        Touch(a);
    }

    /// The component `T` on `go`, added if it is not there. Added with ObjectFactory so a component
    /// added to a prefab instance is saved as an override.
    public static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : ObjectFactory.AddComponent<T>(go);
    }

    /// Marks a change made in code to `o` so that, on a prefab instance, it is saved as an override.
    public static void Touch(Object o)
    {
        EditorUtility.SetDirty(o);
        if (PrefabUtility.IsPartOfPrefabInstance(o))
            PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }
}
