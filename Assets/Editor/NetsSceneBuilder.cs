using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Nets scene from the match scene, so the nets play in VR exactly as a match does:
/// the same OpenXR rig (XRPlayerController / XRRigSetup), hands, laser and menus, the same Main,
/// bowler, keeper, bat, ball, replay and displays, the same lighting and post-processing. The old
/// Nets scene was hand-made around the Oculus rig (OVRPlayerController) with a second, stray
/// camera, and was not kept up with the match scene.
///
/// Tools/CricketVR/Build Nets Scene:
///   1. copies CricketVR.unity over Nets.unity (Nets.unity.meta, so its GUID, is kept). The match
///      scene is made of prefabs (ScenePrefabs: Assets/Prefabs/Scene), so the copy is instances of
///      the same prefabs, and a change to one shows in both scenes; what follows are the nets'
///      own overrides;
///   2. takes out what only a match has: the crowd, every fielder and the keeper (the nets have
///      none: DrillField judges a shot against a virtual field), and the scoreboard (the nets keep
///      no score; the HUD runs on behind it). The keeper is switched off, not deleted, as
///      Main.theKeeper is still moved for each bowler (HUD); NetsBackstop does his work on a ball
///      the batter does not hit;
///   3. puts up a practice-net tunnel at the batting end, side nets and a back net behind the
///      stumps, open in front and above, so a drive or a lofted shot still flies out into the
///      ground; netting, poles and the net's texture are made here (no bought assets), the net's
///      sound by Tools/sfx/build_net_sounds.py;
///   4. saves it and rebuilds the menus in every scene (MenuSceneBuilder), which gives the Nets
///      its own leave-without-asking pause menu.
/// Everything the nets do differently at runtime keys off the scene's name, "Nets" (NetsSession,
/// NetsTrial, NetsDrillRun, BallLedger, Bat's automatic reset, CosmeticLook); the match-only
/// add-ons (MatchFlow, OverBreak, the umpire, pitch lines, sight screen, career match) only come
/// to "CricketVR".
/// </summary>
public static class NetsSceneBuilder
{
    private const string MatchPath = "Assets/Scenes/CricketVR.unity";
    private const string NetsPath = "Assets/Scenes/Nets.unity";
    private const string Folder = "Assets/Nets";
    private const string TexturePath = Folder + "/Netting.png";
    private const string NetMaterialPath = Folder + "/Netting.mat";
    private const string PoleMaterialPath = Folder + "/NetPole.mat";
    private const string MeshPath = Folder + "/NetMeshes.asset";
    private const string PhysicsPath = Folder + "/NetDeadening.physicMaterial";
    private const string PrefabPath = Folder + "/Practice Nets.prefab";
    private const string StadiumScreen = "Stadium/784dfb2044aa49a5ba3580990ca3b420.fbx/RootNode/NMD_stD/NMD_stD_display_0";

    // The tunnel, in the scene's frame (the pitch runs along x, batting stumps at x = 10.3).
    private const float HalfWidth = 3f, Height = 3.2f, FrontX = 2f, BackX = 14.5f;
    // One mesh of the net is CellSize metres; poles every PoleSpacing along the sides.
    private const float CellSize = 0.05f, PoleSpacing = 4f, PoleRadius = 0.035f;

    [MenuItem("Tools/CricketVR/Build Nets Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[NetsSceneBuilder] Leave Play mode first.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // neither file open
        File.Copy(MatchPath, NetsPath, true);
        AssetDatabase.ImportAsset(NetsPath, ImportAssetOptions.ForceUpdate);

        var scene = EditorSceneManager.OpenScene(NetsPath, OpenSceneMode.Single);
        int removed = StripMatchOnly();
        GameObject nets = BuildTunnel();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[NetsSceneBuilder] Nets built from {MatchPath}: {removed} match-only objects out, " +
                  $"{nets.transform.childCount} net pieces up.");

        MenuSceneBuilder.BuildAll();
        EditorSceneManager.OpenScene(NetsPath, OpenSceneMode.Single);
    }

