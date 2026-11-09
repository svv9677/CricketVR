using NUnit.Framework;
using UnityEngine;

/// The ends changing every over, the Test's no-red backdrop, the pre-keyed crowd, and quick play's
/// menus (2026-11-06).
public class EndChangeAndTestLookTests
{
    [Test]
    public void TheEndsChangeEveryOver()
    {
        Assert.IsFalse(EndChange.OtherEndAfter(0), "the first over from this end");
        Assert.IsTrue(EndChange.OtherEndAfter(1));
        Assert.IsFalse(EndChange.OtherEndAfter(2));
        Assert.IsTrue(EndChange.OtherEndAfter(19));
    }

    [Test]
    public void RedsGoBlueAndNothingElseChanges()
    {
        Color32 red = new Color32(200, 20, 30, 255);
        Color32 blue = TestLook.RedToBlue(red);
        Assert.Greater(blue.b, blue.r, "a red board turns blue");
        Assert.AreEqual(255, blue.a);
        foreach (Color32 keep in new[]
                 {
                     new Color32(230, 120, 30, 255),    // the orange seats
                     new Color32(40, 140, 50, 255),     // grass
                     new Color32(240, 240, 240, 255),   // white
                     new Color32(40, 8, 8, 255),        // a red too dark to see
                     new Color32(150, 110, 110, 255),   // a dull pinkish grey
                 })
            Assert.AreEqual(keep, TestLook.RedToBlue(keep), keep.ToString());
    }

    [Test]
    public void TheBakedKeyCutsTheBackgroundAndKeepsThePeople()
    {
        Vector4 key = new Vector4(0.037f, 0.028f, 0.024f, 0f);
        Color whiteTo = new Color(0.03f, 0.03f, 0.035f).linear;
        Color background = new Color(54 / 255f, 47 / 255f, 43 / 255f);
        Assert.Less(CrowdKeyBaker.Keyed(background, key, 0.02f, 0.006f, 0.4f, whiteTo).a, 0.5f, "the brown is cut");
        Color shirt = new Color(0.1f, 0.3f, 0.8f);
        Color kept = CrowdKeyBaker.Keyed(shirt, key, 0.02f, 0.006f, 0.4f, whiteTo);
        Assert.Greater(kept.a, 0.5f, "a blue shirt stays");
        Assert.AreEqual(shirt.b, kept.b, 0.01f, "and keeps its colour");
        Color white = CrowdKeyBaker.Keyed(Color.white, key, 0.02f, 0.006f, 0.4f, whiteTo);
        Assert.Less(white.r, 0.3f, "white shirts darkened, as the shader did");
    }

    [Test]
    public void QuickPlayHasNoBowlerChoiceOrMatchTuning()
    {
        Assert.IsFalse(ModeMenus.CanChangeBowler(ePlayMode.QuickPlay));
        Assert.IsFalse(ModeMenus.CanChangeBowler(ePlayMode.CareerMatch));
        Assert.IsTrue(ModeMenus.CanChangeBowler(ePlayMode.FreeNets));
        Assert.IsFalse(ModeMenus.ShowsMatchTuning(ePlayMode.QuickPlay));
        Assert.IsTrue(ModeMenus.ShowsMatchTuning(ePlayMode.FreeNets));
    }
}
