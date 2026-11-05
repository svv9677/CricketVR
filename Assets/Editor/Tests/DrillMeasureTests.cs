using NUnit.Framework;
using UnityEngine;

/// <summary>
/// What the ledger measures for the drills: the shot's direction (BallLedgerMath.ShotAngle, in
/// FieldSets' convention and mirrored for a left-hander), the head's step down the pitch
/// (HeadStep) and HeadWatch's footwork and clearance on hand-made paths. Pure, no scene.
/// </summary>
public class DrillMeasureTests
{
    private const float Eps = 1e-3f;

    private static FieldSets.Ground Ground(bool leftHanded) => new FieldSets.Ground
    {
        strikerX = BallDelivery.BatsmanStumpsX, nonStrikerX = BallDelivery.BowlerStumpsX, boundary = 56f,
        keeperX = BallDelivery.BatsmanStumpsX + 15f, offSign = leftHanded ? -1f : 1f,
    };

    // ---- ShotAngle --------------------------------------------------------------------------------

    [Test]
    public void ShotAngleIsFieldSetsConvention()
    {
        Assert.AreEqual(0f, BallLedgerMath.ShotAngle(new Vector3(-20f, 3f, 0f), false), Eps, "straight back past the bowler");
        Assert.AreEqual(90f, BallLedgerMath.ShotAngle(new Vector3(0f, 1f, 20f), false), Eps, "square on the off side (+Z)");
        Assert.AreEqual(-90f, BallLedgerMath.ShotAngle(new Vector3(0f, 1f, -20f), false), Eps, "square on the leg side");
        Assert.AreEqual(180f, Mathf.Abs(BallLedgerMath.ShotAngle(new Vector3(20f, 1f, 0f), false)), Eps, "back to the keeper");
        Assert.AreEqual(45f, BallLedgerMath.ShotAngle(new Vector3(-10f, 0f, 10f), false), Eps, "through extra cover");
    }

    [Test]
    public void ShotAngleIsMirroredForALeftHander()
    {
        Vector3 toPlusZ = new Vector3(-5f, 2f, 12f);
        Assert.AreEqual(-BallLedgerMath.ShotAngle(toPlusZ, false), BallLedgerMath.ShotAngle(toPlusZ, true), Eps);
        Assert.AreEqual(-90f, BallLedgerMath.ShotAngle(new Vector3(0f, 1f, 20f), true), Eps, "+Z is his leg side");
        Assert.AreEqual(0f, BallLedgerMath.ShotAngle(new Vector3(-20f, 0f, 0f), true), Eps);
    }

    [Test]
    public void ShotAngleMatchesFieldSetsAngleOf()
    {
        foreach (bool left in new[] { false, true })
        {
            FieldSets.Ground g = Ground(left);
            Vector3 striker = new Vector3(g.strikerX, 0f, 0f);
            for (int deg = -175; deg <= 175; deg += 5)
            {
                float a = deg * Mathf.Deg2Rad;
                Vector3 v = new Vector3(-Mathf.Cos(a) * 25f, 4f, Mathf.Sin(a) * 25f);
                float shot = BallLedgerMath.ShotAngle(v, left);
                float field = FieldSets.AngleOf(striker + new Vector3(v.x, 0f, v.z), g);
                Assert.AreEqual(field, shot, Eps, $"{deg} left={left}");
            }
        }
    }

    [Test]
    public void ShotAngleFindsAFieldersPlace()
    {
        // A ball hit straight at a fielder's place has that fielder's angle.
        foreach (bool left in new[] { false, true })
        {
            FieldSets.Ground g = Ground(left);
            var cover = new FieldSets.Position("Cover", FieldSets.Kind.Ring, 55f, 25f);
            Vector3 at = FieldSets.Place(cover, g) - new Vector3(g.strikerX, 0f, 0f);
            Assert.AreEqual(55f, BallLedgerMath.ShotAngle(at.normalized * 20f + Vector3.up, left), 0.01f, $"left={left}");
        }
    }

    // ---- HeadStep ---------------------------------------------------------------------------------

    [Test]
    public void HeadStepIsPositiveTowardTheBowler()
    {
        Vector3 release = new Vector3(9.6f, 1.6f, 0.1f);
        Assert.AreEqual(0.5f, BallLedgerMath.HeadStep(release, new Vector3(9.1f, 1.5f, 0.3f)), Eps, "down the pitch (-X)");
        Assert.AreEqual(-0.3f, BallLedgerMath.HeadStep(release, new Vector3(9.9f, 1.6f, 0.1f)), Eps, "back toward the stumps");
        Assert.AreEqual(0f, BallLedgerMath.HeadStep(release, new Vector3(9.6f, 1.2f, -0.4f)), Eps, "a duck or a sway is no step");
    }

