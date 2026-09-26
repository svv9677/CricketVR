using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Procedurally generates a set of random clouds high in the sky and drifts them
/// slowly across the skybox. The layout, count, shape and size of the clouds are
/// randomised on every run of the game scene, so the sky looks different each time
/// the scene is played.
///
/// The component bootstraps itself via <see cref="Bootstrap"/> so it works without
/// any manual wiring in the scene: whenever a gameplay scene finishes loading a
/// single "ProceduralClouds" GameObject is spawned to own the clouds.
/// </summary>
public class ProceduralClouds : MonoBehaviour
{
    // Scenes that should get a procedural cloudscape. Kept small so the clouds only
    // appear in the actual stadium / nets play scenes and not in menus or tests.
    private static readonly HashSet<string> CloudScenes = new HashSet<string>
    {
        "CricketVR",
        "Nets",
    };

    [Header("Cloud field")]
    [Tooltip("Distance from the world origin at which clouds are placed (world units).")]
    [SerializeField] private float skyRadius = 420f;
    [Tooltip("Inclusive range for the number of individual clouds generated per run.")]
    [SerializeField] private Vector2Int cloudCountRange = new Vector2Int(9, 16);
    [Tooltip("Lowest / highest elevation angle (degrees above the horizon) a cloud may sit at.")]
    [SerializeField] private Vector2 elevationRange = new Vector2(18f, 62f);

    [Header("Cloud shape")]
    [Tooltip("Inclusive range for the number of overlapping puffs that make up one cloud.")]
    [SerializeField] private Vector2Int puffCountRange = new Vector2Int(6, 13);
    [Tooltip("Overall scale multiplier applied to each cloud.")]
    [SerializeField] private Vector2 cloudScaleRange = new Vector2(28f, 62f);

    [Header("Motion")]
    [Tooltip("Angular drift speed around the vertical axis, in degrees per second.")]
    [SerializeField] private float driftDegreesPerSecond = 0.35f;

    [Header("Appearance")]
    [SerializeField] private Color cloudColor = new Color(0.95f, 0.96f, 0.98f, 1f);

    // Optional fixed seed for reproducible layouts. Leave <= 0 for a fresh random
    // cloudscape every run (the default and what the task asks for).
    [SerializeField] private int seed = 0;

    private Material _cloudMaterial;

    /// <summary>
    /// Auto-spawns the cloud generator once a gameplay scene has loaded so no scene
    /// authoring is required. Runs on every play of the scene.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Handle the scene that is already active when the game starts.
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

    private void Start()
    {
        // A different seed each run gives a different cloudscape; a positive
        // inspector seed can be used to reproduce a specific layout.
        if (seed > 0)
            Random.InitState(seed);
        else
            Random.InitState(unchecked(System.Environment.TickCount ^ System.Guid.NewGuid().GetHashCode()));

        _cloudMaterial = BuildCloudMaterial();
        GenerateClouds();
    }

    private void Update()
    {
        // Slow, steady drift so the sky feels alive without being distracting.
        transform.Rotate(Vector3.up, driftDegreesPerSecond * Time.deltaTime, Space.World);
    }

    private Material BuildCloudMaterial()
    {
        // URP project: prefer the URP lit/simple-lit shaders so clouds catch the
        // scene's directional light, then fall back to the built-in Standard shader.
        Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var mat = new Material(shader) { name = "ProceduralCloudMat" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cloudColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", cloudColor);
        // Matte, non-glossy surface reads as a cloud rather than plastic.
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        mat.enableInstancing = true;
        return mat;
    }

    private void GenerateClouds()
    {
        int cloudCount = Random.Range(cloudCountRange.x, cloudCountRange.y + 1);
        for (int i = 0; i < cloudCount; i++)
            BuildCloud(i);
    }

    private void BuildCloud(int index)
    {
        // Place the cloud somewhere on the sky dome: random azimuth + elevation.
        float azimuth = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float elevation = Random.Range(elevationRange.x, elevationRange.y) * Mathf.Deg2Rad;

        float horizontal = Mathf.Cos(elevation) * skyRadius;
        Vector3 center = new Vector3(
            Mathf.Cos(azimuth) * horizontal,
            Mathf.Sin(elevation) * skyRadius,
            Mathf.Sin(azimuth) * horizontal);

        var cloud = new GameObject("Cloud_" + index);
        cloud.transform.SetParent(transform, false);
        cloud.transform.position = center;

        float cloudScale = Random.Range(cloudScaleRange.x, cloudScaleRange.y);
        int puffs = Random.Range(puffCountRange.x, puffCountRange.y + 1);

        for (int p = 0; p < puffs; p++)
        {
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Puff_" + p;

            // Clouds render very far away; no need for collision on them.
            var col = puff.GetComponent<Collider>();
            if (col != null) Destroy(col);

            puff.GetComponent<Renderer>().sharedMaterial = _cloudMaterial;
            puff.transform.SetParent(cloud.transform, false);

            // Distribute puffs mostly along a horizontal disc so the cloud is
            // wide and flattish, like a real cumulus mass.
            Vector3 local = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-0.25f, 0.35f),
                Random.Range(-0.7f, 0.7f)) * cloudScale;

            // Each puff is a squashed ellipsoid of a slightly random size.
            float puffSize = Random.Range(0.55f, 1.15f) * cloudScale;
            puff.transform.localPosition = local;
            puff.transform.localScale = new Vector3(
                puffSize,
                puffSize * Random.Range(0.45f, 0.7f),
                puffSize * Random.Range(0.75f, 1f));
        }

        // Give the whole cloud a random yaw so the flattened shape faces a
        // different way for each one.
        cloud.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private void OnDestroy()
    {
        if (_cloudMaterial != null)
            Destroy(_cloudMaterial);
    }
}
