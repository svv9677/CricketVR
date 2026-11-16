using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the materials and textures a Test match is dressed in, and places the Test Look under the
/// match scene's Systems (object "Test Look", component TestLook, switched off until a Test).
/// Until 2026-11-15 the game made all of them in code at the start of every Test (TestLook).
///
/// Saved under Assets/Art/Generated/TestLook, and listed in Assets/Resources/TestLook/TestLookAssets
/// (in Resources so a limited-overs match never loads them):
///   * Test Whites: the fielders' kit material (David's, the model every fielder copies), its atlas
///     off and cream on;
///   * Test Sightscreen: the scene's SightScreen material in off-white;
///   * a red and a pink trail material for each trail material on the ball (alpha blended);
///   * for every material under the Stadium, Crowd and ClubGround objects whose texture has reds:
///     a copy with the texture's reds turned blue (the crowd's atlas is the one CrowdKeyBaker
///     baked, TestLook.CrowdForTest). New textures are compressed to ASTC 6x6.
/// Run after Place Sight Screen and Place Club Ground (it places the club ground if it is not
/// there). Run again to update; unchanged assets are rewritten in place.
/// </summary>
public static class TestLookBuilder
{
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/TestLook";
    private const string AssetsPath = "Assets/Resources/TestLook/TestLookAssets.asset";
    private static readonly string[] KitParts = { "Hoody", "Pants", "Sneakers" };

    [MenuItem("CricketVR/Scene Objects/Build Test Look")]
    public static void Build()
    {
        Scene scene = SceneObjectAssets.Open(StadiumBuilderUtil.MatchScene);
        ClubGroundPlacer.PlaceIn(scene);
        var made = ScriptableObject.CreateInstance<TestLookAssets>();
        made.whites = Whites();
        made.sightScreen = SightScreen(scene);
        List<Material> trails = TrailMaterials(scene);
        made.redTrail = TrailSwaps(trails, false);
        made.pinkTrail = TrailSwaps(trails, true);
        Backdrop(scene, out made.backdrop, out made.backdropTextures);
        SceneObjectAssets.EnsureFolder("Assets/Resources/TestLook");
        TestLookAssets saved = SceneObjectAssets.Save(made, AssetsPath);
        EditorUtility.SetDirty(saved);

        GameObject go = StadiumBuilderUtil.SystemsChild(scene, "Test Look");
        StadiumBuilderUtil.Ensure<TestLook>(go).enabled = false;
        SceneObjectAssets.Save(scene);
        Debug.Log($"[TestLookBuilder] Test Look placed in {StadiumBuilderUtil.MatchScene}; {saved.backdrop.Length} backdrop materials, {saved.redTrail.Length} trails.");
    }

    // ---- kit, sightscreen, trail --------------------------------------------------------------

    /// One flat cream material from David's shirt (every fielder is a copy of David).
    private static Material Whites()
    {
        var model = Resources.Load<GameObject>("Models/David");
        Material kit = null;
        if (model != null)
            foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (kit == null && System.Array.Exists(KitParts, part => skin.name.Contains(part)) && skin.sharedMaterial != null)
                    kit = skin.sharedMaterial;
        if (kit == null)
        {
            Debug.LogError("[TestLookBuilder] Resources/Models/David has no Hoody, Pants or Sneakers material: no whites made.");
            return null;
        }
        var whites = new Material(kit) { name = "Test Whites" };
        whites.mainTexture = null;       // one flat colour: the atlas would show through
        if (whites.HasProperty("_BaseColor")) whites.SetColor("_BaseColor", TestLook.Whites);
        if (whites.HasProperty("_Color")) whites.SetColor("_Color", TestLook.Whites);
        return SceneObjectAssets.SaveMaterial(whites, Folder + "/Materials/Test Whites.mat");
    }

    private static Material SightScreen(Scene scene)
    {
        GameObject screen = SceneObjectAssets.Root(scene, SightScreenBuilder.ObjectName);
        Renderer r = screen != null ? screen.GetComponent<Renderer>() : null;
        if (r == null || r.sharedMaterial == null)
        {
            Debug.LogWarning("[TestLookBuilder] No SightScreen in the scene (run Place Sight Screen): no Test sightscreen made.");
            return null;
        }
        var m = new Material(r.sharedMaterial) { name = "Test Sightscreen" };
        if (m.HasProperty("_Color")) m.SetColor("_Color", TestLook.Screen);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", TestLook.Screen);
        return SceneObjectAssets.SaveMaterial(m, Folder + "/Materials/Test Sightscreen.mat");
    }

    /// The trail materials of the ball in the scene and of the ball prefab.
    private static List<Material> TrailMaterials(Scene scene)
    {
        var found = new List<Material>();
        var balls = new List<GameObject>();
        Main main = Object.FindFirstObjectByType<Main>(FindObjectsInactive.Include);
        if (main != null && main.theBall != null) balls.Add(main.theBall);
        var prefab = Resources.Load<GameObject>("Prefabs/Ball");
        if (prefab != null) balls.Add(prefab);
        foreach (GameObject ball in balls)
            foreach (TrailRenderer t in ball.GetComponentsInChildren<TrailRenderer>(true))
                if (t.sharedMaterial != null && !found.Contains(t.sharedMaterial))
                    found.Add(t.sharedMaterial);
        if (found.Count == 0)
            Debug.LogWarning("[TestLookBuilder] The ball has no trail material: the Test keeps the white ball's trail.");
        return found;
    }

