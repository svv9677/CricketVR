using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The main menu's dressing room (2026-11-06: "add like a small room or like a dressing room type
/// of thing where the main menu happens"): a pavilion dressing room built round the player in
/// Splash, the menu in front of him and, through the big window behind it, the stadium (the menu's
/// backdrop, 20 m wide at z 4.45, is the view).
///
///   Room      6.4 m wide (x -3.2..3.2), 5.8 m deep (z -2.6..3.2), 3 m high. The player stands
///             at the origin (XRRigSetup puts his room spot there), facing +Z.
///   Front     the window, 4.8 x 1.8 m, mullioned, with a sill.
///   Sides     a bench along each, kit on pegs over it, a shelf of helmets above, bats leaning,
///             kit bags on the floor.
///   Back      the door, and an honours board.
///   Ceiling   two light panels and a warm point light over each half.
///
/// All primitives and generated textures (own work): no colliders (nothing is hit, and the laser
/// must reach only the panels), no shadows (the ceiling would put the room in the dark), marked
/// static so it batches. Re-running replaces the room. DressingRoomProps has the kit.
/// </summary>
public static class DressingRoomBuilder
{
    public const string RootName = "Dressing Room";
    public const float HalfWidth = 3.2f, Back = -2.6f, Front = 3.2f, Height = 3f;
    private const float Wall = 0.12f;
    /// The window in the front wall.
    private const float WindowHalf = 2.4f, WindowBottom = 0.95f, WindowTop = 2.75f;

    [MenuItem("CricketVR/Splash/Build Dressing Room")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != NetsSession.SplashScene)
        {
            Debug.LogError("[DressingRoom] open the Splash scene first");
            return;
        }
        foreach (GameObject old in scene.GetRootGameObjects())
            if (old.name == RootName)
                Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        DressingRoomMaterials m = DressingRoomMaterials.Load();
        Shell(root.transform, m);
        WindowFrame(root.transform, m);
        DressingRoomProps.Benches(root.transform, m);
        DressingRoomProps.Kit(root.transform, m);
        DressingRoomProps.BackWall(root.transform, m);
        Lights(root.transform, m);
        Finish(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DressingRoom] built: " + root.GetComponentsInChildren<Renderer>().Length + " renderers");
    }

    private static void Shell(Transform root, DressingRoomMaterials m)
    {
        float w = HalfWidth * 2f, d = Front - Back, cz = (Front + Back) * 0.5f;
        Box(root, "Floor", new Vector3(0f, -0.05f, cz), new Vector3(w, 0.1f, d), m.Floor);
        Box(root, "Rug", new Vector3(0f, 0.006f, 0.2f), new Vector3(2.6f, 0.01f, 3.2f), m.Rug);
        Box(root, "Ceiling", new Vector3(0f, Height + 0.05f, cz), new Vector3(w, 0.1f, d), m.Ceiling);
        foreach (float side in new[] { -1f, 1f })
        {
            Box(root, side < 0 ? "Wall Left" : "Wall Right", new Vector3(side * (HalfWidth + Wall * 0.5f), Height * 0.5f, cz),
                new Vector3(Wall, Height, d), m.Wall);
            Box(root, "Panelling", new Vector3(side * (HalfWidth - 0.01f), 0.55f, cz), new Vector3(0.02f, 1.1f, d), m.Panel);
            Box(root, "Dado Rail", new Vector3(side * (HalfWidth - 0.02f), 1.11f, cz), new Vector3(0.04f, 0.05f, d), m.Trim);
        }
        Box(root, "Wall Back", new Vector3(0f, Height * 0.5f, Back - Wall * 0.5f), new Vector3(w, Height, Wall), m.Wall);
        Box(root, "Panelling Back", new Vector3(0f, 0.55f, Back + 0.01f), new Vector3(w, 1.1f, 0.02f), m.Panel);
        Box(root, "Dado Rail Back", new Vector3(0f, 1.11f, Back + 0.02f), new Vector3(w, 0.05f, 0.04f), m.Trim);
        // The front wall: round the window.
        float fz = Front + Wall * 0.5f, sideW = HalfWidth - WindowHalf;
        Box(root, "Wall Front Below", new Vector3(0f, WindowBottom * 0.5f, fz), new Vector3(w, WindowBottom, Wall), m.Wall);
        Box(root, "Wall Front Above", new Vector3(0f, (WindowTop + Height) * 0.5f, fz), new Vector3(w, Height - WindowTop, Wall), m.Wall);
        foreach (float side in new[] { -1f, 1f })
            Box(root, "Wall Front Side", new Vector3(side * (WindowHalf + sideW * 0.5f), (WindowBottom + WindowTop) * 0.5f, fz),
                new Vector3(sideW, WindowTop - WindowBottom, Wall), m.Wall);
        Box(root, "Skirting", new Vector3(0f, 0.06f, Front - 0.01f), new Vector3(w, 0.12f, 0.02f), m.Trim);
    }

    private static void WindowFrame(Transform root, DressingRoomMaterials m)
    {
        float z = Front - 0.02f, mid = (WindowBottom + WindowTop) * 0.5f, h = WindowTop - WindowBottom;
        Box(root, "Sill", new Vector3(0f, WindowBottom - 0.02f, Front - 0.12f), new Vector3(WindowHalf * 2f + 0.2f, 0.05f, 0.28f), m.Trim);
        Box(root, "Head", new Vector3(0f, WindowTop + 0.03f, z), new Vector3(WindowHalf * 2f + 0.1f, 0.07f, 0.08f), m.Frame);
        for (int i = 0; i <= 4; i++)
        {
            float x = -WindowHalf + i * (WindowHalf * 0.5f);
            Box(root, "Mullion", new Vector3(x, mid, z), new Vector3(0.06f, h, 0.07f), m.Frame);
        }
        Box(root, "Transom", new Vector3(0f, WindowTop - 0.45f, z), new Vector3(WindowHalf * 2f, 0.05f, 0.07f), m.Frame);
    }

    private static void Lights(Transform root, DressingRoomMaterials m)
    {
        foreach (float z in new[] { -1.2f, 1.4f })
        {
            Box(root, "Light Panel", new Vector3(0f, Height - 0.02f, z), new Vector3(1.6f, 0.04f, 0.5f), m.LightPanel);
            var go = new GameObject("Room Light");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0f, Height - 0.35f, z);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 6f;
            l.intensity = 1.6f;
            l.color = new Color(1f, 0.92f, 0.8f);
            l.shadows = LightShadows.None;
        }
    }

    /// No colliders, no shadows, static.
    private static void Finish(GameObject root)
    {
        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
    }

    public static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material mat, Quaternion? rot = null) =>
        Prim(PrimitiveType.Cube, parent, name, at, size, mat, rot);

    public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 size, Material mat,
                                  Quaternion? rot = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }
}
