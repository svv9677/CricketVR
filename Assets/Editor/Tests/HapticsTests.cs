using NUnit.Framework;

/// <summary>Every haptic pattern is well formed: times in order, amplitudes 0..1, ending at
/// silence (or the last impulse would keep the controller buzzing), and short.</summary>
public class HapticsTests
{
    [Test]
    public void PatternsAreOrderedBoundedAndEndSilent()
    {
        var all = new[] { Haptics.Middled, Haptics.GoodHit, Haptics.ThickEdge, Haptics.ThinEdge, Haptics.Jar, Haptics.Sting,
                          Haptics.Brush, Haptics.Four, Haptics.Six, Haptics.Wicket, Haptics.DoubleTap };
        foreach (var keys in all)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.That(keys[i].amp, Is.InRange(0f, 1f));
                if (i > 0) Assert.Greater(keys[i].t, keys[i - 1].t);
            }
            Assert.AreEqual(0f, keys[keys.Length - 1].amp, "ends silent");
            Assert.Less(keys[keys.Length - 1].t, 1f, "under a second");
        }
        // A middled hit is the crisp one: over sooner than an edge or a jar.
        Assert.Less(Haptics.Middled[Haptics.Middled.Length - 1].t, Haptics.Jar[Haptics.Jar.Length - 1].t);
    }
}
