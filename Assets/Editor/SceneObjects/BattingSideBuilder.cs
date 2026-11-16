using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the batting side's five team-mates a prefab group (Assets/Prefabs/Scene/Batting Side.prefab)
/// and places it in CricketVR.unity as the root object "Batting Side". Each "Team-mate 1".."5" is
/// David on the fielders' idle with HumanoidReach and PlayerClap, his own skin tint (saved as
/// assets under Assets/Art/Generated/BattingSide) and a kit material for every kit colour
/// (Assets/Art/Generated/TeamKit); the middle one is padded up with helmet, pads and gloves.
/// BattingSide (on the group) picks the side's colour as the match starts.
///
/// They stand along the boundary in front of the pavilion, at the same spots for the stadium and the
/// club ground (BattingSide.Position), so nothing moves for the venue. Until 2026-11-15 the game
/// built all of it in code when the match scene loaded.
///
/// Run again, it rewrites the prefab and its assets; the scene's group is left as it is (it follows
/// the prefab), and is placed only when missing.
/// </summary>
public static class BattingSideBuilder
{
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";
    public const string PrefabPath = "Assets/Prefabs/Scene/Batting Side.prefab";
    private const string SkinFolder = SceneObjectAssets.GeneratedFolder + "/BattingSide";

    /// Skin tints over the model's own skin, so five copies of one man read as five men.
    private static readonly Color[] Skin =
    {
        Color.white, new Color(0.86f, 0.78f, 0.72f), new Color(0.7f, 0.6f, 0.52f), new Color(0.95f, 0.9f, 0.86f),
        new Color(0.6f, 0.5f, 0.43f),
    };

    [MenuItem("CricketVR/Scene Objects/Place Batting Side")]
    public static void Place()
    {
        Scene scene = SceneObjectAssets.Open(MatchScene);
        GameObject prefab = MakePrefab();
        if (prefab == null)
            return;
        if (SceneObjectAssets.Root(scene, BattingSide.ObjectName) == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = BattingSide.ObjectName;
        }
        SceneObjectAssets.Save(scene);
        Debug.Log($"[BattingSideBuilder] {BattingSide.ObjectName} placed in {MatchScene}");
    }

    private static GameObject MakePrefab()
    {
        var model = Resources.Load<GameObject>(PartnerBatterBuilder.ModelPath);
        var controller = Resources.Load<RuntimeAnimatorController>(PartnerBatterBuilder.ControllerPath);
        if (model == null || controller == null)
        {
            Debug.LogError("[BattingSideBuilder] Resources/Models/David or Resources/Animations/Fielder Controller is missing.");
            return null;
        }
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var group = new GameObject(BattingSide.ObjectName);
            SceneManager.MoveGameObjectToScene(group, preview);
            var side = group.AddComponent<BattingSide>();
            var members = new Transform[BattingSide.Count];
            var hands = new PlayerClap[BattingSide.Count];
            var reaches = new HumanoidReach[BattingSide.Count];
            var animators = new Animator[BattingSide.Count];
            var kits = new PartnerKit[BattingSide.Count];
            for (int i = 0; i < BattingSide.Count; i++)
            {
                GameObject who = Member(model, controller, group.transform, i, out kits[i]);
                if (who == null)
                    return null;
                members[i] = who.transform;
                hands[i] = who.GetComponent<PlayerClap>();
                reaches[i] = who.GetComponent<HumanoidReach>();
                animators[i] = who.GetComponent<Animator>();
            }
            TeamKitBuilder.SetField(side, "members", members);
            TeamKitBuilder.SetField(side, "hands", hands);
            TeamKitBuilder.SetField(side, "reaches", reaches);
            TeamKitBuilder.SetField(side, "animators", animators);
            TeamKitBuilder.SetField(side, "kits", kits);
            SceneObjectAssets.EnsureFolder("Assets/Prefabs/Scene");
            return PrefabUtility.SaveAsPrefabAsset(group, PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    /// Team-mate `i`: placed on the boundary facing the middle, dressed, tinted, with his reach and hands.
    private static GameObject Member(GameObject model, RuntimeAnimatorController controller, Transform group, int i, out PartnerKit kit)
    {
        kit = null;
        var who = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
        who.name = "Team-mate " + (i + 1);
        Vector3 at = BattingSide.Position(i);
        Vector3 toMiddle = -at;
        toMiddle.y = 0f;
        who.transform.SetPositionAndRotation(at, Quaternion.LookRotation(toMiddle.normalized, Vector3.up));
        who.transform.localScale = Vector3.one * (0.96f + 0.02f * ((i * 7) % 5));
        Animator anim = who.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Debug.LogError("[BattingSideBuilder] David has no humanoid Animator.");
            return null;
        }
        Debug.Assert(who.GetComponentsInChildren<Collider>(true).Length == 0, "[BattingSideBuilder] David has colliders");
        anim.runtimeAnimatorController = controller;      // Action 0: the idle
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        // The middle one is next in: padded up, helmet on.
        kit = TeamKitBuilder.Dress(anim, i == BattingSide.Count / 2);
        Tint(who, Skin[i % Skin.Length], i);
        who.AddComponent<HumanoidReach>();
        who.AddComponent<PlayerClap>();
        return who;
    }

    private static void Tint(GameObject who, Color tint, int i)
    {
        foreach (SkinnedMeshRenderer skin in who.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body"))
                continue;
            var m = new Material(skin.sharedMaterial) { name = "Team-mate Skin" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            skin.sharedMaterial = SceneObjectAssets.SaveMaterial(m, $"{SkinFolder}/Skin_{i + 1}.mat");
        }
    }
}
