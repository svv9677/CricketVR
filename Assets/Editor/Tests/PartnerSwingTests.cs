using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The partner's procedural swing (PartnerSwing), standalone: every shot family for both hands,
/// against real deliveries (BallDelivery.Solve, met with PartnerShotPlanner.Meet), with the game's
/// bat dimensions (PartnerTestWorld.Grip). What a believable swing has to do: start from a proper
/// stance, meet the ball with the middle of the bat and the face where the ball is going, keep both
/// hands on the handle and within reach, never jump, and keep the bat away from a ball he leaves.
/// Run: Window > General > Test Runner > EditMode > PartnerSwingTests.
/// </summary>
public class PartnerSwingTests
{
    private const float Frame = 1f / 72f;
    private readonly List<BallFlight.Sample> scratch = new List<BallFlight.Sample>(1024);

    private struct Case
    {
        public string name;
        public ShotPlan plan;
        public float o;
        public IncomingBall ball;
    }

    private ShotPlan Make(PartnerShot shot, PartnerFoot foot, IncomingBall b, float angle, float elevation, float speed, float o)
    {
        float x = foot == PartnerFoot.Front ? PartnerShotPlanner.FrontContactX : PartnerShotPlanner.BackContactX;
        BallAtBat at = PartnerShotPlanner.Meet(b, x, scratch);
        Assert.IsTrue(at.found, shot.ToString());
        bool contact = shot != PartnerShot.Leave && shot != PartnerShot.PlayAndMiss;
        Vector3 exit = shot == PartnerShot.Edge ? at.velocity * 0.7f : PartnerShotPlanner.ExitFor(angle, elevation, speed, o);
        Vector3 meant = shot == PartnerShot.Edge || shot == PartnerShot.PlayAndMiss ? PartnerShotPlanner.IntendedDrive(o) : exit;
        return new ShotPlan
        {
            shot = shot, foot = foot, makesContact = contact, contactX = x, contactTime = at.time,
            contactPoint = at.point, incoming = at.velocity, exitVelocity = contact ? exit : Vector3.zero,
            batVelocity = shot == PartnerShot.Leave ? Vector3.zero : PartnerShotPlanner.BatVelocityFor(at.velocity, meant),
            edge = shot == PartnerShot.Edge,
        };
    }

    private List<Case> Cases()
    {
        var list = new List<Case>();
        var turn = new BallFlight.DeliveryEffects { turnDegrees = 4f };
        foreach (float o in new[] { 1f, -1f })
        {
            void Add(string n, PartnerShot s, PartnerFoot f, IncomingBall b, float a, float e, float v) =>
                list.Add(new Case { name = $"{n} ({(o > 0 ? "RH" : "LH")})", plan = Make(s, f, b, a, e, v, o), o = o, ball = b });
            Add("drive", PartnerShot.Drive, PartnerFoot.Front, PartnerTestWorld.Delivery(3f, 36f, 0.1f * o, default), 15f, -2f, 30f);
            Add("yorker dug out", PartnerShot.Drive, PartnerFoot.Front, PartnerTestWorld.Delivery(1.3f, 36f, 0.05f * o, default), 5f, -6f, 18f);
            Add("cover drive", PartnerShot.Drive, PartnerFoot.Front, PartnerTestWorld.Delivery(3.5f, 36f, 0.35f * o, default), 50f, -2f, 32f);
            Add("defend", PartnerShot.Defend, PartnerFoot.Front, PartnerTestWorld.Delivery(4f, 36f, 0.05f * o, default), 0f, -4f, 10f);
            Add("loft", PartnerShot.Loft, PartnerFoot.Front, PartnerTestWorld.Delivery(3f, 34f, 0f, default), -10f, 30f, 42f);
            Add("cut", PartnerShot.Cut, PartnerFoot.Back, PartnerTestWorld.Delivery(8.5f, 36f, 0.5f * o, default), 95f, -2f, 30f);
            Add("pull", PartnerShot.Pull, PartnerFoot.Back, PartnerTestWorld.Delivery(8.5f, 36f, 0f, default), -70f, 2f, 32f);
            Add("sweep", PartnerShot.Sweep, PartnerFoot.Front, PartnerTestWorld.Delivery(3.5f, 22f, 0f, turn), -80f, 0f, 22f);
            Add("glance", PartnerShot.Glance, PartnerFoot.Front, PartnerTestWorld.Delivery(4f, 36f, -0.15f * o, default), -140f, -2f, 22f);
            Add("edge", PartnerShot.Edge, PartnerFoot.Front, PartnerTestWorld.Delivery(5f, 36f, 0.3f * o, default), 0f, 0f, 0f);
            Add("play and miss", PartnerShot.PlayAndMiss, PartnerFoot.Front, PartnerTestWorld.Delivery(5f, 36f, 0.35f * o, default), 0f, 0f, 0f);
            Add("leave", PartnerShot.Leave, PartnerFoot.Front, PartnerTestWorld.Delivery(5.5f, 36f, 0.6f * o, default), 0f, 0f, 0f);
            Add("duck", PartnerShot.Leave, PartnerFoot.Back, PartnerTestWorld.Delivery(10.5f, 38f, 0f, default), 0f, 0f, 0f);
        }
        return list;
    }