    // ---- HeadWatch --------------------------------------------------------------------------------

    private static readonly Vector3 Head = new Vector3(9.6f, 1.6f, 0f);

    [Test]
    public void ANewBallHasSeenNothing()
    {
        var w = new HeadWatch();
        Assert.AreEqual(0f, w.Step);
        Assert.AreEqual(HeadWatch.NeverNear, w.Clearance);
        w.Release(Head);
        w.Sample(new Vector3(9.3f, 1.7f, 0f), new Vector3(9.7f, 1.7f, 0f), Head - new Vector3(0.6f, 0f, 0f));
        Assert.Greater(w.Step, 0f);
        Assert.Less(w.Clearance, 1f);
        w.Begin();
        Assert.AreEqual(0f, w.Step, "Begin forgets the last ball");
        Assert.AreEqual(HeadWatch.NeverNear, w.Clearance);
    }

    [Test]
    public void StepIsTheFurthestTheHeadWentForward()
    {
        var w = new HeadWatch();
        w.Begin();
        w.Release(Head);
        Vector3 far = new Vector3(-5f, 2f, 0f);   // the ball still near the bowler
        w.Sample(far, far, Head - new Vector3(0.2f, 0f, 0f));
        w.Sample(far, far, Head - new Vector3(0.7f, 0f, 0f));
        w.Sample(far, far, Head - new Vector3(0.3f, 0f, 0f));   // and back again
        Assert.AreEqual(0.7f, w.Step, Eps);
        Assert.AreEqual(HeadWatch.NeverNear, w.Clearance, "the ball was never near");
    }

    [Test]
    public void GoingBackIsNoStep()
    {
        var w = new HeadWatch();
        w.Begin();
        w.Release(Head);
        Vector3 far = new Vector3(-5f, 2f, 0f);
        w.Sample(far, far, Head + new Vector3(0.4f, 0f, 0f));
        Assert.AreEqual(0f, w.Step);
    }

    [Test]
    public void NoReleaseNoStep()
    {
        var w = new HeadWatch();
        w.Begin();
        Vector3 far = new Vector3(-5f, 2f, 0f);
        w.Sample(far, far, Head - new Vector3(1f, 0f, 0f));
        Assert.AreEqual(0f, w.Step, "no head at release (no camera): nothing to step from");
    }

    [Test]
    public void ClearanceIsTheNearestPointOfThePathNotTheNearestFrame()
    {
        var w = new HeadWatch();
        w.Begin();
        w.Release(Head);
        // A bouncer passing 0.2 m over the head, frames 0.8 m apart that straddle it: neither end is
        // within 0.4 m, the path's midpoint is 0.2 m away.
        Vector3 over = Head + new Vector3(0f, 0.2f, 0f);
        Vector3 before = over - new Vector3(0.4f, 0f, 0f), after = over + new Vector3(0.4f, 0f, 0f);
        w.Sample(before - new Vector3(0.8f, 0f, 0f), before, Head);
        w.Sample(before, after, Head);
        Assert.AreEqual(0.2f, w.Clearance, Eps);
    }

    [Test]
    public void ClearanceIsOnlyMeasuredNearTheHeadAndBeforeItPasses()
    {
        var w = new HeadWatch();
        w.Begin();
        w.Release(Head);
        // Coming down, still more than Near away.
        w.Sample(Head - new Vector3(6f, 0f, 0f), Head - new Vector3(5f, 0f, 0f), Head);
        Assert.AreEqual(HeadWatch.NeverNear, w.Clearance);
        // Within Near, passing 0.5 m to the side.
        Vector3 side = new Vector3(0f, 0f, 0.5f);
        w.Sample(Head - new Vector3(2f, 0f, 0f) + side, Head - new Vector3(1f, 0f, 0f) + side, Head);
        Assert.AreEqual(Mathf.Sqrt(1.25f), w.Clearance, Eps, "its nearest point, 1 m short of the head");
        float beforePassing = w.Clearance;
        // Already past the head (toward the keeper) and coming back near it: not counted.
        w.Sample(Head + new Vector3(0.6f, 0f, 0.05f), Head + new Vector3(0.7f, 0f, 0.05f), Head);
        Assert.AreEqual(beforePassing, w.Clearance, "a ball past the head is not measured");
    }

    [Test]
    public void ABallThatHitsTheHeadHasNoClearance()
    {
        var w = new HeadWatch();
        w.Begin();
        w.Release(Head);
        w.Sample(Head - new Vector3(0.5f, 0f, 0f), Head + new Vector3(0.1f, 0f, 0f), Head);
        Assert.Less(w.Clearance, 0.15f + Eps, "within DrillScoring.HitHead");
    }
}
