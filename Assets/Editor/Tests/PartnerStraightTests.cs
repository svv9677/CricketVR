using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The partner bats like an orthodox, mostly-straight batter (2026-11-05, "the nonstriker shots
/// look weird, make him play more straight shots"): runs and dots go down the ground in the V
/// (PartnerShotPlanner.StraightArc either side of straight), square shots only when nothing
/// straight lands the intent, sweeps and glances rarely, and a straight shot never at the career
/// player's head at the other end. Same world as PartnerPlannerTests (PartnerTestWorld), every
/// flight stepped at 0.01 s. Run: Window > General > Test Runner > EditMode > PartnerStraightTests.
/// </summary>
public class PartnerStraightTests
{
    private const float Dt = 0.01f;
    private readonly List<BallFlight.Sample> scratch = new List<BallFlight.Sample>(1024);
    private float savedMeetStep;

    [SetUp]
    public void PinMeetStep()
    {
        savedMeetStep = PartnerShotPlanner.MeetStep;
        PartnerShotPlanner.MeetStep = Dt;
    }

    [TearDown]
    public void RestoreMeetStep() => PartnerShotPlanner.MeetStep = savedMeetStep;

    private static float Angle(Vector3 exit, in FieldSnapshot f) => PartnerShotPlanner.AngleOf(exit, f.offSign);

    /// 360 seeded balls across every field (both hands), every delivery and every runs intent,
    /// planned and played from the real contact. Measured 2026-11-05 (seed 101): searching the
    /// whole field as before, 77 of 360 shots were within 40 degrees of straight, 130 were glances
    /// and 10 sweeps; searching the V first, 307 straight, 21 glances, 2 sweeps, 2 cuts and pulls
    /// (seeds 7, 202 and 9001: 306-310 straight, 22-24 glances, 2-3 sweeps). The glances are
    /// twos: on most fields a two is only to be had fine, and the intent comes before the look.
    /// Singles, fours and sixes, which the V nearly always gives, are 94% straight. Measured on a
    /// managed stand-in for UnityEngine's maths outside the editor; the editor run logs its own.
    [Test]
    public void MostRunsShotsAreStraightAndSweepsAreRare()
    {
        var rng = new System.Random(101);
        List<FieldSnapshot> fields = PartnerTestWorld.Fields();
        List<IncomingBall> balls = PartnerTestWorld.Deliveries(out _);
        int[] intents = { 1, 2, 3, 4, 6 };
        int played = 0, straight = 0, sweeps = 0, glances = 0, square = 0, usual = 0, usualStraight = 0;
        var shots = new Dictionary<PartnerShot, int>();
        for (int i = 0; i < 360; i++)
        {
            FieldSnapshot f = fields[i % fields.Count];
            int intent = intents[(i / fields.Count) % intents.Length];
            IncomingBall b = balls[(i * 7 + i / 5) % balls.Count];
            ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
            ShotPlan p = PartnerShotPlanner.Plan(d, b, f, f.offSign, rng, scratch);
            if (!p.makesContact) continue;
            played++;
            bool inTheV = Mathf.Abs(Angle(p.exitVelocity, f)) <= PartnerShotPlanner.StraightArc;
            straight += inTheV ? 1 : 0;
            if (intent == 1 || intent == 4 || intent == 6) { usual++; usualStraight += inTheV ? 1 : 0; }
            sweeps += p.shot == PartnerShot.Sweep ? 1 : 0;
            glances += p.shot == PartnerShot.Glance ? 1 : 0;
            square += p.shot == PartnerShot.Cut || p.shot == PartnerShot.Pull ? 1 : 0;
            shots.TryGetValue(p.shot, out int n);
            shots[p.shot] = n + 1;
            Assert.GreaterOrEqual(p.predicted.closestToHuman, PartnerShotPlanner.HumanClearance, $"clear of the player's head: {p.shot}");
        }
        var line = new System.Text.StringBuilder();
        foreach (KeyValuePair<PartnerShot, int> kv in shots) line.Append($" {kv.Key} {kv.Value}");
        Debug.Log($"[PartnerStraightTests] {straight}/{played} in the V ({usualStraight}/{usual} of the 1s, 4s and 6s); " +
                  $"sweeps {sweeps}, glances {glances}, cuts and pulls {square};{line}");
        Assert.Greater(played, 300);
        Assert.GreaterOrEqual(straight / (float)played, 0.75f, "within StraightArc of straight");
        Assert.GreaterOrEqual(usualStraight / (float)usual, 0.85f, "singles, fours and sixes within StraightArc");
        Assert.LessOrEqual(sweeps / (float)played, 0.02f, "sweeps");
        Assert.LessOrEqual(glances / (float)played, 0.10f, "glances");
        // 23 with the pull starting at -30 (a back-foot push to mid-on played as a pull), 2 now.
        Assert.LessOrEqual(square / (float)played, 0.05f, "cuts and pulls");
    }

    /// A dot is a push straight back at the bowler or at mid-off or mid-on (measured 2026-11-05:
    /// on all 18 fields), and a ball at the stumps is defended that way.
    [Test]
    public void DotsArePushedBackDownTheGround()
    {
        var rng = new System.Random(103);
        IncomingBall atStumps = PartnerTestWorld.Delivery(5.5f, 35f, 0.02f, default);
        int fields = 0, straight = 0;
        foreach (FieldSnapshot f in PartnerTestWorld.Fields())
        {
            fields++;
            ShotDirection d = PartnerShotPlanner.PlanDirection(0, f, PartnerTestWorld.NominalContact, rng);
            straight += Mathf.Abs(Angle(d.exitVelocity, f)) <= PartnerShotPlanner.StraightArc ? 1 : 0;
            ShotPlan p = PartnerShotPlanner.Plan(d, atStumps, f, f.offSign, rng, scratch);
            Assert.AreEqual(PartnerShot.Defend, p.shot);
        }
        Assert.GreaterOrEqual(straight / (float)fields, 0.9f, "dots in the V");
    }

    [Test]
    public void TheArcsAndTheShotsInThem()
    {
        Assert.AreEqual(0, PartnerShotPlanner.ArcOf(0f));
        Assert.AreEqual(0, PartnerShotPlanner.ArcOf(40f), "mid-off");
        Assert.AreEqual(0, PartnerShotPlanner.ArcOf(-40f), "mid-on");
        Assert.AreEqual(1, PartnerShotPlanner.ArcOf(41f));
        Assert.AreEqual(1, PartnerShotPlanner.ArcOf(120f), "point, the cut");
        Assert.AreEqual(2, PartnerShotPlanner.ArcOf(121f), "third man");
        Assert.AreEqual(1, PartnerShotPlanner.ArcOf(-60f), "midwicket");
        Assert.AreEqual(2, PartnerShotPlanner.ArcOf(-61f), "square leg and fine: the sweep and the glance");
        Vector3 toMidOn = PartnerShotPlanner.ExitFor(-35f, 0f, 20f, 1f);
        Assert.AreEqual(PartnerShot.Drive, PartnerShotPlanner.Family(PartnerFoot.Back, 0.9f, toMidOn, 1f, false), "a punch to mid-on, not a pull");
        Assert.AreEqual(PartnerShot.Drive, PartnerShotPlanner.Family(PartnerFoot.Front, 0.5f, toMidOn, 1f, true));
        Assert.AreEqual(PartnerShot.Pull, PartnerShotPlanner.Family(PartnerFoot.Back, 0.9f, PartnerShotPlanner.ExitFor(-50f, 0f, 20f, 1f), 1f, false));
        Assert.AreEqual(PartnerShot.Cut, PartnerShotPlanner.Family(PartnerFoot.Back, 0.9f, PartnerShotPlanner.ExitFor(80f, 0f, 20f, 1f), 1f, false));
    }

    /// Nobody in front of square but the bowler: the four and the six go straight down the ground.
    [Test]
    public void WithTheVOpenHeHitsStraight()
    {
        var rng = new System.Random(107);
        foreach (float o in new[] { 1f, -1f })
        {
            FieldSnapshot f = Field(new[] { 60f, 90f, 120f, 150f, -60f, -90f, -120f, -150f }, o, true);
            foreach (int intent in new[] { 4, 6 })
            {
                ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
                Assert.IsTrue(d.valid, $"intent {intent}, hand {o}");
                Assert.AreEqual(0, PartnerShotPlanner.ArcOf(Angle(d.exitVelocity, f)), $"intent {intent}, hand {o}: {Angle(d.exitVelocity, f)}");
            }
        }
    }

    /// The V packed, ring and deep, and the square field empty: straight cannot give a four, so
    /// the search widens and still finds one - the intent before the look.
    [Test]
    public void WithTheVShutHeGoesSquareForHisFour()
    {
        var rng = new System.Random(109);
        var shut = new List<float>();
        for (float a = -48f; a <= 48f; a += 8f) shut.Add(a);
        foreach (float o in new[] { 1f, -1f })
        {
            FieldSnapshot f = Field(shut.ToArray(), o, true);
            ShotDirection d = PartnerShotPlanner.PlanDirection(4, f, PartnerTestWorld.NominalContact, rng);
            Assert.IsTrue(d.valid, $"hand {o}: a four found");
            Assert.AreEqual(4, d.predicted.runs);
            Assert.Greater(Mathf.Abs(Angle(d.exitVelocity, f)), PartnerShotPlanner.StraightArc, $"hand {o}: square");
        }
    }

    /// The only gap in the V is the one the career player stands in. A drive lofted through it
    /// would pass within HumanClearance of his head (the test checks it would); the plan still
    /// goes straight, and past him.
    [Test]
    public void AStraightShotAtThePlayersEndGoesPastHim()
    {
        var rng = new System.Random(113);
        Vector3 c = PartnerTestWorld.NominalContact;
        foreach (float o in new[] { 1f, -1f })
        {
            float head = PartnerShotPlanner.AngleOf(PartnerTestWorld.NonStrikerHead - c, o);
            var around = new List<float>();
            for (float a = -150f; a <= 150f; a += 15f)
                if (Mathf.Abs(a - head) >= 15f) around.Add(a);
            FieldSnapshot f = Field(around.ToArray(), o, false);
            float nearest = float.MaxValue;
            for (float e = 0f; e <= 14f; e += 0.5f)
                nearest = Mathf.Min(nearest, PartnerField.Evaluate(c, PartnerShotPlanner.ExitFor(head, e, 30f, o), f, scratch).closestToHuman);
            Assert.Less(nearest, PartnerShotPlanner.HumanClearance, $"hand {o}: the test aims through him");
            foreach (int intent in new[] { 1, 4, 6 })
            {
                ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, c, rng);
                Assert.GreaterOrEqual(d.predicted.closestToHuman, PartnerShotPlanner.HumanClearance, $"hand {o}, intent {intent}: clear of him");
                if (intent == 1) continue;
                Assert.IsTrue(d.valid, $"hand {o}, intent {intent}");
                Assert.AreEqual(0, PartnerShotPlanner.ArcOf(Angle(d.exitVelocity, f)), $"hand {o}, intent {intent}: still straight");
            }
        }
    }

    /// A ring (25 m) and a deep (48 m) fielder at each FieldSets angle, the bowler following
    /// through and the keeper (last), at 7.5 m/s, with the career player at the other end.
    private static FieldSnapshot Field(float[] angles, float offSign, bool bowler)
    {
        Vector3 c = PartnerTestWorld.NominalContact;
        var places = new List<Vector3>();
        foreach (float a in angles)
            foreach (float d in new[] { 25f, 48f })
            {
                Vector3 dir = PartnerShotPlanner.ExitFor(a, 0f, 1f, offSign);
                places.Add(new Vector3(c.x + dir.x * d, 0f, c.z + dir.z * d));
            }
        if (bowler) places.Add(new Vector3(BallDelivery.BowlerStumpsX + 3.5f, 0f, 0.8f));
        places.Add(new Vector3(FieldSets.KeeperX(eSwingType.Pace), 0f, 0f));
        var speeds = new float[places.Count];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = AnimatedFielder.BaseRunSpeed * 1.5f;
        return new FieldSnapshot
        {
            fielders = places.ToArray(), runSpeed = speeds, keeperIndex = places.Count - 1, boundary = PartnerTestWorld.Boundary,
            offSign = offSign, delay = AnimatedFielderManagement.SeeingDelay + AnimatedFielderManagement.ReactionTime,
            nonStrikerHead = PartnerTestWorld.NonStrikerHead,
        };
    }
}