    /// The crowd, every fielder and the keeper. Returns how many were taken out or switched off.
    private static int StripMatchOnly()
    {
        int n = 0;
        GameObject crowd = GameObject.Find("Crowd");
        if (crowd != null)
        {
            Object.DestroyImmediate(crowd);
            n++;
        }
        n += HideScoreboard();
        GameObject fielders = GameObject.Find("Main/Fielders");
        if (fielders == null)
        {
            Debug.LogError("[NetsSceneBuilder] Main/Fielders not found: the match scene has changed shape.");
            return n;
        }
        foreach (Transform f in fielders.transform)
        {
            if ((f.name.StartsWith("AFielder_") || f.name.StartsWith("Fielder_Keeper")) && f.gameObject.activeSelf)
            {
                f.gameObject.SetActive(false);
                n++;
            }
        }
        return n;
    }

    /// The nets keep no score, so the stadium's scoreboard is not drawn: its board and the HUD's
    /// canvas on it are switched off, the HUD itself (the over, the bowler, the ball's state) runs
    /// on. Returns how many parts were switched off.
    private static int HideScoreboard()
    {
        GameObject board = GameObject.Find("Main/Displays/Scorecard Display");
        if (board == null)
        {
            Debug.LogError("[NetsSceneBuilder] Main/Displays/Scorecard Display not found: the match scene has changed shape.");
            return 0;
        }
        int n = 0;
        foreach (Renderer r in board.GetComponents<Renderer>())
        {
            r.enabled = false;
            n++;
        }
        foreach (Canvas c in board.GetComponentsInChildren<Canvas>(true))
        {
            c.enabled = false;
            n++;
        }
        // The stadium model's own screen behind it, blank white, that the board covers in a match.
        GameObject screen = GameObject.Find(StadiumScreen);
        if (screen != null && screen.TryGetComponent(out Renderer r2))
        {
            r2.enabled = false;
            n++;
        }
        else
            Debug.LogError($"[NetsSceneBuilder] {StadiumScreen} not found: the stadium model has changed.");
        return n;
    }

    // ---- The tunnel ----------------------------------------------------------------------------

    private static GameObject BuildTunnel()
    {
        Directory.CreateDirectory(Folder);
        Material net = NetMaterial();
        Material pole = PoleMaterial();
        PhysicsMaterial dead = Deadening();
        var meshes = new List<Mesh>();

        var root = new GameObject("Practice Nets", typeof(NetsBackstop));
        float length = BackX - FrontX, midX = (FrontX + BackX) * 0.5f;
        // The two sides (double-sided, so either way round) and the back, facing the pitch.
        Panel(root.transform, "Net Side 1", new Vector3(midX, 0f, -HalfWidth), Quaternion.Euler(0f, 0f, 0f), length, net, dead, meshes);
        Panel(root.transform, "Net Side 2", new Vector3(midX, 0f, HalfWidth), Quaternion.Euler(0f, 180f, 0f), length, net, dead, meshes);
        Panel(root.transform, "Net Back", new Vector3(BackX, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), HalfWidth * 2f, net, dead, meshes);

        Pole(root.transform, new Vector3(BackX, 0f, 0f), pole);    // the back net's middle
        int poles = Mathf.CeilToInt(length / PoleSpacing);
        for (int i = 0; i <= poles; i++)
        {
            float x = Mathf.Lerp(FrontX, BackX, i / (float)poles);
            Pole(root.transform, new Vector3(x, 0f, -HalfWidth), pole);
            Pole(root.transform, new Vector3(x, 0f, HalfWidth), pole);
        }
        Rail(root.transform, new Vector3(midX, Height, -HalfWidth), length, pole);
        Rail(root.transform, new Vector3(midX, Height, HalfWidth), length, pole);
        SaveMeshes(meshes);
        // A prefab like every other piece of the scene, the scene holding an instance of it.
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        return root;
    }

