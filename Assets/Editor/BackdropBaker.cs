using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tools > CricketVR > Bake Menu Backdrops. Captures long-shot plates of the project's own
/// stadium from CricketVR.unity and grades them into the opening menu's floodlit look, writing
/// PNGs to Assets/Resources/UI/Backdrops.
///
/// Self-authored from assets already in the project, so there is no third-party licence to track
/// (Docs/AssetSources.md, tier 2). Re-run whenever the stadium changes.
///
/// The HUD, players and debug boards are hidden for the capture: the baseline renders in
/// Docs/Baseline show why - they carry the Distance/Ball Speed board across the sightscreen.
///
/// CricketVR is reopened from disk afterwards, so nothing this tool switches off is ever saved.
/// </summary>
public static class BackdropBaker
{
    private const string Folder = "Assets/Resources/UI/Backdrops";
    private const string ScenePath = "Assets/Scenes/CricketVR.unity";
    private const int Width = 2048;
    private const int Height = 1024;

    /// Where each plate is shot from, and which screen uses it.
    private struct Shot
    {
        public string Name;
        public Vector3 Position;
        public Vector3 Euler;
        public float Fov;
    }

    private static readonly Shot[] Shots =
    {
        // High behind the bowler's arm, looking down the pitch - the main menu.
        new Shot { Name = "Backdrop-BowlersEnd", Position = new Vector3(0f, 14f, -34f),
                   Euler = new Vector3(11f, 0f, 0f), Fov = 42f },
        // Square of the wicket, the broadcast side-on - settings.
        new Shot { Name = "Backdrop-SquareLeg", Position = new Vector3(-38f, 12f, 2f),
                   Euler = new Vector3(10f, 78f, 0f), Fov = 40f },
        // Deep midwicket, more crowd in frame - pause.
        new Shot { Name = "Backdrop-DeepMidwicket", Position = new Vector3(-30f, 9f, 26f),
                   Euler = new Vector3(8f, 132f, 0f), Fov = 46f },
    };

    [MenuItem("Tools/CricketVR/Bake Menu Backdrops")]
    public static void Bake()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(Folder);

        List<GameObject> hidden = HideForCapture();
        var rig = new GameObject("BackdropCamera", typeof(Camera));
        var camera = rig.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 800f;

        foreach (Shot shot in Shots)
        {
            rig.transform.SetPositionAndRotation(shot.Position, Quaternion.Euler(shot.Euler));
            camera.fieldOfView = shot.Fov;
            Texture2D plate = Capture(camera);
            Grade(plate);
            File.WriteAllBytes($"{Folder}/{shot.Name}.png", plate.EncodeToPNG());
            Object.DestroyImmediate(plate);
        }

        Object.DestroyImmediate(rig);
        foreach (GameObject go in hidden)
            if (go != null) go.SetActive(true);

        AssetDatabase.Refresh();
        foreach (Shot shot in Shots)
            ImportAsBackdrop($"{Folder}/{shot.Name}.png");

