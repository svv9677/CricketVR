using UnityEngine;

/// The swing's keyframes (stance, top of the backlift, contact, finish) built from a plan, and
/// the pose between them at any time. Every piece starts where the one before it ends, with the
/// same velocity where it matters (the hands and the bat's turn through contact), so the pose is
/// continuous in t.
public static partial class PartnerSwing
{
    private struct Keys
    {
        public Frame fr;
        public BatGrip g;
        public float scale;
        public PartnerShot shot;
        public bool leave, front;
        // Times (contact 0): backlift start, downswing, follow-through; the body's moves.
        public float tB, D, T, hipsA, hipsB, frontA, frontB, backA, backB;
        // Body: stance (S), contact (C), finish (F).
        public Vector3 hipsS, hipsC, hipsF, headS, headC, headF, frontS, frontC, backS, backC;
        public float yawS, yawC, yawF;
        // Bat: the hands' grip point, and the bat's turn (about `axis`) through contact.
        public Vector3 gripLocal, handsS, handsTop, handsC, handsF, handsVel;
        public Quaternion rotS, rotTop, rotC;
        public Vector3 axis;
        public float theta, power, omega, carry, tauHands;
        // What he watches.
        public Vector3 lookS, ball, incoming, exitDir;

        public SwingPose At(float t)
        {
            BatAt(t, out Vector3 hands, out Quaternion rot);
            // Never through the turf on the way (the stance and the contact are above it already).
            float toeY = (hands + rot * ((g.toeLocal - gripLocal) * scale)).y;
            if (toeY < ToeY) hands.y += ToeY - toeY;
            Vector3 batPos = hands - rot * (gripLocal * scale);
            var s = new SwingPose { batPosition = batPos, batRotation = rot };
            s.topHand = batPos + rot * (g.topHandLocal * scale);
            s.bottomHand = batPos + rot * (g.bottomHandLocal * scale);
            float b = t < 0f ? Smooth((t - hipsA) / (hipsB - hipsA)) : Smooth(t / T);
            s.hips = t < 0f ? Vector3.Lerp(hipsS, hipsC, b) : Vector3.Lerp(hipsC, hipsF, b);
            s.head = t < 0f ? Vector3.Lerp(headS, headC, b) : Vector3.Lerp(headC, headF, b);
            s.hipsYaw = t < 0f ? Mathf.Lerp(yawS, yawC, b) : Mathf.Lerp(yawC, yawF, b);
            Vector3 frontFoot = Step(frontS, frontC, frontA, frontB, t);
            Vector3 backFoot = Step(backS, backC, backA, backB, t);
            s.leftFoot = fr.o > 0f ? frontFoot : backFoot;
            s.rightFoot = fr.o > 0f ? backFoot : frontFoot;
            s.look = Look(t);
            Reach(ref s);
            return s;
        }

        private void BatAt(float t, out Vector3 hands, out Quaternion rot)
        {
            if (leave)
            {
                float u = Smooth((t - tB) / (-0.5f * D - tB));
                hands = Vector3.Lerp(handsS, handsC, u);
                rot = Quaternion.Slerp(rotS, rotC, u);
                return;
            }
            if (t <= tB) { hands = handsS; rot = rotS; return; }
            if (t < -D)
            {
                float u = Smooth((t - tB) / (-D - tB));
                hands = Vector3.Lerp(handsS, handsTop, u);
                rot = Quaternion.Slerp(rotS, rotTop, u);
                return;
            }
            if (t < 0f)
            {
                float u = (t + D) / D;
                hands = Hermite(handsTop, Vector3.zero, handsC, handsVel * D, u);
                rot = Quaternion.AngleAxis(-theta * (1f - Mathf.Pow(u, power)), axis) * rotC;
                return;
            }
            float tau = Mathf.Min(t, T);
            float phi = carry * (1f - Mathf.Exp(-tau * omega / carry));
            rot = Quaternion.AngleAxis(phi, axis) * rotC;
            Vector3 coast = handsVel * tauHands;
            hands = handsC + coast * (1f - Mathf.Exp(-tau / tauHands)) + Smooth(tau / T) * (handsF - handsC - coast);
        }

