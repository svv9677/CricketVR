using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The field's answer to a shot, without running the fielders: the planner's model of the live
/// fielding (AnimatedFielderManagement, KeeperCatcher) and of HUD's scoring, so a direction chosen
/// here comes out the same way in play. Pure: BallFlight from the contact, each fielder's reach
/// over time, HUD's bands.
///
///   * A fielder moves `delay` after the hit (SeeingDelay + ReactionTime), then runs, accelerating
///     at AnimatedFielder.Acceleration up to his speed, and has it once he is within CatchRadius of
///     the ball while it is no higher than CatchHeight. The first moment anyone has it is where it
///     is fielded: a catch if it has not bounced since the bat.
///   * The keeper does not run: he takes what crosses his glove plane (KeeperHandsAhead in front
///     of him) within his reach. Until he has read the deflection (KeeperReaction) only what comes
///     to his gloves; then he shuffles across after it.
///   * Over the rope: 6 on the full, else 4. Fielded: runs by how long it took (HUD's bands, the
///     time from BallHit to BallFielded).
///   * A dive (AnimatedFielderDive) adds DiveReach. A planned boundary or run must be safe from a
///     dive too and a planned catch must not need one; those margins are in the Assessment.
/// One Assess is one BallFlight run with the field checked at every step as it goes (no list of
/// samples is scanned afterwards). Main thread only: the scan's state is static.
/// </summary>
public static class PartnerField
{
    /// Mirrors AnimatedFielderDive's private dive reach.
    public const float DiveReach = 1.5f;
    /// The step and the longest a hit is followed for: the fielders' own prediction step.
    public const float Step = 0.02f, MaxTime = 14f;
    /// HUD's run bands: fielded within 2 s is a dot, 4.5 one, 7.5 two, 11 three, after that four.
    public static readonly float[] RunBands = { 2f, 4.5f, 7.5f, 11f };
    /// The keeper (KeeperCatcher's private constants): his glove plane in front of him, how long a
    /// deflection takes him to read, how far and fast he shuffles, the reach of the gloves where
    /// they already are, and how much further a dive takes them.
    public const float KeeperHandsAhead = 0.5f, KeeperReaction = 0.22f, KeeperShuffle = 1.6f, KeeperShuffleSpeed = 3f;
    public const float KeeperGlove = 0.45f, KeeperDive = 1.0f;
    /// closestToHuman when there is nobody at the other end.
    public const float NoHuman = 999f;

    /// What a shot does, with the margins the planner scores it by.
    public struct Assessment
    {
        public FieldResult result;
        /// It reached the rope, and how high it was crossing it.
        public bool boundary;
        public float ropeHeight;
        /// Metres the nearest fielder is short of the ball even diving, over the path anyone could
        /// field it on (no higher than CatchHeight, up to where it was fielded): > 0 nobody gets a
        /// hand to it. airDiveMiss: the same before it first bounces (> 0: no catch, even diving).
        public float diveMiss, airDiveMiss;
        /// When a diving fielder would first stop it (fieldedAt if nobody could dive sooner).
        public float diveFieldedAt;
        /// For a catch: metres inside his reach the catcher is, and the ball's height.
        public float catchInside, catchHeight;
    }

    public static bool HasHuman(in FieldSnapshot f) => f.nonStrikerHead != Vector3.zero;

    /// Seconds for a fielder at `from` to get his hands to `to`: the delay, then running at up to
    /// runSpeed, needing to come within CatchRadius.
    public static float ReachTime(Vector3 from, Vector3 to, float runSpeed, float delay)
    {
        float d = Flat(to - from) - AnimatedFielderManagement.CatchRadius;
        if (d <= 0f) return delay;
        float v = Mathf.Max(0.5f, runSpeed), a = AnimatedFielder.Acceleration;
        float accelDistance = 0.5f * v * v / a;
        return delay + (d <= accelDistance ? Mathf.Sqrt(2f * d / a) : v / a + (d - accelDistance) / v);
    }

    /// How far a fielder has run `t` seconds after he set off (the inverse of ReachTime's run).
    public static float RunDistance(float t, float runSpeed)
    {
        if (t <= 0f) return 0f;
        float v = Mathf.Max(0.5f, runSpeed), a = AnimatedFielder.Acceleration, accelTime = v / a;
        return t < accelTime ? 0.5f * a * t * t : 0.5f * v * accelTime + v * (t - accelTime);
    }

    /// Runs for a ball fielded this long after the hit (HUD's bands).
    public static int RunsFor(float fieldedAt)
    {
        for (int i = 0; i < RunBands.Length; i++)
            if (fieldedAt < RunBands[i]) return i;
        return RunBands.Length;
    }

    public static FieldResult Evaluate(Vector3 contact, Vector3 exit, in FieldSnapshot f, List<BallFlight.Sample> scratch) =>
        Assess(contact, exit, f, scratch).result;

    /// Follow the hit from `contact` at `exit` until it is fielded, crosses the rope or stops.
    public static Assessment Assess(Vector3 contact, Vector3 exit, in FieldSnapshot f, List<BallFlight.Sample> scratch)
    {
        Scan s = scan;
        s.Begin(contact, f);
        BallFlight.Simulate(contact, exit, BallFlight.DeliveryEffects.None, MaxTime, scratch ?? fallback, s.Stop, Step);
        if (!s.done)
            s.Stopped();
        return s.a;
    }

    private static readonly Scan scan = new Scan();
    private static readonly List<BallFlight.Sample> fallback = new List<BallFlight.Sample>(1024);

    private static float Flat(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);

