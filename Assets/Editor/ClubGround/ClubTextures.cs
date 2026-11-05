using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The club ground's inputs and materials: where Tools/clubground/build_textures.py and
/// build_sounds.py leave their files, how each is imported for the Quest, and the nine materials
/// the prefab draws with (one per batch).
///
/// Materials are set up here in full - shader keywords, queue and RenderType tag included -
/// because URP's material inspector only does that when a person edits the material
/// (BaseShaderGUI.SetupMaterialBlendModeInternal); a script-made cutout material without
/// _ALPHATEST_ON simply draws its transparent texels as solid.
/// </summary>
public static class ClubTextures
{
    public const string Root = "Assets/ClubGround";
    public const string TextureDir = Root + "/Textures", MaterialDir = Root + "/Materials", MeshDir = Root + "/Meshes";
    public const string Outfield = TextureDir + "/club_outfield.png";
    public const string GrassDetail = TextureDir + "/club_grass_detail.png";
    public const string Surround = TextureDir + "/club_surround.png";
    public const string Atlas = TextureDir + "/club_atlas.png";
    public const string Picket = TextureDir + "/club_picket.png";
    public const string Horizon = TextureDir + "/club_horizon.png";
    public const string Flag = TextureDir + "/club_flag.png";
    public const string Ambience = Root + "/Audio/club_ambience.wav";
    /// The stadium crowd's material and atlas, which the club's spectators reuse.
    public const string CrowdMaterial = "Assets/Resources/Materials/CrowdBillboard.mat";
    public const string CrowdAtlas = "Assets/Resources/Textures/CrowdProcedural.png";
    public const string OutfieldPhysics = "Assets/Resources/Materials/Outfield.physicMaterial";

    public static readonly string[] Inputs =
        { Outfield, GrassDetail, Surround, Atlas, Picket, Horizon, Flag, Ambience, CrowdMaterial, CrowdAtlas, OutfieldPhysics };

    /// Inputs not on disk (the builder stops if any is missing rather than bake a blank ground).
    public static List<string> Missing()
    {
        var missing = new List<string>();
        foreach (string p in Inputs)
            if (!File.Exists(p)) missing.Add(p);
        return missing;
    }

    // ---- Import settings -------------------------------------------------------------------------

    /// ASTC 6x6 with mips on Android (the Quest), trilinear; anisotropic 4 on the outfield only,
    /// the one surface seen at a grazing angle right up to the player's feet. Cutout textures keep
    /// their alpha coverage through the mips (else the pickets and leaves thin away into nothing
    /// in the distance) and bleed colour into their clear texels for alpha-to-coverage edges.
    public static void ConfigureImporters()
    {
        Texture(Outfield, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 4, false);
        Texture(GrassDetail, TextureWrapMode.Repeat, TextureWrapMode.Repeat, 1, false);
        Texture(Surround, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 1, false);
        Texture(Atlas, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 1, true);
        Texture(Picket, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 1, true);
        Texture(Horizon, TextureWrapMode.Repeat, TextureWrapMode.Clamp, 1, true);
        Texture(Flag, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 1, false);
        AmbienceClip(Ambience);
    }

