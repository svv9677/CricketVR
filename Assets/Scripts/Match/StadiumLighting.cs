using System.Collections.Generic;
using UnityEngine;

/// The light for a match (MatchSetup.TimeOfDay).
public enum eTimeOfDay { Afternoon, Evening, Night }

/// <summary>
/// The ground's light for the time of day chosen in Customise (2026-11-05: "add different time of
/// day... check they are receiving the sunlight correctly vs the spot lighting, and shadows"):
///
///   * Afternoon - the scene exactly as it was lit and baked. Nothing here touches it.
///   * Evening   - golden hour: the sun low (22 deg) and side-on to the pitch, so it is not in the
///                 batter's eyes, warm; long shadows; a warmer, dimmer sky and ambient.
///   * Night     - floodlit: a cool white key from high over the ground (the towers' combined
///                 light), soft shadows straight down; a dark sky; the floodlight banks glowing on
///                 the roof rim; Rao's clouds in their night colours (ProceduralClouds reads the
///                 stadium-mode preference); the crowd's camera flashes come with it.
///
/// The lights, skies and glows are authored in the scene (StadiumLights, built by CricketVR >
/// Scene Objects > Build Stadium Lighting); this switches to the one that applies. (Until
/// 2026-11-15 it created them in code on every load.)
///
/// Why a light of its own and not the scene's sun turned: the scene is baked in Shadowmask mode, so
/// the static ground keeps the sun's baked shadows in the afternoon direction whatever the sun does
/// at runtime. For Evening and Night the scene's sun is switched off (its baked shadowmask with it)
/// and a realtime key takes over; at Night the static meshes also leave their daylight lightmaps
/// for the night ambient, or the ground would glow as if it were noon. The scene is loaded fresh
/// for each match, so nothing needs putting back for the next one.
/// </summary>
public static class StadiumLighting
{
    private static readonly Dictionary<Renderer, int> savedLightmaps = new Dictionary<Renderer, int>();
    private static readonly List<Renderer> dimmed = new List<Renderer>();

    public static eTimeOfDay Applied { get; private set; } = eTimeOfDay.Afternoon;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        savedLightmaps.Clear();
        dimmed.Clear();
        Applied = eTimeOfDay.Afternoon;
    }

    public static string Name(eTimeOfDay t) => t == eTimeOfDay.Afternoon ? "Afternoon" : t == eTimeOfDay.Evening ? "Golden hour" : "Night";

    public static string Hint(eTimeOfDay t) =>
        t == eTimeOfDay.Afternoon ? "Bright sun, high overhead."
      : t == eTimeOfDay.Evening ? "Low, clear sun from square of the wicket; long shadows."
      : "Under the floodlights.";

    /// Light the loaded match scene for `t`, at the club ground or the stadium. Called by
    /// StadiumConditions as the scene loads.
    public static void Apply(eTimeOfDay t, bool club)
    {
        Applied = t;
        // Rao's clouds and the crowd's flashes follow the old stadium-mode preference.
        PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)(t == eTimeOfDay.Night ? eStadiumMode.Night : eStadiumMode.Day));
        StadiumLights lights = Object.FindFirstObjectByType<StadiumLights>();
        if (lights == null)
        {
            Debug.LogWarning("[StadiumLighting] No StadiumLights in the scene (run CricketVR > Scene Objects > Build Stadium Lighting): the scene's own light is kept.");
            return;
        }
        Light sun = lights.SceneSun;
        StadiumLights.Preset preset = lights.For(t, club);
        if (preset == null)
        {
            if (sun != null) { sun.enabled = true; RenderSettings.sun = sun; }
            RestoreLightmaps();
            UndimStands();
            return;
        }
        preset.Apply(sun, club);
        if (t == eTimeOfDay.Night)
        {
            NightLightmaps();
            DimStands(0.55f, 0.6f);
            return;
        }
        UndimStands();
        if (t == eTimeOfDay.Evening)
            RestoreLightmaps();
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

    private static void UndimStands()
    {
        foreach (Renderer r in dimmed)
            if (r != null) r.SetPropertyBlock(null);
        dimmed.Clear();
    }
}