    private static SwingPose Pose(in Case c, float t) =>
        PartnerSwing.Pose(c.plan, PartnerTestWorld.Grip, c.o, PartnerTestWorld.StanceHips, t);

    private static Vector3 Local(in SwingPose s, Vector3 local) => s.batPosition + s.batRotation * (local * PartnerTestWorld.Grip.scale);
    private static Vector3 Sweet(in SwingPose s) => Local(s, PartnerTestWorld.Grip.sweetLocal);
    private static Vector3 Toe(in SwingPose s) => Local(s, PartnerTestWorld.Grip.toeLocal);
    private static Vector3 Front(in SwingPose s, float o) => o > 0f ? s.leftFoot : s.rightFoot;
    private static Vector3 Back(in SwingPose s, float o) => o > 0f ? s.rightFoot : s.leftFoot;
    private static bool Contact(PartnerShot s) => s != PartnerShot.Leave && s != PartnerShot.PlayAndMiss && s != PartnerShot.Edge;

    [Test]
    public void TheMiddleOfTheBatMeetsTheBallWithTheFaceAlongTheShot()
    {
        foreach (Case c in Cases())
        {
            if (!Contact(c.plan.shot)) continue;
            SwingPose s = Pose(c, 0f);
            Assert.Less((Sweet(s) - c.plan.contactPoint).magnitude, 0.01f, c.name);
            Assert.Less(Vector3.Angle(s.batRotation * Vector3.up, c.plan.exitVelocity), 1f, $"{c.name}: face");
        }
    }

    [Test]
    public void TheSweetSpotMovesAlongTheBatVelocityAtContact()
    {
        foreach (Case c in Cases())
        {
            if (c.plan.shot == PartnerShot.Leave) continue;
            Vector3 v = (Sweet(Pose(c, 0f)) - Sweet(Pose(c, -0.002f))) / 0.002f;
            Assert.Less(Vector3.Angle(v, c.plan.batVelocity), 25f, c.name);
        }
    }

    [Test]
    public void BothHandsStayOnTheHandleAndWithinReach()
    {
        BatGrip g = PartnerTestWorld.Grip;
        float apart = (g.topHandLocal - g.bottomHandLocal).magnitude * g.scale;
        foreach (Case c in Cases())
            for (float t = -0.6f; t <= 0.5f; t += 1f / 240f)
            {
                SwingPose s = Pose(c, t);
                Vector3 sh = PartnerSwing.ShoulderCentre(s);
                Assert.LessOrEqual((s.topHand - sh).magnitude, 0.75f, $"{c.name} t={t:F3}: top hand");
                Assert.LessOrEqual((s.bottomHand - sh).magnitude, 0.75f, $"{c.name} t={t:F3}: bottom hand");
                Assert.AreEqual(apart, (s.topHand - s.bottomHand).magnitude, 1e-4f, "on the handle");
                Assert.Less((Local(s, g.topHandLocal) - s.topHand).magnitude, 1e-4f, "top hand at its grip point");
            }
    }

    [Test]
    public void TheBatNeverMovesFasterThanASwingCan()
    {
        foreach (Case c in Cases())
        {
            Vector3 last = Sweet(Pose(c, -1f));
            for (float t = -1f + Frame; t <= 1f; t += Frame)
            {
                Vector3 now = Sweet(Pose(c, t));
                Assert.Less((now - last).magnitude / Frame, 45f, $"{c.name} t={t:F3}");
                last = now;
            }
        }
    }

