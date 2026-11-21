using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the career partner batter a prefab (Assets/Prefabs/Scene/Partner.prefab) and places it in
/// CricketVR.unity as the root object "Partner", saved switched off. It is the fielders' "David" on
/// the fielders' idle, with PartnerBatterBody and PartnerBatter, his helmet, pads and gloves, a
/// kit material for every kit colour (Assets/Art/Generated/TeamKit) and "Partner Bat" as a child
/// (PartnerBatter lets it go to be a root object when he wakes). The bat has the game bat's mesh
/// and materials (the Classic bat) and a kinematic body with no collider.
///
/// Until 2026-11-15 the game built all of it in code when a career match loaded (PartnerBatter).
/// He is wanted only in a career match: list "Partner" in the scene's Systems > SceneSystems
/// "Only When" as eWhen.CareerMatch (that wiring is not done here).
///
/// Run again, it rewrites the prefab and its assets; the scene's "Partner" is left as it is (it
/// follows the prefab), and is placed only when missing.
/// </summary>
public static class PartnerBatterBuilder
{
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";
    public const string PrefabPath = "Assets/Prefabs/Scene/Partner.prefab";
    public const string ModelPath = "Models/David";
    public const string ControllerPath = "Animations/Fielder Controller";

    [MenuItem("CricketVR/Scene Objects/Place Partner Batter")]
    public static void Place()
    {
        Scene scene = SceneObjectAssets.Open(MatchScene);
        MeshFilter source = GameBat();
        if (source == null)
        {
            Debug.LogError("[PartnerBatterBuilder] No game bat mesh found (Main.theBat or Resources/Prefabs/Bat).");
            return;
        }
        GameObject prefab = MakePrefab(source);
        if (prefab == null)
            return;
        if (SceneObjectAssets.Root(scene, PartnerBatter.ObjectName) == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = PartnerBatter.ObjectName;
            instance.SetActive(false);
        }
        SceneObjectAssets.Save(scene);
        Debug.Log($"[PartnerBatterBuilder] {PartnerBatter.ObjectName} placed (inactive) in {MatchScene}; list it in Systems > SceneSystems as CareerMatch.");
    }

    /// The game bat's mesh filter: Main.theBat in the open scene, else the bat prefab's.
    private static MeshFilter GameBat()
    {
        Main main = Object.FindFirstObjectByType<Main>();
        MeshFilter source = main != null && main.theBat != null ? main.theBat.GetComponent<MeshFilter>() : null;
        if (source != null && source.sharedMesh != null && source.GetComponent<MeshRenderer>() != null)
            return source;
        GameObject bat = Resources.Load<GameObject>("Prefabs/Bat");
        source = bat != null ? bat.GetComponent<MeshFilter>() : null;
        return source != null && source.sharedMesh != null && source.GetComponent<MeshRenderer>() != null ? source : null;
    }

    private static GameObject MakePrefab(MeshFilter source)
    {
        var model = Resources.Load<GameObject>(ModelPath);
        var controller = Resources.Load<RuntimeAnimatorController>(ControllerPath);
        if (model == null || controller == null)
        {
            Debug.LogError("[PartnerBatterBuilder] Resources/" + ModelPath + " or Resources/" + ControllerPath + " is missing.");
            return null;
        }
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var who = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
            who.name = PartnerBatter.ObjectName;
            Animator a = who.GetComponent<Animator>();
            if (a == null || !a.isHuman)
            {
                Debug.LogError("[PartnerBatterBuilder] David has no humanoid Animator.");
                return null;
            }
            a.runtimeAnimatorController = controller;   // Action 0: the idle, under the IK
            a.applyRootMotion = false;
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the replay records him
            // The ball goes through him to the stumps: nothing of his may have a collider.
            Debug.Assert(who.GetComponentsInChildren<Collider>(true).Length == 0, "[PartnerBatterBuilder] David has colliders");
            PartnerKit kit = TeamKitBuilder.Dress(a, true);
            Transform bat = MakeBat(who.transform, source);
            var body = who.AddComponent<PartnerBatterBody>();
            var partner = who.AddComponent<PartnerBatter>();
            TeamKitBuilder.SetField(partner, "animator", a);
            TeamKitBuilder.SetField(partner, "body", body);
            TeamKitBuilder.SetField(partner, "bat", bat);
            TeamKitBuilder.SetField(partner, "kit", kit);
            SceneObjectAssets.EnsureFolder("Assets/Prefabs/Scene");
            return PrefabUtility.SaveAsPrefabAsset(who, PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    /// "Partner Bat": the game bat's mesh and materials at its size, a kinematic body, no collider.
    private static Transform MakeBat(Transform parent, MeshFilter source)
    {
        var bat = new GameObject(PartnerBatter.BatName);
        SceneManager.MoveGameObjectToScene(bat, parent.gameObject.scene);
        bat.transform.SetParent(parent, false);
        bat.transform.localScale = source.transform.lossyScale;
        bat.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
        bat.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        Rigidbody body = bat.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = false;
        return bat.transform;
    }
}
