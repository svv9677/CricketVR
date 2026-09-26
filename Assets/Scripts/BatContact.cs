using UnityEngine;

/// <summary>
/// Bat-ball contact: finding the contact (a continuous sweep) and what the ball does next.
/// Pure maths on poses and positions - no Unity physics - so the game (Bat) and the scenario
/// tests (Assets/Editor/Tests/BatContactTests) run exactly the same code.
///
/// Finding it. The ball's path over a frame is expressed in the bat's own frame and tested
/// against the blade box grown by the ball's radius. A fast swing turns the bat through a large
/// angle in one frame, and the ball's path relative to a turning bat is a curve, so the frame is
/// cut into sub-steps (enough that the bat turns at most MaxDegreesPerSubstep and its toe moves at
/// most MaxToeTravelPerSubstep in each) and each is tested as a straight segment. The earliest
/// hit wins. A contact therefore cannot slip between samples at any swing or ball speed.
///
/// What happens. u = ball velocity - bat velocity at the contact point. The bat is a body of
/// effective mass M at that point, so the ball gets M/(m+M) of what an immovable bat would give:
/// -(1+e) of the normal part, and a friction loss on the tangential part. Where on the bat decides
/// e, M and the normal:
///   * along the blade  e 0.52 -> 0.25 and M 0.75 -> 0.30 kg, smoothly, with distance from the
///     sweet spot - reaching the floor at the toe on one side and 0.30 m up toward the shoulder on
///     the other; the handle is dead;
///   * across the blade the blade twists about its long axis: 1/M += x^2 / I_twist, with I_twist
///     from the blade's real width and thickness, and e falls off toward the edges;
///   * edges            the face bevels over the outer 20% of the width (normal tilts up to 50
///     degrees sideways - a thick edge); a hit on the side of the blade is a thin edge;
///   * back / toe       weaker (e x0.7, e 0.2).
/// Head on at the sweet spot this is v_out = q v_in + (1+q) v_bat with q ~ 0.24.
/// </summary>
public static class BatContact
{
    public const float BatMass = 1.2f;
    public const float MaxDegreesPerSubstep = 3f;
    public const float MaxToeTravelPerSubstep = 0.04f;
    public const int MaxSubsteps = 32;
    /// From the sweet spot toward the shoulder, the distance (m) over which a hit goes dead.
    public const float SweetSpotFalloff = 0.30f;

    /// The blade, in the bat's local units (see BatGeometry).
    public struct Blade
    {
        public float toeZ, shoulderZ, sweetZ, halfWidth, faceY, backY;
    }

    public struct Hit
    {
        /// Fraction of the frame at which they met.
        public float t;
        /// Ball centre and the touched point on the bat, bat-local, at contact.
        public Vector3 localBall, localBat;
        /// Outward surface normal at the touch, bat-local: +Y on the face, rounding over the edges.
        public Vector3 localNormal;
        /// Box face first entered: 0 = side (x), 1 = face/back (y), 2 = toe/top (z); outward sign.
        public int axis;
        public float sign;
        /// Bat pose at contact.
        public Vector3 position;
        public Quaternion rotation;
    }

    public struct Result
    {
        public Vector3 velocity;
        /// 1 = middled, falling toward 0 off the sweet spot and toward the edges.
        public float quality;
        public bool edge;
        public float restitution, effectiveMass;
    }

