using System.Collections.Generic;
using UnityEngine;

/// The light for a match (MatchSetup.TimeOfDay).
public enum eTimeOfDay { Afternoon, Evening, Night }

/// <summary>
/// The ground's light for the time of day chosen in Customise (2026-11-05: "add different time of
/// day... check they are receiving the sunlight correctly vs the spot lighting, and shadows"):
///
///   * Afternoon - the scene exactly as it was lit and baked. Nothing here touches it.
///   * Evening   - golden hour: the sun low (14 deg) and side-on to the pitch, so it is not in the
///                 batter's eyes, warm; long shadows; a warmer, dimmer sky and ambient.
///   * Night     - floodlit: a cool white key from high over the ground (the towers' combined
///                 light), soft shadows straight down; a dark sky; the floodlight banks glowing on
///                 the roof rim; Rao's clouds in their night colours (ProceduralClouds reads the
///                 stadium-mode preference); the crowd's camera flashes come with it.
///
/// Why a light of its own and not the scene's sun turned: the scene is baked in Shadowmask mode, so
/// the static ground keeps the sun's baked shadows in the afternoon direction whatever the sun does
/// at runtime. For Evening and Night the scene's sun is switched off (its baked shadowmask with it)
/// and a realtime key takes over; at Night the static meshes also leave their daylight lightmaps
/// for the night ambient, or the ground would glow as if it were noon. Everything is put back if
/// the next match is in the afternoon (all of it is runtime state; no asset is changed).
/// </summary>
public static class StadiumLighting
{
    private static Light key;
    private static GameObject glows;
    private static readonly Dictionary<Renderer, int> savedLightmaps = new Dictionary<Renderer, int>();