        /// A foot moving between two places over [a, b], lifted a little on the way.
        private static Vector3 Step(Vector3 from, Vector3 to, float a, float b, float t)
        {
            float u = b > a ? Mathf.Clamp01((t - a) / (b - a)) : (t >= b ? 1f : 0f);
            Vector3 p = Vector3.Lerp(from, to, Smooth(u));
            if ((to - from).sqrMagnitude > 0.0025f) p.y += 0.06f * Mathf.Sin(Mathf.PI * u);
            return p;
        }

        /// The bowler (eyes level), then the ball in, then the ball away.
        private Vector3 Look(float t)
        {
            if (t < 0f)
            {
                Vector3 coming = ball + incoming * t;
                coming.y = Mathf.Max(0.05f, coming.y);
                return Vector3.Lerp(lookS, coming, Smooth((t - tB) / (-0.12f - tB)));
            }
            Vector3 away = leave || shot == PartnerShot.PlayAndMiss ? ball + incoming.normalized * 3f : ball + exitDir * 8f;
            return Vector3.Lerp(ball, away, Smooth(t / 0.4f));
        }

        /// The hands are never further than HandReach from the shoulders: move the body to them.
        private static void Reach(ref SwingPose s)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 sh = Shoulders(s.hips, s.head);
                Vector3 far = (s.topHand - sh).sqrMagnitude > (s.bottomHand - sh).sqrMagnitude ? s.topHand : s.bottomHand;
                float d = (far - sh).magnitude;
                if (d <= HandReach) return;
                Vector3 shift = (far - sh) * ((d - HandReach) / d);
                s.hips += shift;
                s.head += shift;
            }
        }
    }

    private static Keys Build(in ShotPlan p, in BatGrip g, float offSign, Vector3 stanceHips)
    {
        var k = new Keys
        {
            fr = new Frame(offSign), g = g, scale = g.scale > 0f ? g.scale : 1f, shot = p.shot,
            leave = p.shot == PartnerShot.Leave,
            front = p.foot == PartnerFoot.Front || p.shot == PartnerShot.Sweep,
            D = DownswingTime(p.shot), tB = BackliftStart(p), T = FollowThroughTime,
            gripLocal = 0.5f * (g.topHandLocal + g.bottomHandLocal),
            ball = p.contactPoint, incoming = p.incoming,
            exitDir = SafeNormal(p.exitVelocity, Vector3.left),
        };
        Timing(ref k);
        Stance(ref k, stanceHips);
        Footwork(ref k, p.contactPoint);
        if (k.leave) LeavePose(ref k);
        else
        {
            ContactPose(ref k, p);
            TopOfBacklift(ref k, p);
        }
        Finish(ref k);
        return k;
    }

    private static void Timing(ref Keys k)
    {
        k.hipsA = Mathf.Lerp(k.tB, -k.D, 0.25f);
        k.hipsB = -0.2f * k.D;
        if (k.front)
        {
            k.frontA = Mathf.Lerp(k.tB, -k.D, 0.35f);
            k.frontB = -0.45f * k.D;
            k.backA = k.backB = k.tB;
        }
        else
        {
            k.backA = k.tB;
            k.backB = Mathf.Lerp(k.tB, -k.D, 0.85f);
            k.frontA = Mathf.Lerp(k.tB, -k.D, 0.3f);
            k.frontB = -k.D;
        }
    }

    /// Side-on, the bat grounded behind the back foot leaning to the front thigh, face to the
    /// bowler; head over the off-stump line; eyes level down the pitch.
    private static void Stance(ref Keys k, Vector3 hips)
    {
        Frame fr = k.fr;
        k.hipsS = hips;
        k.frontS = new Vector3(hips.x - 0.26f, FootY, hips.z + 0.02f * fr.o);
        k.backS = new Vector3(hips.x + 0.24f, FootY, hips.z + 0.02f * fr.o);
        k.yawS = fr.o > 0f ? 0f : 180f;
        float offStump = fr.o * BallLedgerMath.StumpHalfWidth;
        k.headS = new Vector3(hips.x - 0.04f, hips.y + EyesAboveHips, Mathf.Clamp(offStump, hips.z - 0.25f, hips.z + 0.25f));
        k.lookS = new Vector3(BallDelivery.NominalRelease.x, k.headS.y, 0.6f);

        Vector3 toe = new Vector3(k.backS.x + 0.02f, ToeY, hips.z + 0.22f * fr.o);
        float length = ((k.gripLocal - k.g.toeLocal) * k.scale).magnitude;
        Vector3 lean = fr.F * 0.26f - fr.S * 0.04f;
        float rise = Mathf.Sqrt(Mathf.Max(0.01f, length * length - lean.sqrMagnitude));
        Vector3 along = (lean + fr.U * rise).normalized;
        k.rotS = Quaternion.LookRotation(along, Perpendicular(fr.F, along, fr.S));
        Vector3 batPos = toe - k.rotS * (k.g.toeLocal * k.scale);
        k.handsS = batPos + k.rotS * (k.gripLocal * k.scale);
        Crouch(ref k, batPos);
    }

    /// Bend the knees until the lower hand on the grounded bat is within reach: the stance hips
    /// and head come straight down (a taller stance than the bat allows would pull him off line).
    private static void Crouch(ref Keys k, Vector3 batPos)
    {
        Vector3 sh = Shoulders(k.hipsS, k.headS);
        Vector3 top = batPos + k.rotS * (k.g.topHandLocal * k.scale), bottom = batPos + k.rotS * (k.g.bottomHandLocal * k.scale);
        Vector3 v = ((top - sh).sqrMagnitude > (bottom - sh).sqrMagnitude ? top : bottom) - sh;
        float room = HandReach - 0.02f;
        if (v.magnitude <= room) return;
        float disc = v.y * v.y - v.sqrMagnitude + room * room;
        float drop = Mathf.Clamp(disc >= 0f ? -v.y - Mathf.Sqrt(disc) : -v.y, 0f, 0.2f);
        k.hipsS.y -= drop;
        k.headS.y -= drop;
        k.lookS.y = k.headS.y;
    }

    /// The feet and the hips at contact: forward to the ball, or back and across.
    private static void Footwork(ref Keys k, Vector3 ball)
    {
        Frame fr = k.fr;
        if (k.front)
        {
            float across = Mathf.Clamp(((ball.z - 0.25f * fr.o) - k.frontS.z) * fr.o, -0.35f, 0.55f);
            float stride = Mathf.Clamp(k.frontS.x - (ball.x + 0.15f), 0.15f, 0.75f);
            k.frontC = new Vector3(k.frontS.x - stride, FootY, k.frontS.z + across * fr.o);
            k.backC = k.backS;
            float drop = k.shot == PartnerShot.Sweep ? 0.38f : 0.10f;
            k.hipsC = new Vector3(Mathf.Lerp(k.frontC.x, k.backC.x, 0.45f), k.hipsS.y - drop, Mathf.Lerp(k.hipsS.z, k.frontC.z, 0.6f));
        }
        else
        {
            float across = Mathf.Clamp((ball.z - k.backS.z) * fr.o - 0.35f, -0.10f, 0.30f);
            k.backC = new Vector3(Mathf.Min(k.backS.x + 0.14f, BallDelivery.BatsmanStumpsX - 0.30f), FootY, k.backS.z + across * fr.o);
            k.frontC = new Vector3(k.frontS.x + 0.10f, FootY, k.frontS.z + across * 0.6f * fr.o);
            k.hipsC = new Vector3(Mathf.Lerp(k.frontC.x, k.backC.x, 0.6f), k.hipsS.y + 0.03f, Mathf.Lerp(k.hipsS.z, k.backC.z, 0.7f));
        }
        if (k.leave)
        {
            k.frontC = k.frontS;
            k.backC = k.backS;
            bool duck = ball.y > 1.0f;
            k.hipsC = duck ? k.hipsS - fr.U * 0.35f - fr.F * 0.05f : k.hipsS + fr.U * 0.02f;
        }
        float toBall = k.leave ? 0f : Mathf.Clamp(ball.z - k.hipsC.z, -0.2f, 0.2f);
        Vector3 lean = k.leave ? fr.F * 0.08f : k.front ? fr.F * 0.35f : fr.F * 0.06f;
        bool bend = k.front && !k.leave;
        k.headC = k.hipsC + fr.U * (EyesAboveHips - (bend ? 0.08f : 0f)) + lean + new Vector3(0f, 0f, 0.6f * toBall);
        Open(k.shot, out float open, out float openEnd);
        k.yawC = k.yawS - fr.o * open;
        k.yawF = k.yawS - fr.o * openEnd;
    }

    /// Shoulder arms (the bat high, blade up behind him), or under a high one with it down.
    private static void LeavePose(ref Keys k)
    {
        Frame fr = k.fr;
        bool duck = k.ball.y > 1.0f;
        k.handsC = duck ? k.hipsC + fr.U * 0.10f + fr.S * 0.15f + fr.F * 0.10f
                        : k.hipsC + fr.U * 0.62f + fr.S * 0.10f - fr.F * 0.12f;
        // Ducking, the blade points down and forward off his front pad; shoulder arms, up behind him.
        Vector3 along = duck ? (fr.U * 0.55f - fr.F * 0.75f - fr.S * 0.30f).normalized : (-fr.U * 0.8f + fr.F * 0.5f).normalized;
        k.rotC = Quaternion.LookRotation(along, Perpendicular(fr.S, along, fr.F));
        Vector3 toe = k.handsC + k.rotC * ((k.g.toeLocal - k.gripLocal) * k.scale);
        if (toe.y < ToeY + 0.03f) k.handsC.y += ToeY + 0.03f - toe.y;
        k.rotTop = k.rotC;
        k.axis = fr.S;
    }

    /// The bat turned within its face's plane (the face stays on the shot) until its toe is off the
    /// ground: a ball met low is met with the bat angled, not with the toe in the turf.
    private static Vector3 ClearOfTheGround(Vector3 along, Vector3 face, Vector3 target, in Keys k)
    {
        float toeBelow = Mathf.Max(1e-3f, ((k.g.sweetLocal - k.g.toeLocal) * k.scale).magnitude);
        float allowed = (target.y - ToeY - 0.01f) / toeBelow;   // the most along.y may be
        if (along.y <= allowed) return along;
        Vector3 up = Perpendicular(Vector3.up, face, along);
        Vector3 side = Vector3.Cross(face, up).normalized;
        float s = Vector3.Dot(along, side) >= 0f ? 1f : -1f;
        float c = Mathf.Clamp(allowed / Mathf.Max(1e-3f, up.y), -1f, 1f);
        return (up * c + side * (s * Mathf.Sqrt(1f - c * c))).normalized;
    }

    /// The sweet spot on the contact point (inside it for a miss or an edge), the face along the
    /// exit, the handle toward where the hands want to be; then the body brought within reach.
    private static void ContactPose(ref Keys k, in ShotPlan p)
    {
        Frame fr = k.fr;
        Vector3 inward = k.hipsC - k.ball;
        inward.y = 0f;
        Vector3 flatIn = new Vector3(k.incoming.x, 0f, k.incoming.z);
        inward = SafeNormal(Vector3.ProjectOnPlane(inward, SafeNormal(flatIn, Vector3.right)), -fr.S);
        bool meant = p.shot == PartnerShot.PlayAndMiss || p.shot == PartnerShot.Edge;
        Vector3 target = k.ball + inward * (p.shot == PartnerShot.PlayAndMiss ? MissInside : p.shot == PartnerShot.Edge ? EdgeInside : 0f);
        Vector3 intended = PartnerShotPlanner.ExitForBat(p.incoming, p.batVelocity);
        Vector3 face = meant ? SafeNormal(intended, PartnerShotPlanner.IntendedDrive(fr.o).normalized)
                             : SafeNormal(p.exitVelocity, fr.F);
        Vector3 want = HandsAtContact(p.shot, k.hipsC, fr);
        Vector3 along = ClearOfTheGround(Perpendicular(want - target, face, fr.U), face, target, k);
        k.rotC = Quaternion.LookRotation(along, face);
        Vector3 batPos = target - k.rotC * (k.g.sweetLocal * k.scale);
        k.handsC = batPos + k.rotC * (k.gripLocal * k.scale);
        Vector3 top = batPos + k.rotC * (k.g.topHandLocal * k.scale), bottom = batPos + k.rotC * (k.g.bottomHandLocal * k.scale);
        for (int i = 0; i < 4; i++)
        {
            Vector3 sh = Shoulders(k.hipsC, k.headC);
            Vector3 far = (top - sh).sqrMagnitude > (bottom - sh).sqrMagnitude ? top : bottom;
            float d = (far - sh).magnitude, room = HandReach - 0.03f;
            if (d <= room) break;
            Vector3 shift = (far - sh) * ((d - room) / d);
            shift.y = Mathf.Clamp(shift.y, -0.30f, 0.10f);
            k.hipsC += shift;
            k.headC += shift;
        }
    }

    /// The top of the backlift is the contact pose turned back by Backlift degrees about the axis
    /// that swings the sweet spot along the bat velocity; the turn gathers pace (u^power) so the
    /// sweet spot reaches contact at the planned speed, the hands making up the rest.
    private static void TopOfBacklift(ref Keys k, in ShotPlan p)
    {
        Frame fr = k.fr;
        Vector3 r = k.rotC * ((k.g.sweetLocal - k.gripLocal) * k.scale);
        Vector3 dir = SafeNormal(p.batVelocity, SafeNormal(p.exitVelocity, fr.F));
        float speed = Mathf.Clamp(p.batVelocity.magnitude, MinBatSpeed, MaxBatSpeed);
        Vector3 axis = Vector3.Cross(r, dir);
        if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(r, fr.F);
        k.axis = SafeNormal(axis, fr.S);
        k.theta = Backlift(p.shot);
        k.rotTop = Quaternion.AngleAxis(-k.theta, k.axis) * k.rotC;
        float lever = Mathf.Max(0.05f, (r - dir * Vector3.Dot(dir, r)).magnitude);
        float thetaRad = k.theta * Mathf.Deg2Rad;
        k.power = Mathf.Clamp(0.8f * speed * k.D / (thetaRad * lever), 1.6f, 4f);
        float omegaRad = thetaRad * k.power / k.D;
        k.omega = k.theta * k.power / k.D;
        k.handsVel = dir * speed - Vector3.Cross(k.axis * omegaRad, r);
        k.handsTop = HandsAtTop(p.shot, k, fr);
    }

    private static void Finish(ref Keys k)
    {
        Frame fr = k.fr;
        k.hipsF = k.front && !k.leave ? k.hipsC + fr.F * 0.06f : k.hipsC;
        k.headF = k.headC + fr.U * 0.03f;
        k.carry = k.leave ? 1f : Carry(k.shot);
        k.tauHands = Mathf.Clamp(0.25f / Mathf.Max(k.handsVel.magnitude, 0.1f), 0.02f, 0.08f);
        k.handsF = k.leave ? k.handsC
            : k.shot == PartnerShot.Defend ? k.handsC + k.handsVel * k.tauHands
            : HandsAtFinish(k.shot, k.hipsF, fr);
    }
}
