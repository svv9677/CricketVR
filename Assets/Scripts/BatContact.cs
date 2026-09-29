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
///   * edges            the long edges are rounded (EdgeRounding), so the normal turns from the face
///     through 90 degrees onto the side as the contact moves out: a thick edge (met on the
///     rounding) is turned off square, a thin edge (ball centre outside the blade) only deflected;
///     friction is Coulomb-limited, so a light touch barely slows the ball;
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
    /// Radius (m) of the rounding where the face and the back turn into the side of the blade.
    public const float EdgeRounding = 0.025f;
    /// A face contact whose normal is turned at least this far (degrees) toward the side is an edge.
    public const float ThickEdgeAngle = 12f;
    /// Leather on willow: sliding friction, and the most of the slip it can take out (a solid
    /// sphere stops slipping once it has lost 2/7 of it).
    public const float Friction = 0.4f, RollGrip = 0.3f;

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

    /// Where on the bat it was struck, as a batter would describe it.
    public enum ContactKind { Middled, Good, Toe, Shoulder, Handle, ThickEdge, ThinEdge, Back }

    public struct Result
    {
        public Vector3 velocity;
        /// 1 = middled, falling toward 0 off the sweet spot and toward the edges.
        public float quality;
        public bool edge;
        public float restitution, effectiveMass;
        public ContactKind kind;
        /// Closing speed along the contact normal, m/s: how hard the ball and bat actually met.
        public float impactSpeed;
    }

    /// The "Bat power" setting (75 = realistic) as a multiplier on the swing velocity, held to
    /// +-50% so it can only nudge a real swing, never manufacture pace the bat did not have.
    public static float SwingScale(float batPower) => Mathf.Clamp(batPower / 75f, 0.5f, 1.5f);

    /// A word for the contact, for the scoreboard.
    public static string Label(ContactKind kind) => kind switch
    {
        ContactKind.Middled => "Middled",
        ContactKind.Good => "Good contact",
        ContactKind.Toe => "Toe end",
        ContactKind.Shoulder => "Off the shoulder",
        ContactKind.Handle => "Off the handle",
        ContactKind.ThickEdge => "Thick edge",
        ContactKind.ThinEdge => "Thin edge",
        _ => "Back of the bat",
    };

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
            if (!FirstTouch(la, lb, s, bladeMin, bladeMax, r, out float touch, out Vector3 closest, out bool overlapping))
                continue;

            float t = Mathf.Lerp(ta, tb, touch);
            Vector3 localBall = Vector3.Lerp(la, lb, touch);
            Quaternion rotAtHit = Quaternion.Slerp(rotA, rotB, t);
            Vector3 posAtHit = Vector3.Lerp(pivotA, pivotB, t) - rotAtHit * pivot;
            Vector3 offset = localBall - closest;
            Vector3 normal;
            if (overlapping)
            {
                // Already touching when the step began, so there is no clean first touch to read the
                // surface from. The nearest face is the wrong answer here: a ball more than half-way
                // through a 12 cm blade is nearer the back, and pushing it out there sends a
                // middled drive straight back past the keeper. The ball came in through the face its
                // motion relative to the bat points away from, so it leaves by that face.
                normal = EntryNormal(localBall, lb - la, bladeMin, bladeMax);
            }
            else if (offset.sqrMagnitude > 1e-10f)
            {
                normal = offset.normalized;
            }
            else
            {
                normal = Vector3.zero;
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
                                   out float touch, out Vector3 closest, out bool overlapping)
    {
        const int steps = 24;
        float r2 = r * r * 1.0001f;
        overlapping = false;
        for (int i = 0; i <= steps; i++)
        {
            float f = Mathf.Lerp(s, 1f, (float)i / steps);
            Vector3 p = Vector3.Lerp(a, b, f);
            Vector3 c = Vector3.Max(min, Vector3.Min(max, p));
            if ((p - c).sqrMagnitude > r2)
                continue;
            // Touching at the very start of the segment: nothing clear before it to refine from.
            overlapping = i == 0 && s <= 0f;
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
    /// The outward normal of the blade face a ball entered through, from where it is now and how it
    /// is moving relative to the bat: trace back along -motion and take the face that is reached
    /// first. Falls back to the hitting face when the ball is not moving relative to the bat.
    /// </summary>
    public static Vector3 EntryNormal(Vector3 localBall, Vector3 relativeMotion, Vector3 min, Vector3 max)
    {
        int best = 1;
        float bestT = float.MaxValue, bestSign = 1f;
        for (int i = 0; i < 3; i++)
        {
            float d = relativeMotion[i];
            if (Mathf.Abs(d) < 1e-7f)
                continue;
            // Moving toward -i means it came in through the +i face, and the reverse.
            float sign = d < 0f ? 1f : -1f;
            float face = sign > 0f ? max[i] : min[i];
            float back = (face - localBall[i]) * sign / Mathf.Abs(d);
            if (back < bestT)
            {
                bestT = back;
                best = i;
                bestSign = sign;
            }
        }
        Vector3 n = Vector3.zero;
        n[best] = bestT == float.MaxValue ? 1f : bestSign;
        return n;
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

    /// <param name="widthMultiplier">The difficulty's bat width, as passed to Sweep. Easy and Medium
    /// widen the blade for finding a hit; the response must read that wider blade too. It used to
    /// measure from the real blade, so every hit on the added width counted as the very edge -
    /// bevelled off sideways at quality 0, and weak, the twist taken at up to 4x the real offset.</param>
    public static Result Respond(Hit hit, Blade blade, float scale, Vector3 ballVelocity,
                                 Vector3 batPointVelocity, float ballMass, float widthMultiplier = 1f)
    {
        Vector3 p = hit.localBat;
        float widen = Mathf.Max(1f, widthMultiplier);
        // Across the face as a share of the (widened) half-width, and the same share of the real blade.
        float lateral = Mathf.Clamp01(Mathf.Abs(p.x) / Mathf.Max(blade.halfWidth * widen, 1e-5f));
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
        float x = p.x / widen * scale;
        float mass = 1f / (1f / longitudinalMass + x * x / twistInertia);
        e *= 1f - 0.35f * lateral * lateral;

        // The touched surface decides the kind of contact. The long edges are rounded (EdgeNormal),
        // so the normal turns smoothly from the face, over the edge, onto the side: the further
        // out the ball is met, the more it is turned aside and the less it is pushed back.
        Vector3 localNormal = hit.localNormal;
        bool edge = false, thin = false, back = false, toeEnd = false;
        if (Mathf.Abs(localNormal.z) > 0.5f)
        {
            e = 0.2f;    // the toe
            toeEnd = true;
        }
        else
        {
            float halfWidth = blade.halfWidth * widen;
            float ballRadius = BallFlight.Radius / scale;
            Vector3 centre = p + localNormal * ballRadius;
            localNormal = EdgeNormal(centre, blade, halfWidth, scale, ballRadius, localNormal);
            // Thin: more than half the ball passed outside the blade, so it only clipped the edge.
            // Thick: met on the rounding, square enough to be turned well off the face.
            thin = Mathf.Abs(centre.x) > halfWidth;
            edge = thin || Mathf.Abs(localNormal.x) > Mathf.Sin(ThickEdgeAngle * Mathf.Deg2Rad);
            if (thin)
            {
                e = Mathf.Min(e, 0.3f);
                mass = Mathf.Min(mass, 0.35f);
            }
            else if (localNormal.y < -0.5f)
            {
                e *= 0.7f;   // the back of the bat
                back = true;
            }
        }

        Vector3 normal = hit.rotation * localNormal;
        Vector3 u = ballVelocity - batPointVelocity;
        float un = Vector3.Dot(u, normal);
        Vector3 ut = u - un * normal;
        float share = mass / (ballMass + mass);
        Vector3 change = Vector3.zero;
        if (un < 0f)
        {
            // Friction along the surface is at most Friction times the push it gets (Coulomb), and
            // never more than takes the slip out of it (RollGrip). A flat 30% of the slip used to
            // apply however light the touch: a feather edge lost ~40% of its pace and was dragged
            // round with the swing instead of carrying on to the keeper.
            float pushed = share * (1f + e) * -un;
            float slip = ut.magnitude;
            float grip = Mathf.Min(Friction * pushed, share * RollGrip * slip);
            change = pushed * normal - (slip > 1e-6f ? ut / slip * grip : Vector3.zero);
        }
        float quality = (1f - off) * (1f - lateral * lateral) * (handle ? 0.2f : 1f);

        return new Result
        {
            velocity = ballVelocity + change,
            quality = quality,
            edge = edge,
            restitution = e,
            effectiveMass = mass,
            kind = Classify(quality, handle, thin, edge, back, toeEnd || (p.z < blade.sweetZ && off > 0.55f), off > 0.55f),
            impactSpeed = Mathf.Max(0f, -un),
        };
    }

    /// <summary>
    /// The surface normal a ball centred at `centre` (bat-local) meets on a blade whose long edges
    /// are rounded with radius EdgeRounding: flat on the middle of the face, back and side, and on
    /// the rounding the direction from the rounding's axis to the ball. It turns continuously from
    /// 0 to 90 degrees as the contact moves out across the edge, so a thick edge is turned less than
    /// a thin one - the sharp box corner used to jump from a 50 degree face bevel back to ~18.
    ///
    /// The sweep finds the first touch on the sharp-cornered box, whose corner stands proud of the
    /// rounding, so the ball is met there too early and too far out on the face. The ball is taken
    /// on into the face (its x across the blade kept) until it meets the rounding itself; read at
    /// the box corner instead, a ball clipping the edge by 4 mm was turned 46 degrees, not ~15.
    /// </summary>
    public static Vector3 EdgeNormal(Vector3 centre, Blade blade, float halfWidth, float scale, float ballRadius, Vector3 flatNormal)
    {
        float rc = Mathf.Min(EdgeRounding / scale, halfWidth, 0.5f * (blade.faceY - blade.backY));
        float innerX = halfWidth - rc;
        float across = Mathf.Abs(centre.x) - innerX;
        if (across <= 0f)
            return flatNormal;
        bool faceSide = centre.y >= 0.5f * (blade.faceY + blade.backY);
        float axisY = faceSide ? blade.faceY - rc : blade.backY + rc;
        float reach = ballRadius + rc;
        // Beside the flat part of the side, not over a rounding - or clear of the rounding
        // altogether: the side itself.
        if ((faceSide ? centre.y < axisY : centre.y > axisY) || across >= reach)
            return new Vector3(Mathf.Sign(centre.x), 0f, 0f);
        float up = Mathf.Sqrt(reach * reach - across * across);
        return new Vector3(Mathf.Sign(centre.x) * across, faceSide ? up : -up, 0f) / reach;
    }

    private static ContactKind Classify(float quality, bool handle, bool thin, bool edge, bool back, bool toe, bool offSweet)
    {
        if (handle) return ContactKind.Handle;
        if (thin) return ContactKind.ThinEdge;
        if (back) return ContactKind.Back;
        if (edge) return ContactKind.ThickEdge;
        if (toe) return ContactKind.Toe;
        if (offSweet) return ContactKind.Shoulder;
        return quality >= 0.8f ? ContactKind.Middled : ContactKind.Good;
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
