using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fields a captain sets, as data, and the geometry that turns one into nine places on this
/// ground. Pure (no scene), so the sets and the Laws' limits on them can be tested.
///
/// How a field is described: every position by its angle from the striker - 0 straight back past
/// the bowler, 90 square on the off side, 180 behind him toward the keeper, negative on the leg
/// side - and how far out. Off is +z for a right-hander (WideCollider, DeliveryPicker), so a
/// left-hander's field is the same set mirrored. The usual names map to angles like this:
///
///   off  mid-off 15 · extra cover 40 · cover 55 · cover point 75 · point 92 · backward point 110
///        gully 130 · slips beside the keeper · third man 150 (deep)
///   leg  mid-on -15 · midwicket -60 · square leg -90 · backward square -115 · short fine -150
///        fine leg -155 (deep) · leg slip beside the keeper · short leg -100 (bat-pad)
///
/// Kinds of place: Close (a catcher a few metres from the bat), Ring (saving the single, a few
/// metres inside the 30-yard circle: RingPlace), Deep (on the rope - placed Inset in from it along its line from the
/// striker, so it fits any ground), and Slip (beside the keeper, wherever he stands for this
/// bowler: Constants.KeeperPosition*).
///
/// The fields themselves are the bowling plans' (BowlingPlans): each plan is bowled to its own
/// field. Every field must keep the Laws' and the limited-overs limits (Valid): at most two behind
/// square on the leg side (Law 28.4), at most five on the leg side, and at most five outside the
/// circle - two in the powerplay (Ground.maxOutside).
///
/// Where the ring stands (2026-11-03, reported "they seem a bit close"): the circle is two 30-yard
/// semicircles about the two sets of stumps, joined by straight lines, so seen from the striker
/// it is 27.4 m away square of the wicket and behind it, ~33 m at cover and midwicket, ~47 m
/// straight. Ring fielders save the single a few metres inside that line (RingInside), never
/// further out than RingFurthest (a mid-off at ~29 m, as on television), never nearer than
/// RingNearest; they had been set at fixed 14-24 m, which put square leg and the short fine leg
/// barely 15 m from the bat. ICC playing conditions (men's ODI/T20I clause 28) for the limits.
/// </summary>
public static class FieldSets
{
    public enum Kind { Close, Ring, Deep, Slip }

    public struct Position
    {
        public string name;
        public Kind kind;
        /// Degrees from straight, off side positive (right-hander).
        public float angle;
        /// Metres from the striker's stumps (Close, Ring); unused for Deep; the slip number for Slip.
        public float distance;
        public Position(string n, Kind k, float a, float d = 0f) { name = n; kind = k; angle = a; distance = d; }
    }

    /// How the captain is setting the plan's field: catchers in, as it is, or men back.
    public enum Plan { Attack, Standard, Defend }

    /// The circle: 30 yards from each middle stump, joined by straight lines (Law-style limited
    /// overs marking).
    public const float RingRadius = 27.43f;
    /// Deep fielders stand this far in from the rope, as the scene's did (1.5-2 m).
    public const float Inset = 2.5f;
    /// A ring fielder: this far inside the circle's line along his angle, and within these.
    public const float RingInside = 3f, RingFurthest = 29f, RingNearest = 20f;
    /// Brought in to stop the singles: this much nearer than his usual ring place.
    public const float RingInFor1s = 4.5f;
    /// The most on the leg side (the keeper not counted).
    public const int MaxLegSide = 5;
    /// The most outside the circle, and behind square on the leg side.
    public const int MaxOutside = 5, MaxBehindSquareLeg = 2;

    public static bool IsSpin(eSwingType t) => t == eSwingType.LegSpin || t == eSwingType.OffSpin;

    /// Where the keeper stands to this bowler (HUD.UpdateKeeperPosition), along the pitch.
    public static float KeeperX(eSwingType bowler)
    {
        switch (bowler)
        {
            case eSwingType.Pace: return Constants.KeeperPositionFast.x;
            case eSwingType.InSwing:
            case eSwingType.OutSwing: return Constants.KeeperPositionMedium.x;
            default: return Constants.KeeperPositionSlow.x;
        }
    }

    // ---- Geometry ----------------------------------------------------------------------------

    /// The ground the field is set on: where the striker's stumps are, the rope, which way is off.
    public struct Ground
    {
        public float strikerX, nonStrikerX, boundary, keeperX;
        /// +1 for a right-hander (off side +z), -1 for a left-hander.
        public float offSign;
        /// The most allowed outside the circle now: 2 in the powerplay, else MaxOutside (0 = that).
        public int maxOutside;
        /// Ring fielders brought in this far (metres) to stop the singles; 0 = their usual place.
        public float ringIn;
        public int MaxOutsideNow => maxOutside > 0 ? maxOutside : MaxOutside;
    }

