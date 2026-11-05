using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The partner's shot planner (PartnerIntent, PartnerField, PartnerShotPlanner), standalone - no
/// scene, no PhysX. Fields are the bowling plans' own (BowlingPlans) placed on a 56 m ground with
/// FieldSets.Place, plus the keeper and the bowler following through; deliveries come from
/// BallDelivery.Solve. Every flight steps at 0.01 s (the project's fixed timestep) explicitly,
/// Meet's included (PartnerShotPlanner.MeetStep, pinned for each test and put back).
/// Run: Window > General > Test Runner > EditMode > PartnerPlannerTests.
/// </summary>
public class PartnerPlannerTests
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

    [Test]
    public void MeetAgreesWithTheDeliverySolveAtTheStumps()
    {
        List<IncomingBall> balls = PartnerTestWorld.Deliveries(out List<BallDelivery.Solution> solutions);
        for (int i = 0; i < balls.Count; i++)
        {
            BallAtBat at = PartnerShotPlanner.Meet(balls[i], BallDelivery.BatsmanStumpsX, scratch);
            Assert.IsTrue(at.found);
            Assert.AreEqual(solutions[i].lineZ, at.point.z, 0.01f, $"line, ball {i}");
            Assert.AreEqual(solutions[i].heightAtStumps, at.point.y, 0.01f, $"height, ball {i}");
        }
    }

    [Test]
    public void MeetFindsTheBallOnThePlane()
    {
        IncomingBall b = PartnerTestWorld.Delivery(5f, 36f, 0.1f, default);
        BallAtBat at = PartnerShotPlanner.Meet(b, PartnerShotPlanner.FrontContactX, scratch);
        Assert.IsTrue(at.found);
        Assert.AreEqual(PartnerShotPlanner.FrontContactX, at.point.x, 1e-3f);
        BallFlight.Simulate(b.release, b.velocity, b.effects, at.time, scratch, null, Dt);
        Assert.Less((scratch[scratch.Count - 1].position - at.point).magnitude, 0.4f, "where the flight is at that time");
        Assert.Greater(at.velocity.x, 20f);
    }

    [Test]
    public void IntentNeverWideNeverAWicketWhenProtectedAndLeavesAWide()
    {
        var rng = new System.Random(5);
        for (int i = 0; i < 20000; i++)
        {
            int r = PartnerIntent.Draw(45f, eBowlingLevel.Medium, 0.6f, rng, false, i % 2 == 0);
            Assert.AreNotEqual(CareerSim.Wide, r);
            Assert.AreNotEqual(5, r);
            if (i % 2 == 0) Assert.AreNotEqual(CareerSim.Wicket, r, "protected ball");
        }
        Assert.AreEqual(PartnerIntent.LeaveWide, PartnerIntent.Draw(45f, eBowlingLevel.Medium, 0.5f, rng, true, false));
    }

    [Test]
    public void IntentFollowsCareerSimsOddsLessTheWides()
    {
        const int n = 60000;
        var draw = new Dictionary<int, int>();
        var sim = new Dictionary<int, int>();
        var a = new System.Random(11);
        var b = new System.Random(12);
        int simBalls = 0;
        for (int i = 0; i < n; i++)
        {
            int r = PartnerIntent.Draw(60f, eBowlingLevel.Hard, 0.5f, a, false, false);
            draw[r] = draw.TryGetValue(r, out int c) ? c + 1 : 1;
            int s = CareerSim.Ball(60f, eBowlingLevel.Hard, 0.5f, b);
            if (s == CareerSim.Wide) continue;
            simBalls++;
            sim[s] = sim.TryGetValue(s, out int d) ? d + 1 : 1;
        }
        foreach (int code in new[] { CareerSim.Wicket, 0, 1, 2, 3, 4, 6 })
        {
            draw.TryGetValue(code, out int x);
            sim.TryGetValue(code, out int y);
            Assert.AreEqual(y / (float)simBalls, x / (float)n, 0.01f, $"share of {code}");
        }
    }

    [Test]
    public void WideLinesMatchTheWideBoxesAndTheHead()
    {
        var d = new BallDelivery.Solution { lineZ = 0f, heightAtStumps = 0.6f };
        Assert.IsFalse(PartnerIntent.IsWide(d, 0f, 1f, 1.8f), "middle");
        d.lineZ = 0.889f + 0.036f + 0.01f;
        Assert.IsTrue(PartnerIntent.IsWide(d, 0f, 1f, 1.8f), "past the off guideline");
        Assert.IsTrue(PartnerIntent.IsWide(d, 0f, -1f, 1.8f), "+z is a left-hander's leg side: wide there too");
        d.lineZ = 0.889f + 0.036f - 0.01f;
        Assert.IsFalse(PartnerIntent.IsWide(d, 0f, 1f, 1.8f), "touching the guideline");
        Assert.IsFalse(PartnerIntent.IsWide(d, 0.3f, 1f, 1.8f), "he stepped across: the line moved");
        d.lineZ = -(0.1143f + 0.036f + 0.01f);
        Assert.IsTrue(PartnerIntent.IsWide(d, 0f, 1f, 1.8f), "down leg");
        d.lineZ = 0f;
        d.heightAtStumps = 1.8f + 0.036f + 0.01f;
        Assert.IsTrue(PartnerIntent.IsWide(d, 0f, 1f, 1.8f), "over his head");
    }

    [Test]
    public void ReachTimeFollowsTheFieldersRun()
    {
        Vector3 from = Vector3.zero;
        Assert.AreEqual(0.37f, PartnerField.ReachTime(from, new Vector3(1f, 0f, 0f), 7.5f, 0.37f), 1e-5f, "within reach");
        // 20 m: 18.8 m to run at 10 m/s^2 up to 7.5 m/s (0.75 s, 2.8125 m), then flat out.
        float expected = 0.37f + 0.75f + (18.8f - 2.8125f) / 7.5f;
        Assert.AreEqual(expected, PartnerField.ReachTime(from, new Vector3(20f, 5f, 0f), 7.5f, 0.37f), 1e-4f, "height ignored");
        float t = PartnerField.ReachTime(from, new Vector3(12f, 0f, 9f), 6f, 0.3f);
        Assert.AreEqual(15f - AnimatedFielderManagement.CatchRadius, PartnerField.RunDistance(t - 0.3f, 6f), 1e-3f, "RunDistance inverts it");
    }

    [Test]
    public void FieldModelGivesDotsCatchesFoursAndSixes()
    {
        FieldSets.Ground g = PartnerTestWorld.Ground(eSwingType.Pace, 1f);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.OffStump.field, eSwingType.Pace, 1f, false);
        Vector3 c = PartnerTestWorld.NominalContact;
        int cover = PartnerTestWorld.Nearest(f, FieldSets.Place(new FieldSets.Position("Cover", FieldSets.Kind.Ring, 55f), g));
        Vector3 toCover = f.fielders[cover] - c;
        toCover.y = 0f;
        FieldResult push = PartnerField.Evaluate(c, toCover.normalized * 14f + Vector3.down, f, scratch);
        Assert.AreEqual(0, push.runs, "pushed along the ground to cover");
        Assert.AreEqual(cover, push.byFielder);
        Assert.IsFalse(push.caught);
        FieldResult chip = PartnerField.Evaluate(c, PartnerShotPlanner.ExitFor(PartnerShotPlanner.AngleOf(toCover, 1f), 24f, 20f, 1f), f, scratch);
        Assert.IsTrue(chip.caught, "chipped in the air to cover");
        Assert.AreEqual(CareerSim.Wicket, chip.runs);
        FieldResult six = PartnerField.Evaluate(c, PartnerShotPlanner.ExitFor(-5f, 35f, 45f, 1f), f, scratch);
        Assert.AreEqual(6, six.runs, "a big hit straight");
        FieldResult edge = PartnerField.Evaluate(c, new Vector3(22f, 4f, 0f), f, scratch);
        Assert.IsTrue(edge.caught, "an edge carrying to the keeper");
        Assert.AreEqual(f.keeperIndex, edge.byFielder);
    }

    /// Measured 2026-11-05 on the 18 fields (9 plans x both hands) at the default fielder speed
    /// (7.5 m/s): every intent but the twos and threes is there to be had on every field. A two
    /// needs a ball fielded 4.5-7.5 s after the hit: a grid of ~18,500 shots per field (every 4
    /// degrees, 2 m/s, 4 degrees up) found none on 4 of the 18 fields and 6-400 on the rest; a
    /// three (7.5-11 s) none on any (the search, finer, finds one on 2 of 18) - the fielders run
    /// too well. So a two is planned where one exists and a three comes out a run short (or
    /// over), never a wicket; the live result stands either way.
    [Test]
    public void PlannedDirectionsGiveTheIntent()
    {
        var rng = new System.Random(21);
        var planned = new Dictionary<int, int>();
        var near = new Dictionary<int, int>();
        int fields = 0;
        foreach (FieldSnapshot f in PartnerTestWorld.Fields())
        {
            fields++;
            foreach (int intent in new[] { 0, 1, 2, 3, 4, 6 })
            {
                ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
                planned.TryGetValue(intent, out int ok);
                near.TryGetValue(intent, out int close);
                planned[intent] = ok + (d.valid && d.predicted.runs == intent ? 1 : 0);
                near[intent] = close + (!d.predicted.caught && Mathf.Abs(d.predicted.runs - intent) <= 1 ? 1 : 0);
                Assert.LessOrEqual(d.exitVelocity.magnitude, PartnerShotPlanner.SpeedCap(intent) + 1e-3f);
                Assert.GreaterOrEqual(d.predicted.closestToHuman, PartnerShotPlanner.HumanClearance, "clear of the player's head");
            }
        }
        foreach (int intent in new[] { 0, 1, 2, 3, 4, 6 })
            Debug.Log($"[PartnerPlannerTests] intent {intent}: {planned[intent]}/{fields} planned, {near[intent]}/{fields} within a run");
        foreach (int intent in new[] { 0, 1, 4 })
            Assert.GreaterOrEqual(planned[intent] / (float)fields, 0.9f, $"intent {intent}");
        Assert.GreaterOrEqual(planned[6] / (float)fields, 0.95f, "intent 6");
        Assert.GreaterOrEqual(planned[2] / (float)fields, 0.6f, "intent 2, where the field has one");
        Assert.GreaterOrEqual(near[3] / (float)fields, 0.9f, "intent 3: a run either side");
        Assert.GreaterOrEqual(near[2] / (float)fields, 0.9f, "intent 2: a run either side");
    }

    [Test]
    public void PlansFromTheRealContactStillGiveTheIntent()
    {
        var rng = new System.Random(31);
        var hit = new Dictionary<int, int>();
        var all = new Dictionary<int, int>();
        var fields = PartnerTestWorld.Fields();
        List<IncomingBall> balls = PartnerTestWorld.Deliveries(out _);
        int[] intents = { 1, 2, 4, 6 };
        for (int i = 0; i < 120; i++)
        {
            FieldSnapshot f = fields[i % fields.Count];
            int intent = intents[i % intents.Length];
            IncomingBall b = balls[(i * 7) % balls.Count];
            ShotDirection d = PartnerShotPlanner.PlanDirection(intent, f, PartnerTestWorld.NominalContact, rng);
            ShotPlan p = PartnerShotPlanner.Plan(d, b, f, f.offSign, rng, scratch);
            if (!p.makesContact) continue;
            all.TryGetValue(intent, out int n);
            all[intent] = n + 1;
            hit.TryGetValue(intent, out int h);
            hit[intent] = h + (p.predicted.runs == intent && !p.predicted.caught ? 1 : 0);
            Assert.LessOrEqual(p.exitVelocity.magnitude, PartnerShotPlanner.SpeedCap(intent) + 1e-3f, "exit speed");
            Assert.GreaterOrEqual(p.predicted.closestToHuman, PartnerShotPlanner.HumanClearance, $"clear of the player's head: {p.shot}");
            Assert.Less((p.batVelocity - PartnerShotPlanner.BatVelocityFor(p.incoming, p.exitVelocity)).magnitude, 1e-4f);
        }
        int got = 0, played = 0;
        foreach (int intent in intents)
        {
            hit.TryGetValue(intent, out int h);
            all.TryGetValue(intent, out int n);
            Debug.Log($"[PartnerPlannerTests] intent {intent} from the real contact: {h}/{n}");
            if (intent == 2) continue;
            got += h;
            played += n;
        }
        Assert.Greater(played, 60);
        Assert.GreaterOrEqual(got / (float)played, 0.85f, "ones, fours and sixes");
    }

    [Test]
    public void ADotToABallMissingTheStumpsIsNotPlayed()
    {
        var rng = new System.Random(41);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.OffStump.field, eSwingType.Pace, 1f, true);
        ShotDirection d = PartnerShotPlanner.PlanDirection(0, f, PartnerTestWorld.NominalContact, rng);
        foreach (float line in new[] { 0.35f, 0.55f, 0.75f, -0.4f })
            foreach (float pitch in new[] { 4f, 5.5f, 7f })
            {
                IncomingBall b = PartnerTestWorld.Delivery(pitch, 35f, line, default);
                Assert.IsFalse(PartnerShotPlanner.WouldHitStumps(b, scratch));
                ShotPlan p = PartnerShotPlanner.Plan(d, b, f, 1f, rng, scratch);
                Assert.IsFalse(p.makesContact, $"line {line} pitch {pitch}: {p.shot}");
                Assert.IsTrue(p.shot == PartnerShot.Leave || p.shot == PartnerShot.PlayAndMiss);
            }
    }

    [Test]
    public void ADotToABallHittingTheStumpsIsDefended()
    {
        var rng = new System.Random(43);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.AttackStumps.field, eSwingType.Pace, 1f, true);
        ShotDirection d = PartnerShotPlanner.PlanDirection(0, f, PartnerTestWorld.NominalContact, rng);
        IncomingBall b = PartnerTestWorld.Delivery(5.5f, 35f, 0.02f, default);
        Assert.IsTrue(PartnerShotPlanner.WouldHitStumps(b, scratch));
        ShotPlan p = PartnerShotPlanner.Plan(d, b, f, 1f, rng, scratch);
        Assert.IsTrue(p.makesContact);
        Assert.AreEqual(PartnerShot.Defend, p.shot);
        Assert.Less(p.predicted.fieldedAt, 2f, "fielded inside HUD's dot");
        Assert.IsFalse(p.predicted.caught);
    }

    [Test]
    public void AWicketBallHittingTheStumpsIsPlayedAndMissed()
    {
        var rng = new System.Random(47);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.AttackStumps.field, eSwingType.Pace, 1f, true);
        ShotDirection d = PartnerShotPlanner.PlanDirection(CareerSim.Wicket, f, PartnerTestWorld.NominalContact, rng);
        IncomingBall b = PartnerTestWorld.Delivery(3f, 36f, -0.03f, default);
        Assert.IsTrue(PartnerShotPlanner.WouldHitStumps(b, scratch));
        ShotPlan p = PartnerShotPlanner.Plan(d, b, f, 1f, rng, scratch);
        Assert.AreEqual(PartnerShot.PlayAndMiss, p.shot);
        Assert.AreEqual(PartnerWicket.Bowled, p.wicket);
        Assert.IsFalse(p.makesContact);
    }

    [Test]
    public void AWicketBallMissingTheStumpsIsCaughtOrEdged()
    {
        var rng = new System.Random(53);
        int got = 0, all = 0;
        var fields = PartnerTestWorld.Fields();
        foreach (IncomingBall b in PartnerTestWorld.Deliveries(out _))
        {
            if (PartnerShotPlanner.WouldHitStumps(b, scratch)) continue;
            FieldSnapshot f = fields[all % fields.Count];
            ShotDirection d = PartnerShotPlanner.PlanDirection(CareerSim.Wicket, f, PartnerTestWorld.NominalContact, rng);
            ShotPlan p = PartnerShotPlanner.Plan(d, b, f, f.offSign, rng, scratch);
            all++;
            bool taken = p.makesContact && p.predicted.caught && (p.wicket == PartnerWicket.Caught || p.wicket == PartnerWicket.CaughtBehind);
            if (taken) got++;
        }
        Debug.Log($"[PartnerPlannerTests] wicket balls missing the stumps caught or edged: {got}/{all}");
        Assert.Greater(all, 20);
        Assert.GreaterOrEqual(got / (float)all, 0.9f);
    }

    [Test]
    public void AWideIsLeftAlone()
    {
        var rng = new System.Random(59);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.OffStump.field, eSwingType.Pace, 1f, true);
        IEnumerator<ShotDirection> steps = PartnerShotPlanner.PlanDirectionSteps(PartnerIntent.LeaveWide, f, PartnerTestWorld.NominalContact, rng, 10);
        Assert.IsTrue(steps.MoveNext());
        ShotPlan p = PartnerShotPlanner.Plan(steps.Current, PartnerTestWorld.Delivery(5f, 35f, 1.1f, default), f, 1f, rng, scratch);
        Assert.AreEqual(PartnerShot.Leave, p.shot);
        Assert.IsFalse(p.makesContact);
    }

    [Test]
    public void TheDirectionSearchIsBoundedInSteps()
    {
        var rng = new System.Random(61);
        foreach (FieldSnapshot f in PartnerTestWorld.Fields())
            foreach (int intent in new[] { CareerSim.Wicket, 0, 1, 2, 3, 4, 6 })
            {
                int steps = 0;
                IEnumerator<ShotDirection> e = PartnerShotPlanner.PlanDirectionSteps(intent, f, PartnerTestWorld.NominalContact, rng, 1);
                while (e.MoveNext()) steps++;
                Assert.LessOrEqual(steps, PartnerShotPlanner.MaxCandidates + 1, $"intent {intent}: one candidate a step");
                int batches = 0;
                e = PartnerShotPlanner.PlanDirectionSteps(intent, f, PartnerTestWorld.NominalContact, rng, 25);
                while (e.MoveNext()) batches++;
                Assert.LessOrEqual(batches, PartnerShotPlanner.MaxCandidates / 25 + 1, $"intent {intent}: 25 a step");
            }
    }

    [Test]
    public void NoShotIsAimedAtThePlayersHead()
    {
        var rng = new System.Random(67);
        FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.Yorkers.field, eSwingType.Pace, 1f, true);
        Vector3 c = PartnerTestWorld.NominalContact;
        Vector3 atHead = Vector3.zero;
        float nearest = float.MaxValue;
        for (float lift = 0f; lift <= 20f; lift += 0.25f)
        {
            Vector3 aim = Quaternion.AngleAxis(lift, Vector3.Cross(f.nonStrikerHead - c, Vector3.up)) * (f.nonStrikerHead - c).normalized * 30f;
            float d = PartnerField.Evaluate(c, aim, f, scratch).closestToHuman;
            if (d < nearest) { nearest = d; atHead = aim; }
        }
        Assert.Less(nearest, 0.8f, "the test aims at him");
        for (int i = 0; i < 10; i++)
        {
            ShotDirection d = new ShotDirection { intent = 4, exitVelocity = atHead, valid = true };
            ShotPlan p = PartnerShotPlanner.Plan(d, PartnerTestWorld.Delivery(7.5f, 34f, 0.05f, default), f, 1f, rng, scratch);
            if (p.makesContact) Assert.GreaterOrEqual(p.predicted.closestToHuman, 0.8f);
        }
    }

    /// The player's head right in front of the bat, so every turn of the shot passes within 0.8 m
    /// of him: the four is given up for a push under him; and with his head as low as the ball,
    /// where even the push comes near, the turn that passes furthest from him - never the shot as
    /// aimed, and its prediction is the one for the exit actually played.
    [Test]
    public void AShotWithNoClearPathIsGivenUp()
    {
        var rng = new System.Random(71);
        IncomingBall b = PartnerTestWorld.Delivery(7.5f, 34f, 0.05f, default);
        FieldSnapshot nobody = PartnerTestWorld.Field(BowlingPlans.Yorkers.field, eSwingType.Pace, 1f, false);
        var straight = new ShotDirection { intent = 4, exitVelocity = PartnerShotPlanner.ExitFor(0f, 0f, 30f, 1f), valid = true };
        Vector3 contact = PartnerShotPlanner.Plan(straight, b, nobody, 1f, rng, scratch).contactPoint;
        foreach (float headUp in new[] { 1.1f, 0f })
        {
            FieldSnapshot f = PartnerTestWorld.Field(BowlingPlans.Yorkers.field, eSwingType.Pace, 1f, true);
            f.nonStrikerHead = contact + new Vector3(-1.2f, headUp, 0f);
            Vector3 aimed = (f.nonStrikerHead - contact).normalized * 30f;
            float aimedGap = PartnerField.Evaluate(contact, aimed, f, scratch).closestToHuman;
            Assert.Less(aimedGap, 0.1f, "the test aims at him");
            ShotPlan p = PartnerShotPlanner.Plan(new ShotDirection { intent = 4, exitVelocity = aimed, valid = true }, b, f, 1f, rng, scratch);
            if (!p.makesContact) continue;
            float gap = PartnerField.Evaluate(p.contactPoint, p.exitVelocity, f, scratch).closestToHuman;
            Assert.AreEqual(gap, p.predicted.closestToHuman, 1e-3f, $"head +{headUp}: the prediction is the exit's");
            Assert.Greater(gap, aimedGap + 0.3f, $"head +{headUp}: turned off him");
            if (headUp > 0f) Assert.GreaterOrEqual(gap, PartnerShotPlanner.HumanClearance, "pushed under him");
        }
    }
}

