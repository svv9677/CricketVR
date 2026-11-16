using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The lighting presets of the match scene, authored in the scene and switched by StadiumLighting
/// as the scene loads. Placed under the match scene's Systems by CricketVR > Scene Objects > Build
/// Stadium Lighting. (Until 2026-11-15 the game made the lights, the night sky and the floodlight
/// glows in code on every match load.)
///
/// Each preset is a key light, switched off in the scene and switched on when it applies, plus the
/// RenderSettings it brings: the ambient, the reflection strength, the sky (a material asset) and
/// whether the fog stays. The afternoon at the stadium has no preset: it is the scene as it was lit
/// and baked. The scene's own sun is switched off by a preset that replaces it.
///
///   * Club afternoon: the scene's sun as a realtime light (the club ground's baked shadows and
///     probe were made with the stadium standing).
///   * Evening: golden hour, a low realtime sun.
///   * Night: a floodlit key from high over the ground; the floodlight glows (the stadium's ring or
///     the club's pylon heads) come on with it.
/// </summary>
public class StadiumLights : MonoBehaviour
{
    [System.Serializable]
    public class Preset
    {
        [Tooltip("The key light. Its object is switched on when the preset applies.")]
        public Light key;
        [Tooltip("When off, the scene's own ambient and reflections stay.")]
        public bool setsAmbient;
        public Color ambientSky, ambientEquator, ambientGround;
        public float reflectionIntensity = 1f;
        [Tooltip("The sky for this preset; empty leaves the scene's.")]
        public Material skybox;
        [Tooltip("Fog off (the night).")]
        public bool fogOff;
        [Tooltip("The stadium's ring of floodlight glows, shown for a stadium match.")]
        public GameObject stadiumGlows;
        [Tooltip("The club ground's pylon glows, shown for a club match.")]
        public GameObject clubGlows;

        /// Switches the scene's sun off, this preset's key on, and sets what it brings.
        public void Apply(Light sceneSun, bool club)
        {
            if (sceneSun != null)
                sceneSun.enabled = false;
            if (key != null)
            {
                key.gameObject.SetActive(true);
                RenderSettings.sun = key;
            }
            if (setsAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
                RenderSettings.reflectionIntensity = reflectionIntensity;
            }
            if (skybox != null)
                RenderSettings.skybox = skybox;
            if (fogOff)
                RenderSettings.fog = false;
            if (stadiumGlows != null)
                stadiumGlows.SetActive(!club);
            if (clubGlows != null)
                clubGlows.SetActive(club);
        }
    }

    [Tooltip("The scene's own sun (RenderSettings.sun as saved): baked and mixed.")]
    [SerializeField] private Light sceneSun;
    [SerializeField] private Preset clubAfternoon = new Preset();
    [SerializeField] private Preset evening = new Preset();
    [SerializeField] private Preset night = new Preset();

    public Light SceneSun => sceneSun;

    /// The preset for `t` at the stadium or the club ground; null for the stadium's afternoon,
    /// which is the scene as it is.
    public Preset For(eTimeOfDay t, bool club)
    {
        if (t == eTimeOfDay.Evening) return evening;
        if (t == eTimeOfDay.Night) return night;
        return club ? clubAfternoon : null;
    }
}
