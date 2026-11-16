using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the match scene's lighting presets: a "Lighting" root with the key lights of the club
/// afternoon, the evening and the night (all switched off), two "Floodlight Glows" roots (the
/// stadium's ring of eight on the roof rim, and one glow at each pylon head of the club ground;
/// both switched off), and the "Stadium Lights" component under Systems that holds them and the
/// RenderSettings each preset brings. Until 2026-11-15 the game made all of this in code on every
/// match load (StadiumLighting).
///
/// Saved under Assets/Art/Generated/Lighting: the evening and night skies (copies of the scene's
/// skybox material with the settings the code gave them), the glow texture and its additive
/// material. The glows are scene roots, not under "Lighting": EndChange finds the active
/// "Floodlight Glows" root by name and turns it half way round with the ground.
///
/// Places the club ground first if it is not in the scene (the club's glows hang on its pylon
/// heads). Run again: the lights and glows are rebuilt, the assets updated in place.
/// </summary>
public static class StadiumLightingBuilder
{
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/Lighting";
    private const int Banks = 8;
    private const float BankRadius = 70f, BankHeight = 36f;

    [MenuItem("CricketVR/Scene Objects/Build Stadium Lighting")]
    public static void Build()
    {
        Scene scene = SceneObjectAssets.Open(StadiumBuilderUtil.MatchScene);
        Light sun = SceneSun();
        ClubGround club = ClubGroundPlacer.PlaceIn(scene);
        Remove(scene);
        GameObject lighting = new GameObject("Lighting");
        SceneManager.MoveGameObjectToScene(lighting, scene);
        Material baseSky = RenderSettings.skybox;
        Light clubKey = sun != null ? Key(lighting, "Club Sun", sun.transform.rotation, sun.color, sun.intensity, sun.shadowStrength) : null;
        // Low in the west, square of the pitch (it runs along X): from -Z, a little behind the bowler.
        Light eveningKey = Key(lighting, "Evening Sun", Quaternion.Euler(22f, 20f, 0f), new Color(1f, 0.88f, 0.74f), 1.55f, 0.75f);
        Light nightKey = Key(lighting, "Floodlight Key", Quaternion.Euler(68f, 35f, 0f), new Color(0.95f, 0.97f, 1f), 1.7f, 0.6f);
        Material glow = GlowMaterial();
        GameObject stadiumGlows = Glows(scene, RoofRim(), VenueRules.GlowSize(eVenue.Stadium), glow);
        GameObject clubGlows = Glows(scene, club != null ? club.GlowPoints() : new Vector3[0], VenueRules.GlowSize(eVenue.ClubGround), glow);

        GameObject go = StadiumBuilderUtil.SystemsChild(scene, "Stadium Lights");
        StadiumLights lights = StadiumBuilderUtil.Ensure<StadiumLights>(go);
        StadiumBuilderUtil.SetRef(lights, "sceneSun", sun);
        StadiumBuilderUtil.SetRef(lights, "clubAfternoon.key", clubKey);
        SetPreset(lights, "evening", eveningKey, new Color(0.52f, 0.60f, 0.74f), new Color(0.58f, 0.57f, 0.54f), new Color(0.24f, 0.22f, 0.18f), 0.8f,
                  Sky("Evening Sky", baseSky, 1.25f, 1.2f, null, new Color(0.5f, 0.52f, 0.56f)), false, null, null);
        SetPreset(lights, "night", nightKey, new Color(0.30f, 0.34f, 0.46f), new Color(0.42f, 0.44f, 0.48f), new Color(0.30f, 0.32f, 0.30f), 0.25f,
                  Sky("Night Sky", baseSky, 0.5f, 0.12f, 0f, new Color(0.2f, 0.24f, 0.4f)), true, stadiumGlows, clubGlows);
        SceneObjectAssets.Save(scene);
        Debug.Log("[StadiumLightingBuilder] Lighting presets placed in " + StadiumBuilderUtil.MatchScene);
    }