    /// Distance from `p` to the segment a-b.
    public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float l = ab.sqrMagnitude;
        float t = l > 1e-9f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l) : 0f;
        return (a + ab * t - p).magnitude;
    }

    /// The field checked at every step of the flight (BallFlight's stop callback).
    private sealed class Scan
    {
        public Assessment a;
        public bool done;
        public readonly System.Func<BallFlight.Sample, bool> Stop;
        private FieldSnapshot f;
        private Vector3 prev;
        private bool human, diveSeen;

        public Scan() { Stop = Check; }

        public void Begin(Vector3 contact, in FieldSnapshot snapshot)
        {
            f = snapshot;
            prev = contact;
            human = HasHuman(snapshot);
            done = diveSeen = false;
            lastTime = 0f;
            a = new Assessment
            {
                result = new FieldResult { byFielder = -1, closestToHuman = NoHuman },
                diveMiss = NoHuman, airDiveMiss = NoHuman,
            };
        }

        private bool Check(BallFlight.Sample s)
        {
            Vector3 p = s.position;
            if (human)
                a.result.closestToHuman = Mathf.Min(a.result.closestToHuman, SegmentDistance(prev, p, f.nonStrikerHead));
            if (Rope(s) || Keeper(s) || Fielders(s))
                done = true;
            prev = p;
            lastTime = s.time;
            return done;
        }

        private bool Rope(BallFlight.Sample s)
        {
            float b = f.boundary, here = Flat(s.position);
            if (b <= 0f || here <= b) return false;
            float before = Flat(prev);
            float k = here - before > 1e-5f ? Mathf.Clamp01((b - before) / (here - before)) : 1f;
            a.boundary = true;
            a.ropeHeight = Mathf.Lerp(prev.y, s.position.y, k);
            a.result.runs = s.bounced ? 4 : 6;
            a.result.fieldedAt = s.time;
            if (!diveSeen) a.diveFieldedAt = s.time;
            return true;
        }

        /// The keeper's glove plane, crossed between the last step and this one.
        private bool Keeper(BallFlight.Sample s)
        {
            int k = f.keeperIndex;
            if (f.fielders == null || k < 0 || k >= f.fielders.Length) return false;
            Vector3 keeper = f.fielders[k];
            float plane = keeper.x - KeeperHandsAhead;
            Vector3 p = s.position;
            if (!(prev.x < plane && p.x >= plane)) return false;
            float u = Mathf.Clamp01((plane - prev.x) / Mathf.Max(1e-5f, p.x - prev.x));
            float t = s.time - Step * (1f - u);
            Vector3 at = Vector3.Lerp(prev, p, u);
            float reach = t < KeeperReaction ? KeeperGlove
                : KeeperCatcher.Reach + Mathf.Min(KeeperShuffle, KeeperShuffleSpeed * (t - KeeperReaction));
            float miss = Mathf.Abs(at.z - keeper.z) - reach;
            if (at.y > KeeperCatcher.ReachHeight) return false;
            Note(miss - KeeperDive, s.bounced, t);
            if (miss > 0f) return false;
            Take(k, t, !s.bounced, -miss, at.y);
            return true;
        }

        private bool Fielders(BallFlight.Sample s)
        {
            Vector3 p = s.position;
            if (f.fielders == null || p.y > AnimatedFielderManagement.CatchHeight) return false;
            int best = -1;
            float bestInside = 0f;
            for (int j = 0; j < f.fielders.Length; j++)
            {
                if (j == f.keeperIndex) continue;
                float speed = f.runSpeed != null && j < f.runSpeed.Length ? f.runSpeed[j] : AnimatedFielder.BaseRunSpeed;
                float miss = Flat(p - f.fielders[j]) - AnimatedFielderManagement.CatchRadius - RunDistance(s.time - f.delay, speed);
                Note(miss - DiveReach, s.bounced, s.time);
                if (miss <= 0f && (best < 0 || -miss > bestInside)) { best = j; bestInside = -miss; }
            }
            if (best < 0) return false;
            Take(best, s.time, !s.bounced, bestInside, p.y);
            return true;
        }

        private void Note(float diveMiss, bool bounced, float t)
        {
            a.diveMiss = Mathf.Min(a.diveMiss, diveMiss);
            if (!bounced) a.airDiveMiss = Mathf.Min(a.airDiveMiss, diveMiss);
            if (diveMiss <= 0f && !diveSeen) { diveSeen = true; a.diveFieldedAt = t; }
        }

        private void Take(int fielder, float t, bool caught, float inside, float height)
        {
            a.result.byFielder = fielder;
            a.result.fieldedAt = t;
            a.result.caught = caught;
            a.result.runs = caught ? CareerSim.Wicket : RunsFor(t);
            a.catchInside = caught ? inside : 0f;
            a.catchHeight = height;
            if (!diveSeen) a.diveFieldedAt = t;
        }

        /// It stopped (or ran out of time) with nobody there: the nearest fielder walks to it.
        public void Stopped()
        {
            float when = float.MaxValue;
            int who = -1;
            if (f.fielders != null)
                for (int j = 0; j < f.fielders.Length; j++)
                {
                    if (j == f.keeperIndex) continue;
                    float speed = f.runSpeed != null && j < f.runSpeed.Length ? f.runSpeed[j] : AnimatedFielder.BaseRunSpeed;
                    float t = ReachTime(f.fielders[j], prev, speed, f.delay);
                    if (t < when) { when = t; who = j; }
                }
            a.result.byFielder = who;
            a.result.fieldedAt = Mathf.Max(lastTime, who >= 0 ? when : MaxTime);
            a.result.runs = RunsFor(a.result.fieldedAt);
            if (!diveSeen) a.diveFieldedAt = a.result.fieldedAt;
            done = true;
        }

        private float lastTime;
    }
}