    public static eTimeOfDay Applied { get; private set; } = eTimeOfDay.Afternoon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        key = null; glows = null; sceneSun = null; savedLightmaps.Clear(); dimmed.Clear(); Applied = eTimeOfDay.Afternoon;
    }

    public static string Name(eTimeOfDay t) => t == eTimeOfDay.Afternoon ? "Afternoon" : t == eTimeOfDay.Evening ? "Golden hour" : "Night";

    public static string Hint(eTimeOfDay t) =>
        t == eTimeOfDay.Afternoon ? "Bright sun, high overhead."
      : t == eTimeOfDay.Evening ? "Low, warm sun from square of the wicket; long shadows."
      : "Under the floodlights.";

    /// Light the loaded match scene for `t`. Called by StadiumConditions as the scene loads.
    public static void Apply(eTimeOfDay t)
    {
        Light sun = FindSceneSun();
        Applied = t;
        // Rao's clouds and the crowd's flashes follow the old stadium-mode preference.
        PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)(t == eTimeOfDay.Night ? eStadiumMode.Night : eStadiumMode.Day));
        if (t == eTimeOfDay.Afternoon)
        {
            if (sun != null) { sun.enabled = true; RenderSettings.sun = sun; }
            if (key != null) Object.Destroy(key.gameObject);
            if (glows != null) Object.Destroy(glows);
            RestoreLightmaps();
            UndimStands();
            return;
        }
        if (sun != null) sun.enabled = false;
        if (key == null)
        {
            var go = new GameObject(t == eTimeOfDay.Night ? "Floodlight Key" : "Evening Sun");
            key = go.AddComponent<Light>();
            key.type = LightType.Directional;
            key.shadows = LightShadows.Soft;
        }
        RenderSettings.sun = key;
        Material sky = RenderSettings.skybox != null ? new Material(RenderSettings.skybox) : null;
        if (t == eTimeOfDay.Evening)
        {
            // Low in the west, square of the pitch (the pitch runs along X): from -Z, a little
            // behind the bowler, so the batter is lit from the side, not dazzled.
            key.transform.rotation = Quaternion.Euler(14f, 20f, 0f);
            key.color = new Color(1f, 0.64f, 0.36f);
            key.intensity = 1.4f;
            key.shadowStrength = 0.8f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.46f, 0.50f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.42f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.15f, 0.11f);
            RenderSettings.reflectionIntensity = 0.7f;
            if (sky != null) { Set(sky, "_AtmosphereThickness", 2.2f); Set(sky, "_Exposure", 1.25f); SetColor(sky, "_SkyTint", new Color(0.75f, 0.5f, 0.38f)); }
            UndimStands();
            RestoreLightmaps();
        }
        else
        {
            key.transform.rotation = Quaternion.Euler(68f, 35f, 0f);
            key.color = new Color(0.93f, 0.96f, 1f);
            key.intensity = 1.15f;
            key.shadowStrength = 0.6f;          // many towers: softer, fainter shadows
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.24f, 0.34f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.32f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.24f, 0.22f);
            RenderSettings.reflectionIntensity = 0.25f;
            if (sky != null) { Set(sky, "_Exposure", 0.12f); Set(sky, "_SunSize", 0f); Set(sky, "_AtmosphereThickness", 0.5f); SetColor(sky, "_SkyTint", new Color(0.2f, 0.24f, 0.4f)); }
            NightLightmaps();
            FloodlightGlows();
            DimStands(0.55f, 0.6f);
        }
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.fog = t == eTimeOfDay.Night ? false : RenderSettings.fog;
    }

    private static Light sceneSun;

    /// The scene's own sun: RenderSettings.sun as the scene set it, else its shadow-casting
    /// directional light. (Light.lightmapBakeType is editor-only, so not that.)
    private static Light FindSceneSun()
    {
        if (sceneSun != null) return sceneSun;
        if (RenderSettings.sun != null && RenderSettings.sun != key)
            return sceneSun = RenderSettings.sun;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Directional && l != key && l.shadows != LightShadows.None)
                return sceneSun = l;
        return null;
    }

    private static void NightLightmaps()
    {
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.lightmapIndex < 0 || r.lightmapIndex >= 0xFFFE) continue;
            savedLightmaps[r] = r.lightmapIndex;
            r.lightmapIndex = -1;             // lit by the night ambient and the key instead
        }
    }

    private static void RestoreLightmaps()
    {
        foreach (var kv in savedLightmaps)
            if (kv.Key != null) kv.Key.lightmapIndex = kv.Value;
        savedLightmaps.Clear();
    }

    /// The floodlight banks on the roof rim: soft camera-facing glows, no real lights (eight point
    /// lights would cost more than the whole ground on a Quest).
    private static void FloodlightGlows()
    {
        if (glows != null) return;
        glows = new GameObject("Floodlight Glows");
        Texture2D tex = GlowTexture();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(1f, 0.97f, 0.9f, 1f));
        // Additive blending.
        mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 2f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3100;
        const int Banks = 8;
        for (int i = 0; i < Banks; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / Banks;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = "Bank " + i;
            q.transform.SetParent(glows.transform, false);
            // Hung under the roof's inner edge, where the banks are (at 86 m they were inside the
            // canopy and never seen).
            q.transform.position = new Vector3(Mathf.Cos(a) * BankRadius, BankHeight, Mathf.Sin(a) * BankRadius);
            q.transform.localScale = new Vector3(13f, 5.5f, 1f);
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            q.AddComponent<FaceTheField>();
        }
    }

    public static float BankRadius = 70f, BankHeight = 36f;

    /// Under floodlights the field is the bright thing and the stands fall away: the stadium's
    /// structure and its crowd drawn darker (property blocks - their materials are not touched).
    private static void DimStands(float structure, float crowd)
    {
        var block = new MaterialPropertyBlock();
        foreach (string root in new[] { "Stadium", "Crowd" })
        {
            GameObject g = GameObject.Find(root);
            if (g == null) continue;
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
            {
                if (r.name.Contains("Ground00")) continue;          // the outfield is lit
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                r.GetPropertyBlock(block);
                Color c = m.GetColor("_BaseColor");
                float k = root == "Crowd" ? crowd : structure;
                block.SetColor("_BaseColor", new Color(c.r * k, c.g * k, c.b * k, c.a));
                r.SetPropertyBlock(block);
                dimmed.Add(r);
            }
        }
    }

    private static readonly List<Renderer> dimmed = new List<Renderer>();

    private static void UndimStands()
    {
        foreach (Renderer r in dimmed)
            if (r != null) r.SetPropertyBlock(null);
        dimmed.Clear();
    }

    private static Texture2D GlowTexture()
    {
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
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

    private static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
    private static void SetColor(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }

    /// A glow turned toward the middle of the ground (and so toward the player, near enough).
    private class FaceTheField : MonoBehaviour
    {
        private void Start()
        {
            Vector3 to = new Vector3(0f, 2f, 0f) - transform.position;
            transform.rotation = Quaternion.LookRotation(-to.normalized, Vector3.up);
        }
    }
}
