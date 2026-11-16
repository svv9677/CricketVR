using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the cloud plates under Resources/Sky/Clouds. They are saved in each plate's
/// .meta, so the Texture Import inspector shows what is used. After adding a plate, run
/// CricketVR > Sky > Apply Cloud Plate Import Settings. (Until 2026-11-15 this was an import hook
/// that forced them on every import, overwriting anything changed in the inspector.)
///
/// The one that matters is Alpha Is Transparency. A cloud plate is mostly transparent, and
/// without it Unity leaves the colour of fully-transparent texels alone; bilinear filtering and
/// the lower mips then blend that colour into the cloud's edge and hang a dark fringe around
/// every cloud. That fringe is the single clearest tell that a sky is made of billboards.
/// </summary>
public static class CloudPlateImporter
{
    private const string PlateFolder = "Assets/Resources/" + ProceduralClouds.PlateResourcePath;

    [MenuItem("CricketVR/Sky/Apply Cloud Plate Import Settings")]
    public static void ApplyAll()
    {
        int changed = 0, plates = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PlateFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                continue;
            plates++;
            string before = EditorJsonUtility.ToJson(importer) + Android(importer);
            Apply(importer);
            if (EditorJsonUtility.ToJson(importer) + Android(importer) == before)
                continue;
            importer.SaveAndReimport();
            changed++;
        }
        Debug.Log($"[CloudPlateImporter] {plates} cloud plates; {changed} changed");
    }

    private static string Android(TextureImporter importer) =>
        JsonUtility.ToJson(importer.GetPlatformTextureSettings("Android"));

    public static void Apply(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;          // clouds are viewed at a wide range of sizes
        importer.wrapMode = TextureWrapMode.Clamp;  // a tiled cloud would wrap its own edge in
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Compressed;

        // ASTC keeps the alpha gradients in the wispy edges, which DXT/ETC visibly band.
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 1024,
            format = TextureImporterFormat.ASTC_6x6,
            compressionQuality = (int)TextureCompressionQuality.Normal,
        });
    }
}
