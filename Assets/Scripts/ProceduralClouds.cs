using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The sky's cloudscape: a set of photographic cloud plates hung on a dome around the ground and
/// drifted slowly around it. The layout is randomised on every run, so the sky is different each
/// time the scene is played.
///
/// Billboards rather than geometry. At 420m a cloud is far enough away that both eyes see the
/// same thing, so a flat plate costs nothing in stereo and a photograph beats anything that can
/// be built out of meshes on a mobile GPU.
///
/// Three things are done per cloud to keep the sky from reading as flat: it is tinted by how much
/// of the sun it faces, faded into the horizon haze by how low it sits, and drifted at its own
/// speed. See <see cref="CloudShading"/> and <see cref="CloudField"/>, where that maths lives.
///
/// The component bootstraps itself via <see cref="Bootstrap"/> so it works without any manual
/// wiring in the scene.
/// </summary>
public class ProceduralClouds : MonoBehaviour
{
    // Scenes that should get a cloudscape: the actual play scenes, not menus or tests.
    private static readonly HashSet<string> CloudScenes = new HashSet<string>
    {
        "CricketVR",
        "Nets",
    };

    /// Cloud plates live here. Straight-alpha PNGs, one cloud each, cropped to the cloud.
    public const string PlateResourcePath = "Sky/Clouds";

    // Defaults come from CloudFieldSettings.Default so the sky the tests prove and the sky the
    // game builds cannot drift apart. This component is only ever created by Bootstrap via
    // AddComponent, never serialised into a scene, so these initialisers are what actually run.
    [Header("Cloud field")]
    [Tooltip("Number of clouds generated per run (inclusive range).")]
    [SerializeField] private Vector2Int cloudCountRange = CloudFieldSettings.Default.CountRange;
    [Tooltip("Lowest / highest elevation angle, in degrees above the horizon.")]
    [SerializeField] private Vector2 elevationRange = CloudFieldSettings.Default.ElevationRange;
    [Tooltip("Width of a cloud billboard in world units. Height follows the plate's aspect.")]
    [SerializeField] private Vector2 widthRange = CloudFieldSettings.Default.WidthRange;
    [Tooltip("Distance from the world origin. Must stay inside the camera's far clip (1000).")]
    [SerializeField] private float skyRadius = CloudFieldSettings.Default.Radius;

    [Header("Motion")]
    [Tooltip("Base drift speed around the vertical axis, in degrees per second.")]
    [SerializeField] private float driftDegreesPerSecond = CloudFieldSettings.Default.DriftDegPerSecond;
    [Tooltip("How much each cloud's drift may differ from the base speed, e.g. 0.2 for +/-20%.")]
    [SerializeField] private float driftVariance = CloudFieldSettings.Default.DriftVariance;

    [Header("Daylight")]
    [SerializeField] private Color dayColour = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color dayHaze = new Color(0.72f, 0.80f, 0.89f, 1f);
    [Tooltip("How much brighter the side of the sky facing the sun is.")]
    [SerializeField] private float daySunBoost = 0.12f;
    [Tooltip("How much darker a cloud turned away from the sun is.")]
    [SerializeField] private float dayShadeDrop = 0.25f;
    [SerializeField, Range(0f, 1f)] private float dayOpacity = 1f;

    [Header("Night")]
    [SerializeField] private Color nightColour = new Color(0.20f, 0.24f, 0.32f, 1f);
    [SerializeField] private Color nightHaze = new Color(0.10f, 0.12f, 0.18f, 1f);
    [SerializeField] private float nightSunBoost = 0.35f;
    [SerializeField] private float nightShadeDrop = 0.15f;
    [SerializeField, Range(0f, 1f)] private float nightOpacity = 0.85f;

    [Header("Haze")]
    [Tooltip("At or below this elevation a cloud is fully washed into the horizon haze.")]
    [SerializeField] private float hazeHorizonDeg = 10f;
    [Tooltip("At or above this elevation a cloud is completely clear of haze.")]
    [SerializeField] private float hazeClearDeg = 42f;

    // Optional fixed seed for a reproducible sky. Leave <= 0 for a fresh one every run.
    [SerializeField] private int seed = 0;

    // How often the day/night mode and sun direction are re-checked. The player can change the
    // stadium mode from the settings panel at any time and Main broadcasts no event for it, so
    // this is a poll -- cheap at four times a second, and nothing here is per-frame work.
    private const float ModePollSeconds = 0.25f;

    private Texture2D[] _plates;
    private Material _cloudMaterial;
    private Mesh _quad;
    private CloudPlacement[] _placements;
    private Transform[] _transforms;
    private MeshRenderer[] _renderers;
    private MaterialPropertyBlock _props;