    static void Texture(string path, TextureWrapMode u, TextureWrapMode v, int aniso, bool cutout)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogError("[ClubGround] no texture importer for " + path); return; }
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = true;
        ti.wrapModeU = u;
        ti.wrapModeV = v;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = aniso;
        ti.maxTextureSize = 2048;
        ti.alphaSource = cutout ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.alphaIsTransparency = cutout;
        ti.mipMapsPreserveCoverage = cutout;
        ti.alphaTestReferenceValue = 0.5f;
        ti.textureCompression = TextureImporterCompression.Compressed;
        TextureImporterPlatformSettings android = ti.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;
        ti.SetPlatformTextureSettings(android);
        ti.SaveAndReimport();
    }

    /// Vorbis, decompressed as it plays from a compressed copy in memory (a 30 s mono loop is about
    /// 0.3 MB that way, 2.6 MB as PCM). The importer has no loop flag: ClubGroundVenue loops it
    /// (source.loop = true on the crowd's AudioSource).
    static void AmbienceClip(string path)
    {
        var ai = AssetImporter.GetAtPath(path) as AudioImporter;
        if (ai == null) { Debug.LogError("[ClubGround] no audio importer for " + path); return; }
        AudioImporterSampleSettings s = ai.defaultSampleSettings;
        s.loadType = AudioClipLoadType.CompressedInMemory;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        ai.defaultSampleSettings = s;
        ai.forceToMono = true;
        ai.SaveAndReimport();
    }

    // ---- Materials -----------------------------------------------------------------------------

    const string SimpleLitShader = "Universal Render Pipeline/Simple Lit";

    /// Simple Lit, diffuse only (no specular keyword: the cheapest lit path URP has), no shadows
    /// received. `cutout` = alpha test at 0.5 with alpha-to-coverage; `twoSided` = Cull Off.
    public static Material SimpleLit(string name, Texture tex, bool cutout, bool twoSided)
    {
        var m = new Material(Shader.Find(SimpleLitShader)) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.SetFloat("_ReceiveShadows", 0f);
        m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
        Cutout(m, cutout);
        return m;
    }

    /// The outfield: URP Lit so it can carry the grass detail map (Simple Lit has none) and take
    /// the players' shadows; no specular highlight or reflections (grass has neither worth paying for).
    public static Material OutfieldMaterial(Texture baseMap, Texture detail, float detailRepeats)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ClubOutfield" };
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_WorkflowMode", 1f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.12f);
        m.SetFloat("_SpecularHighlights", 0f);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.SetFloat("_ReceiveShadows", 1f);
        m.SetTexture("_DetailAlbedoMap", detail);
        m.SetTextureScale("_DetailAlbedoMap", Vector2.one * detailRepeats);
        m.SetFloat("_DetailAlbedoMapScale", 1f);
        m.EnableKeyword("_DETAIL_MULX2");    // what LitDetailGUI sets for a scale of exactly 1
        m.SetFloat("_Cull", (float)CullMode.Back);
        Cutout(m, false);
        return m;
    }

    /// Unlit (the horizon band, and the pavilion windows lit at night): fog still applies.
    public static Material Unlit(string name, Texture tex, Color tint, bool cutout)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Cull", (float)CullMode.Back);
        Cutout(m, cutout);
        return m;
    }

    /// The club's own copy of the stadium crowd material. Camera flashes are off by colour, not by
    /// strength (CrowdStand adds _CrowdFlash * 2 to _FlashStrength on every cheer, so only a black
    /// _FlashColor keeps a village crowd from flashing); no Mexican wave; a smaller hop and bob.
    public static Material Spectators(Material stadiumCrowd)
    {
        var m = new Material(stadiumCrowd) { name = "ClubSpectators" };
        m.SetColor("_FlashColor", new Color(0f, 0f, 0f, 1f));
        m.SetFloat("_WaveHeight", 0f);
        m.SetFloat("_JumpHeight", 0.1f);
        m.SetFloat("_CheerBobGain", 1.5f);
        return m;
    }

    /// Opaque or alpha-tested, set the way BaseShaderGUI would set it (opaque + clip turns
    /// alpha-to-coverage on; 4x MSAA then smooths the cut edges instead of letting them crawl).
    static void Cutout(Material m, bool cutout)
    {
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_AlphaToMask", cutout ? 1f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1f);
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (cutout)
        {
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry;
        }
    }

    // ---- Saving ----------------------------------------------------------------------------------

    /// Saves `m` at `path`, or copies it over the asset already there (keeping its GUID, so the
    /// prefab's references survive a rebuild).
    public static Material SaveMaterial(Material m, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(m);
        return existing;
    }

    /// Saves the geometry as a mesh asset at `path` (refilling one already there), vertex-cache optimised.
    public static Mesh SaveMesh(MeshData md, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Mesh mesh = existing != null ? md.Fill(existing) : md.ToMesh(Path.GetFileNameWithoutExtension(path));
        MeshUtility.Optimize(mesh);
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(existing);
        return mesh;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---- Reading pixels --------------------------------------------------------------------------

    /// A PNG's pixels straight from disk, bottom row first (the importer's copy is not readable).
    public static Color32[] ReadPixels(string path, out int width, out int height)
    {
        width = height = 0;
        if (!File.Exists(path)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            width = tex.width; height = tex.height;
            return tex.GetPixels32();
        }
        finally { Object.DestroyImmediate(tex); }
    }

    /// How dark club_surround.png is at the footprints the layout lists, against the mean of the
    /// ring of ground at the same distance (1 = no pool there). build_textures.py paints the contact
    /// shadows from its own copy of the numbers; this is the check that both agree. `flipV` reads
    /// the image with world +z at the bottom instead of the top.
    public static float FootprintShade(Color32[] px, int w, int h, IList<Vector3> spots, bool flipV)
    {
        float range = ClubGroundLayout.SurroundRadius;
        float spotSum = 0f, ringSum = 0f;
        foreach (Vector3 s in spots)
        {
            spotSum += Luma(px, w, h, s.x, s.z, range, flipV);
            float r = new Vector2(s.x, s.z).magnitude, ring = 0f;
            const int N = 48;
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                ring += Luma(px, w, h, Mathf.Cos(a) * r, Mathf.Sin(a) * r, range, flipV);
            }
            ringSum += ring / N;
        }
        return ringSum > 0f ? spotSum / ringSum : 1f;
    }

    /// True only when the pools clearly sit under the spots with the image read upside down.
    public static bool SurroundLooksFlipped(Color32[] px, int w, int h, List<Vector3> feet, List<Vector3> trees)
    {
        var all = new List<Vector3>(feet);
        all.AddRange(trees);
        if (all.Count == 0) return false;
        float straight = FootprintShade(px, w, h, all, false), flipped = FootprintShade(px, w, h, all, true);
        return flipped < 0.95f && flipped < straight - 0.05f;
    }

    static float Luma(Color32[] px, int w, int h, float x, float z, float range, bool flipV)
    {
        float u = x / (2f * range) + 0.5f, v = z / (2f * range) + 0.5f;
        if (flipV) v = 1f - v;
        int ix = Mathf.Clamp(Mathf.RoundToInt(u * (w - 1)), 0, w - 1);
        int iy = Mathf.Clamp(Mathf.RoundToInt(v * (h - 1)), 0, h - 1);
        Color32 c = px[iy * w + ix];
        return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