/// <summary>Grounds, fields, deliveries and a bat for the partner tests.</summary>
internal static class PartnerTestWorld
{
    public const float Boundary = 56f;
    public static readonly Vector3 NonStrikerHead = new Vector3(EndPose.NonStrikerHeadXZ.x, 1.62f, EndPose.NonStrikerHeadXZ.y);
    public static Vector3 NominalContact => new Vector3(PartnerShotPlanner.FrontContactX, 0.5f, 0f);

    /// The game's bat (Bat.prefab: scale 0.475, toe -0.89, sweet spot -0.50 local) with the hands
    /// at 0.35 and 0.8 of the handle.
    public static BatGrip Grip => new BatGrip
    {
        sweetLocal = new Vector3(0f, 0f, -0.50f), toeLocal = new Vector3(0f, 0f, -0.89f),
        bottomHandLocal = new Vector3(0f, 0f, 0.50f), topHandLocal = new Vector3(0f, 0f, 0.92f), scale = 0.475f,
    };

    public static Vector3 StanceHips => new Vector3(9.55f, 0.88f, 0f);

    public static FieldSets.Ground Ground(eSwingType bowler, float offSign) => new FieldSets.Ground
    {
        strikerX = BallDelivery.BatsmanStumpsX, nonStrikerX = BallDelivery.BowlerStumpsX, boundary = Boundary,
        keeperX = FieldSets.KeeperX(bowler), offSign = offSign,
    };

