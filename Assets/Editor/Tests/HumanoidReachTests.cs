using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The pure maths behind the fielders' and keeper's full-body reach (ReachMath, KeeperFootwork,
/// HumanoidReach.PalmNormal): springs, the stiffness chain, gathering and footwork. No scene.
/// Run: Window > General > Test Runner > EditMode > HumanoidReachTests.
/// </summary>
public class HumanoidReachTests
{
    private const float Frame = 1f / 72f;   // Quest refresh
    private static readonly ReachMath.Body Body = ReachMath.Body.Default;
    private static readonly ReachMath.Limits Limits = ReachMath.Limits.Default;

    // ---- Springs ---------------------------------------------------------------------------------

    [Test]
    public void Spring_SettlesWithoutOvershoot()
    {
        float x = 0f, v = 0f, previous = 0f;
        for (float t = 0f; t < 2f; t += Frame)
        {
            x = ReachMath.Spring(x, ref v, 1f, 10f, Frame);
            Assert.LessOrEqual(x, 1f + 1e-5f, "a critically damped spring never overshoots");
            Assert.GreaterOrEqual(x, previous - 1e-6f, "and never turns back");
            previous = x;
        }
        Assert.AreEqual(1f, x, 1e-3f);
    }

    [Test]
    public void Spring_SettlesInFourOverOmega()
    {
        float x = 0f, v = 0f;
        int frames = Mathf.RoundToInt(0.4f / Frame);
        for (int i = 0; i < frames; i++)
            x = ReachMath.Spring(x, ref v, 1f, 10f, Frame);
        // (1 + wt) e^-wt at wt = 4 is 9%.
        Assert.Less(1f - x, 0.1f);
    }

    [Test]
    public void Spring_IsFrameRateIndependent()
    {
        float a = 0.2f, va = 1f, b = 0.2f, vb = 1f;
        a = ReachMath.Spring(a, ref va, 3f, 12f, 0.1f);
        for (int i = 0; i < 10; i++)
            b = ReachMath.Spring(b, ref vb, 3f, 12f, 0.01f);
        Assert.AreEqual(a, b, 1e-4f);
        Assert.AreEqual(va, vb, 1e-3f);
    }

    [Test]
    public void Spring_VectorMatchesScalar()
    {
        Vector3 x = new Vector3(0f, 1f, -2f), v = Vector3.zero;
        float sx = -2f, sv = 0f;
        for (int i = 0; i < 20; i++)
        {
            x = ReachMath.Spring(x, ref v, Vector3.one, 9f, Frame);
            sx = ReachMath.Spring(sx, ref sv, 1f, 9f, Frame);
        }
        Assert.AreEqual(sx, x.z, 1e-5f);
        Assert.AreEqual(1f, x.y, 1e-5f, "an axis already on target stays there");
    }

    // ---- Stiffness chain -------------------------------------------------------------------------

    [Test]
    public void Chain_KneesSoftestSpineStiffest()
    {
        ReachMath.Pose p = ReachMath.PoseForEffort(0.3f, Limits);
        float knee = p.drop / Limits.maxDrop, hip = p.hinge / Limits.maxHinge, spine = p.spine / Limits.maxSpine;
        Assert.Greater(knee, hip);
        Assert.Greater(hip, spine);
    }

    [Test]
    public void Solve_ChestHighBall_NeedsNoCrouch()
    {
        ReachMath.Pose p = ReachMath.Solve(Body, Limits, new Vector3(0f, 1.2f, 0.4f));
        Assert.AreEqual(0f, p.drop, 1e-5f);
        Assert.AreEqual(0f, p.hinge, 1e-5f);
        Assert.AreEqual(0f, p.spine, 1e-5f);
    }

    [Test]
    public void Solve_LowBall_CrouchesAndHingesWithinLimits()
    {
        var target = new Vector3(0f, 0.1f, 0.45f);
        ReachMath.Pose p = ReachMath.Solve(Body, Limits, target);
        Assert.Greater(p.drop, 0.2f, "the knees take the height");
        Assert.LessOrEqual(p.drop, Limits.maxDrop + 1e-5f);
        Assert.LessOrEqual(p.hinge, Limits.maxHinge + 1e-4f);
        Assert.LessOrEqual(p.spine, 25f + 1e-4f, "the back never folds past 25 degrees");
        Assert.GreaterOrEqual(p.drop / Limits.maxDrop, p.hinge / Limits.maxHinge);
        Assert.GreaterOrEqual(p.hinge / Limits.maxHinge, p.spine / Limits.maxSpine);
        Assert.IsTrue(ReachMath.InReach(Body, p, target.z, target.y), "the hands get there");
    }

    [Test]
    public void Solve_UsesLeastEffort()
    {
        var target = new Vector3(0f, 0.3f, 0.5f);
        ReachMath.Pose p = ReachMath.Solve(Body, Limits, target);
        // A little less of every joint and the ball is out of reach.
        ReachMath.Pose less = ReachMath.Pose.Lerp(default, p, 0.97f);
        Assert.IsFalse(ReachMath.InReach(Body, less, target.z, target.y));
    }

    [Test]
    public void Solve_LowerTargets_NeverCrouchLess()
    {
        float previous = -1f;
        for (float y = 1.2f; y >= 0.05f; y -= 0.05f)
        {
            ReachMath.Pose p = ReachMath.Solve(Body, Limits, new Vector3(0f, y, 0.45f));
            Assert.GreaterOrEqual(p.drop, previous - 1e-5f, $"at height {y}");
            previous = p.drop;
        }
    }

    [Test]
    public void Solve_OutOfReach_SaturatesCleanly()
    {
        ReachMath.Pose p = ReachMath.Solve(Body, Limits, new Vector3(0f, -1f, 3f));
        Assert.IsFalse(float.IsNaN(p.drop) || float.IsNaN(p.hinge) || float.IsNaN(p.spine));
        Assert.AreEqual(Limits.maxDrop, p.drop, 1e-5f);
        Assert.AreEqual(Limits.maxHinge, p.hinge, 1e-4f);
        Assert.AreEqual(Limits.maxSpine, p.spine, 1e-4f);
    }

    [Test]
    public void Solve_WideBall_ShiftsTurnsAndLeansThatWay()
    {
        ReachMath.Pose right = ReachMath.Solve(Body, Limits, new Vector3(0.9f, 0.3f, 0.4f));
        Assert.Greater(right.shift, 0f);
        Assert.LessOrEqual(right.shift, Limits.maxShift + 1e-5f);
        Assert.Greater(right.twist, 0f);
        Assert.LessOrEqual(right.twist, Limits.maxTwist + 1e-4f);
        Assert.GreaterOrEqual(right.roll, 0f);
        ReachMath.Pose left = ReachMath.Solve(Body, Limits, new Vector3(-0.9f, 0.3f, 0.4f));
        Assert.AreEqual(-right.shift, left.shift, 1e-5f, "mirror image");
        Assert.AreEqual(-right.twist, left.twist, 1e-4f);
    }

    // ---- Gathering -------------------------------------------------------------------------------

    [Test]
    public void SegmentDistance_CatchesAFastBallBetweenFrames()
    {
        // 30 m/s at 72 Hz is 0.42 m a frame: both ends are far from the palm, the path is not.
        Vector3 palm = new Vector3(0f, 0.5f, 0.1f);
        Vector3 a = new Vector3(-0.21f, 0.5f, 0f), b = new Vector3(0.21f, 0.5f, 0f);
        Assert.Greater(Vector3.Distance(a, palm), AnimatedFielder.CatchPocket);
        Assert.Greater(Vector3.Distance(b, palm), AnimatedFielder.CatchPocket);
        Assert.AreEqual(0.1f, ReachMath.SegmentDistance(a, b, palm), 1e-5f);
        Assert.AreEqual(0.1f, ReachMath.SegmentDistance(palm + Vector3.back * 0.1f, palm + Vector3.back * 0.1f, palm), 1e-5f, "a stopped ball");
    }

    [Test]
    public void Residual_FadesToNothingOverTheSettleTime()
    {
        Vector3 offset = new Vector3(0.05f, -0.02f, 0.04f);
        Assert.AreEqual(offset, ReachMath.Residual(offset, 0f, 0.1f));
        Assert.Less(ReachMath.Residual(offset, 0.05f, 0.1f).magnitude, offset.magnitude);
        Assert.AreEqual(Vector3.zero, ReachMath.Residual(offset, 0.1f, 0.1f));
        Assert.AreEqual(Vector3.zero, ReachMath.Residual(offset, 5f, 0.1f));
    }

    [Test]
    public void PalmNormal_PalmDownHandsFaceDown()
    {
        // Palm down, fingers forward: the right thumb (index side) is to the left, the left's to the right.
        Vector3 right = HumanoidReach.PalmNormal(Vector3.forward, Vector3.left, false);
        Vector3 left = HumanoidReach.PalmNormal(Vector3.forward, Vector3.right, true);
        Assert.AreEqual(-1f, right.y, 1e-5f);
        Assert.AreEqual(-1f, left.y, 1e-5f);
    }

    // ---- Keeper footwork -------------------------------------------------------------------------

    [Test]
    public void Shuffle_CapsSpeedAndStopsOnTheLine()
    {
        float x = 0f, v = 0f;
        for (int i = 0; i < 200; i++)
        {
            x = ReachMath.Shuffle(x, ref v, 1.5f, 3f, 20f, Frame);
            Assert.LessOrEqual(Mathf.Abs(v), 3f + 1e-4f);
            Assert.LessOrEqual(x, 1.5f + 1e-5f, "never past the line");
        }
        Assert.AreEqual(1.5f, x, 1e-3f);
    }

    [Test]
    public void StepArc_LiftsAndPlants()
    {
        Assert.AreEqual(0f, ReachMath.StepArc(0f, 0.07f), 1e-6f);
        Assert.AreEqual(0.07f, ReachMath.StepArc(0.5f, 0.07f), 1e-6f);
        Assert.AreEqual(0f, ReachMath.StepArc(1f, 0.07f), 1e-6f);
    }

    [Test]
    public void Footwork_StepsNeverSlide()
    {
        var feet = new KeeperFootwork();
        feet.Plant(Vector3.zero, Vector3.right);
        float x = 0f, v = 0f;
        Vector3 prevL = feet.Left, prevR = feet.Right;
        int steps = 0;
        for (int i = 0; i < 150; i++)
        {
            float before = x;
            x = ReachMath.Shuffle(x, ref v, 1.2f, 3f, 20f, Frame);
            feet.Tick(new Vector3(x, 0f, 0f), Vector3.right, new Vector3((x - before) / Frame, 0f, 0f), Frame);
            AssertNoSlide(prevL, feet.Left, feet.AnkleHeight);
            AssertNoSlide(prevR, feet.Right, feet.AnkleHeight);
            if (feet.Left.y > feet.AnkleHeight + 1e-5f && feet.Right.y > feet.AnkleHeight + 1e-5f)
                Assert.Fail("a shuffle keeps one foot on the ground");
            if (prevL.y <= feet.AnkleHeight + 1e-5f && feet.Left.y > feet.AnkleHeight + 1e-5f) steps++;
            prevL = feet.Left;
            prevR = feet.Right;
        }
        Assert.Greater(steps, 0, "the feet stepped");
        Assert.AreEqual(1.2f - feet.StanceHalfWidth, feet.Left.x, 0.15f);
        Assert.AreEqual(1.2f + feet.StanceHalfWidth, feet.Right.x, 0.15f);
    }

    /// A foot on the ground in two consecutive frames must not have moved.
    private static void AssertNoSlide(Vector3 before, Vector3 after, float ankle)
    {
        bool grounded = before.y <= ankle + 1e-5f && after.y <= ankle + 1e-5f;
        if (!grounded)
            return;
        // The frame a step lands the foot arrives from the air, so both frames grounded means planted.
        Assert.Less(new Vector2(after.x - before.x, after.z - before.z).magnitude, 1e-5f, "a planted foot slid");
    }
}
