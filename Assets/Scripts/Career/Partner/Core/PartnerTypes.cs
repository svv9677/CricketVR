using System.Collections.Generic;
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
    public Vector3[] fielders;
    public float[] runSpeed;
    public int keeperIndex;
    public float boundary, offSign, delay;
    /// The career player's head at the non-striker's end: no shot is played at it.
    public Vector3 nonStrikerHead;
}

/// What the field would make of a shot: runs, a catch, who fields it and when.
public struct FieldResult { public int runs; public bool caught; public int byFielder; public float fieldedAt; public float closestToHuman; }

/// A direction and speed chosen to realise an intent (runs, or a wicket).
public struct ShotDirection { public int intent; public Vector3 exitVelocity; public FieldResult predicted; public PartnerWicket wicket; public bool valid; }

/// The whole plan for one ball: the shot, where and when the bat meets it, what it does to it.
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

/// The bat in the partner's hands, in the bat's own frame (from BatGeometry's markers).
public struct BatGrip { public Vector3 sweetLocal, topHandLocal, bottomHandLocal, toeLocal; public float scale; }

/// The partner's body and bat at one moment of a swing (world).
public struct SwingPose
{
    public Vector3 batPosition;
    public Quaternion batRotation;
    public Vector3 hips;
    public float hipsYaw;
    public Vector3 leftFoot, rightFoot, look, topHand, bottomHand;
}

/// <summary>What the partner means to do with a ball: CareerSim's outcome for his rating.
/// CONTRACT STUB.</summary>
public static class PartnerIntent
{
    /// Leave a wide alone.
    public const int LeaveWide = 100;
    /// An outcome code: CareerSim.Wicket, 0..4, 6 (a Wide re-rolled; 5 is 4; no wicket off a
    /// no-ball/free hit), or LeaveWide when the delivery is a wide.
    public static int Draw(float rating, eBowlingLevel level, float aggression, System.Random rng, bool deliveryIsWide, bool protectedBall) => 0;
    public static bool IsWide(in BallDelivery.Solution d, float stanceZ, float offSign, float headTop) => false;
}

/// <summary>The field's answer to a shot, without running the fielders. CONTRACT STUB.</summary>
public static class PartnerField
{
    /// Mirrors AnimatedFielderDive's private dive reach.
    public const float DiveReach = 1.5f;
    public static float ReachTime(Vector3 from, Vector3 to, float runSpeed, float delay) => 0f;
    public static FieldResult Evaluate(Vector3 contact, Vector3 exit, in FieldSnapshot f, List<BallFlight.Sample> scratch) => default;
}

/// <summary>Chooses the shot, its direction and its contact. CONTRACT STUB.</summary>
public static class PartnerShotPlanner
{
    public static BallAtBat Meet(in IncomingBall b, float contactX, List<BallFlight.Sample> scratch) => default;
    /// The direction search, spread over frames: yields the best so far after each batch.
    public static IEnumerator<ShotDirection> PlanDirectionSteps(int intent, FieldSnapshot f, Vector3 nominalContact, System.Random rng, int candidatesPerStep)
    {
        yield break;
    }
    public static ShotPlan Plan(in ShotDirection d, in IncomingBall b, in FieldSnapshot f, float offSign, System.Random rng, List<BallFlight.Sample> scratch) => default;
    /// The bat's velocity that sends `incoming` away at `exit` (BatContact: v_out = q v_in + (1+q) v_bat, q = 0.24).
    public static Vector3 BatVelocityFor(Vector3 incoming, Vector3 exit) => (exit - 0.24f * incoming) / 1.24f;
}

/// <summary>The swing as poses over time, relative to contact. CONTRACT STUB.</summary>
public static class PartnerSwing
{
    public static SwingPose Pose(in ShotPlan p, in BatGrip g, float offSign, Vector3 stanceHips, float tFromContact) => default;
    public static float DownswingTime(PartnerShot s) => 0.2f;
    public static float BackliftStart(in ShotPlan p) => -0.6f;
}

/// <summary>The two ends: the striker's crease and the non-striker's spot, and mapping a pose
/// placed for one onto the other (the pitch turned end for end). CONTRACT STUB.</summary>
public static class EndPose
{
    public static readonly Vector2 CreaseHeadXZ = new Vector2(BallDelivery.BatsmanStumpsX - 0.95f, 0f);
    public static readonly Vector2 NonStrikerHeadXZ = new Vector2(BallDelivery.BowlerStumpsX + 0.95f, -1.6f);
    public static Vector3 FromCrease(Vector3 p, bool nonStriker) => p;
    public static Quaternion FromCrease(Quaternion q, bool nonStriker) => q;
    public static Vector3 ToCrease(Vector3 p, bool nonStriker) => p;
    public static Quaternion ToCrease(Quaternion q, bool nonStriker) => q;
    /// The stumps at the far end from the player (where BallResultText floats).
    public static Vector3 FarStumps(bool nonStriker) => new Vector3(BallDelivery.BowlerStumpsX, 0f, 0f);
}

/// <summary>Which stumps can bowl the striker. CONTRACT STUB.</summary>
public static class StumpRules
{
    public static bool CountsAsBowled(bool strikersEnd, eGameState s) => true;
}
