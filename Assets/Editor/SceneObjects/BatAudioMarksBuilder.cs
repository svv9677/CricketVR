using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts on Assets/Resources/Prefabs/Bat.prefab what the game used to add to the bat in code as a
/// match started (until 2026-11-15):
///   * BatWhoosh, with its "Whoosh Voice" child under the sweet spot (an AudioSource looping the
///     air noise, with a low-pass and a high-pass filter). The noise is written to
///     Assets/Art/Generated/Bat/Bat Air.wav from the same samples the game used to make.
///   * BatMarks, with its "Ball Marks" child (MeshFilter + MeshRenderer, shadows off) using the
///     CricketVR/BatMark material and the print texture, both saved as assets. The marks mesh
///     itself is rebuilt as the match goes, so the filter's mesh stays empty here.
///   * The Impact Voice's 3D sound settings (BatSounds used to overwrite them as it started).
/// Run again to put them back to these settings; it changes nothing else on the prefab.
/// </summary>
public static class BatAudioMarksBuilder
{
    private const string BatPrefab = "Assets/Resources/Prefabs/Bat.prefab";
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/Bat";
    public const string NoisePath = Folder + "/Bat Air.wav";
    public const string PrintPath = Folder + "/Ball Print.png";
    public const string MarksMaterialPath = Folder + "/Bat Marks.mat";

    [MenuItem("CricketVR/Scene Objects/Place Bat Audio And Marks")]
    public static void Place()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BatPrefab);
        try
        {
            PlaceWhoosh(root);
            PlaceMarks(root);
            ConfigureImpactVoice(root);
            PrefabUtility.SaveAsPrefabAsset(root, BatPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BatAudioMarksBuilder] Bat prefab: whoosh, marks and impact voice placed.");
    }

    private static void PlaceWhoosh(GameObject root)
    {
        BatWhoosh whoosh = SceneObjectAssets.Ensure<BatWhoosh>(root.gameObject);
        Transform spot = root.GetComponent<BatGeometry>().sweetSpot;
        Transform child = spot.Find("Whoosh Voice");
        if (child == null)
        {
            child = new GameObject("Whoosh Voice").transform;
            child.SetParent(spot, false);
        }
        AudioClip noise = VisualAssetFiles.SaveWav(Noise(), 44100, NoisePath);
        AudioSource source = SceneObjectAssets.Ensure<AudioSource>(child.gameObject);
        source.clip = noise;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 0.6f;
        source.maxDistance = 12f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.volume = 0f;
        source.priority = 64;
        var low = SceneObjectAssets.Ensure<AudioLowPassFilter>(child.gameObject);
        low.lowpassResonanceQ = 1.2f;
        var high = SceneObjectAssets.Ensure<AudioHighPassFilter>(child.gameObject);
        high.highpassResonanceQ = 1f;
        var so = new SerializedObject(whoosh);
        so.FindProperty("voice").objectReferenceValue = source;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlaceMarks(GameObject root)
    {
        BatMarks marks = SceneObjectAssets.Ensure<BatMarks>(root.gameObject);
        Transform child = root.transform.Find("Ball Marks");
        if (child == null)
        {
            child = new GameObject("Ball Marks").transform;
            child.SetParent(root.transform, false);
        }
        child.gameObject.layer = root.layer;
        var filter = SceneObjectAssets.Ensure<MeshFilter>(child.gameObject);
        filter.sharedMesh = null;       // made as prints are added (BatMarks)
        var r = SceneObjectAssets.Ensure<MeshRenderer>(child.gameObject);
        r.sharedMaterial = MarksMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        var so = new SerializedObject(marks);
        so.FindProperty("marksFilter").objectReferenceValue = filter;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// CricketVR/BatMark with the print texture (clamped, no mipmaps, as the game made it).
    private static Material MarksMaterial()
    {
        Texture2D print = VisualAssetFiles.SavePng(BatMarks.PrintTexture(true), PrintPath, false, TextureWrapMode.Clamp, true);
        var mat = new Material(Shader.Find("CricketVR/BatMark")) { name = "Bat Marks" };
        mat.SetTexture("_BaseMap", print);
        return SceneObjectAssets.SaveMaterial(mat, MarksMaterialPath);
    }

    /// What BatSounds used to set on the Impact Voice each time the game started.
    private static void ConfigureImpactVoice(GameObject root)
    {
        AudioSource source = new SerializedObject(root.GetComponent<BatSounds>()).FindProperty("voice").objectReferenceValue as AudioSource;
        if (source == null)
        {
            Debug.LogWarning("[BatAudioMarksBuilder] BatSounds has no Impact Voice; run Tools > CricketVR > Add Bat Impact Voice first.");
            return;
        }
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 1.5f;
        source.maxDistance = 60f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        var tone = SceneObjectAssets.Ensure<AudioLowPassFilter>(source.gameObject);
        tone.lowpassResonanceQ = 1f;
    }

    /// Two seconds of pink-ish noise with a slow flutter, its ends crossfaded so the loop has no seam
    /// (the samples BatWhoosh used to make as the game started).
    private static float[] Noise()
    {
        const int rate = 44100, n = rate * 2, fade = rate / 10;
        var data = new float[n + fade];
        var rng = new System.Random(1108);
        float b0 = 0f, b1 = 0f, b2 = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            // Paul Kellet's economy pink filter.
            b0 = 0.99765f * b0 + w * 0.0990460f;
            b1 = 0.96300f * b1 + w * 0.2965164f;
            b2 = 0.57000f * b2 + w * 1.0526913f;
            float pink = (b0 + b1 + b2 + w * 0.1848f) * 0.18f;
            float t = i / (float)rate;
            float flutter = 1f + 0.12f * Mathf.Sin(t * 2f * Mathf.PI * 7f) + 0.08f * Mathf.Sin(t * 2f * Mathf.PI * 11.5f);
            data[i] = pink * flutter;
        }
        var loop = new float[n];
        for (int i = 0; i < n; i++)
            loop[i] = data[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            loop[i] = data[i] * k + data[n + i] * (1f - k);
        }
        return loop;
    }
}