    /// One flat net, `width` wide and Height tall, its bottom edge on the ground at `centre`.
    private static void Panel(Transform parent, string name, Vector3 centre, Quaternion rotation, float width,
                              Material material, PhysicsMaterial physics, List<Mesh> meshes)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(centre, rotation);
        go.isStatic = false;    // not in the baked lighting: it was baked without it
        Mesh mesh = Quad(name, width, Height);
        meshes.Add(mesh);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        // A thin wall the ball drops off (dead netting), standing on the ground.
        var box = go.GetComponent<BoxCollider>();
        box.center = new Vector3(0f, Height * 0.5f, 0f);
        box.size = new Vector3(width, Height, 0.08f);
        box.sharedMaterial = physics;
    }

    /// A quad in the local XY plane, bottom edge on y = 0, facing -z (double-sided material), its
    /// UVs in net cells so one material tiles every panel the same.
    private static Mesh Quad(string name, float width, float height)
    {
        float hw = width * 0.5f, u = width / (CellSize * 8f), v = height / (CellSize * 8f);
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f), new Vector3(-hw, height, 0f), new Vector3(hw, height, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(u, 0f), new Vector2(0f, v), new Vector2(u, v) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void Pole(Transform parent, Vector3 foot, Material material)
    {
        GameObject p = Cylinder(parent, "Pole", material);
        p.transform.localPosition = foot + new Vector3(0f, Height * 0.5f, 0f);
        p.transform.localScale = new Vector3(PoleRadius * 2f, Height * 0.5f, PoleRadius * 2f);
    }

    private static void Rail(Transform parent, Vector3 centre, float length, Material material)
    {
        GameObject r = Cylinder(parent, "Rail", material);
        r.transform.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, 0f, 90f));
        r.transform.localScale = new Vector3(PoleRadius * 1.6f, length * 0.5f, PoleRadius * 1.6f);
    }

    private static GameObject Cylinder(Transform parent, string name, Material material)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        Object.DestroyImmediate(c.GetComponent<Collider>());    // the net's own collider is the wall
        c.transform.SetParent(parent, false);
        var renderer = c.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return c;
    }

    // ---- Assets --------------------------------------------------------------------------------

    /// A square of knotted netting: dark cord on clear, one cell to every 8 texels, so the cord
    /// stays a texel or two wide and the mips keep it (coverage preserved for the alpha test).
    private static Texture2D NetTexture()
    {
        const int size = 64, cell = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var cord = new Color(0.08f, 0.09f, 0.10f, 1f);
        var clear = new Color(0.08f, 0.09f, 0.10f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, x % cell == 0 || y % cell == 0 ? cord : clear);
        File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = true;
        importer.alphaTestReferenceValue = 0.5f;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 4;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }

    private static Material NetMaterial()
    {
        Texture2D tex = NetTexture();
        Material m = AssetDatabase.LoadAssetAtPath<Material>(NetMaterialPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(m, NetMaterialPath);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 0f);         // opaque, alpha-tested: no sorting, cheap on the Quest
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Cull", 0f);            // both sides
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material PoleMaterial()
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(PoleMaterialPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, PoleMaterialPath);
        }
        m.SetColor("_BaseColor", new Color(0.18f, 0.2f, 0.22f));
        m.SetFloat("_Metallic", 0.6f);
        m.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static PhysicsMaterial Deadening()
    {
        PhysicsMaterial p = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsPath);
        if (p == null)
        {
            p = new PhysicsMaterial("NetDeadening");
            AssetDatabase.CreateAsset(p, PhysicsPath);
        }
        p.bounciness = 0.05f;
        p.dynamicFriction = 0.8f;
        p.staticFriction = 0.8f;
        p.bounceCombine = PhysicsMaterialCombine.Minimum;
        p.frictionCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(p);
        return p;
    }

    /// The panels' meshes, kept in one asset so the scene can reference them.
    private static void SaveMeshes(List<Mesh> meshes)
    {
        if (AssetDatabase.LoadAssetAtPath<Object>(MeshPath) != null)
            AssetDatabase.DeleteAsset(MeshPath);
        var holder = new Mesh { name = "NetMeshes" };
        AssetDatabase.CreateAsset(holder, MeshPath);
        foreach (Mesh m in meshes)
            AssetDatabase.AddObjectToAsset(m, holder);
        AssetDatabase.SaveAssets();
    }
}
