using UnityEngine;

/// <summary>
/// The two ends the career player can stand at: the striker's crease, where every screen, card
/// and panel was placed, and the non-striker's spot while he watches his partner bat. Anything
/// placed for the crease is carried to the other end rigidly: turned end for end (180 degrees
/// about the vertical) and moved so the crease head lands on the non-striker head - what he saw in
/// front of him at the crease is in front of him there, at the same height. ToCrease is the way
/// back, so a pose saved at either end can be stored as the crease pose.
///
/// The non-striker stands behind the popping crease on -Z: every bowler is right-arm over and runs
/// in on +Z (AnimatedBowler), so that side is clear of the run-up and the follow-through.
/// </summary>
public static class EndPose
{
    public static readonly Vector2 CreaseHeadXZ = new Vector2(BallDelivery.BatsmanStumpsX - 0.95f, 0f);
    public static readonly Vector2 NonStrikerHeadXZ = new Vector2(BallDelivery.BowlerStumpsX + 0.95f, -1.6f);
    /// Half a turn about world up.
    private static readonly Quaternion EndForEnd = new Quaternion(0f, 1f, 0f, 0f);

    public static Vector3 FromCrease(Vector3 p, bool nonStriker) =>
        !nonStriker ? p : new Vector3(NonStrikerHeadXZ.x - (p.x - CreaseHeadXZ.x), p.y, NonStrikerHeadXZ.y - (p.z - CreaseHeadXZ.y));

    public static Quaternion FromCrease(Quaternion q, bool nonStriker) => !nonStriker ? q : EndForEnd * q;

    public static Vector3 ToCrease(Vector3 p, bool nonStriker) =>
        !nonStriker ? p : new Vector3(CreaseHeadXZ.x - (p.x - NonStrikerHeadXZ.x), p.y, CreaseHeadXZ.y - (p.z - NonStrikerHeadXZ.y));

    public static Quaternion ToCrease(Quaternion q, bool nonStriker) => !nonStriker ? q : Quaternion.Inverse(EndForEnd) * q;

    /// The stumps at the far end from the player (where BallResultText floats): the bowler's from
    /// the crease, the striker's from the non-striker's end.
    public static Vector3 FarStumps(bool nonStriker) =>
        new Vector3(nonStriker ? BallDelivery.BatsmanStumpsX : BallDelivery.BowlerStumpsX, 0f, 0f);
}