    [Test]
    public void NothingJumps()
    {
        const float dt = 0.0005f;
        foreach (Case c in Cases())
        {
            SwingPose a = Pose(c, -1.2f);
            for (float t = -1.2f + dt; t <= 1f; t += dt)
            {
                SwingPose b = Pose(c, t);
                string at = $"{c.name} t={t:F4}";
                Assert.Less((Toe(b) - Toe(a)).magnitude, 60f * dt, $"{at}: toe");
                Assert.Less((b.topHand - a.topHand).magnitude, 30f * dt, $"{at}: hands");
                Assert.Less(Quaternion.Angle(a.batRotation, b.batRotation), 4000f * dt, $"{at}: bat turn");
                Assert.Less((b.hips - a.hips).magnitude, 8f * dt, $"{at}: hips");
                Assert.Less((b.head - a.head).magnitude, 8f * dt, $"{at}: head");
                Assert.Less((b.leftFoot - a.leftFoot).magnitude + (b.rightFoot - a.rightFoot).magnitude, 10f * dt, $"{at}: feet");
                Assert.Less(Mathf.Abs(b.hipsYaw - a.hipsYaw), 1500f * dt, $"{at}: hips turn");
                a = b;
            }
        }
    }

    [Test]
    public void TheBatStaysOutOfTheGround()
    {
        foreach (Case c in Cases())
            for (float t = -1f; t <= 1f; t += 1f / 240f)
                Assert.GreaterOrEqual(Toe(Pose(c, t)).y, PartnerSwing.ToeY - 1e-4f, $"{c.name} t={t:F3}");
    }

    [Test]
    public void HeTakesAProperStance()
    {
        foreach (Case c in Cases())
        {
            SwingPose s = Pose(c, -3f);
            Vector3 toe = Toe(s), back = Back(s, c.o), front = Front(s, c.o);
            Assert.AreEqual(PartnerSwing.ToeY, toe.y, 0.005f, $"{c.name}: bat grounded");
            Assert.Greater(toe.x, back.x, $"{c.name}: behind the back foot");
            Assert.Less(front.x, back.x, $"{c.name}: front foot toward the bowler");
            Assert.AreEqual(PartnerSwing.FootY, front.y, 1e-4f);
            Assert.AreEqual(PartnerSwing.FootY, back.y, 1e-4f);
            Assert.AreEqual(c.o > 0f ? 0f : 180f, s.hipsYaw, 1e-3f, $"{c.name}: side-on");
            Assert.AreEqual(c.o * BallLedgerMath.StumpHalfWidth, s.head.z, 0.01f, $"{c.name}: head over off stump");
            Assert.AreEqual(s.head.y, s.look.y, 0.01f, $"{c.name}: eyes level");
            Assert.Less(s.look.x, s.head.x - 10f, $"{c.name}: watching the bowler");
            Assert.Less(s.hips.y, PartnerTestWorld.StanceHips.y + 1e-4f, "knees bent");
            Vector3 face = s.batRotation * Vector3.up;
            Assert.Less(face.x, -0.8f, $"{c.name}: face to the bowler");
        }
    }

    [Test]
    public void TheBackliftStartsOnTime()
    {
        foreach (Case c in Cases())
        {
            float start = PartnerSwing.BackliftStart(c.plan);
            float d = PartnerSwing.DownswingTime(c.plan.shot);
            Assert.GreaterOrEqual(d, 0.16f);
            Assert.LessOrEqual(d, 0.24f);
            Assert.LessOrEqual(start, -(d + 0.12f) + 1e-5f, c.name);
            Assert.GreaterOrEqual(start, -Mathf.Max(c.plan.contactTime, d + 0.12f) - 1e-5f, $"{c.name}: not before release");
            SwingPose before = Pose(c, start - 0.01f), stance = Pose(c, -3f), after = Pose(c, start + 0.08f);
            Assert.Less((Toe(before) - Toe(stance)).magnitude, 1e-4f, $"{c.name}: still in his stance");
            Assert.Greater((Toe(after) - Toe(stance)).magnitude, 0.02f, $"{c.name}: picking it up");
        }
    }

