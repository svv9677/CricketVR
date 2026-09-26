using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scenario tests for bat-ball contact (BatContact + BatGeometry), standalone - no scene, no
/// PhysX. A bat swings about the batter's hands, a ball comes down the pitch timed to meet a
/// chosen point on the blade, and the game's own per-frame code is run over it frame by frame.
///
/// Swings: straight drive (vertical bat), pull (bat horizontal, toe to leg), cut (toe to off).
/// Every scenario is repeated at random frame phases so the contact lands anywhere inside a frame.
/// Run: Window > General > Test Runner > EditMode > BatContactTests.
/// </summary>
public class BatContactTests
{
    private const string BatPrefab = "Assets/Resources/Prefabs/Bat.prefab";
    private static readonly Vector3 Hands = new Vector3(9.6f, 1.0f, 0.3f);
    /// The bat's grip point, bat-local (see BatGripPreview).
    private const float GripZ = 0.83f;
    private const float BallMass = 0.16f;

    private BatContact.Blade blade;
    private float scale;

    [OneTimeSetUp]
    public void LoadBat()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefab);
        blade = BatGeometry.FitBlade(prefab.GetComponent<MeshFilter>().sharedMesh.vertices);
        scale = prefab.transform.localScale.x;
    }

    public enum Swing { Drive, Pull, Cut }

    public struct Scenario
    {
        public Swing swing;
        public float toeSpeed;      // m/s at the toe
        public float ballSpeed;     // m/s
        public float fps;
        public Vector2 contact;     // bat-local (x across the face, z along the blade)
        public float phase;         // 0..1: where in its frame the contact falls
    }

    public struct Outcome
    {
        /// Whether the brute-force oracle says they really touch.
        public bool truthExists;
        public bool detected;
        public float exitSpeed;
        public Vector3 exitVelocity;
        public BatContact.Result result;
        public Vector3 localBat;
        public float contactError;  // metres between planned and found contact
    }

    // ---- Scenario kinematics ---------------------------------------------------------------

    private static Vector3 HandleDirection(Swing s) =>
        s == Swing.Drive ? Vector3.up : s == Swing.Pull ? Vector3.back : Vector3.forward;

    /// Bat pose at time t (contact at t = 0): rotating about the hands so the face moves toward the
    /// bowler (-X) at the chosen toe speed.
    private void Pose(Scenario sc, float t, out Vector3 pos, out Quaternion rot)
    {
        Vector3 h = HandleDirection(sc.swing);
        Vector3 axis = Vector3.Cross(h, Vector3.right).normalized;
        float toeRadius = (GripZ - blade.toeZ) * scale;
        float omega = sc.toeSpeed / toeRadius * Mathf.Rad2Deg;
        Quaternion atContact = Quaternion.LookRotation(h, Vector3.left);   // handle along h, face to the bowler
        rot = Quaternion.AngleAxis(omega * t, axis) * atContact;
        pos = Hands - rot * new Vector3(0f, 0f, GripZ * scale);
    }

    /// Run a scenario through the game's frame-by-frame code.
    public Outcome Run(Scenario sc, float lateralMiss = 0f, float toeMiss = 0f)
    {
        Pose(sc, 0f, out Vector3 p0, out Quaternion r0);
        Vector3 localTarget = new Vector3(sc.contact.x + lateralMiss / scale, blade.faceY, sc.contact.y - toeMiss / scale);
        Vector3 face = p0 + r0 * (localTarget * scale);
        Vector3 ballAtContact = face + Vector3.left * BallFlight.Radius;
        Vector3 ballVelocity = Vector3.right * sc.ballSpeed;

        float dt = 1f / sc.fps;
        float start = -(sc.phase + 12f) * dt;   // contact lands at `phase` of some frame
        var outcome = new Outcome();
        outcome.truthExists = TrueFirstTouch(sc, ballAtContact, ballVelocity, start, start + 24 * dt, out Vector3 truth);
        for (int f = 0; f < 24; f++)
        {
            float ta = start + f * dt, tb = ta + dt;
            Pose(sc, ta, out Vector3 pa, out Quaternion ra);
            Pose(sc, tb, out Vector3 pb, out Quaternion rb);
            Vector3 ballA = ballAtContact + ballVelocity * ta, ballB = ballAtContact + ballVelocity * tb;
            Vector3 grip = new Vector3(0f, 0f, GripZ);
            if (!BatContact.Sweep(pa, ra, pb, rb, scale, ballA, ballB, blade, 1f, BallFlight.Radius, grip, out BatContact.Hit hit))
                continue;
            Vector3 batVel = BatContact.PointVelocity(hit.localBat, scale, pa, ra, pb, rb, dt, grip, hit.t);
            BatContact.Result res = BatContact.Respond(hit, blade, scale, ballVelocity, batVel, BallMass);
            if (res.velocity == ballVelocity)
                continue;
            outcome.detected = true;
            outcome.result = res;
            outcome.exitVelocity = res.velocity;
            outcome.exitSpeed = res.velocity.magnitude;
            outcome.localBat = hit.localBat;
            outcome.contactError = outcome.truthExists ? Vector3.Distance(hit.localBat, truth) * scale : float.PositiveInfinity;
            return outcome;
        }
        return outcome;
    }

    /// Ground truth, by brute force: step the exact poses at 20 kHz and report the first moment the
    /// ball is within its radius of the blade box, and the point it touches.
    private bool TrueFirstTouch(Scenario sc, Vector3 ballAtContact, Vector3 ballVelocity, float from, float to, out Vector3 touch)
    {
        Vector3 min = new Vector3(-blade.halfWidth, blade.backY, blade.toeZ);
        Vector3 max = new Vector3(blade.halfWidth, blade.faceY, blade.shoulderZ);
        float r = BallFlight.Radius / scale;
        for (float t = from; t < to; t += 0.00005f)
        {
            Pose(sc, t, out Vector3 p, out Quaternion q);
            Vector3 l = Quaternion.Inverse(q) * (ballAtContact + ballVelocity * t - p) / scale;
            Vector3 c = Vector3.Max(min, Vector3.Min(max, l));
            if ((l - c).sqrMagnitude <= r * r)
            {
                touch = c;
                return true;
            }
        }
        touch = Vector3.zero;
        return false;
    }

    /// The old detection: a trigger overlap sampled at physics steps, bat pose held per frame.
    private bool LegacyDetects(Scenario sc)
    {
        Pose(sc, 0f, out Vector3 p0, out Quaternion r0);
        Vector3 face = p0 + r0 * (new Vector3(sc.contact.x, blade.faceY, sc.contact.y) * scale);
        Vector3 ballAtContact = face + Vector3.left * BallFlight.Radius;
        float dt = 1f / sc.fps, step = 0.007f;
        float start = -(sc.phase + 12f) * dt;
        Vector3 min = new Vector3(-blade.halfWidth, blade.backY, blade.toeZ);
        Vector3 max = new Vector3(blade.halfWidth, blade.faceY, blade.shoulderZ);
        for (float t = start; t < start + 24 * dt; t += step)
        {
            float frameStart = start + Mathf.Floor((t - start) / dt) * dt;
            Pose(sc, frameStart, out Vector3 pa, out Quaternion ra);
            Vector3 l = Quaternion.Inverse(ra) * (ballAtContact + Vector3.right * sc.ballSpeed * t - pa) / scale;
            float r = BallFlight.Radius / scale;
            if (l.x > min.x - r && l.x < max.x + r && l.y > min.y - r && l.y < max.y + r && l.z > min.z - r && l.z < max.z)
                return true;
        }
        return false;
    }

    private Vector2 SweetSpot => new Vector2(0f, blade.sweetZ);
    private Vector2 Toe => new Vector2(0f, blade.toeZ + 0.05f / scale);
    private Vector2 Shoulder => new Vector2(0f, blade.shoulderZ - 0.06f / scale);

    // ---- Tests -------------------------------------------------------------------------------

    [Test]
    public void SweetSpotSitsWhereARealBatHasIt()
    {
        float blade = (this.blade.shoulderZ - this.blade.toeZ) * scale;
        float fromToe = (this.blade.sweetZ - this.blade.toeZ) * scale;
        Debug.Log($"[BatContactTests] blade {blade:F3} m, sweet spot {fromToe:F3} m from the toe ({fromToe / blade:P0}), width {2 * this.blade.halfWidth * scale:F3} m");
        Assert.That(fromToe / blade, Is.InRange(0.22f, 0.45f));
        Assert.That(fromToe, Is.InRange(0.12f, 0.25f));
    }

    [Test]
    public void GeometryScalesWithTheBat()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefab).GetComponent<MeshFilter>().sharedMesh;
        var a = BatGeometry.FitBlade(mesh.vertices);
        var scaled = mesh.vertices;
        for (int i = 0; i < scaled.Length; i++) scaled[i] *= 1.3f;
        var b = BatGeometry.FitBlade(scaled);
        Assert.That(b.sweetZ, Is.EqualTo(a.sweetZ * 1.3f).Within(0.02f));
        Assert.That(b.halfWidth, Is.EqualTo(a.halfWidth * 1.3f).Within(0.002f));
        Assert.That((b.sweetZ - b.toeZ) / (b.shoulderZ - b.toeZ), Is.EqualTo((a.sweetZ - a.toeZ) / (a.shoulderZ - a.toeZ)).Within(0.02f));
    }

    [Test]
    public void EveryContactIsFound_AtAnySpeed()
    {
        var rng = new System.Random(11);
        int total = 0, found = 0, legacyFound = 0, falsePositives = 0;
        float worstError = 0f;
        var report = new StringBuilder("[BatContactTests] detection matrix (found / scenarios, legacy overlap in brackets)\n");
        foreach (Swing swing in new[] { Swing.Drive, Swing.Pull, Swing.Cut })
        foreach (float fps in new[] { 72f, 36f })
        foreach (float toe in new[] { 0f, 10f, 20f, 30f, 40f })
        {
            int t = 0, ok = 0, legacy = 0;
            foreach (float ball in new[] { 20f, 30f, 40f, 45f })
            for (int i = 0; i < 12; i++)
            {
                var sc = new Scenario
                {
                    swing = swing, toeSpeed = toe, ballSpeed = ball, fps = fps, phase = (float)rng.NextDouble(),
                    contact = new Vector2((float)(rng.NextDouble() * 1.6 - 0.8) * blade.halfWidth,
                                          Mathf.Lerp(blade.toeZ + 0.03f / scale, blade.shoulderZ - 0.03f / scale, (float)rng.NextDouble())),
                };
                Outcome o = Run(sc);
                if (!o.truthExists) { if (o.detected) falsePositives++; continue; }
                t++; if (o.detected) { ok++; worstError = Mathf.Max(worstError, o.contactError); }
                if (LegacyDetects(sc)) legacy++;
            }
            total += t; found += ok; legacyFound += legacy;
            report.AppendLine($"  {swing,-5} {fps,2} fps toe {toe,2} m/s: {ok}/{t} [{legacy}/{t}]");
        }
        report.AppendLine($"  TOTAL {found}/{total} (legacy {legacyFound}/{total}), false positives {falsePositives}, worst contact-point error vs 20 kHz ground truth {worstError * 100f:F2} cm");
        Debug.Log(report.ToString());
        Assert.That(found, Is.EqualTo(total), report.ToString());
        Assert.That(falsePositives, Is.Zero, report.ToString());
        Assert.That(worstError, Is.LessThan(0.02f), report.ToString());
    }

    [Test]
    public void NearMissesAreNotHits()
    {
        var rng = new System.Random(5);
        int falseHits = 0, total = 0;
        foreach (Swing swing in new[] { Swing.Drive, Swing.Pull, Swing.Cut })
        foreach (float toe in new[] { 0f, 20f, 40f })
        for (int i = 0; i < 20; i++)
        {
            var sc = new Scenario { swing = swing, toeSpeed = toe, ballSpeed = 35f, fps = 72f, phase = (float)rng.NextDouble(),
                                    contact = new Vector2(blade.halfWidth, Mathf.Lerp(blade.toeZ, blade.shoulderZ, 0.5f)) };
            // Past the edge of the blade, or below the toe, by 3 cm clear.
            if (Run(sc, lateralMiss: 2f * BallFlight.Radius + 0.03f).detected) falseHits++;
            sc.contact = new Vector2(0f, blade.toeZ);
            if (Run(sc, toeMiss: 2f * BallFlight.Radius + 0.03f).detected) falseHits++;
            total += 2;
        }
        Assert.That(falseHits, Is.Zero, $"{falseHits}/{total} near misses registered as hits");
    }

    [Test]
    public void DeadBatDropsTheBall()
    {
        foreach (float ball in new[] { 25f, 35f, 45f })
        {
            Outcome o = Run(new Scenario { swing = Swing.Drive, toeSpeed = 0f, ballSpeed = ball, fps = 72f, phase = 0.4f, contact = SweetSpot });
            Assert.That(o.detected);
            Assert.That(o.exitSpeed, Is.LessThan(0.3f * ball), $"dead bat returned a {ball} m/s ball at {o.exitSpeed:F1}");
        }
    }

    [Test]
    public void HardSwingMatchesRealBatPhysics()
    {
        var report = new StringBuilder("[BatContactTests] middled drive, exit speed (m/s)\n");
        float sweetRadius = (GripZ - blade.sweetZ) * scale, toeRadius = (GripZ - blade.toeZ) * scale;
        foreach (float toe in new[] { 20f, 30f, 40f })
        foreach (float ball in new[] { 30f, 40f, 45f })
        {
            Outcome o = Run(new Scenario { swing = Swing.Drive, toeSpeed = toe, ballSpeed = ball, fps = 72f, phase = 0.7f, contact = SweetSpot });
            float batAtSweet = toe * sweetRadius / toeRadius;
            // Real bats: v_out = q v_in + (1+q) v_bat with q ~ 0.2-0.25 at the sweet spot.
            float lo = 0.18f * ball + 1.18f * batAtSweet, hi = 0.28f * ball + 1.28f * batAtSweet;
            report.AppendLine($"  toe {toe} (sweet {batAtSweet:F1}) vs ball {ball}: {o.exitSpeed:F1}  [{lo:F1} .. {hi:F1}]");
            Assert.That(o.detected);
            Assert.That(o.exitSpeed, Is.InRange(lo, hi), report.ToString());
        }
        Debug.Log(report.ToString());
    }

    [Test]
    public void TheMiddleBeatsTheToeAndTheShoulder()
    {
        var sc = new Scenario { swing = Swing.Drive, toeSpeed = 30f, ballSpeed = 38f, fps = 72f, phase = 0.3f };
        sc.contact = SweetSpot; Outcome middle = Run(sc);
        sc.contact = Toe; Outcome toe = Run(sc);
        sc.contact = Shoulder; Outcome shoulder = Run(sc);
        // Compare as a share of the bat speed at each point, so the lever arm does not flatter the toe.
        Debug.Log($"[BatContactTests] middle {middle.exitSpeed:F1} q{middle.result.quality:F2}, toe {toe.exitSpeed:F1} q{toe.result.quality:F2}, shoulder {shoulder.exitSpeed:F1} q{shoulder.result.quality:F2}");
        Assert.That(middle.result.quality, Is.GreaterThan(0.9f));
        Assert.That(middle.result.restitution, Is.GreaterThan(toe.result.restitution + 0.1f));
        Assert.That(middle.exitSpeed, Is.GreaterThan(shoulder.exitSpeed * 1.15f));
        Assert.That(toe.result.quality, Is.LessThan(0.5f));
        Assert.That(shoulder.result.quality, Is.LessThan(0.6f));
    }

    [Test]
    public void EdgesDeflectAndLoseSpeed()
    {
        var sc = new Scenario { swing = Swing.Drive, toeSpeed = 25f, ballSpeed = 38f, fps = 72f, phase = 0.5f };
        sc.contact = SweetSpot; Outcome centre = Run(sc);
        sc.contact = new Vector2(0.95f * blade.halfWidth, blade.sweetZ); Outcome outside = Run(sc);
        sc.contact = new Vector2(-0.95f * blade.halfWidth, blade.sweetZ); Outcome inside = Run(sc);
        float outsideAngle = Vector3.Angle(centre.exitVelocity, outside.exitVelocity);
        float insideAngle = Vector3.Angle(centre.exitVelocity, inside.exitVelocity);
        Debug.Log($"[BatContactTests] centre {centre.exitSpeed:F1} m/s; edges {outside.exitSpeed:F1} / {inside.exitSpeed:F1} m/s, deflected {outsideAngle:F0} / {insideAngle:F0} deg, edge flags {outside.result.edge}/{inside.result.edge}");
        Assert.That(outside.result.edge && inside.result.edge);
        Assert.That(outsideAngle, Is.GreaterThan(20f));
        Assert.That(insideAngle, Is.GreaterThan(20f));
        Assert.That(outside.exitSpeed, Is.LessThan(centre.exitSpeed * 0.85f));
        // They go opposite ways off the face.
        Assert.That(Mathf.Sign(outside.exitVelocity.z - centre.exitVelocity.z), Is.Not.EqualTo(Mathf.Sign(inside.exitVelocity.z - centre.exitVelocity.z)));
    }

    [Test]
    public void ABallAlreadyInTheBladeLeavesByTheFaceItCameIn()
    {
        // The ball begins the frame more than half-way through the blade - nearer the back - moving
        // from the face side (+Y, toward the bowler) into it, while the bat swings at it. The old
        // rule pushed it out of the nearest face (the back): the normal pointed away from the
        // bowler, so the contact read as separating and the ball went on through the bat.
        float depth = blade.faceY - blade.backY;
        Vector3 inside = new Vector3(0f, blade.backY + 0.3f * depth, blade.sweetZ) * scale;
        Vector3 ballA = inside, ballB = inside + Vector3.down * 0.3f;
        Vector3 grip = new Vector3(0f, 0f, GripZ);
        Vector3 batStep = Vector3.up * 0.2f;   // bat moving toward the bowler
        Assert.That(BatContact.Sweep(Vector3.zero, Quaternion.identity, batStep, Quaternion.identity, scale,
                                     ballA, ballB + batStep, blade, 1f, BallFlight.Radius, grip, out BatContact.Hit hit));
        Assert.That(hit.localNormal.y, Is.EqualTo(1f), $"normal {hit.localNormal}");
        BatContact.Result res = BatContact.Respond(hit, blade, scale, Vector3.down * 35f, Vector3.up * 20f, BallMass);
        Assert.That(res.velocity.y, Is.GreaterThan(20f), "should be driven back toward the bowler");
    }

    [Test]
    public void ContactKindsNameWhereItWasStruck()
    {
        var sc = new Scenario { swing = Swing.Drive, toeSpeed = 25f, ballSpeed = 38f, fps = 72f, phase = 0.5f };
        sc.contact = SweetSpot;
        Assert.That(Run(sc).result.kind, Is.EqualTo(BatContact.ContactKind.Middled));
        sc.contact = Toe;
        Assert.That(Run(sc).result.kind, Is.EqualTo(BatContact.ContactKind.Toe));
        sc.contact = Shoulder;
        Assert.That(Run(sc).result.kind, Is.EqualTo(BatContact.ContactKind.Shoulder));
        sc.contact = new Vector2(0.95f * blade.halfWidth, blade.sweetZ);
        Assert.That(Run(sc).result.kind, Is.EqualTo(BatContact.ContactKind.ThickEdge));
        sc.contact = new Vector2(blade.halfWidth, blade.sweetZ);
        Outcome thin = Run(sc, lateralMiss: BallFlight.Radius * 0.7f);
        Assert.That(thin.result.kind, Is.EqualTo(BatContact.ContactKind.ThinEdge));
        Assert.That(thin.result.impactSpeed, Is.LessThan(Run(new Scenario { swing = Swing.Drive, toeSpeed = 25f, ballSpeed = 38f,
                                                                             fps = 72f, phase = 0.5f, contact = SweetSpot }).result.impactSpeed));
    }

    [Test]
    public void SwingSpeedDecidesHowFarItGoes()
    {
        // Against the same 35 m/s ball, doubling the swing should add far more exit speed than
        // anything the ball's own pace contributes: q ~ 0.24 of the ball, (1+q) of the bat.
        float Exit(float toe, float ball) => Run(new Scenario { swing = Swing.Drive, toeSpeed = toe, ballSpeed = ball,
                                                                fps = 72f, phase = 0.4f, contact = SweetSpot }).exitSpeed;
        float fromSwing = Exit(40f, 35f) - Exit(20f, 35f);
        float fromPace = Exit(30f, 45f) - Exit(30f, 25f);
        Debug.Log($"[BatContactTests] +20 m/s toe speed adds {fromSwing:F1} m/s; +20 m/s ball speed adds {fromPace:F1} m/s");
        Assert.That(fromSwing, Is.GreaterThan(3f * fromPace));
    }

    [Test]
    public void ARampOffASlowBatNeverLeavesFasterThanItCame()
    {
        // Reported: a 143 km/h delivery ramped with a 17 km/h bat left at 172 km/h. A glancing
        // contact keeps most of the ball's pace and adds only what the bat's own motion along the
        // face gives it - it cannot come off faster than it arrived plus twice the bat's normal speed,
        // and against a nearly still bat it must come off slower.
        Vector3 ball = Vector3.right * (143f / 3.6f);
        float batSpeed = 17f / 3.6f;
        int contacts = 0;
        foreach (float glance in new[] { 15f, 30f, 45f })
        foreach (float lateral in new[] { 0f, 0.9f, 1f })
        {
            // Face tilted `glance` degrees to the ball's path, normal back against it and upward.
            Vector3 normal = new Vector3(-Mathf.Sin(glance * Mathf.Deg2Rad), Mathf.Cos(glance * Mathf.Deg2Rad), 0f);
            var hit = new BatContact.Hit
            {
                localBat = new Vector3(lateral * blade.halfWidth, blade.faceY, blade.sweetZ),
                localNormal = Vector3.up,
                rotation = Quaternion.FromToRotation(Vector3.up, normal),
            };
            foreach (float amp in new[] { 75f, 150f })
            {
                Vector3 bat = normal * batSpeed * BatContact.SwingScale(amp);
                BatContact.Result r = BatContact.Respond(hit, blade, scale, ball, bat, BallMass);
                if (r.velocity == ball)
                    continue;   // already leaving that (bevelled) surface: no contact, Bat ignores it
                contacts++;
                Assert.That(r.velocity.magnitude, Is.LessThan(ball.magnitude),
                    $"glance {glance} lateral {lateral} power {amp}: {r.velocity.magnitude * 3.6f:F0} km/h from {ball.magnitude * 3.6f:F0}");
            }
        }
        Debug.Log($"[BatContactTests] ramp: {contacts}/18 glances made contact, none left faster than it came");
        Assert.That(contacts, Is.GreaterThanOrEqualTo(9), "most of these glances should really touch the bat");
        Assert.That(BatContact.SwingScale(300f), Is.EqualTo(1.5f));
        Assert.That(BatContact.SwingScale(0f), Is.EqualTo(0.5f));
    }

    [Test]
    public void AThinEdgeCarriesOnBehind()
    {
        // Ball clipping the side of the blade: it keeps going toward the keeper, only deflected.
        var sc = new Scenario { swing = Swing.Drive, toeSpeed = 15f, ballSpeed = 38f, fps = 72f, phase = 0.5f,
                                contact = new Vector2(blade.halfWidth, blade.sweetZ) };
        Outcome o = Run(sc, lateralMiss: BallFlight.Radius * 0.7f);
        Assert.That(o.truthExists, "scenario should really touch the blade");
        Assert.That(o.detected, "a ball brushing the side of the blade should register");
        Assert.That(o.result.edge);
        Assert.That(o.exitVelocity.x, Is.GreaterThan(0f), "a thin edge should carry on past the batter");
    }
}
