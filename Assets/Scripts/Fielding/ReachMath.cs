using UnityEngine;

/// <summary>
/// The pure maths behind <see cref="HumanoidReach"/>, kept free of Unity objects so EditMode tests
/// can drive it (Assets/Editor/Tests/HumanoidReachTests.cs).
///
/// A reach is spread over a chain of joints with different stiffness, the way a person picks a
/// ball up: the knees are soft and take most of the height (a squat drops the pelvis up to
/// ~0.45 m), the hips hinge a moderate amount (up to ~60 degrees) and the spine is stiff (at most
/// ~25 degrees, spread over three bones), so the body crouches and hinges instead of folding the
/// back. One "effort" value drives every joint: joint j deflects min(1, effort / stiffness_j) of
/// its limit, and the solver finds the least effort that brings the shoulders within arm's reach.
/// </summary>
public static class ReachMath
{
    // ---- Critically damped springs ----------------------------------------------------------
    // Exact solution of x'' = -w^2 (x - target) - 2 w x', so it never overshoots or pops and is
    // frame-rate independent (two half steps equal one full step). Settles to ~2% in 4 / w
    // seconds: w 10 -> 0.4 s.

    public static float Spring(float x, ref float velocity, float target, float omega, float dt)
    {
        if (dt <= 0f)
            return x;
        float y = x - target;
        float tmp = (velocity + omega * y) * dt;
        float decay = Mathf.Exp(-omega * dt);
        velocity = (velocity - omega * tmp) * decay;
        return target + (y + tmp) * decay;
    }

    public static Vector3 Spring(Vector3 x, ref Vector3 velocity, Vector3 target, float omega, float dt)
    {
        float vx = velocity.x, vy = velocity.y, vz = velocity.z;
        var r = new Vector3(Spring(x.x, ref vx, target.x, omega, dt),
                            Spring(x.y, ref vy, target.y, omega, dt),
                            Spring(x.z, ref vz, target.z, omega, dt));
        velocity = new Vector3(vx, vy, vz);
        return r;
    }

    // ---- The body ---------------------------------------------------------------------------

    /// Measured from the rig at start-up (metres, character space, feet at y 0).
    public struct Body
    {
        public float hipHeight;     // standing pelvis height
        public float torsoLength;   // pelvis to the shoulder line
        public float armReach;      // shoulder to palm, less a little so the elbow never locks
        public float shoulderWidth; // between the two shoulders

        public static Body Default => new Body { hipHeight = 0.95f, torsoLength = 0.52f, armReach = 0.62f, shoulderWidth = 0.36f };
    }

    /// Stiffness and range of each joint. Lower stiffness = deflects sooner.
    public struct Limits
    {
        public float maxDrop, maxHinge, maxSpine;       // metres, degrees, degrees
        public float kneeStiffness, hipStiffness, spineStiffness;
        public float maxShift, maxTwist, maxRoll;       // lateral weight shift (m), chest turn, side lean (deg)

        public static Limits Default => new Limits
        {
            maxDrop = 0.45f, maxHinge = 60f, maxSpine = 25f,
            kneeStiffness = 0.6f, hipStiffness = 1.0f, spineStiffness = 2.0f,
            maxShift = 0.28f, maxTwist = 35f, maxRoll = 12f,
        };

        /// Effort at which every joint is at its limit.
        public float MaxEffort => Mathf.Max(kneeStiffness, Mathf.Max(hipStiffness, spineStiffness));
    }

    /// How the body is posed for a reach. Angles in degrees.
    public struct Pose
    {
        public float drop;     // pelvis lowered by the knees
        public float hinge;    // forward pitch at the hips
        public float spine;    // extra forward flex spread over the spine bones
        public float shift;    // pelvis moved sideways (+ right), over the leg on that side
        public float twist;    // chest turned toward the target (+ right)
        public float roll;     // side lean toward the target (+ right)

        public static Pose Lerp(Pose a, Pose b, float t) => new Pose
        {
            drop = Mathf.Lerp(a.drop, b.drop, t), hinge = Mathf.Lerp(a.hinge, b.hinge, t),
            spine = Mathf.Lerp(a.spine, b.spine, t), shift = Mathf.Lerp(a.shift, b.shift, t),
            twist = Mathf.Lerp(a.twist, b.twist, t), roll = Mathf.Lerp(a.roll, b.roll, t),
        };
    }

    /// Fraction of its range a joint gives for an effort.
    public static float Deflection(float effort, float stiffness) =>
        stiffness <= 0f ? 1f : Mathf.Clamp01(effort / stiffness);

    /// The sagittal pose (drop, hinge, spine) for one effort value.
    public static Pose PoseForEffort(float effort, Limits l) => new Pose
    {
        drop = l.maxDrop * Deflection(effort, l.kneeStiffness),
        hinge = l.maxHinge * Deflection(effort, l.hipStiffness),
        spine = l.maxSpine * Deflection(effort, l.spineStiffness),
    };

    /// How far the hips sit back to keep balance over the feet when hinging: a quarter of the
    /// torso's forward swing keeps the centre of mass over the feet without losing much reach.
    public static float HipsBack(Body b, float hingeDeg) => 0.25f * b.torsoLength * Mathf.Sin(hingeDeg * Mathf.Deg2Rad);

