using UnityEngine;

/// The shot the AI partner plays to a delivery (phase 3: watching the partner bat).
public enum PartnerShot { Leave, Defend, Drive, Loft, Cut, Pull, Sweep, Glance, Edge, PlayAndMiss }
public enum PartnerFoot { Front, Back }
public enum PartnerWicket { None, Bowled, Caught, CaughtBehind }

/// The delivery as released: where from, how fast, and its swing/turn.
public struct IncomingBall { public Vector3 release, velocity; public BallFlight.DeliveryEffects effects; }

/// Where and when the ball reaches the partner's contact plane.
public struct BallAtBat { public bool found; public float time; public Vector3 point, velocity; }

/// The field as the planner sees it at release.
public struct FieldSnapshot
{
    /// Where each fielder stands (world, on the grass), the keeper among them at keeperIndex.
    public Vector3[] fielders;
    /// Each fielder's top running speed, m/s (AnimatedFielder.RunSpeed).
    public float[] runSpeed;
    /// The keeper's index in fielders, or -1 for none.
    public int keeperIndex;
    /// The rope (CatchJudge.Boundary), which way is off (+1 right-hander), and how late a fielder
    /// moves after the hit (SeeingDelay + ReactionTime).
    public float boundary, offSign, delay;
    /// The career player's head at the non-striker's end: no shot is played at it. Left at zero
    /// there is no one to keep clear (PartnerField.HasHuman).
    public Vector3 nonStrikerHead;
}

/// What the field would make of a shot: runs (CareerSim.Wicket for a catch), a catch, who fields
/// it and when (seconds after the hit), and how near the path comes to the player's head
/// (PartnerField.NoHuman when there is none).
public struct FieldResult { public int runs; public bool caught; public int byFielder; public float fieldedAt; public float closestToHuman; }

/// A direction and speed chosen to realise an intent (runs, or a wicket). valid: the field model
/// says it does.
public struct ShotDirection { public int intent; public Vector3 exitVelocity; public FieldResult predicted; public PartnerWicket wicket; public bool valid; }

/// <summary>The whole plan for one ball: the shot, where and when the bat meets it, what it does
/// to it. contactTime is seconds after release; contactPoint is where the ball crosses the plane
/// x = contactX (for a leave or a play-and-miss too: the ball, which the bat is kept away from).
/// incoming is the ball's velocity there, exitVelocity what the contact sends it away at, and
/// batVelocity the sweet spot's velocity that does it (BatContact's head-on rule; for an edge, the
/// shot he meant to play).</summary>
public struct ShotPlan
{
    public PartnerShot shot;
    public PartnerFoot foot;
    public int intent;
    public PartnerWicket wicket;
    public bool makesContact;
    public float contactX, contactTime;
    public Vector3 contactPoint, incoming, exitVelocity, batVelocity;
    public bool edge;
    public BatContact.ContactKind kind;
    public float quality;
    public FieldResult predicted;
}

/// The bat in the partner's hands, in the bat's own frame (from BatGeometry's markers): long axis
/// local +Z toward the handle, face +Y. scale is the bat's world scale (lossyScale).
public struct BatGrip { public Vector3 sweetLocal, topHandLocal, bottomHandLocal, toeLocal; public float scale; }

/// <summary>The partner's body and bat at one moment of a swing (world). The bat pose is exact
/// (place the bat there); the rest are IK targets: the hips (pelvis) and the way they face -
/// hipsYaw, degrees about world up with 0 facing +Z (a right-hander side-on faces 0, a
/// left-hander 180) - both feet (ankles), both hands on the handle (topHand is the left hand of a
/// right-hander), where he looks, and where his head wants to be (head: over the off-stump line
/// in his stance, over the ball at contact; PartnerSwing.ShoulderCentre lies on hips-to-head).</summary>
public struct SwingPose
{
    public Vector3 batPosition;
    public Quaternion batRotation;
    public Vector3 hips;
    public float hipsYaw;
    public Vector3 leftFoot, rightFoot, look, topHand, bottomHand;
    public Vector3 head;
}