    /// A position's angle on this ground: off side positive is +z for offSign +1.
    static Vector3 Direction(float angle, float offSign)
    {
        float a = angle * Mathf.Deg2Rad;
        return new Vector3(-Mathf.Cos(a), 0f, Mathf.Sin(a) * offSign);
    }

    /// The point on the grass (y 0) where this position stands.
    public static Vector3 Place(Position p, Ground g)
    {
        Vector3 striker = new Vector3(g.strikerX, 0f, 0f);
        switch (p.kind)
        {
            case Kind.Slip:
                // Beside the keeper, a little in front of him, a stride further out for each slip;
                // standing up to the stumps, the slip is no further back than he is.
                float depth = Mathf.Max(1.5f, g.keeperX - g.strikerX - 0.5f);
                float wide = 1.4f + 1.8f * (p.distance - 1f) + depth * 0.08f;
                return new Vector3(g.strikerX + depth, 0f, wide * g.offSign);
            case Kind.Deep:
                Vector3 u = Direction(p.angle, g.offSign);
                return striker + u * (ToRope(striker, u, g.boundary) - Inset);
            case Kind.Ring when p.distance <= 0f:
                return striker + Direction(p.angle, g.offSign) * RingPlace(p.angle, g);
            default:
                return striker + Direction(p.angle, g.offSign) * p.distance;
        }
    }

    /// How far from the striker a ring fielder at this angle stands: RingInside inside the
    /// circle's line, between RingNearest and RingFurthest, less any ringIn.
    public static float RingPlace(float angle, Ground g) =>
        Mathf.Clamp(RingEdge(angle, g) - RingInside, RingNearest, RingFurthest) - Mathf.Max(0f, g.ringIn);

    /// Distance from the striker's stumps to the circle's line along this angle (Law-style oval:
    /// RingRadius from the segment between the two middle stumps).
    public static float RingEdge(float angle, Ground g)
    {
        Vector3 striker = new Vector3(g.strikerX, 0f, 0f);
        Vector3 u = Direction(angle, g.offSign);
        float lo = 0f, hi = 120f;
        for (int i = 0; i < 30; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (InsideRing(striker + u * mid, g)) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// Distance along `u` from `from` to the rope (a circle about the pitch's middle, the origin).
    static float ToRope(Vector3 from, Vector3 u, float radius)
    {
        float b = Vector3.Dot(from, u);
        float c = from.sqrMagnitude - radius * radius;
        return -b + Mathf.Sqrt(Mathf.Max(0f, b * b - c));
    }

    /// Inside the circle: within RingRadius of the line between the two middle stumps.
    public static bool InsideRing(Vector3 p, Ground g)
    {
        float lo = Mathf.Min(g.strikerX, g.nonStrikerX), hi = Mathf.Max(g.strikerX, g.nonStrikerX);
        Vector2 nearest = new Vector2(Mathf.Clamp(p.x, lo, hi), 0f);
        return Vector2.Distance(new Vector2(p.x, p.z), nearest) <= RingRadius;
    }

    /// Behind square on the leg side: behind the line through the striker's stumps, leg side. Half
    /// a metre of grace, so a man placed square (90 degrees) is square, whatever the rounding.
    public static bool BehindSquareLeg(Vector3 p, Ground g) => p.x > g.strikerX + 0.5f && p.z * g.offSign < 0f;

    /// On the leg side of the striker (and not straight: within a degree of it counts as neither).
    public static bool LegSide(Vector3 p, Ground g) => AngleOf(p, g) < -1f;

    /// The limits: at most two behind square on the leg side, five on the leg side, and
    /// Ground.MaxOutsideNow outside the circle.
    public static bool Valid(IList<Vector3> places, Ground g)
    {
        Count(places, g, out int outside, out int behindLeg, out int leg);
        return outside <= g.MaxOutsideNow && behindLeg <= MaxBehindSquareLeg && leg <= MaxLegSide;
    }

    public static void Count(IList<Vector3> places, Ground g, out int outside, out int behindLeg, out int leg)
    {
        outside = behindLeg = leg = 0;
        foreach (Vector3 p in places)
        {
            if (!InsideRing(p, g)) outside++;
            if (BehindSquareLeg(p, g)) behindLeg++;
            if (LegSide(p, g)) leg++;
        }
    }

    /// Angle from straight for a point (or a direction) on this ground, off side positive: the
    /// inverse of Place for anything not a slip.
    public static float AngleOf(Vector3 p, Ground g)
    {
        Vector3 d = p - new Vector3(g.strikerX, 0f, 0f);
        return Mathf.Atan2(d.z * g.offSign, -d.x) * Mathf.Rad2Deg;
    }
}
