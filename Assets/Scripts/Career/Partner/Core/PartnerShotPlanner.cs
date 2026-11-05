using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chooses the partner's shot: a direction before the ball is bowled (PlanDirectionSteps, spread
/// over frames during the run-up), then at release the shot itself (Plan): which foot, where and
/// when the bat meets the ball, and the exit velocity the contact gives it. The live game then
/// fields, scores and gives out the real ball as for any other hit, so the plan only has to be
/// believable and land the intent most of the time - the result in play always stands.
///
/// Plan, by intent (PartnerIntent):
///   * LeaveWide: left alone.
///   * 0: a ball missing the stumps is left or played and missed; one that would hit them is
///     defended, a firm push at a ring fielder who has it inside DotWithin (HUD gives a dot only
///     for a ball fielded inside 2 s, so a dead bat at the crease would usually be a single).
///   * Wicket: a ball that would hit the stumps is played and missed (bowled); otherwise a catch
///     to the fielder the search found, else an edge to the keeper or the slips (within EdgeCone of
///     the incoming line at EdgeSpeedMin..Max of its speed), else nothing is given (a dot).
///   * Runs: the searched direction from the real contact point, nudged NudgeDegrees either way
///     (toward straight first) if it no longer works from there, played with the shot that fits
///     its height and direction. The search keeps him straight - down the ground in the V -
///     unless nothing there lands the intent (PartnerDirectionSearch), so most are drives.
/// No shot is aimed within HumanClearance of the career player's head at the other end, and exit
/// speeds are capped (SpeedCap): 45 m/s for a six, 38 for a four, 30 otherwise.
/// </summary>
public static class PartnerShotPlanner
{
    /// BatContact's head-on rule at the sweet spot, as vectors: v_out = -q v_in + (1 + q) v_bat,
    /// q ~ 0.24 (Respond with e 0.52, M 0.75 kg: v_out = (1 - k) v_in + k v_bat, k ~ 1.25). The
    /// "q v_in" in BatContact's header is about speeds - the ball comes back the other way.
    public const float Restitution = 0.24f;
    /// The contact planes: a front-foot stride meets it this far in front of the stumps, a
    /// back-foot shot this far.
    public const float FrontContactBehind = 1.45f, BackContactBehind = 0.60f;
    public static float FrontContactX => BallDelivery.BatsmanStumpsX - FrontContactBehind;
    public static float BackContactX => BallDelivery.BatsmanStumpsX - BackContactBehind;
    /// Meet's step: 0 steps at the live physics step (Time.fixedDeltaTime, which DisplayRate sets
    /// to 1/90 or 1/144 s on the headset), so Meet predicts the ball the player sees. Tests pin it
    /// (0.01 s, the project's fixed timestep) and put it back.
    public static float MeetStep = 0f;
    /// The longest Meet follows a delivery.
    public const float MeetTime = 3f;
    public const int MaxCandidates = PartnerDirectionSearch.MaxCandidates;
    /// The V he plays in, degrees either side of straight (PartnerDirectionSearch.StraightArc).
    public const float StraightArc = PartnerDirectionSearch.StraightArc;
    /// Which of the search's arcs a FieldSets angle is in: 0 straight (the V), 1 square, 2 fine.
    public static int ArcOf(float angle) => PartnerDirectionSearch.Arc(angle);
    public const float HumanClearance = 0.8f;
    /// A dot is a ball fielded inside this (HUD: 2 s), with room to spare.
    public const float DotWithin = 1.7f;
    /// Pitched within this of the stumps the ball is played forward.
    public const float FullWithin = 6f;
    public const float NudgeDegrees = 4f;
    public const float EdgeCone = 15f, EdgeSpeedMin = 0.6f, EdgeSpeedMax = 0.8f;
    /// The six must be this far above catching height crossing the rope.
    public const float SixClearance = 0.3f;

    public static float SpeedCap(int intent) => intent == 6 ? 45f : intent == 4 ? 38f : 30f;

    /// The bat's velocity that sends `incoming` away at `exit`: v_bat = (v_out + q v_in) / (1 + q)
    /// (Restitution). Exact off the face; a ball met obliquely keeps some of its slide along the face
    /// (BatContact's friction), which no bat speed along the exit could take out. Checked through
    /// BatContact.Respond itself (PartnerSwingTests).
    public static Vector3 BatVelocityFor(Vector3 incoming, Vector3 exit) => (exit + Restitution * incoming) / (1f + Restitution);

    /// Where a bat moving at `batVelocity` sends `incoming` (BatVelocityFor's inverse).
    public static Vector3 ExitForBat(Vector3 incoming, Vector3 batVelocity) => (1f + Restitution) * batVelocity - Restitution * incoming;