    private Camera _camera;
    private Light _sun;
    private bool _isNight;
    private float _nextModePoll;

    /// <summary>
    /// Auto-spawns the cloud generator once a play scene has loaded, so no scene authoring is
    /// required.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid())
            TrySpawnForScene(active);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnForScene(scene);
    }

    private static void TrySpawnForScene(Scene scene)
    {
        if (!CloudScenes.Contains(scene.name))
            return;

        // Avoid duplicates if the scene is (re)loaded additively / multiple times.
        if (FindFirstObjectByType<ProceduralClouds>() != null)
            return;

        var go = new GameObject("ProceduralClouds");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ProceduralClouds>();
    }

    /// <summary>
    /// Builds the whole cloudscape. On enable rather than in Start, so that switching the
    /// ProceduralClouds object off and on in the hierarchy throws the sky away and generates a
    /// fresh one -- the quickest way to look at a lot of different skies while tuning.
    /// </summary>
    private void OnEnable()
    {
        Teardown();

        _plates = Resources.LoadAll<Texture2D>(PlateResourcePath);
        if (_plates == null || _plates.Length == 0)
        {
            // Deliberately no fallback. An empty sky is a clearer signal than a sky full of
            // stand-in geometry that somebody then ships by accident. The component stays
            // enabled, so adding plates and toggling the object again picks them up.
            Debug.LogWarning($"[ProceduralClouds] No cloud plates found in Resources/{PlateResourcePath}. " +
                             "The sky will be empty. Add straight-alpha cloud PNGs there.");
            return;
        }

        Shader shader = Shader.Find("CricketVR/CloudBillboard");
        if (shader == null)
        {
            Debug.LogWarning("[ProceduralClouds] Shader CricketVR/CloudBillboard is missing. The sky will be empty.");
            return;
        }

        _cloudMaterial = new Material(shader) { name = "CloudBillboardMat", enableInstancing = true };
        _quad = BuildQuad();
        _props = new MaterialPropertyBlock();

        var settings = new CloudFieldSettings
        {
            CountRange = cloudCountRange,
            ElevationRange = elevationRange,
            WidthRange = widthRange,
            Radius = skyRadius,
            DriftDegPerSecond = driftDegreesPerSecond,
            DriftVariance = driftVariance,
            HorizonShrink = CloudFieldSettings.Default.HorizonShrink,
            AllowMirroring = CloudFieldSettings.Default.AllowMirroring,
        };

        // TickCount alone repeats when the object is toggled twice inside its ~15ms resolution,
        // which is exactly what happens when someone is flicking the checkbox to see new skies.
        int runSeed = seed > 0
            ? seed
            : unchecked(System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode());

        _placements = CloudField.Generate(settings, _plates.Length, new System.Random(runSeed));
        BuildClouds();

        RefreshMode(force: true);
    }

    private void OnDisable()
    {
        Teardown();
    }

    /// <summary>
    /// Throws away every cloud and everything built for them. Safe to call when nothing has been
    /// built yet, so OnEnable can lead with it.
    /// </summary>
    private void Teardown()
    {
        if (_transforms != null)
        {
            foreach (Transform t in _transforms)
                if (t != null) DestroyObject(t.gameObject);
        }
        _transforms = null;
        _renderers = null;
        _placements = null;

        if (_cloudMaterial != null) { DestroyObject(_cloudMaterial); _cloudMaterial = null; }
        if (_quad != null) { DestroyObject(_quad); _quad = null; }

        _plates = null;
        _sun = null;
        _nextModePoll = 0f;
    }

    /// <summary>
    /// Destroy() is deferred to the end of the frame and is the right call in play mode, which is
    /// where this component lives. DestroyImmediate is only for the edit-mode case, where Destroy
    /// does nothing at all.
    ///
    /// Toggling the object off and straight back on inside one frame therefore leaves the old
    /// clouds alive alongside the new ones until that frame ends. Harmless, and not worth
    /// DestroyImmediate inside OnDisable to avoid.
    /// </summary>
    private static void DestroyObject(UnityEngine.Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    private void LateUpdate()
    {
        if (_placements == null || _placements.Length == 0)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        Vector3 eye = cam.transform.position;
        float dt = Time.deltaTime;

        for (int i = 0; i < _placements.Length; i++)
        {
            // Each cloud carries its own azimuth and its own speed, so the sky shears slowly
            // instead of turning as one rigid shell -- which the eye reads as a rotating dome.
            _placements[i].AzimuthDeg = Mathf.Repeat(
                _placements[i].AzimuthDeg + _placements[i].DriftDegPerSecond * dt, 360f);

            Vector3 pos = _placements[i].Position;
            Transform t = _transforms[i];
            t.position = pos;
            // Face the eye, but keep the plate's own up along world up so it never rolls: these
            // are photographs of clouds, and a rolled cloud reads instantly as a texture.
            t.rotation = Quaternion.LookRotation(pos - eye, Vector3.up);
        }

        if (Time.unscaledTime >= _nextModePoll)
        {
            _nextModePoll = Time.unscaledTime + ModePollSeconds;
            RefreshMode(force: false);
        }
    }

    /// <summary>
    /// Re-reads the stadium mode and the sun, and re-tints every cloud if either has changed.
    /// </summary>
    private void RefreshMode(bool force)
    {
        bool night = PlayerPrefs.GetInt(Constants.PP_StadiumMode, (int)eStadiumMode.Day) == (int)eStadiumMode.Night;
        Light sun = ResolveSun();

        if (!force && night == _isNight && sun == _sun)
            return;

        _isNight = night;
        _sun = sun;
        ApplyShading();
    }

    private void ApplyShading()
    {
        Color body = _isNight ? nightColour : dayColour;
        Color haze = _isNight ? nightHaze : dayHaze;
        float boost = _isNight ? nightSunBoost : daySunBoost;
        float drop = _isNight ? nightShadeDrop : dayShadeDrop;
        float opacity = _isNight ? nightOpacity : dayOpacity;

        // With no directional light to go on, light every cloud as if half-facing the sun, which
        // is the flat look -- correct, because without a sun there is no side for it to be on.
        Vector3 sunTravel = _sun != null ? _sun.transform.forward : Vector3.zero;

        for (int i = 0; i < _placements.Length; i++)
        {
            float facing = sunTravel == Vector3.zero
                ? 0.5f
                : CloudShading.SunFacing(_placements[i].Position, sunTravel);

            Color tint = CloudShading.BodyTint(body, facing, boost, drop);
            float hazeAmount = CloudShading.HazeAmount(_placements[i].ElevationDeg, hazeHorizonDeg, hazeClearDeg);

            _renderers[i].GetPropertyBlock(_props);
            _props.SetColor("_Tint", tint);
            _props.SetColor("_HazeColor", haze);
            _props.SetFloat("_Haze", hazeAmount);
            _props.SetFloat("_Opacity", opacity);
            _renderers[i].SetPropertyBlock(_props);
        }
    }

    private void BuildClouds()
    {
        _transforms = new Transform[_placements.Length];
        _renderers = new MeshRenderer[_placements.Length];

        for (int i = 0; i < _placements.Length; i++)
        {
            CloudPlacement p = _placements[i];
            Texture2D plate = _plates[Mathf.Clamp(p.PlateIndex, 0, _plates.Length - 1)];

            var go = new GameObject("Cloud_" + i);
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = _quad;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _cloudMaterial;
            // Clouds neither receive nor cast: they are painted light, and a shadow cast from a
            // flat quad onto the outfield would be a giveaway.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // The plate was padded to a power of two around its own aspect, so the texture's
            // shape is the cloud's shape and the quad can take it straight off the texture.
            float aspect = (float)plate.width / plate.height;
            go.transform.localScale = new Vector3(p.WidthMetres, p.WidthMetres / aspect, 1f);

            _props.Clear();
            _props.SetTexture("_BaseMap", plate);
            _props.SetVector("_BaseMap_ST", p.MirrorX ? new Vector4(-1f, 1f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f));
            renderer.SetPropertyBlock(_props);

            _transforms[i] = go.transform;
            _renderers[i] = renderer;
        }
    }

    /// <summary>A 1x1 quad on the XY plane, facing +Z, which the billboard then aims.</summary>
    private static Mesh BuildQuad()
    {
        var mesh = new Mesh { name = "CloudQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        // The quad is aimed every frame, so Unity's own bounds would be recomputed for nothing.
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
        return mesh;
    }

    private Camera ResolveCamera()
    {
        if (_camera != null && _camera.isActiveAndEnabled)
            return _camera;

        _camera = Camera.main;
        if (_camera == null)
        {
            // The XR rig's eye camera is not always tagged MainCamera.
            foreach (Camera c in Camera.allCameras)
            {
                if (c.isActiveAndEnabled && c.targetTexture == null) { _camera = c; break; }
            }
        }
        return _camera;
    }

    /// <summary>
    /// The directional light currently lighting the ground. Main swaps whole light rigs when the
    /// stadium mode changes, so this has to find the live one rather than cache it forever.
    /// </summary>
    private Light ResolveSun()
    {
        if (RenderSettings.sun != null && RenderSettings.sun.isActiveAndEnabled)
            return RenderSettings.sun;

        Light best = null;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || !light.isActiveAndEnabled) continue;
            if (best == null || light.intensity > best.intensity) best = light;
        }
        return best;
    }

}
