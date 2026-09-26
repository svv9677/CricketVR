using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The one visual system every world-space panel is built from (editor only): a dark translucent
/// rounded card, one accent colour, an 8 px spacing grid and large, laser-friendly controls.
/// Panels are 1 canvas px = 1 mm unless a builder says otherwise, so a 64 px control is 64 mm.
///
/// The rounded corners are two generated 9-slice sprites saved under Assets/Resources/UI:
/// UIRounded (corner radius 24 px at pixelsPerUnitMultiplier 1) and UICircle (a disc; sliced, it
/// makes a pill of any size).
/// </summary>
public static class UIStyle
{
    // ---- Palette -------------------------------------------------------------------------------
    public static readonly Color Card = Hex(0x0F1217, 0.9f);
    public static readonly Color Well = Hex(0x07090C, 0.6f);          // behind segmented options
    public static readonly Color Surface = Hex(0x252B35);
    public static readonly Color SurfaceHover = Hex(0x323A47);
    public static readonly Color SurfacePressed = Hex(0x1B2028);
    public static readonly Color Accent = Hex(0x3B82F6);
    public static readonly Color AccentHover = Hex(0x5B98FA);
    public static readonly Color AccentPressed = Hex(0x2B6AD6);
    public static readonly Color AccentText = Hex(0x8AB8FF);          // accent for text on the card
    public static readonly Color Text = Hex(0xF3F5F8);
    public static readonly Color TextMuted = Hex(0x98A2B3);
    public static readonly Color Track = Hex(0x343C49);
    public static readonly Color Divider = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    // ---- Metrics (canvas px) -------------------------------------------------------------------
    public const float Unit = 8f;
    public const float Pad = 4 * Unit;               // card padding
    public const float Gap = 2 * Unit;               // between controls
    public const float SectionGap = 3 * Unit;        // between sections
    public const float Control = 8 * Unit;           // button / segment height: 64 mm
    public const float SliderRow = 7 * Unit;         // slider hit area: 56 mm
    public const float LabelRow = 4 * Unit;          // label + value line above a control
    public const float TrackHeight = 12f;
    public const float Knob = 40f;

    public const float TitleSize = 40f;
    public const float SectionSize = 20f;
    public const float LabelSize = 26f;
    public const float ValueSize = 26f;
    public const float ButtonSize = 26f;
    public const float HintSize = 20f;

    // Radii, as pixelsPerUnitMultiplier of the 24 px UIRounded sprite.
    public const float CardCorners = 1f;             // 24 px
    public const float ControlCorners = 2f;          // 12 px
    public const float OptionCorners = 2.4f;         // 10 px (inside a segmented well)

    // ---- Sprites -------------------------------------------------------------------------------
    public const string Folder = "Assets/Resources/UI";
    private static Sprite rounded, circle;

    public static Sprite Rounded => rounded != null ? rounded : rounded = MakeSprite("UIRounded", 64, 24f, 25);
    public static Sprite Circle => circle != null ? circle : circle = MakeSprite("UICircle", 64, 32f, 31);

    /// Call once before building so both sprites exist and are imported.
    public static void EnsureSprites()
    {
        rounded = null;
        circle = null;
        _ = Rounded;
        _ = Circle;
    }

    /// A rounded (9-sliced) image. `corners` is the pixelsPerUnitMultiplier: bigger = tighter.
    public static void RoundedImage(Image image, Color color, float corners)
    {
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = corners;
        image.color = color;
    }

    /// A pill (track, fill) or, at equal width and height, a disc.
    public static void PillImage(Image image, Color color)
    {
        image.sprite = Circle;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
    }

    /// Colours for a control whose Image is white: the block supplies the actual colour.
    public static ColorBlock Colors(bool primary)
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = primary ? Accent : Surface;
        block.highlightedColor = primary ? AccentHover : SurfaceHover;
        block.pressedColor = primary ? AccentPressed : SurfacePressed;
        block.selectedColor = block.normalColor;
        block.disabledColor = new Color(block.normalColor.r, block.normalColor.g, block.normalColor.b, 0.35f);
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;
        return block;
    }

    /// Colours for an option inside a segmented well: see-through until hovered.
    public static ColorBlock OptionColors()
    {
        ColorBlock block = ColorBlock.defaultColorBlock;
        block.normalColor = Clear;
        block.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
        block.pressedColor = new Color(1f, 1f, 1f, 0.04f);
        block.selectedColor = Clear;
        block.disabledColor = Clear;
        block.colorMultiplier = 1f;
        block.fadeDuration = 0.08f;
        return block;
    }

    public static string HexString(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    private static Color Hex(int rgb, float alpha = 1f) =>
        new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

    /// A white anti-aliased rounded square saved as a 9-slice sprite (made once, then reused).
    private static Sprite MakeSprite(string name, int size, float radius, int border)
    {
        string path = $"{Folder}/{name}.png";
        if (!File.Exists(path))
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));
            File.WriteAllBytes(path, RoundedSquarePng(size, radius));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var borders = new Vector4(border, border, border, border);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != borders)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = borders;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static byte[] RoundedSquarePng(int size, float radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance from the pixel centre to the rounded square's edge.
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - outside) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        return png;
    }
}