    /// Red (pink under lights), nearly opaque at the ball and alpha blended, not added to what is
    /// behind it (the white ball's trail is additive, URP Particles _Blend 2).
    private static TestLookAssets.MaterialSwap[] TrailSwaps(List<Material> white, bool pink)
    {
        Color c = pink ? TestLook.PinkLeather : TestLook.Trail;
        var swaps = new List<TestLookAssets.MaterialSwap>();
        foreach (Material w in white)
        {
            var tm = new Material(w) { name = (pink ? "Pink Ball Trail " : "Test Ball Trail ") + w.name };
            if (tm.HasProperty("_BaseColor")) tm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 1f));
            if (tm.HasProperty("_Color")) tm.SetColor("_Color", new Color(c.r, c.g, c.b, 1f));
            if (tm.HasProperty("_Blend"))
            {
                tm.SetFloat("_Blend", 0f);                                              // Alpha
                tm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                tm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                tm.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            Material saved = SceneObjectAssets.SaveMaterial(tm, Folder + "/Materials/" + Safe(tm.name) + ".mat");
            swaps.Add(new TestLookAssets.MaterialSwap { from = w, to = saved });
        }
        return swaps.ToArray();
    }

    // ---- nothing red behind the bowler --------------------------------------------------------

    /// Every red in the ground's textures turned blue, as TestLook did at the start of a Test: the
    /// grass is left alone, so are browns and oranges. A texture with no red worth changing keeps
    /// its material.
    private static void Backdrop(Scene scene, out TestLookAssets.MaterialSwap[] materials, out TestLookAssets.TextureSwap[] textures)
    {
        var blueOf = new Dictionary<Texture, Texture2D>();
        var done = new Dictionary<Material, Material>();
        foreach (GameObject root in BackdropRoots(scene))
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !done.ContainsKey(m))
                        done[m] = TestMaterial(m, blueOf);
        var ms = new List<TestLookAssets.MaterialSwap>();
        foreach (var kv in done)
            if (kv.Value != null)
                ms.Add(new TestLookAssets.MaterialSwap { from = kv.Key, to = kv.Value });
        var ts = new List<TestLookAssets.TextureSwap>();
        foreach (var kv in blueOf)
            if (kv.Value != null)
                ts.Add(new TestLookAssets.TextureSwap { from = kv.Key, to = kv.Value });
        materials = ms.ToArray();
        textures = ts.ToArray();
    }

    private static List<GameObject> BackdropRoots(Scene scene)
    {
        var roots = new List<GameObject>();
        foreach (string name in new[] { "Stadium", "Crowd" })
        {
            GameObject g = SceneObjectAssets.Root(scene, name);
            if (g != null) roots.Add(g);
            else Debug.LogWarning("[TestLookBuilder] No root object '" + name + "' in the scene.");
        }
        foreach (GameObject g in scene.GetRootGameObjects())
            if (g.GetComponent<ClubGround>() != null)
                roots.Add(g);
        return roots;
    }

    /// `m` with its texture's reds turned blue, saved; null if it has no texture to change.
    private static Material TestMaterial(Material m, Dictionary<Texture, Texture2D> blueOf)
    {
        Texture tex = m.mainTexture;
        if (tex == null || tex.name.Contains("Ground") || tex.name.Contains("Grass"))
            return null;
        if (!blueOf.TryGetValue(tex, out Texture2D blue))
            blueOf[tex] = blue = tex.name.EndsWith("_Keyed") ? TestLook.CrowdForTest(tex) : DerredAsset(tex);
        if (blue == null)
            return null;
        var copy = new Material(m) { name = m.name + " (Test)" };
        copy.mainTexture = blue;
        if (copy.HasProperty("_BaseMap")) copy.SetTexture("_BaseMap", blue);
        return SceneObjectAssets.SaveMaterial(copy, Folder + "/Materials/" + Safe(m.name) + "_Test_" + Guid(m) + ".mat");
    }

    /// The texture with its reds turned blue, compressed (ASTC 6x6, as the project's other large
    /// textures) and saved; null when it has no red worth changing.
    private static Texture2D DerredAsset(Texture source)
    {
        Texture2D blue = TestLook.Derred(source);
        if (blue == null)
            return null;
        blue.name = source.name + " (Test)";
        EditorUtility.CompressTexture(blue, TextureFormat.ASTC_6x6, TextureCompressionQuality.Normal);
        return SceneObjectAssets.Save(blue, Folder + "/Textures/" + Safe(source.name) + "_Test_" + Guid(source) + ".asset");
    }

    /// The first eight characters of an asset's GUID, so two assets with one name keep two files.
    private static string Guid(Object o)
    {
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(o));
        return string.IsNullOrEmpty(guid) ? "x" : guid.Substring(0, 8);
    }

    private static string Safe(string name)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace('/', '_');
    }
}
