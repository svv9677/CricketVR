using UnityEngine;

/// <summary>
/// The virtual field for Hitting the gaps. The nets have no fielders, so a shot is judged against
/// a real field (DrillDefs.FieldFor: the off-stump field to the seamer, darts to the spinner) set on
/// a nets-sized ground (NetsGround), in the batter's frame - the record's shotAngle is already
/// there, off side +, so the ground's offSign is +1 for both hands.
///
/// In order (Judge):
///   1. not a clean strike on the real blade (left, missed, edged): Wasted;
///   2. a six by the ledger's own rule (BallRecord.end): Six;
///   3. back past the keeper (|angle| beyond KeeperCone): Wasted;
///   4. in the air and coming down inside the rope where a fielder - any of them, slips and the
///      ring too - can get to it in its hang time (Reach + RunSpeed x hang): Caught;
///   5. reaching a ring, close or slip fielder along the ground (he is in its path, within the
///      angle he can cover at that pace, Cover): Fielder;
///   6. otherwise a carry of GroundCarry or more is OverTop (cleared the ring, safe), anything
///      lower a Gap. A man in the deep turns a gap into a single, but it is still a gap found.
/// Distances come from FieldSets.Place, so the ring stands where it does in a match.
/// </summary>
public static class DrillField
{
    public const float Reach = 1.5f, RunSpeed = 5f, KeeperCone = 165f, GroundCarry = 20f, Boundary = 56f;

    /// A ground ball slows as it goes: the speed a fielder has to react to, as a share of the
    /// exit speed, and never under GroundFloor m/s.
    private const float GroundPace = 0.8f, GroundFloor = 5f;

    public static FieldSets.Ground NetsGround(eBowlerKind k) => new FieldSets.Ground
    {
        strikerX = BallDelivery.BatsmanStumpsX,
        nonStrikerX = BallDelivery.BowlerStumpsX,
        boundary = Boundary,
        keeperX = FieldSets.KeeperX(BowlerRepertoire.BaseType(k)),
        offSign = 1f,
    };

    public static GapJudgement Judge(BallRecord r, FieldSets.Position[] field, FieldSets.Ground g)
    {
        if (r == null || r.action != eBallAction.Played || r.realContact != eRealContact.Clean)
            return Call(eGapCall.Wasted);
        if (r.end == eBallEnd.Six)
            return Call(eGapCall.Six);
        if (Mathf.Abs(r.shotAngle) > KeeperCone)
            return Call(eGapCall.Wasted);
        field ??= new FieldSets.Position[0];

        string catcher = Catcher(r, field, g);
        if (catcher != null)
            return Call(eGapCall.Caught, catcher);
        string stopper = Stopper(r, field, g);
        if (stopper != null)
            return Call(eGapCall.Fielder, stopper);
        return Call(r.carry >= GroundCarry ? eGapCall.OverTop : eGapCall.Gap);
    }

    /// Half the angle (degrees) a fielder this far away covers against a ground ball this fast:
    /// his reach plus the ground he can run while it comes to him.
    public static float Cover(float distance, float ballSpeed)
    {
        if (distance <= 0.01f)
            return 90f;
        float time = distance / Mathf.Max(ballSpeed * GroundPace, GroundFloor);
        return Mathf.Atan((Reach + RunSpeed * time) / distance) * Mathf.Rad2Deg;
    }

    private static GapJudgement Call(eGapCall c, string fielder = "") => new GapJudgement { call = c, fielder = fielder };

    /// Where the ball comes down (grass level), from the striker along the shot.
    private static Vector3 Landing(BallRecord r, FieldSets.Ground g)
    {
        float a = r.shotAngle * Mathf.Deg2Rad;
        return new Vector3(g.strikerX - Mathf.Cos(a) * r.carry, 0f, Mathf.Sin(a) * g.offSign * r.carry);
    }

    /// Seconds in the air: the carry over the speed along the ground.
    private static float Hang(BallRecord r) =>
        r.carry / Mathf.Max(1f, r.exitSpeed * Mathf.Cos(r.launchDeg * Mathf.Deg2Rad));

    /// The fielder who catches it (the nearest who can get there), or null: it must be in the air
    /// and come down inside the rope - nobody catches beyond it.
    private static string Catcher(BallRecord r, FieldSets.Position[] field, FieldSets.Ground g)
    {
        if (r.carry <= 0f || r.launchDeg <= 0f)
            return null;
        Vector3 land = Landing(r, g);
        if (new Vector2(land.x, land.z).magnitude > g.boundary)
            return null;
        float reach = Reach + RunSpeed * Hang(r);
        string best = null;
        float nearest = float.MaxValue;
        foreach (FieldSets.Position p in field)
        {
            float d = Vector3.Distance(FieldSets.Place(p, g), land);
            if (d <= reach && d < nearest)
            {
                nearest = d;
                best = p.name;
            }
        }
        return best;
    }

    /// The ring, close or slip fielder who stops it along the ground (the one most in its path),
    /// or null. He must be in its path once it is down: it comes down before it reaches him.
    private static string Stopper(BallRecord r, FieldSets.Position[] field, FieldSets.Ground g)
    {
        Vector3 striker = new Vector3(g.strikerX, 0f, 0f);
        string best = null;
        float closest = float.MaxValue;
        foreach (FieldSets.Position p in field)
        {
            if (p.kind == FieldSets.Kind.Deep)
                continue;
            Vector3 at = FieldSets.Place(p, g);
            float dist = Vector3.Distance(at, striker);
            if (dist < r.carry - Reach)
                continue;                                   // it went over him
            float off = Mathf.Abs(Mathf.DeltaAngle(r.shotAngle, FieldSets.AngleOf(at, g)));
            if (off <= Cover(dist, r.exitSpeed) && off < closest)
            {
                closest = off;
                best = p.name;
            }
        }
        return best;
    }
}