    /// <summary>
    /// Sweep one frame. The bat moves from pose A to pose B (uniform scale), the ball from ballA to
    /// ballB. widthMultiplier widens the blade (difficulty). False when they do not meet.
    /// </summary>
    /// <param name="pivotLocal">Where the bat is held, bat-local (the hands). Between frames the bat
    /// is moved as a real swing moves it: the hands in a straight line while the bat turns about them,
    /// so the blade sweeps its true arc. Interpolating the bat's centre instead cuts across the arc -
    /// 9 cm off at a 40 m/s swing and 36 fps.</param>
    public static bool Sweep(Vector3 posA, Quaternion rotA, Vector3 posB, Quaternion rotB, float scale,
                             Vector3 ballA, Vector3 ballB, Blade blade, float widthMultiplier,
                             float ballRadius, Vector3 pivotLocal, out Hit hit)
    {
        hit = default;
        float r = ballRadius / scale;
        Vector3 min = new Vector3(-blade.halfWidth * widthMultiplier - r, blade.backY - r, blade.toeZ - r);
        Vector3 max = new Vector3(blade.halfWidth * widthMultiplier + r, blade.faceY + r, blade.shoulderZ);
        Vector3 pivot = pivotLocal * scale;
        Vector3 pivotA = posA + rotA * pivot, pivotB = posB + rotB * pivot;

        int n = Substeps(posA, rotA, posB, rotB, scale, blade);
        for (int k = 0; k < n; k++)
        {
            float ta = (float)k / n, tb = (float)(k + 1) / n;
            Quaternion qa = Quaternion.Slerp(rotA, rotB, ta), qb = Quaternion.Slerp(rotA, rotB, tb);
            Vector3 pa = Vector3.Lerp(pivotA, pivotB, ta) - qa * pivot;
            Vector3 pb = Vector3.Lerp(pivotA, pivotB, tb) - qb * pivot;
            Vector3 la = ToLocal(pa, qa, scale, Vector3.Lerp(ballA, ballB, ta));
            Vector3 lb = ToLocal(pb, qb, scale, Vector3.Lerp(ballA, ballB, tb));
            if (!BatSweep.SegmentBox(la, lb, min, max, out float s, out int axis, out float sign))
                continue;

            // The grown box has square corners, which a sphere does not: near an edge or the toe
            // the box can be touched while the ball is still clear. Confirm against the true
            // distance to the blade, marching on to the first real touch.
            Vector3 bladeMin = new Vector3(-blade.halfWidth * widthMultiplier, blade.backY, blade.toeZ);
            Vector3 bladeMax = new Vector3(blade.halfWidth * widthMultiplier, blade.faceY, blade.shoulderZ);
            if (!FirstTouch(la, lb, s, bladeMin, bladeMax, r, out float touch, out Vector3 closest))
                continue;

            float t = Mathf.Lerp(ta, tb, touch);
            Vector3 localBall = Vector3.Lerp(la, lb, touch);
            Quaternion rotAtHit = Quaternion.Slerp(rotA, rotB, t);
            Vector3 posAtHit = Vector3.Lerp(pivotA, pivotB, t) - rotAtHit * pivot;
            Vector3 offset = localBall - closest;
            Vector3 normal;
            if (offset.sqrMagnitude > 1e-10f)
            {
                normal = offset.normalized;
            }
            else
            {
                normal = Vector3.zero;   // started inside: leave by the nearest face
                normal[axis] = sign;
            }
            hit = new Hit
            {
                t = t,
                localBall = localBall,
                localBat = closest,
                localNormal = normal,
                axis = axis,
                sign = sign,
                position = posAtHit,
                rotation = rotAtHit,
            };
            return true;
        }
        return false;
    }

    /// From fraction s along a->b (where the grown box was entered), the first point at which the
    /// ball is really within r of the blade box [min, max]. Also returns the touched point.
    private static bool FirstTouch(Vector3 a, Vector3 b, float s, Vector3 min, Vector3 max, float r,
                                   out float touch, out Vector3 closest)
    {
        const int steps = 24;
        float r2 = r * r * 1.0001f;
        for (int i = 0; i <= steps; i++)
        {
            float f = Mathf.Lerp(s, 1f, (float)i / steps);
            Vector3 p = Vector3.Lerp(a, b, f);
            Vector3 c = Vector3.Max(min, Vector3.Min(max, p));
            if ((p - c).sqrMagnitude > r2)
                continue;
            // Refine between the previous (clear) sample and this one.
            float lo = i == 0 ? s : Mathf.Lerp(s, 1f, (float)(i - 1) / steps), hi = f;
            for (int k = 0; k < 12 && i > 0; k++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector3 pm = Vector3.Lerp(a, b, mid);
                Vector3 cm = Vector3.Max(min, Vector3.Min(max, pm));
                if ((pm - cm).sqrMagnitude > r2) lo = mid; else hi = mid;
            }
            touch = hi;
            Vector3 at = Vector3.Lerp(a, b, touch);
            closest = Vector3.Max(min, Vector3.Min(max, at));
            return true;
        }
        touch = 0f;
        closest = Vector3.zero;
        return false;
    }

    /// <summary>
    /// Velocity of a bat-local point at the moment of the hit: the hands' velocity plus the spin
    /// about them (v = v_pivot + w x r). Unlike the chord between the two frame poses, this is the
    /// true tangent velocity on the arc, which matters in a fast swing (a 73-degree frame shortens
    /// the chord by 7%).
    /// </summary>
    public static Vector3 PointVelocity(Vector3 localPoint, float scale, Vector3 posA, Quaternion rotA,
                                        Vector3 posB, Quaternion rotB, float dt, Vector3 pivotLocal, float t)
    {
        dt = Mathf.Max(dt, 1e-5f);
        Vector3 pivot = pivotLocal * scale;
        Vector3 pivotVelocity = ((posB + rotB * pivot) - (posA + rotA * pivot)) / dt;
        (rotB * Quaternion.Inverse(rotA)).ToAngleAxis(out float degrees, out Vector3 axis);
        if (degrees > 180f) degrees -= 360f;
        Vector3 omega = axis.sqrMagnitude > 0f && !float.IsInfinity(axis.x) ? axis.normalized * (degrees * Mathf.Deg2Rad / dt) : Vector3.zero;
        Quaternion rot = Quaternion.Slerp(rotA, rotB, t);
        return pivotVelocity + Vector3.Cross(omega, rot * ((localPoint - pivotLocal) * scale));
    }

