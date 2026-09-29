using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the cloud plates under Resources/Sky/Clouds, applied automatically so a
/// plate dropped into that folder is always usable without anyone remembering the checkboxes.
///
/// The one that matters is Alpha Is Transparency. A cloud plate is mostly transparent, and
/// without it Unity leaves the colour of fully-transparent texels alone; bilinear filtering and
/// the lower mips then blend that colour into the cloud's edge and hang a dark fringe around
/// every cloud. That fringe is the single clearest tell that a sky is made of billboards.
/// </summary>
public class CloudPlateImporter : AssetPostprocessor
{
    private const string PlateFolder = "Assets/Resources/" + ProceduralClouds.PlateResourcePath;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(PlateFolder + "/"))
            return;

        var importer = (TextureImporter)assetImporter;
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