    /// A plan's nine, the bowler following through and the keeper (last), at 7.5 m/s.
    public static FieldSnapshot Field(FieldSets.Position[] set, eSwingType bowler, float offSign, bool withHuman)
    {
        FieldSets.Ground g = Ground(bowler, offSign);
        var places = new List<Vector3>();
        foreach (FieldSets.Position p in set) places.Add(FieldSets.Place(p, g));
        places.Add(new Vector3(BallDelivery.BowlerStumpsX + 3.5f, 0f, 0.8f));
        places.Add(new Vector3(g.keeperX, 0f, 0f));
        var speeds = new float[places.Count];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = AnimatedFielder.BaseRunSpeed * 1.5f;
        return new FieldSnapshot
        {
            fielders = places.ToArray(), runSpeed = speeds, keeperIndex = places.Count - 1, boundary = Boundary,
            offSign = offSign, delay = AnimatedFielderManagement.SeeingDelay + AnimatedFielderManagement.ReactionTime,
            nonStrikerHead = withHuman ? NonStrikerHead : Vector3.zero,
        };
    }

    /// Every pace and spin plan's field, for both hands.
    public static List<FieldSnapshot> Fields()
    {
        var list = new List<FieldSnapshot>();
        foreach (float o in new[] { 1f, -1f })
        {
            foreach (BowlingPlan p in BowlingPlans.Pace) list.Add(Field(p.field, eSwingType.Pace, o, true));
            foreach (BowlingPlan p in BowlingPlans.Spin) list.Add(Field(p.field, eSwingType.OffSpin, o, true));
        }
        return list;
    }