    public static Result Respond(Hit hit, Blade blade, float scale, Vector3 ballVelocity,
                                 Vector3 batPointVelocity, float ballMass)
    {
        Vector3 p = hit.localBat;
        float lateral = Mathf.Clamp01(Mathf.Abs(p.x) / Mathf.Max(blade.halfWidth, 1e-5f));
        float fromSweet = Mathf.Abs(p.z - blade.sweetZ) * scale;
        bool handle = p.z > blade.shoulderZ;
        // How far from the sweet spot the dead end is: the toe itself on that side (a real bat's
        // toe is dead), SweetSpotFalloff up toward the shoulder.
        float span = p.z < blade.sweetZ ? Mathf.Max(0.05f, (blade.sweetZ - blade.toeZ) * scale) : SweetSpotFalloff;
        float off = Smooth(fromSweet / span);

        float e = handle ? 0.15f : Mathf.Lerp(0.52f, 0.25f, off);
        float longitudinalMass = handle ? 0.25f : Mathf.Lerp(0.75f, 0.30f, off);

        // Twist about the long axis for an off-centre hit.
        float w = 2f * blade.halfWidth * scale, th = (blade.faceY - blade.backY) * scale;
        float twistInertia = BatMass * (w * w + th * th) / 12f;
        float x = p.x * scale;
        float mass = 1f / (1f / longitudinalMass + x * x / twistInertia);
        e *= 1f - 0.35f * lateral * lateral;

        // The touched surface decides the kind of contact. The geometric normal already rounds over
        // the corners of the blade; on the face itself the outer 20% is bevelled as a real bat's
        // edges are, which is what makes a thick edge fly off square.
        Vector3 localNormal = hit.localNormal;
        bool edge = false;
        if (localNormal.y > 0.95f)
        {
            float bevel = Mathf.Clamp01((lateral - 0.8f) / 0.2f);
            localNormal = new Vector3(Mathf.Sign(p.x) * bevel * 1.19f, 1f, 0f).normalized;
            edge = bevel > 0f;
        }
        else if (Mathf.Abs(localNormal.x) > 0.3f)
        {
            // Over the edge or on the side of the blade: a thin edge.
            e = Mathf.Min(e, 0.3f);
            mass = Mathf.Min(mass, 0.35f);
            edge = true;
        }
        else if (localNormal.y < -0.5f)
        {
            e *= 0.7f;   // the back of the bat
        }
        else if (Mathf.Abs(localNormal.z) > 0.5f)
        {
            e = 0.2f;    // the toe
        }

        Vector3 normal = hit.rotation * localNormal;
        Vector3 u = ballVelocity - batPointVelocity;
        float un = Vector3.Dot(u, normal);
        Vector3 ut = u - un * normal;
        float share = mass / (ballMass + mass);
        Vector3 change = un < 0f ? share * (-(1f + e) * un * normal - 0.3f * ut) : Vector3.zero;

        return new Result
        {
            velocity = ballVelocity + change,
            quality = (1f - off) * (1f - lateral * lateral) * (handle ? 0.2f : 1f),
            edge = edge,
            restitution = e,
            effectiveMass = mass,
        };
    }

    private static int Substeps(Vector3 posA, Quaternion rotA, Vector3 posB, Quaternion rotB, float scale, Blade blade)
    {
        float degrees = Quaternion.Angle(rotA, rotB);
        Vector3 toe = new Vector3(0f, 0f, blade.toeZ) * scale;
        float toeTravel = ((posB + rotB * toe) - (posA + rotA * toe)).magnitude;
        int n = Mathf.CeilToInt(Mathf.Max(degrees / MaxDegreesPerSubstep, toeTravel / MaxToeTravelPerSubstep));
        return Mathf.Clamp(n, 1, MaxSubsteps);
    }

    private static Vector3 ToLocal(Vector3 pos, Quaternion rot, float scale, Vector3 world)
    {
        return Quaternion.Inverse(rot) * (world - pos) / scale;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