        // Reopen from disk: none of the hiding above is ever written to the scene.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log($"[BackdropBaker] Baked {Shots.Length} plates to {Folder}. " +
                  "CricketVR reloaded from disk; nothing was saved to it.");
    }

    /// Hide anything that must not appear in a menu backdrop: the HUD and debug boards, every
    /// canvas, the players, and the bat.
    private static List<GameObject> HideForCapture()
    {
        var hidden = new List<GameObject>();
        void HideAll<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude,
                                                        FindObjectsSortMode.None))
            {
                if (c != null && c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(false);
                    hidden.Add(c.gameObject);
                }
            }
        }
        HideAll<Canvas>();
        HideAll<HUD>();
        HideAll<Bat>();
        // Every human in the scene - batsmen, bowler, fielders, keeper - is a skinned mesh,
        // while the stadium and pitch are plain mesh renderers. Hiding by renderer type catches
        // them all, including ones reached through prefab variants that a component-by-component
        // sweep misses.
        HideAll<SkinnedMeshRenderer>();
        // The Distance / Ball Speed board is world-space TextMeshPro on a mesh, not a Canvas, so
        // the sweep above walks straight past it. It is the exact thing that spoils the baseline
        // renders in Docs/Baseline.
        HideAll<TMPro.TextMeshPro>();
        HideAll<ShotDistance>();
        HideAll<BallSpeed>();
        return hidden;
    }

    private static Texture2D Capture(Camera camera)
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 4
        };
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var plate = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        plate.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        plate.Apply();
        RenderTexture.active = previous;
        camera.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        return plate;
    }

    /// The floodlit grade: indigo at the top through magenta to coral at the horizon, mixed over
    /// the render at 55% - the balance chosen in the design mockups. A left-side vignette darkens
    /// where the menu card sits so white text always has something to sit on.
    /// The floodlit grade. The key thing is that a ground-level long shot is mostly stadium and
    /// outfield, with only a thin band of sky at the top - so the vivid colour has to live in the
    /// upper third. Pushing the full gradient down the frame turns the turf orange and blows the
    /// pitch out to hot pink, which is exactly what the first bake did.
    ///
    /// So: strong indigo-to-coral through the sky and stands, falling away to a cool night blue
    /// over the ground, at a much lower strength so the turf still reads as grass.
    private static void Grade(Texture2D plate)
    {
        Color sky = new Color(0.106f, 0.043f, 0.231f);      // #1B0B3B indigo
        Color glow = new Color(0.541f, 0.157f, 0.639f);     // #8A28A3 magenta, stand height
        Color rim = new Color(1f, 0.420f, 0.290f);          // #FF6B4A coral, floodlight rim
        Color night = new Color(0.286f, 0.196f, 0.541f);    // #49328A violet night on the grass

        Color32[] pixels = plate.GetPixels32();
        for (int y = 0; y < Height; y++)
        {
            // v = 0 at the top of the image. Texture rows run bottom-up.
            float v = 1f - (y / (float)(Height - 1));

            Color tint = v < 0.22f ? Color.Lerp(sky, glow, v / 0.22f)
                       : v < 0.42f ? Color.Lerp(glow, rim, (v - 0.22f) / 0.20f)
                                   : Color.Lerp(rim, night, Mathf.Clamp01((v - 0.42f) / 0.22f));

            // Strong through the sky and stands, light over the ground.
            float strength = v < 0.42f ? Mathf.Lerp(0.88f, 0.70f, v / 0.42f)
                                       : Mathf.Lerp(0.70f, 0.46f, Mathf.Clamp01((v - 0.42f) / 0.30f));
            // Gain below 2 keeps bright surfaces - the pitch - from clipping to neon.
            float gain = Mathf.Lerp(1.75f, 1.30f, Mathf.Clamp01(v));

            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                Color src = pixels[i];
                // Multiplicative, not a flat Lerp toward the tint: a Lerp washes every pixel to
                // the same mauve and the turf stops reading as grass. Multiplying keeps the
                // render's own luminance and texture, so the ground stays legible under colour.
                Color tinted = new Color(
                    Mathf.Clamp01(src.r * tint.r * gain),
                    Mathf.Clamp01(src.g * tint.g * gain),
                    Mathf.Clamp01(src.b * tint.b * gain));
                Color graded = Color.Lerp(src, tinted, strength);

                // Vignette: strong on the left where the card sits, lighter on the right.
                float u = x / (float)(Width - 1);
                float darken = Mathf.Lerp(0.58f, 0.04f, Mathf.Clamp01(u / 0.55f))
                             + Mathf.Lerp(0f, 0.30f, Mathf.Clamp01((u - 0.7f) / 0.3f));
                graded = Color.Lerp(graded, Color.black, darken);

                graded.a = 1f;
                pixels[i] = graded;
            }
        }
        plate.SetPixels32(pixels);
        plate.Apply();
    }

    private static void ImportAsBackdrop(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 2048,
            format = TextureImporterFormat.ASTC_6x6,
            compressionQuality = 100,
        });
        importer.SaveAndReimport();
    }
}
