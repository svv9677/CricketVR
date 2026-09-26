using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > CricketVR > Build Shot Replay. Does in the editor, once, everything the holographic
/// shot replay needs, so the build creates nothing at runtime:
///   * makes the hologram materials (CricketVR/Hologram) under Resources/Materials;
///   * places a "ShotReplay" object in the open scene with ShotRecorder + ShotReplay and the
///     ghosts (bat from the real bat's mesh and scale, ball, trail, contact ring) inactive under
///     it, and wires every reference;
///   * points CameraReplay at its screen and takes the screen out of static batching, so
///     "bring to front" can move it (a statically batched mesh is baked in place).
/// Re-run it after changing any of this; it replaces what it made last time. Run it once per
/// scene that has a Main (CricketVR, Nets).
/// </summary>
public static class ReplayBuilder
{
    private const string MaterialFolder = "Assets/Resources/Materials";
    private const string BatPrefabPath = "Assets/Resources/Prefabs/Bat.prefab";
    private const string RootName = "ShotReplay";
    private static readonly Color Cyan = new Color(0.25f, 0.95f, 1f, 1f);

    [MenuItem("Tools/CricketVR/Build Shot Replay")]
    public static void Build()
    {
        var shader = Shader.Find("CricketVR/Hologram");
        if (shader == null)
        {
            Debug.LogError("[ReplayBuilder] Shader CricketVR/Hologram not found - is Assets/Shaders/Hologram.shader imported?");
            return;
        }
        var main = Object.FindFirstObjectByType<Main>();
        if (main == null)
        {
            Debug.LogError("[ReplayBuilder] No Main in the open scene - open CricketVR or Nets first.");
            return;
        }

        // 0.18 all but vanished over the sunlit pitch in the editor check; 0.5 reads clearly.
        Material body = HologramMaterial("Hologram", shader, 0.5f, 0.35f, false);
        Material ball = HologramMaterial("HologramBall", shader, 0.55f, 0.25f, false);
        Material line = HologramMaterial("HologramTrail", shader, 0.9f, 0.15f, true);

        BuildReplayObject(main, body, ball, line);
        SetUpScreen();

        EditorSceneManager.MarkSceneDirty(main.gameObject.scene);
        EditorSceneManager.SaveScene(main.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[ReplayBuilder] Shot replay built: materials, ShotReplay object and ghosts, replay screen wired. Nothing is created at runtime.");
    }

    // ---- Materials -----------------------------------------------------------------------------

    private static Material HologramMaterial(string name, Shader shader, float alpha, float scan, bool vertexColor)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Color", Cyan);
        material.SetFloat("_Alpha", alpha);
        material.SetFloat("_ScanStrength", scan);
        material.SetFloat("_Flicker", 0f);
        material.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---- The ShotReplay object -----------------------------------------------------------------

    private static void BuildReplayObject(Main main, Material body, Material ballMaterial, Material line)
    {
        var scene = main.gameObject.scene;
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == RootName)
                Object.DestroyImmediate(go);

        var root = new GameObject(RootName);
        MoveToScene(root, scene);
        var recorder = root.AddComponent<ShotRecorder>();
        var replay = root.AddComponent<ShotReplay>();

        var ghosts = new GameObject("Ghosts");
        ghosts.transform.SetParent(root.transform, false);
        Transform bat = GhostBat(ghosts.transform, main, body);
        Transform ball = GhostBall(ghosts.transform, ballMaterial);
        LineRenderer trail = Trail(ghosts.transform, line);
        LineRenderer ring = ContactRing(ghosts.transform, line);
        ghosts.SetActive(false);

        SetField(recorder, "main", main);
        SetField(replay, "recorder", recorder);
        SetField(replay, "main", main);
        SetField(replay, "ghosts", ghosts);
        SetField(replay, "ghostBat", bat);
        SetField(replay, "ghostBall", ball);
        SetField(replay, "trail", trail);
        SetField(replay, "contactRing", ring);
    }

