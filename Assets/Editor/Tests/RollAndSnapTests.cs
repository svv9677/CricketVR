using NUnit.Framework;
using UnityEngine;

/// The outfield lets a slow ball roll on (2026-11-09: "it takes much more time for it to actually
/// stop moving"), and a pace bowler's arm speeds up into the release (BowlerSnap).
public class RollAndSnapTests
{
    private static readonly Vector3 Outfield = new Vector3(30f, 0f, 20f);

    /// Seconds and metres to a stop from `v`, stepping the decel as Ball.FixedUpdate does.
    private static void Roll(float v, out float seconds, out float metres)
    {
        const float dt = 0.02f;
        seconds = 0f;
        metres = 0f;
        while (v > 0.1f)
        {
            v = Mathf.Max(0f, v - BallFlight.RollingDecelAt(Outfield, v) * dt);
            metres += v * dt;
            seconds += dt;
        }
    }

    [Test]
    public void AHardHitBallSlowsAsBeforeAndASlowOneRollsOn()
    {
        Assert.AreEqual(BallFlight.OutfieldRollingDecel, BallFlight.RollingDecelAt(Outfield, 20f), 1e-4f, "as before at 20 m/s");
        Assert.AreEqual(BallFlight.RollFloor, BallFlight.RollingDecelAt(Outfield, 0f), 1e-4f);
        Roll(3f, out float slowSeconds, out _);
        Assert.Greater(slowSeconds, 4f, "3 m/s rolls on for more than 4 s (was 1.9)");
        Roll(10f, out _, out float pushMetres);
        Assert.AreEqual(BallFlight.OutfieldRollDistance(10f), pushMetres, 1f, "the closed form matches the steps");
        Assert.Greater(pushMetres, 50f, "a 10 m/s push carries past 50 m (was 31)");
    }

    [Test]
    public void TheSpeedForADistanceRollsThatFar()
    {
        foreach (float d in new[] { 2f, 10f, 30f, 60f })
            Assert.AreEqual(d, BallFlight.OutfieldRollDistance(BallFlight.OutfieldRollSpeedFor(d)), 0.01f);
    }

    [Test]
    public void APaceArmSpeedsUpIntoTheReleaseWithNoJumps()
    {
        foreach (BowlerSnap.Swing s in BowlerSnap.Swings)
        {
            Assert.AreEqual(BowlerSnap.Base, BowlerSnap.SpeedAt(s, 0.1f), 1e-4f, "the run-in, a little quicker");
            Assert.AreEqual(BowlerSnap.Base * (1f + BowlerSnap.Snap), BowlerSnap.SpeedAt(s, s.release), 1e-4f, "quickest at the release");
            float last = BowlerSnap.SpeedAt(s, 0f);
            for (float t = 0.005f; t <= 1f; t += 0.005f)
            {
                float m = BowlerSnap.SpeedAt(s, t);
                Assert.Less(Mathf.Abs(m - last), 0.1f, $"{s.state}: no jump at {t:F3}");
                if (t <= s.release)
                    Assert.GreaterOrEqual(m, last - 1e-5f, $"{s.state}: never slower on the way to the release ({t:F3})");
                last = m;
            }
            Assert.AreEqual(BowlerSnap.Base, BowlerSnap.SpeedAt(s, 0.95f), 1e-4f, "eased back by the follow-through");
        }
    }
}
