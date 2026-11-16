using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the umpire prefab (Assets/Prefabs/Scene/Umpire.prefab, a variant of Models/David) and
/// places it in CricketVR.unity: the fielders' "David" in a black coat (a white one hid the ball
/// coming out of the bowler's hand), dark trousers and shoes and a black wide-brimmed hat, on the
/// fielders' controller (its idle). His materials are saved under Art/Generated/Umpire, including
/// the white shirt a Test match puts him in (Umpire swaps to it). Run again to rebuild.
/// Until 2026-11-15 the game built him in code as the match scene loaded.
/// </summary>
public static class UmpireBuilder
{
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";
    public const string PrefabPath = "Assets/Prefabs/Scene/Umpire.prefab";
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/Umpire";

    private static readonly Color Coat = new Color(0.06f, 0.06f, 0.07f);
    private static readonly Color Hat = new Color(0.06f, 0.06f, 0.07f);   // black too: nothing white behind the ball
    private static readonly Color Trousers = new Color(0.1f, 0.11f, 0.16f);
    private static readonly Color Shoes = new Color(0.05f, 0.05f, 0.05f);

    [MenuItem("CricketVR/Scene Objects/Build Umpire")]
    public static void Build()
    {
        var model = Resources.Load<GameObject>("Models/David");
        var controller = Resources.Load<RuntimeAnimatorController>("Animations/Fielder Controller");
        if (model == null || controller == null)
        {
            Debug.LogError("[UmpireBuilder] Models/David or Animations/Fielder Controller is missing");
            return;
        }
        var umpire = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            umpire.name = "Umpire";
            Animator animator = umpire.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;   // Action 0: the idle, and nothing else
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the replay records him
            Umpire component = umpire.GetComponent<Umpire>() ?? umpire.AddComponent<Umpire>();
            if (umpire.GetComponent<UmpireSignals>() == null)
                umpire.AddComponent<UmpireSignals>();
            Dress(umpire, component);
            PutOnHat(animator);
            PrefabUtility.SaveAsPrefabAsset(umpire, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(umpire);
        }
        Place();
    }

    /// Puts the prefab in the match scene if it is not there (its place is set each ball: Umpire.TakePlace).
    private static void Place()
    {
        Scene scene = SceneObjectAssets.Open(MatchScene);
        GameObject placed = SceneObjectAssets.Root(scene, "Umpire");
        if (placed == null)
        {
            placed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            placed.name = "Umpire";
        }
        placed.GetComponent<Umpire>().TakePlace();
        SceneObjectAssets.Save(scene);
        Debug.Log($"[UmpireBuilder] {PrefabPath} built and placed in {MatchScene}");
    }

    private static void Dress(GameObject umpire, Umpire component)
    {
        var coats = new System.Collections.Generic.List<Renderer>();
        foreach (SkinnedMeshRenderer skin in umpire.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string n = skin.name;
            Color? plain = n.Contains("Hoody") ? Coat : n.Contains("Pants") ? Trousers : n.Contains("Sneakers") ? Shoes : (Color?)null;
            if (plain == null)
                continue;
            skin.sharedMaterial = Plain(skin.sharedMaterial, plain.Value, "Umpire " + n);
            if (n.Contains("Hoody"))
                coats.Add(skin);
        }
        Material shirt = coats.Count > 0 ? Plain(coats[0].sharedMaterial, TestLook.UmpireShirt, "Umpire Test Shirt") : null;
        var so = new SerializedObject(component);
        SerializedProperty list = so.FindProperty("coat");
        list.arraySize = coats.Count;
        for (int i = 0; i < coats.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = coats[i];
        so.FindProperty("testShirt").objectReferenceValue = shirt;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// One flat colour: the clothes' part of the atlas would show through.
    private static Material Plain(Material source, Color colour, string name)
    {
        var material = new Material(source) { name = name };
        material.mainTexture = null;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
        return SceneObjectAssets.SaveMaterial(material, $"{Folder}/{name}.mat");
    }

    private static void PutOnHat(Animator animator)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (head == null || lit == null)
            return;
        for (int i = head.childCount - 1; i >= 0; i--)
            if (head.GetChild(i).name == "Hat")
                Object.DestroyImmediate(head.GetChild(i).gameObject);
        var hat = new Material(lit) { name = "Umpire Hat" };
        hat.SetColor("_BaseColor", Hat);
        hat = SceneObjectAssets.SaveMaterial(hat, $"{Folder}/Umpire Hat.mat");
        float top = TopOfHead(animator, head);
        // Worn, not perched: the brim at the brow, a low rounded crown over the top of the head.
        Part(PrimitiveType.Cylinder, head, hat, new Vector3(0.36f, 0.005f, 0.36f), top - 0.085f);    // brim
        Part(PrimitiveType.Sphere, head, hat, new Vector3(0.21f, 0.15f, 0.23f), top - 0.045f);       // crown
    }

    /// Height of the top of the head (hair included) above the head bone, from the skinned mesh
    /// itself: the renderers' bounds are padded, and a hat set on them floated above the head.
    private static float TopOfHead(Animator animator, Transform head)
    {
        float top = float.NegativeInfinity;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body") && !skin.name.Contains("Hair"))
                continue;
            skin.BakeMesh(baked, true);     // scale applied: only the position and turn to add
            Vector3 at = skin.transform.position;
            Quaternion turn = skin.transform.rotation;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = at + turn * v;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(head.position.x, head.position.z)) < 0.2f)
                    top = Mathf.Max(top, w.y);
            }
        }
        Object.DestroyImmediate(baked);
        return float.IsNegativeInfinity(top) ? 0.17f : top - head.position.y;
    }

    private static void Part(PrimitiveType shape, Transform head, Material material, Vector3 size, float up)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = "Hat";
        part.transform.SetPositionAndRotation(head.position + Vector3.up * up, Quaternion.identity);
        part.transform.localScale = size;
        part.transform.SetParent(head, true);
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
}
