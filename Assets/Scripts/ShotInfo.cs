using UnityEngine;

/// <summary>
/// What the bat reports for each struck ball (Bat.ShotStruck). Speeds are in m/s; quality runs
/// 0..1, 1 being the middle of the bat; contactLabel is the word shown to the player ("Middled",
/// "Thick edge", "Toe"); edge is true for any edge.
///
/// The rest is for measuring the shot (BallLedger, the career): kind is the contact as an enum
/// (contactLabel's source), impactSpeed how hard bat and ball met along the contact normal,
/// exitVelocity the ball's velocity off the bat (world), contactPoint where the ball's centre was
/// at the touch (world). quality is read off the bat as the shield widened it; realQuality and
/// realContact are the same contact judged on the real-width blade (RealBlade), which is what a
/// rating may use - the shield must never flatter a rating.
/// </summary>
public struct ShotInfo
{
    public float ballSpeedIn, batSpeed, exitSpeed, quality; public string contactLabel; public bool edge;
    public BatContact.ContactKind kind;
    public float impactSpeed;
    public Vector3 exitVelocity, contactPoint;
    public float realQuality;
    public eRealContact realContact;
}
