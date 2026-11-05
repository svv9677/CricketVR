using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The club's picket fence is stained brown wood, not white paint: club_picket.png's opaque texels
/// average a mid brown (red over green over blue, well short of white), as build_textures.py's
/// STAIN paints them; and the gate is timber from the atlas, not white paint. Reads the PNG from
/// disk, so it needs the editor (Texture2D.LoadImage), unlike the geometry tests.
/// </summary>
public class ClubFenceStainTests
{
    [Test]
    public void ThePicketsAreStainedBrownWood()
    {
        Color32[] px = ClubTextures.ReadPixels(ClubTextures.Picket, out int w, out int h);
        Assert.IsNotNull(px, ClubTextures.Picket + " is missing: run Tools/clubground/build_textures.py");
        double r = 0, g = 0, b = 0;
        int n = 0;
        foreach (Color32 c in px)
        {
            if (c.a < 128) continue;
            r += c.r; g += c.g; b += c.b; n++;
        }
        Assert.Greater(n, w * h / 3, "the fence covers most of its strip");
        r /= n; g /= n; b /= n;
        Assert.Greater(r, g + 15, "warm: red over green");
        Assert.Greater(g, b + 15, "green over blue: brown, not grey");
        Assert.Less(0.2126 * r + 0.7152 * g + 0.0722 * b, 140, "a wood stain, not white paint");
    }

    [Test]
    public void TheGateIsTimber()
    {
        Assert.AreNotEqual(ClubCell.WhitePaint, ClubFence.GateWood);
        Assert.AreEqual(ClubCellMode.Tiled, ClubAtlas.ModeOf(ClubFence.GateWood), "boards, laid at their real size");
    }
}