    /// A hit at this angle (FieldSets': 0 straight back, off side positive), elevation (degrees)
    /// and speed.
    public static Vector3 ExitFor(float angle, float elevation, float speed, float offSign)
    {
        float a = angle * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad, o = offSign < 0f ? -1f : 1f;
        return new Vector3(-Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * o * Mathf.Cos(e)) * speed;
    }

    public static float AngleOf(Vector3 v, float offSign) => Mathf.Atan2(v.z * (offSign < 0f ? -1f : 1f), -v.x) * Mathf.Rad2Deg;
    public static float ElevationOf(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;

    // ---- Where the ball meets the bat ------------------------------------------------------

    public static BallAtBat Meet(in IncomingBall b, float contactX, List<BallFlight.Sample> scratch)
    {
        List<BallFlight.Sample> s = scratch ?? fallback;
        meetX = contactX;
        BallFlight.Simulate(b.release, b.velocity, b.effects, MeetTime, s, meetStop, MeetStep);
        for (int i = 0; i < s.Count; i++)
        {
            if (s[i].position.x < contactX) continue;
            Vector3 p0 = i > 0 ? s[i - 1].position : b.release, v0 = i > 0 ? s[i - 1].velocity : b.velocity;
            float t0 = i > 0 ? s[i - 1].time : 0f;
            float span = s[i].position.x - p0.x;
            float u = span > 1e-5f ? Mathf.Clamp01((contactX - p0.x) / span) : 1f;
            return new BallAtBat
            {
                found = true, time = Mathf.Lerp(t0, s[i].time, u),
                point = Vector3.Lerp(p0, s[i].position, u), velocity = Vector3.Lerp(v0, s[i].velocity, u),
            };
        }
        return default;
    }

    private static float meetX;
    private static readonly System.Func<BallFlight.Sample, bool> meetStop = x => x.position.x >= meetX || x.position.y < -1f;
    private static readonly List<BallFlight.Sample> fallback = new List<BallFlight.Sample>(512);

    /// Where the delivery in `samples` (a Meet's) first pitched, or +infinity if it did not.
    private static float PitchX(List<BallFlight.Sample> samples)
    {
        foreach (BallFlight.Sample x in samples)
            if (x.bounced) return x.position.x;
        return float.PositiveInfinity;
    }

    /// Would hit the stumps (the ball at BatsmanStumpsX, BallLedgerMath's rule).
    public static bool WouldHitStumps(in IncomingBall b, List<BallFlight.Sample> scratch)
    {
        BallAtBat at = Meet(b, BallDelivery.BatsmanStumpsX, scratch);
        return at.found && BallLedgerMath.WouldHitStumps(at.point.z, at.point.y);
    }

    // ---- Direction (before the ball) ---------------------------------------------------------

    /// The direction search, spread over frames: yields the best so far after each batch.
    public static IEnumerator<ShotDirection> PlanDirectionSteps(int intent, FieldSnapshot f, Vector3 nominalContact, System.Random rng, int candidatesPerStep)
    {
        var search = new PartnerDirectionSearch(intent, f, nominalContact, rng);
        int per = Mathf.Max(1, candidatesPerStep), n = 0;
        foreach (PartnerDirectionSearch.Candidate c in search.Candidates())
        {
            if (!search.Try(c)) break;
            if (++n >= per) { n = 0; yield return search.Best; }
        }
        yield return search.Best;
    }

    /// The whole search at once (tests, or a ball bowled before the frames ran out).
    public static ShotDirection PlanDirection(int intent, in FieldSnapshot f, Vector3 nominalContact, System.Random rng)
    {
        ShotDirection best = default;
        IEnumerator<ShotDirection> steps = PlanDirectionSteps(intent, f, nominalContact, rng, MaxCandidates);
        while (steps.MoveNext()) best = steps.Current;
        return best;
    }

    /// Does the field model's answer give the intent, and with what margin (metres or seconds;
    /// bigger is safer).
    internal static bool Realises(int intent, in PartnerField.Assessment a, out float margin)
    {
        FieldResult r = a.result;
        float catchHeight = AnimatedFielderManagement.CatchHeight;
        switch (intent)
        {
            case 6:
                margin = Mathf.Min(a.diveMiss, a.ropeHeight - catchHeight - SixClearance);
                return a.boundary && r.runs == 6 && margin > 0f;
            case 4:
                margin = a.diveMiss;
                return a.boundary && r.runs == 4 && margin > 0f;
            case 0:
                margin = Mathf.Min(DotWithin - r.fieldedAt, 0.3f * a.airDiveMiss);
                return !a.boundary && !r.caught && margin > 0f;
            case 1: case 2: case 3:
                margin = Mathf.Min(Mathf.Min(BandMargin(r.fieldedAt, intent), BandMargin(a.diveFieldedAt, intent)), 0.3f * a.airDiveMiss);
                return !a.boundary && !r.caught && r.runs == intent && margin > 0f;
            case CareerSim.Wicket:
                margin = Mathf.Min(a.catchInside, 1f) - 0.3f * Mathf.Abs(a.catchHeight - 1.3f);
                return r.caught;
            default:
                margin = 0f;
                return false;
        }
    }

    /// Seconds inside the HUD band that scores `runs`.
    private static float BandMargin(float t, int runs)
    {
        float lo = runs <= 0 ? 0f : PartnerField.RunBands[runs - 1];
        float hi = runs < PartnerField.RunBands.Length ? PartnerField.RunBands[runs] : float.PositiveInfinity;
        return Mathf.Min(t - lo, hi - t);
    }

    internal static float Score(int intent, in PartnerField.Assessment a, bool match, float margin)
    {
        float m = Mathf.Clamp(margin, -5f, 5f);
        if (match) return 10f + m;
        FieldResult r = a.result;
        // Short of the intent rather than over it, when it cannot be had (a three, on most fields).
        float off = intent == CareerSim.Wicket ? (r.caught ? 0f : 3f)
            : r.caught ? 6f : Mathf.Abs(r.runs - intent) + (r.runs > intent ? 0.25f : 0f);
        return -2f * off + 0.01f * m;
    }

    // ---- The shot (at release) ----------------------------------------------------------------

    public static ShotPlan Plan(in ShotDirection d, in IncomingBall b, in FieldSnapshot f, float offSign, System.Random rng, List<BallFlight.Sample> scratch)
    {
        List<BallFlight.Sample> s = scratch ?? fallback;
        System.Random dice = rng ?? new System.Random(0);
        BallAtBat stumps = Meet(b, BallDelivery.BatsmanStumpsX, s);
        float pitchX = PitchX(s);
        bool hits = stumps.found && BallLedgerMath.WouldHitStumps(stumps.point.z, stumps.point.y);
        BallAtBat front = Meet(b, FrontContactX, s), back = Meet(b, BackContactX, s);
        PartnerFoot foot = ChooseFoot(front, back, pitchX);
        BallAtBat at = foot == PartnerFoot.Front ? front : back;
        ShotPlan p = new ShotPlan
        {
            intent = d.intent, foot = foot, shot = PartnerShot.Leave,
            contactX = foot == PartnerFoot.Front ? FrontContactX : BackContactX,
            contactTime = at.time, contactPoint = at.point, incoming = at.velocity,
            kind = BatContact.ContactKind.Middled,
            predicted = new FieldResult { byFielder = -1, closestToHuman = PartnerField.NoHuman },
        };
        if (!at.found || d.intent == PartnerIntent.LeaveWide)
            return p;
        if (d.intent == CareerSim.Wicket)
            return PlanWicket(p, d, f, offSign, dice, s, hits);
        if (d.intent == 0)
            return hits ? Defend(p, d, f, offSign, s) : NoShot(p, offSign, dice);
        Vector3 exit = d.exitVelocity.sqrMagnitude > 1f ? d.exitVelocity : ExitFor(20f, 0f, 22f, offSign);
        exit = Revalidate(d.intent, at.point, Cap(exit, d.intent), f, s, out FieldResult pr);
        PartnerShot shot = Family(foot, at.point.y, exit, offSign, b.effects.turnDegrees != 0f);
        if (shot == PartnerShot.Leave)
            return hits ? Defend(p, d, f, offSign, s) : p;
        return Contact(p, shot, exit, pr, f);
    }

    private static PartnerFoot ChooseFoot(BallAtBat front, BallAtBat back, float pitchX)
    {
        bool full = pitchX > BallDelivery.BatsmanStumpsX - FullWithin;
        if (front.found && front.point.y <= (full ? 1.2f : 0.75f)) return PartnerFoot.Front;
        return back.found ? PartnerFoot.Back : PartnerFoot.Front;
    }

    /// The shot for a hit of this height and direction off this foot.
    public static PartnerShot Family(PartnerFoot foot, float height, Vector3 exit, float offSign, bool spin)
    {
        float angle = AngleOf(exit, offSign), elevation = ElevationOf(exit);
        if (foot == PartnerFoot.Front)
        {
            if (spin && angle <= -60f && angle >= -150f && height < 0.6f) return PartnerShot.Sweep;
            if (angle < -80f || angle > 130f) return PartnerShot.Glance;
            return elevation > 15f ? PartnerShot.Loft : PartnerShot.Drive;
        }
        if (height > 1.6f) return angle < 0f ? PartnerShot.Pull : PartnerShot.Leave;
        if (angle >= 60f) return PartnerShot.Cut;
        // Down the ground off the back foot is a punch (a Drive), not a pull: the pull starts
        // where the V ends (was -30, which made a push to mid-on a horizontal-bat shot).
        if (angle < -PartnerDirectionSearch.StraightArc && angle >= -120f) return PartnerShot.Pull;
        if (angle < -120f) return PartnerShot.Glance;
        return elevation > 15f ? PartnerShot.Loft : PartnerShot.Drive;
    }

    private static Vector3 Cap(Vector3 exit, int intent) => Vector3.ClampMagnitude(exit, SpeedCap(intent));

    /// The searched exit from the real contact: as it is if it still gives the intent, else nudged
    /// either way (at most three tries), toward straight first so a shot planned in the V stays in
    /// it; never one that passes too near the player's head, which is turned further off - away
    /// from him first - if it must. If every turn passes within HumanClearance of him, the shot
    /// is given up for a push back down the ground (DeadBatPush); if even that comes near him, the
    /// push or the turn, whichever passes further from him.
    private static Vector3 Revalidate(int intent, Vector3 contact, Vector3 exit, in FieldSnapshot f, List<BallFlight.Sample> s, out FieldResult result)
    {
        float[] turns = Turns(contact, exit, f);
        bool human = PartnerField.HasHuman(f);
        Vector3 clear = exit, widest = exit;
        FieldResult clearResult = default, widestResult = default;
        bool haveClear = false;
        float widestGap = float.NegativeInfinity;
        for (int i = 0; i < turns.Length; i++)
        {
            Vector3 e = Quaternion.AngleAxis(turns[i], Vector3.up) * exit;
            PartnerField.Assessment a = PartnerField.Assess(contact, e, f, s);
            bool safe = !human || a.result.closestToHuman >= HumanClearance;
            if (safe && i < 4 && Realises(intent, a, out _)) { result = a.result; return e; }
            if (safe && !haveClear) { haveClear = true; clear = e; clearResult = a.result; }
            if (a.result.closestToHuman > widestGap) { widestGap = a.result.closestToHuman; widest = e; widestResult = a.result; }
            if (i >= 3 && haveClear) break;
        }
        if (!haveClear && human)
        {
            PartnerField.Assessment push = PartnerField.Assess(contact, DeadBatPush, f, s);
            if (push.result.closestToHuman >= HumanClearance || push.result.closestToHuman > widestGap)
            {
                haveClear = true; clear = DeadBatPush; clearResult = push.result;
            }
        }
        result = haveClear ? clearResult : widestResult;
        return haveClear ? clear : widest;
    }

    /// Revalidate's turns, degrees about world up (a turn t moves a FieldSets angle by t x offSign):
    /// the shot as planned, the three nudges that may still give the intent - toward straight
    /// first - then the wider turns that only take it off the player, away from his head first.
    private static float[] Turns(Vector3 contact, Vector3 exit, in FieldSnapshot f)
    {
        float o = f.offSign < 0f ? -1f : 1f, angle = AngleOf(exit, f.offSign);
        float toStraight = -Mathf.Sign(angle) * o;
        float away = PartnerField.HasHuman(f) ? Mathf.Sign(angle - AngleOf(f.nonStrikerHead - contact, f.offSign)) * o : 1f;
        return new[]
        {
            0f, toStraight * NudgeDegrees, -toStraight * NudgeDegrees, toStraight * 2f * NudgeDegrees,
            away * 16f, -away * 16f, away * 30f, -away * 30f,
        };
    }

    /// Straight back down the pitch along the ground, slowly (ExitFor(0, -4, 10): either hand).
    private static Vector3 DeadBatPush => ExitFor(0f, -4f, 10f, 1f);

    private static ShotPlan Contact(ShotPlan p, PartnerShot shot, Vector3 exit, FieldResult pr, in FieldSnapshot f)
    {
        p.shot = shot;
        p.makesContact = true;
        p.exitVelocity = exit;
        p.batVelocity = BatVelocityFor(p.incoming, exit);
        p.kind = shot == PartnerShot.Defend ? BatContact.ContactKind.Good : BatContact.ContactKind.Middled;
        p.quality = shot == PartnerShot.Defend ? 0.8f : 0.95f;
        p.predicted = pr;
        p.wicket = !pr.caught ? PartnerWicket.None : pr.byFielder == f.keeperIndex ? PartnerWicket.CaughtBehind : PartnerWicket.Caught;
        return p;
    }

    /// A ball that would hit the stumps, kept out: the searched push, or straight back down the pitch.
    private static ShotPlan Defend(ShotPlan p, in ShotDirection d, in FieldSnapshot f, float offSign, List<BallFlight.Sample> s)
    {
        Vector3 exit = d.intent == 0 && d.exitVelocity.sqrMagnitude > 1f ? d.exitVelocity : ExitFor(0f, -4f, 10f, offSign);
        exit = Revalidate(0, p.contactPoint, Cap(exit, 0), f, s, out FieldResult pr);
        return Contact(p, PartnerShot.Defend, exit, pr, f);
    }

    /// The drive he means when he plays and misses, or edges it: to mid-off along the ground.
    public static Vector3 IntendedDrive(float offSign) => ExitFor(20f, -2f, 22f, offSign);

    /// No contact: left alone, or (near the stumps, sometimes) played and missed.
    private static ShotPlan NoShot(ShotPlan p, float offSign, System.Random rng)
    {
        bool near = Mathf.Abs(p.contactPoint.z) < 0.5f && p.contactPoint.y < 1.2f;
        p.shot = near && rng.NextDouble() < 0.4 ? PartnerShot.PlayAndMiss : PartnerShot.Leave;
        p.batVelocity = p.shot == PartnerShot.PlayAndMiss ? BatVelocityFor(p.incoming, IntendedDrive(offSign)) : Vector3.zero;
        p.predicted.runs = 0;
        return p;
    }

    private static ShotPlan PlanWicket(ShotPlan p, in ShotDirection d, in FieldSnapshot f, float offSign, System.Random rng, List<BallFlight.Sample> s, bool hits)
    {
        if (hits)
        {
            p.shot = PartnerShot.PlayAndMiss;
            p.wicket = PartnerWicket.Bowled;
            p.batVelocity = BatVelocityFor(p.incoming, IntendedDrive(offSign));
            p.predicted.runs = CareerSim.Wicket;
            return p;
        }
        if (d.valid && d.wicket != PartnerWicket.None)
        {
            Vector3 exit = Revalidate(CareerSim.Wicket, p.contactPoint, Cap(d.exitVelocity, CareerSim.Wicket), f, s, out FieldResult pr);
            if (pr.caught)
                return Contact(p, Family(p.foot, p.contactPoint.y, exit, offSign, false), exit, pr, f);
        }
        if (TryEdge(p.contactPoint, p.incoming, f, s, out Vector3 edge, out FieldResult er))
        {
            p = Contact(p, PartnerShot.Edge, edge, er, f);
            p.edge = true;
            p.kind = BatContact.ContactKind.ThinEdge;
            p.quality = 0.1f;
            p.batVelocity = BatVelocityFor(p.incoming, IntendedDrive(offSign));
            return p;
        }
        return NoShot(p, offSign, rng);
    }

    /// An edge carried to the keeper or the slips: close to the incoming line, slower than it.
    /// Deviation from the plan, which names only the keeper (keeperIndex): a catch by any fielder
    /// in that cone is taken - the slips and gully stand there to catch exactly these.
    private static bool TryEdge(Vector3 contact, Vector3 incoming, in FieldSnapshot f, List<BallFlight.Sample> s, out Vector3 exit, out FieldResult result)
    {
        float[] fractions = { 0.7f, EdgeSpeedMin, EdgeSpeedMax };
        float[] yaws = { 0f, 4f, -4f, 8f, -8f, 12f, -12f };
        float[] lifts = { 6f, 12f, 0f, -4f };
        Vector3 dir = incoming.normalized;
        Vector3 side = Vector3.Cross(dir, Vector3.up);
        bool human = PartnerField.HasHuman(f);
        foreach (float fr in fractions)
            foreach (float yaw in yaws)
                foreach (float lift in lifts)
                {
                    Vector3 e = Quaternion.AngleAxis(yaw, Vector3.up) * (Quaternion.AngleAxis(lift, side) * dir);
                    if (Vector3.Angle(e, dir) > EdgeCone) continue;
                    e = Vector3.ClampMagnitude(e * (incoming.magnitude * fr), SpeedCap(CareerSim.Wicket));
                    PartnerField.Assessment a = PartnerField.Assess(contact, e, f, s);
                    if (!a.result.caught || (human && a.result.closestToHuman < HumanClearance)) continue;
                    exit = e;
                    result = a.result;
                    return true;
                }
        exit = Vector3.zero;
        result = default;
        return false;
    }
}