    [Test]
    public void FrontFootForTheDrivesBackFootForTheCutAndPull()
    {
        foreach (Case c in Cases())
        {
            SwingPose stance = Pose(c, -3f), hit = Pose(c, 0f);
            PartnerShot s = c.plan.shot;
            if (s == PartnerShot.Drive || s == PartnerShot.Defend || s == PartnerShot.Sweep || s == PartnerShot.Loft)
            {
                Assert.Less(Front(hit, c.o).x, Front(stance, c.o).x - 0.1f, $"{c.name}: strides forward");
                Assert.Less((Back(hit, c.o) - Back(stance, c.o)).magnitude, 1e-4f, $"{c.name}: back foot planted");
                Assert.Greater(Front(hit, c.o).x, c.plan.contactX, $"{c.name}: just behind the ball");
            }
            if (s == PartnerShot.Cut || s == PartnerShot.Pull)
            {
                Assert.Greater(Back(hit, c.o).x, Back(stance, c.o).x + 0.05f, $"{c.name}: back foot goes back");
                Assert.GreaterOrEqual(Front(hit, c.o).x, Front(stance, c.o).x, $"{c.name}: front foot not forward");
                Assert.Less(Back(hit, c.o).x, BallDelivery.BatsmanStumpsX - 0.25f, $"{c.name}: off his stumps");
            }
            if (s == PartnerShot.Sweep)
                Assert.Less(hit.hips.y, stance.hips.y - 0.3f, "down on one knee");
        }
    }

    [Test]
    public void HeFollowsThroughAndHoldsIt()
    {
        foreach (Case c in Cases())
        {
            if (!Contact(c.plan.shot)) continue;
            Vector3 at = Sweet(Pose(c, 0f)), later = Sweet(Pose(c, 0.15f));
            Vector3 held = Sweet(Pose(c, PartnerSwing.FollowThroughTime)), after = Sweet(Pose(c, 1f));
            if (c.plan.shot == PartnerShot.Defend)
                Assert.Less((later - at).magnitude, 0.35f, $"{c.name}: checked");
            else
                Assert.Greater((later - at).magnitude, 0.2f, $"{c.name}: carries on through");
            Assert.Less((after - held).magnitude, 0.01f, $"{c.name}: holds the finish");
        }
    }

    [Test]
    public void ALeaveKeepsTheBatAwayFromTheBall()
    {
        foreach (Case c in Cases())
        {
            if (c.plan.shot != PartnerShot.Leave && c.plan.shot != PartnerShot.PlayAndMiss) continue;
            BallFlight.Simulate(c.ball.release, c.ball.velocity, c.ball.effects, c.plan.contactTime + 0.4f, scratch, null, 0.01f);
            float clearance = c.plan.shot == PartnerShot.Leave ? 0.3f : BallFlight.Radius + 0.04f;
            foreach (BallFlight.Sample b in scratch)
            {
                float t = b.time - c.plan.contactTime;
                if (t < -0.3f || t > 0.3f) continue;
                SwingPose s = Pose(c, t);
                float d = PartnerField.SegmentDistance(Toe(s), s.topHand, b.position);
                Assert.Greater(d, clearance, $"{c.name} t={t:F2}");
            }
        }
    }

    [Test]
    public void APlayAndMissIsInsideTheLineAndAnEdgeOnIt()
    {
        foreach (Case c in Cases())
        {
            if (c.plan.shot != PartnerShot.PlayAndMiss && c.plan.shot != PartnerShot.Edge) continue;
            SwingPose s = Pose(c, 0f);
            float apart = (Sweet(s) - c.plan.contactPoint).magnitude;
            if (c.plan.shot == PartnerShot.PlayAndMiss)
                Assert.AreEqual(PartnerSwing.MissInside, apart, 0.01f, c.name);
            else
                Assert.AreEqual(PartnerSwing.EdgeInside, apart, 0.01f, c.name);
            Assert.Less(Mathf.Abs(Sweet(s).z - PartnerTestWorld.StanceHips.z), Mathf.Abs(c.plan.contactPoint.z - PartnerTestWorld.StanceHips.z) + 1e-3f,
                $"{c.name}: on the body's side of the ball");
        }
    }
}