    private static void MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    /// The real bat's meshes, at the real bat's world scale, posed like it. The recorded pose is
    /// the bat root's, so each mesh keeps its offset from the root.
    private static Transform GhostBat(Transform parent, Main main, Material material)
    {
        GameObject source = main.theBat != null ? main.theBat : AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefabPath);
        var ghost = new GameObject("GhostBat");
        ghost.transform.SetParent(parent, false);
        if (source == null)
        {
            Debug.LogError("[ReplayBuilder] No bat in the scene (Main.theBat) and no Bat prefab - the ghost bat has no mesh.");
            return ghost.transform;
        }
        Transform src = source.transform;
        ghost.transform.localScale = src.lossyScale;
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>(false))
        {
            var sourceRenderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || sourceRenderer == null || !sourceRenderer.enabled)
                continue;
            var part = new GameObject(filter.name == source.name ? "Mesh" : filter.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(ghost.transform, false);
            part.transform.localPosition = src.InverseTransformPoint(filter.transform.position);
            part.transform.localRotation = Quaternion.Inverse(src.rotation) * filter.transform.rotation;
            Vector3 rootScale = src.lossyScale, scale = filter.transform.lossyScale;
            part.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
            part.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var partRenderer = part.GetComponent<MeshRenderer>();
            Quiet(partRenderer, material);
            // One slot per submesh: the bat mesh has six (blade, handle, grip...), and with a
            // single material only the first drew - a sliver of handle, no blade.
            var slots = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            partRenderer.sharedMaterials = slots;
        }
        return ghost.transform;
    }

    private static Transform GhostBall(Transform parent, Material material)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "GhostBall";
        Object.DestroyImmediate(ball.GetComponent<Collider>());   // a ghost must never collide
        ball.transform.SetParent(parent, false);
        ball.transform.localScale = Vector3.one * (2f * BallFlight.Radius);
        Quiet(ball.GetComponent<MeshRenderer>(), material);
        return ball.transform;
    }

    /// 48 world-space points, oldest first: transparent and thin at the tail, full at the ball.
    private static LineRenderer Trail(Transform parent, Material material)
    {
        var line = NewLine(parent, "Trail", material);
        line.useWorldSpace = true;
        line.positionCount = 48;
        line.widthMultiplier = 0.03f;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(1f, 1f));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Cyan, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = gradient;
        return line;
    }

    /// A 32-point loop in its own XY plane; ShotReplay turns it to face the player and opens it.
    private static LineRenderer ContactRing(Transform parent, Material material)
    {
        var line = NewLine(parent, "ContactRing", material);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 32;
        line.widthMultiplier = 0.008f;
        line.startColor = line.endColor = Color.white;
        line.enabled = false;
        return line;
    }

    private static LineRenderer NewLine(Transform parent, string name, Material material)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(parent, false);
        var line = go.GetComponent<LineRenderer>();
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.generateLightingData = false;
        Quiet(line, material);
        return line;
    }

    /// Unlit ghosts: no shadows, probes or motion vectors to pay for.
    private static void Quiet(Renderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
    }

    // ---- The replay screen ---------------------------------------------------------------------

    private static void SetUpScreen()
    {
        var replay = Object.FindFirstObjectByType<CameraReplay>();
        if (replay == null)
        {
            Debug.LogWarning("[ReplayBuilder] No CameraReplay in the scene - screen not wired.");
            return;
        }
        var target = new SerializedObject(replay).FindProperty("targetMat").objectReferenceValue as Material;
        Renderer screen = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (target != null && r.sharedMaterial == target) { screen = r; break; }
        if (screen == null)
        {
            Debug.LogWarning("[ReplayBuilder] No renderer uses the replay material - screen not wired.");
            return;
        }
        SetField(replay, "screen", screen.transform);

        // Keep its lightmap/GI flags; drop the ones that bake it in place.
        var flags = GameObjectUtility.GetStaticEditorFlags(screen.gameObject);
        flags &= ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        GameObjectUtility.SetStaticEditorFlags(screen.gameObject, flags);
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static void SetField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[ReplayBuilder] {target.GetType().Name} has no field '{field}'");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
