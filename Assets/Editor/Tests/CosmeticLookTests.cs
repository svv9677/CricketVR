using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The locker's look in a scene (phase 7): there only in a career match or career nets; the bat's
/// finish as material copies on a 6-material bat (the shared assets untouched, Classic and other
/// renderers left alone); the gloves' base and emission colours; and the replay helmet's shell and
/// grille (ReplayBatterLook), gone again after Clear.
/// </summary>
public class CosmeticLookTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        ReplayBatterLook.Clear();
        foreach (Object o in made)
            if (o != null)
                Object.DestroyImmediate(o);
        made.Clear();
    }

    private static CareerProfile Career()
    {
        var p = new CareerProfile { name = "Sam Carter", look = new CareerAppearance { skin = 4, kit = 6 } };
        p.season = new Season { yourTeam = "a" };
        p.season.teams.Add(new Team { id = "a", kit = 2 });
        p.season.teams.Add(new Team { id = "b", kit = 5 });
        return p;
    }

    private Material Lit(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Assert.IsNotNull(shader, "URP Lit is in the project");
        var m = new Material(shader);
        m.SetColor(CosmeticLook.BaseColor, c);
        made.Add(m);
        return m;
    }

    /// A material colour as set (to float precision: the colour space may convert it on the way).
    private static void Same(Color want, Color got, string what = "")
    {
        for (int i = 0; i < 4; i++)
            Assert.AreEqual(want[i], got[i], 1e-3f, $"{what} channel {i}");
    }

    private MeshRenderer Renderer(int materials)
    {
        var go = new GameObject("Bat");
        made.Add(go);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        var mats = new Material[materials];
        for (int i = 0; i < materials; i++)
            mats[i] = Lit(new Color(0.1f * i, 0.5f, 0.5f));
        r.sharedMaterials = mats;
        return r;
    }

    // ---- Where there is a look ----------------------------------------------------------------

    [Test]
    public void ThereIsALookOnlyInACareerMatchOrCareerNets()
    {
        CareerProfile p = Career();
        var plan = new MatchPlan { yourTeam = "a" };
        Assert.IsNotNull(CosmeticLook.For("CricketVR", p, true, plan, false));
        Assert.IsNotNull(CosmeticLook.For("Nets", p, false, null, true));
        Assert.IsNull(CosmeticLook.For("CricketVR", p, false, null, false), "Quick Match");
        Assert.IsNull(CosmeticLook.For("Nets", p, false, null, false), "free nets");
        Assert.IsNull(CosmeticLook.For("Nets", p, true, plan, false), "a match flag is not the nets");
        Assert.IsNull(CosmeticLook.For("Splash", p, true, plan, true));
        Assert.IsNull(CosmeticLook.For("CricketVR", null, true, plan, false), "no career");
    }

    [Test]
    public void TheLookIsWhatHeHasOn_InHisSidesColour()
    {
        CareerProfile p = Career();
        p.unlocked.Add("bat.pro");
        p.unlocked.Add("gloves.side");
        p.batFinish = "bat.pro";
        p.gloves = "gloves.side";
        p.kitTrim = "trim.gold";   // not his: the default
        CosmeticLook look = CosmeticLook.For("CricketVR", p, true, new MatchPlan { yourTeam = "b" }, false);
        Assert.AreEqual("bat.pro", look.bat.id);
        Assert.AreEqual(Appearance.KitColours[5], look.side, "the match's side");
        Assert.AreEqual(Appearance.KitColours[5], look.GloveColour.Value);
        Assert.IsNull(look.TrimColour, "the default trim changes nothing");
        Assert.AreEqual(Appearance.SkinTones[4], look.skin);

        Assert.AreEqual(Appearance.KitColours[2], CosmeticLook.SideColour(p, null), "the season's side");
        p.season = null;
        Assert.AreEqual(Appearance.KitColours[6], CosmeticLook.SideColour(p, null), "his own kit");
        Assert.AreEqual(Appearance.KitColours[6], CosmeticLook.For("Nets", p, false, null, true).side);
    }

    // ---- Putting it on --------------------------------------------------------------------------

    [Test]
    public void TheBatFinishIsCopiesOfItsMaterials_TheAssetsUntouched()
    {
        MeshRenderer r = Renderer(BatSubmesh.Count);
        Material[] before = r.sharedMaterials;
        var colours = new Color[BatSubmesh.Count];
        for (int i = 0; i < colours.Length; i++)
            colours[i] = before[i].GetColor(CosmeticLook.BaseColor);
        CosmeticDef ton = CosmeticCatalog.For("bat.ton");
        var look = new CosmeticLook { bat = ton };

        List<Material> copies = look.ApplyBat(r);
        made.AddRange(copies);
        Material[] after = r.sharedMaterials;
        for (int i = 0; i < BatSubmesh.Count; i++)
        {
            Assert.AreNotSame(before[i], after[i], $"submesh {i} is a copy");
            Same(ton.bat[i], after[i].GetColor(CosmeticLook.BaseColor), $"submesh {i}");
            Same(colours[i], before[i].GetColor(CosmeticLook.BaseColor), "the shared material is unchanged");
        }
        Assert.AreEqual(BatSubmesh.Count, copies.Count);
    }

    [Test]
    public void AKeptPartKeepsItsMaterial()
    {
        MeshRenderer r = Renderer(BatSubmesh.Count);
        Material[] before = r.sharedMaterials;
        made.AddRange(new CosmeticLook { bat = CosmeticCatalog.For("bat.pro") }.ApplyBat(r));
        Assert.AreSame(before[BatSubmesh.Blade], r.sharedMaterials[BatSubmesh.Blade], "the willow is the model's");
        Assert.AreSame(before[BatSubmesh.Face], r.sharedMaterials[BatSubmesh.Face]);
        Assert.AreNotSame(before[BatSubmesh.Grip], r.sharedMaterials[BatSubmesh.Grip]);
    }

    [Test]
    public void ClassicAndOtherRenderersAreLeftAlone()
    {
        MeshRenderer bat = Renderer(BatSubmesh.Count);
        Material[] before = bat.sharedMaterials;
        Assert.IsEmpty(new CosmeticLook { bat = CosmeticCatalog.Default(eCosmeticSlot.Bat) }.ApplyBat(bat));
        Assert.AreSame(before[0], bat.sharedMaterials[0]);

        MeshRenderer five = Renderer(5);
        Material[] fiveBefore = five.sharedMaterials;
        Assert.IsEmpty(new CosmeticLook { bat = CosmeticCatalog.For("bat.pro") }.ApplyBat(five));
        for (int i = 0; i < 5; i++)
            Assert.AreSame(fiveBefore[i], five.sharedMaterials[i]);
        Assert.IsEmpty(new CosmeticLook().ApplyBat(null));
    }

    [Test]
    public void GlovesTintBaseAndEmission_OneCopyAMaterial()
    {
        Material hand = Lit(new Color(0.72f, 0.78f, 0.86f));
        MeshRenderer left = Renderer(0), right = Renderer(0);
        left.sharedMaterial = hand;
        right.sharedMaterial = hand;
        var look = new CosmeticLook { gloves = CosmeticCatalog.For("gloves.black") };
        List<Material> copies = look.ApplyGloves(new Renderer[] { left, right });
        made.AddRange(copies);
        Assert.AreEqual(1, copies.Count, "both hands share the one copy");
        Color c = CosmeticCatalog.For("gloves.black").colour;
        Same(c, left.sharedMaterial.GetColor(CosmeticLook.BaseColor), "base");
        Same(c * CosmeticLook.GloveEmission, left.sharedMaterial.GetColor(CosmeticLook.EmissionColor), "emission");
        Assert.AreSame(left.sharedMaterial, right.sharedMaterial);
        Same(new Color(0.72f, 0.78f, 0.86f), hand.GetColor(CosmeticLook.BaseColor), "the asset is unchanged");

        Assert.IsEmpty(new CosmeticLook { gloves = CosmeticCatalog.Default(eCosmeticSlot.Gloves) }.ApplyGloves(new Renderer[] { left }));
    }

    [Test]
    public void TheReplayHelmetWearsShellAndTrim_AndNothingAfterClear()
    {
        MeshRenderer helmet = Renderer(2);
        Material[] before = helmet.sharedMaterials;
        ReplayBatterLook.Shell = Color.red;
        ReplayBatterLook.Trim = Color.yellow;
        ReplayBatterLook.Dress(helmet);
        Same(Color.red, helmet.sharedMaterials[ReplayBatterLook.ShellIndex].GetColor(CosmeticLook.BaseColor), "shell");
        Same(Color.yellow, helmet.sharedMaterials[ReplayBatterLook.TrimIndex].GetColor(CosmeticLook.BaseColor), "trim");
        Assert.AreNotEqual(Color.red, before[0].GetColor(CosmeticLook.BaseColor), "the keeper's helmet is unchanged");

        ReplayBatterLook.Clear();
        Assert.IsNull(ReplayBatterLook.Shell);
        Assert.AreEqual(ReplayBatterLook.DefaultSkin, ReplayBatterLook.Skin);
        MeshRenderer again = Renderer(2);
        Material[] plain = again.sharedMaterials;
        ReplayBatterLook.Dress(again);
        Assert.AreSame(plain[0], again.sharedMaterials[0]);
        Assert.AreSame(plain[1], again.sharedMaterials[1]);

        ReplayBatterLook.Shell = Color.red;
        MeshRenderer one = Renderer(1);
        Material only = one.sharedMaterials[0];
        ReplayBatterLook.Dress(one);
        Assert.AreSame(only, one.sharedMaterials[0], "a one-material helmet is not the keeper's");
    }
}