    public static IncomingBall Delivery(float pitchFromStumps, float speed, float lineZ, BallFlight.DeliveryEffects fx) =>
        Delivery(pitchFromStumps, speed, lineZ, fx, out _);

    public static IncomingBall Delivery(float pitchFromStumps, float speed, float lineZ, BallFlight.DeliveryEffects fx, out BallDelivery.Solution s)
    {
        s = BallDelivery.Solve(BallDelivery.NominalRelease + new Vector3(0f, 0f, 0.6f), BallDelivery.BatsmanStumpsX - pitchFromStumps, speed, lineZ, fx);
        return new IncomingBall { release = s.releasePosition, velocity = s.releaseVelocity, effects = fx };
    }

    /// Pace at every length and line, swing both ways, and spin turning both ways.
    public static List<IncomingBall> Deliveries(out List<BallDelivery.Solution> solutions)
    {
        var balls = new List<IncomingBall>();
        solutions = new List<BallDelivery.Solution>();
        var swing = new BallFlight.DeliveryEffects { swingAccelPerV2 = BallFlight.SwingAccelPerV2(0.5f), swingSign = 1f };
        var turn = new BallFlight.DeliveryEffects { turnDegrees = 4f };
        foreach (float pitch in new[] { 2.5f, 4f, 5.5f, 7f, 8.5f })
            foreach (float line in new[] { -0.25f, 0f, 0.1f, 0.3f, 0.55f })
            {
                balls.Add(Delivery(pitch, 36f, line, default, out BallDelivery.Solution s));
                solutions.Add(s);
            }
        foreach (float line in new[] { -0.1f, 0.2f, 0.45f })
        {
            balls.Add(Delivery(5f, 33f, line, swing, out BallDelivery.Solution s1)); solutions.Add(s1);
            swing.swingSign = -1f;
            balls.Add(Delivery(5f, 33f, line, swing, out BallDelivery.Solution s2)); solutions.Add(s2);
            swing.swingSign = 1f;
            balls.Add(Delivery(3.5f, 22f, line, turn, out BallDelivery.Solution s3)); solutions.Add(s3);
            turn.turnDegrees = -turn.turnDegrees;
        }
        return balls;
    }

    public static int Nearest(in FieldSnapshot f, Vector3 p)
    {
        int best = 0;
        for (int i = 1; i < f.fielders.Length; i++)
            if ((f.fielders[i] - p).sqrMagnitude < (f.fielders[best] - p).sqrMagnitude) best = i;
        return best;
    }

    public static Vector3 RandomVector(System.Random rng, float size) => new Vector3(
        (float)(rng.NextDouble() * 2 - 1) * size, (float)(rng.NextDouble() * 2 - 1) * size, (float)(rng.NextDouble() * 2 - 1) * size);
}