    /// The scene's own sun: RenderSettings.sun as saved, else its shadow-casting directional light.
    private static Light SceneSun()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l.shadows != LightShadows.None)
                return l;
        return null;
    }

    /// Takes away what an earlier run placed.
    private static void Remove(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Lighting" || root.name == "Floodlight Glows")
                Object.DestroyImmediate(root);
    }

    /// A realtime directional key light under `parent`, saved switched off.
    private static Light Key(GameObject parent, string name, Quaternion rotation, Color color, float intensity, float shadowStrength)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.rotation = rotation;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.shadows = LightShadows.Soft;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.color = color;
        l.intensity = intensity;
        l.shadowStrength = shadowStrength;
        go.SetActive(false);
        return l;
    }

    private static void SetPreset(StadiumLights lights, string preset, Light key, Color sky, Color equator, Color ground,
                                  float reflection, Material skybox, bool fogOff, GameObject stadiumGlows, GameObject clubGlows)
    {
        StadiumBuilderUtil.SetRef(lights, preset + ".key", key);
        StadiumBuilderUtil.SetBool(lights, preset + ".setsAmbient", true);
        StadiumBuilderUtil.SetColor(lights, preset + ".ambientSky", sky);
        StadiumBuilderUtil.SetColor(lights, preset + ".ambientEquator", equator);
        StadiumBuilderUtil.SetColor(lights, preset + ".ambientGround", ground);
        StadiumBuilderUtil.SetFloat(lights, preset + ".reflectionIntensity", reflection);
        StadiumBuilderUtil.SetRef(lights, preset + ".skybox", skybox);
        StadiumBuilderUtil.SetBool(lights, preset + ".fogOff", fogOff);
        StadiumBuilderUtil.SetRef(lights, preset + ".stadiumGlows", stadiumGlows);
        StadiumBuilderUtil.SetRef(lights, preset + ".clubGlows", clubGlows);
    }

    /// A copy of the scene's sky with this preset's settings, saved as an asset; null if the scene
    /// has no skybox (the preset then leaves it).
    private static Material Sky(string name, Material source, float thickness, float exposure, float? sunSize, Color tint)
    {
        if (source == null)
            return null;
        var sky = new Material(source) { name = name };
        if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", thickness);
        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", exposure);
        if (sunSize.HasValue && sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", sunSize.Value);
        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", tint);
        return SceneObjectAssets.SaveMaterial(sky, Folder + "/" + name + ".mat");
    }

    /// The soft glow, additively blended, as the game made it for the floodlight banks.
    private static Material GlowMaterial()
    {
        Texture2D tex = SceneObjectAssets.Save(GlowTexture(), Folder + "/Floodlight Glow.asset");
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Floodlight Glow" };
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f, 1f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3100;
        return SceneObjectAssets.SaveMaterial(mat, Folder + "/Floodlight Glow.mat");
    }

    private static Texture2D GlowTexture()
    {
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "Floodlight Glow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float core = Mathf.Clamp01(1f - d * 1.6f);
                float a = Mathf.Clamp01(core * core + 0.35f * Mathf.Exp(-d * d * 4f));
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    /// The stadium's eight banks, hung under the roof's inner edge, where the banks are.
    private static Vector3[] RoofRim()
    {
        var at = new Vector3[Banks];
        for (int i = 0; i < Banks; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / Banks;
            at[i] = new Vector3(Mathf.Cos(a) * BankRadius, BankHeight, Mathf.Sin(a) * BankRadius);
        }
        return at;
    }

    /// A scene root "Floodlight Glows" with one camera-facing glow quad at each point, switched
    /// off. Each is turned toward the middle of the ground (and so toward the player, near enough).
    private static GameObject Glows(Scene scene, Vector3[] points, Vector2 size, Material mat)
    {
        var root = new GameObject("Floodlight Glows");
        SceneManager.MoveGameObjectToScene(root, scene);
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        for (int i = 0; i < points.Length; i++)
        {
            var q = new GameObject("Bank " + i, typeof(MeshFilter), typeof(MeshRenderer));
            q.transform.SetParent(root.transform, false);
            Vector3 to = new Vector3(0f, 2f, 0f) - points[i];
            q.transform.SetPositionAndRotation(points[i], Quaternion.LookRotation(-to.normalized, Vector3.up));
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        root.SetActive(false);
        return root;
    }
}
