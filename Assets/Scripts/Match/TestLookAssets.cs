using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The materials and textures a Test match is dressed in (TestLook), made once by CricketVR > Scene
/// Objects > Build Test Look and saved at Resources/TestLook/TestLookAssets. They are in Resources,
/// not referenced from the scene, so a limited-overs match never loads them: TestLook loads this
/// only when the match is a Test. (Until 2026-11-15 the game made them in code at the start of
/// every Test.)
/// </summary>
public class TestLookAssets : ScriptableObject
{
    public const string ResourcePath = "TestLook/TestLookAssets";

    [System.Serializable]
    public struct MaterialSwap
    {
        public Material from, to;
    }

    [System.Serializable]
    public struct TextureSwap
    {
        public Texture from, to;
    }

    [Tooltip("The fielders' kit: one flat cream material for shirt, trousers and shoes.")]
    public Material whites;
    [Tooltip("The sightscreen, off-white.")]
    public Material sightScreen;
    [Tooltip("The ball's trail in a Test, by the trail material it replaces: red, and pink under lights.")]
    public MaterialSwap[] redTrail = new MaterialSwap[0];
    public MaterialSwap[] pinkTrail = new MaterialSwap[0];
    [Tooltip("The stadium, crowd and club ground's materials, by the one each replaces: the same with its reds turned blue.")]
    public MaterialSwap[] backdrop = new MaterialSwap[0];
    [Tooltip("The textures behind those, for materials that are copies made at runtime (the club ground's night copies).")]
    public TextureSwap[] backdropTextures = new TextureSwap[0];

    /// `swaps` as a lookup from the material replaced.
    public static Dictionary<Material, Material> Lookup(MaterialSwap[] swaps)
    {
        var map = new Dictionary<Material, Material>();
        foreach (MaterialSwap s in swaps)
            if (s.from != null && s.to != null)
                map[s.from] = s.to;
        return map;
    }

    public static Dictionary<Texture, Texture> Lookup(TextureSwap[] swaps)
    {
        var map = new Dictionary<Texture, Texture>();
        foreach (TextureSwap s in swaps)
            if (s.from != null && s.to != null)
                map[s.from] = s.to;
        return map;
    }
}
