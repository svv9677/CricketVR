using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the crowd shader's per-pixel work into its atlas, once (2026-11-06: "when I have a bigger
/// crowd, it seems to be much, much laggier... make it more optimized"). CricketVR/CrowdStand keyed
/// the flat brown background out of every crowd pixel, every frame - a colour distance, two
/// smoothsteps and fwidth, then the "no white shirts" recolour - over a large part of the view,
/// twice where the rows overlap, and the upper tier on top for a packed crowd. All of that depends
/// on the texture alone, so it is done here: the key's coverage into the alpha channel, the shirts
/// darkened in the colour, saved beside the original as *_Keyed.png, and every crowd material
/// switched to it with _PreKeyed on (the shader's one-sample path). Mipmaps keep the coverage
/// (mipMapsPreserveCoverage), so the far stands do not thin out.
///
/// Tools > CricketVR > Bake Crowd Key. Safe to run again (it always bakes from the original).
/// </summary>
public static class CrowdKeyBaker
{
    private const string ShaderName = "CricketVR/CrowdStand";

    [MenuItem("Tools/CricketVR/Bake Crowd Key")]
    public static void BakeAll()
    {
        var baked = new Dictionary<string, Texture2D>();
        int materials = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || mat.shader == null || mat.shader.name != ShaderName)
                continue;
            Texture2D source = Original(mat);
            if (source == null)
                continue;
            string key = AssetDatabase.GetAssetPath(source) + "|" + Params(mat);
            if (!baked.TryGetValue(key, out Texture2D keyed))
                baked[key] = keyed = Bake(source, mat);
            if (keyed == null)
                continue;
            mat.SetTexture("_BaseMap", keyed);
            mat.SetFloat("_PreKeyed", 1f);
            EditorUtility.SetDirty(mat);
            materials++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CrowdKeyBaker] {baked.Count} atlas(es) baked, {materials} crowd material(s) on the pre-keyed path");
    }

    /// The material's original atlas (its keyed one's source if it already points at a bake).
    private static Texture2D Original(Material mat)
    {
        var tex = mat.GetTexture("_BaseMap") as Texture2D;
        if (tex == null)
            return null;
        string path = AssetDatabase.GetAssetPath(tex);
        if (path.EndsWith("_Keyed.png") || path.EndsWith(TestSuffix + ".png"))
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace("_Keyed.png", ".png").Replace(TestSuffix + ".png", ".png"));
        return tex;
    }

    private static string Params(Material m) =>
        $"{m.GetVector("_KeyColor")}{m.GetFloat("_KeyTolerance")}{m.GetFloat("_KeySoftness")}{m.GetFloat("_WhiteFrom")}{m.GetColor("_WhiteTo")}";

    private static Texture2D Bake(Texture2D source, Material mat)
    {
        string srcPath = AssetDatabase.GetAssetPath(source);
        string outPath = srcPath.Replace(".png", "_Keyed.png");
        Color[] px = ReadOriginal(srcPath, out int w, out int h);
        if (px == null)
            return null;
        Vector4 key = mat.GetVector("_KeyColor");
        float tol = mat.GetFloat("_KeyTolerance"), soft = mat.GetFloat("_KeySoftness"), whiteFrom = mat.GetFloat("_WhiteFrom");
        Color whiteTo = mat.GetColor("_WhiteTo").linear;
        var outPx = new Color[px.Length];
        for (int i = 0; i < px.Length; i++)
            outPx[i] = Keyed(px[i], key, tol, soft, whiteFrom, whiteTo);
        Write(outPath, srcPath, outPx, w, h);
        // A Test's crowd: the same, its red shirts blue, so nothing red is behind the red ball
        // (TestLook swaps it in; recolouring at runtime cost an uncompressed copy and the coverage).
        for (int i = 0; i < outPx.Length; i++)
        {
            Color32 c = outPx[i];
            outPx[i] = TestLook.RedToBlue(c);
        }
        Write(outPath.Replace("_Keyed.png", TestSuffix + ".png"), srcPath, outPx, w, h);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    /// The Test variant of a keyed atlas: CrowdProcedural_KeyedTest beside CrowdProcedural_Keyed.
    public const string TestSuffix = "_KeyedTest";

    private static void Write(string path, string srcPath, Color[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        Import(path, srcPath);
    }

    /// One pixel as the shader's key and white-shirt passes made it, in linear space as the shader
    /// works (the atlas is sRGB), written back as sRGB colour with linear coverage in alpha.
    public static Color Keyed(Color srgb, Vector4 keyLinear, float tol, float soft, float whiteFrom, Color whiteToLinear)
    {
        Color lin = srgb.linear;
        float d = Vector3.Distance(new Vector3(lin.r, lin.g, lin.b), new Vector3(keyLinear.x, keyLinear.y, keyLinear.z));
        float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tol, tol + soft, d));
        float brightest = Mathf.Max(lin.r, Mathf.Max(lin.g, lin.b));
        float spread = brightest - Mathf.Min(lin.r, Mathf.Min(lin.g, lin.b));
        float whiteness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(whiteFrom, whiteFrom + 0.15f, brightest)) *
                          (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.14f, spread)));
        Color c = Color.Lerp(lin, whiteToLinear, whiteness).gamma;
        c.a = coverage;
        return c;
    }

    /// The original's pixels as authored (the PNG itself, not its compressed import).
    private static Color[] ReadOriginal(string path, out int w, out int h)
    {
        w = h = 0;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!t.LoadImage(File.ReadAllBytes(path)))
            return null;
        w = t.width;
        h = t.height;
        Color[] px = t.GetPixels();
        Object.DestroyImmediate(t);
        return px;
    }

    /// The keyed atlas imported as the original is (size, compression), with alpha, and mipmaps that
    /// keep its coverage at a 0.5 cut.
    private static void Import(string outPath, string srcPath)
    {
        var src = (TextureImporter)AssetImporter.GetAtPath(srcPath);
        var dst = (TextureImporter)AssetImporter.GetAtPath(outPath);
        dst.textureType = TextureImporterType.Default;
        dst.sRGBTexture = true;
        dst.alphaSource = TextureImporterAlphaSource.FromInput;
        dst.alphaIsTransparency = false;
        dst.mipmapEnabled = true;
        dst.mipMapsPreserveCoverage = true;
        dst.alphaTestReferenceValue = 0.5f;
        dst.maxTextureSize = src.maxTextureSize;
        dst.wrapMode = src.wrapMode;
        dst.filterMode = src.filterMode;
        dst.anisoLevel = src.anisoLevel;
        TextureImporterPlatformSettings android = src.GetPlatformTextureSettings("Android");
        android.format = TextureImporterFormat.ASTC_6x6;
        dst.SetPlatformTextureSettings(android);
        dst.SaveAndReimport();
    }
}