    /// Shoulder-line centre in the sagittal plane, (forward, height), for a pose. The torso is two
    /// halves: the lower pitches with the hip hinge, the upper with hinge + spine flex.
    public static Vector2 Shoulder(Body b, Pose p)
    {
        float h = p.hinge * Mathf.Deg2Rad, s = (p.hinge + p.spine) * Mathf.Deg2Rad;
        float half = b.torsoLength * 0.5f;
        var hip = new Vector2(-HipsBack(b, p.hinge), b.hipHeight - p.drop);
        return hip + half * new Vector2(Mathf.Sin(h), Mathf.Cos(h)) + half * new Vector2(Mathf.Sin(s), Mathf.Cos(s));
    }

    public static bool InReach(Body b, Pose p, float forward, float height) =>
        (Shoulder(b, p) - new Vector2(forward, height)).sqrMagnitude <= b.armReach * b.armReach;

    /// Least-effort crouch + hinge + flex that brings a target (forward, height above the feet)
    /// within arm's reach. Saturates at every limit when the target cannot be reached.
    public static Pose SolveSagittal(Body b, Limits l, float forward, float height)
    {
        if (InReach(b, default, forward, height))
            return default;
        float hi = l.MaxEffort;
        if (!InReach(b, PoseForEffort(hi, l), forward, height))
            return PoseForEffort(hi, l);
        float lo = 0f;
        for (int i = 0; i < 18; i++)   // 2^-18 of the range: far below a millimetre
        {
            float mid = 0.5f * (lo + hi);
            if (InReach(b, PoseForEffort(mid, l), forward, height)) hi = mid; else lo = mid;
        }
        return PoseForEffort(hi, l);
    }

    /// Full reach pose for a target in character space (x right, y up from the feet, z forward).
    /// Sideways targets are met by shifting the weight over that leg first, then turning the chest
    /// (moderate), then leaning (stiff); the rest is the sagittal crouch toward it.
    public static Pose Solve(Body b, Limits l, Vector3 target)
    {
        float shift = Mathf.Clamp(target.x * 0.6f, -l.maxShift, l.maxShift);
        float x = target.x - shift;
        float forward = Mathf.Max(0.15f, target.z);
        float twist = Mathf.Clamp(Mathf.Atan2(x, forward) * Mathf.Rad2Deg, -l.maxTwist, l.maxTwist);
        float along = Mathf.Sqrt(x * x + forward * forward);
        Pose p = SolveSagittal(b, l, along, target.y);
        p.shift = shift;
        p.twist = twist;
        // Lean sideways only for what the turn could not cover, and only when reaching down.
        float residual = x - Mathf.Tan(twist * Mathf.Deg2Rad) * forward;
        float low = Mathf.InverseLerp(b.hipHeight + 0.3f, 0.2f, target.y);
        p.roll = Mathf.Clamp(residual * 40f * low, -l.maxRoll, l.maxRoll);
        return p;
    }

    /// Share of the spine flex given to Spine, Chest and UpperChest: the lower back is the
    /// stiffest-looking when bent, so the flex is spread with slightly more low.
    public static readonly float[] SpineShare = { 0.4f, 0.35f, 0.25f };

    // ---- Gathering ----------------------------------------------------------------------------

    /// Closest distance from point p to the segment a-b (the ball's path over one frame, so a
    /// fast ball cannot tunnel between the hands).
    public static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 d = b - a;
        float len2 = d.sqrMagnitude;
        float t = len2 > 1e-10f ? Mathf.Clamp01(Vector3.Dot(p - a, d) / len2) : 0f;
        return Vector3.Distance(a + d * t, p);
    }

    /// The residual gap between where the ball was and the hands when it was gathered, faded out
    /// over `duration` so the ball settles into the hands instead of teleporting.
    public static Vector3 Residual(Vector3 offset, float elapsed, float duration)
    {
        if (duration <= 0f || elapsed >= duration)
            return Vector3.zero;
        float t = Mathf.Clamp01(elapsed / duration);
        return offset * (1f - t * t * (3f - 2f * t));   // smoothstep ease-out
    }

    // ---- Keeper footwork ------------------------------------------------------------------------

    /// Move a lateral position toward a goal with a top speed and an acceleration, stopping on
    /// the goal without overshoot. Returns the new position; velocity is updated.
    public static float Shuffle(float x, ref float velocity, float goal, float maxSpeed, float accel, float dt)
    {
        float to = goal - x;
        float stopping = Mathf.Sqrt(2f * accel * Mathf.Abs(to));
        float want = Mathf.Sign(to) * Mathf.Min(maxSpeed, stopping);
        velocity = Mathf.MoveTowards(velocity, want, accel * dt);
        float step = velocity * dt;
        if (Mathf.Abs(step) > Mathf.Abs(to) && Mathf.Sign(step) == Mathf.Sign(to))
        {
            velocity = 0f;
            return goal;
        }
        return x + step;
    }

    /// Height of a foot during a step, 0..1 of the step: a sine arc, zero at lift and plant.
    public static float StepArc(float t, float height) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * height;
}
