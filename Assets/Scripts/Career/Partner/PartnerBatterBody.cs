using UnityEngine;

/// <summary>
/// The partner's body following his bat (phase 3): the Animator's IK puts his hands on the handle,
/// his feet where the swing says, his hips down into his stance and round with the shot, and his
/// head on the ball; after the Animator, his fingers close round the handle (Celebration's finger
/// helpers). The idle animation underneath keeps him breathing; everything else is the IK.
///
/// It does nothing on its own: PartnerBatter hands it a pose every frame (Pose) and says how he
/// holds the bat at this end (Configure).
/// </summary>
// After the Animator (its IK pass is inside the animation update), before the recorder (1000).
[DefaultExecutionOrder(850)]
public class PartnerBatterBody : MonoBehaviour
{
    /// Two hands on the bat at guard; one (the right, on the top of the handle) backing up.
    public enum Hold { BothHands, RightHandOnTop }

    // ---- Tuning (headset) ----------------------------------------------------------------------
    /// The IK target is the wrist, which sits this far back from where the fist holds the handle.
    public const float WristToGrip = 0.07f;
    /// Elbow hints: halfway down the arm, this far lower and this far out to the side.
    public const float ElbowDrop = 0.22f, ElbowOut = 0.12f;
    /// How hard the elbows follow their hints (1 pins them; the IK is free at 0).
    public const float ElbowHintWeight = 0.5f;
    /// Feet turned to their targets this much (the humanoid foot goal's frame is the body's).
    public const float FootRotationWeight = 0.8f;
    /// The feet turn this share of the hips' turn through the shot (the back foot pivots, the
    /// front stays more or less planted).
    public const float FootYawShare = 0.5f;
    /// Look-at: overall, body, head, eyes, clamp (Animator.SetLookAtWeight's five weights).
    public const float LookWeight = 1f, LookBody = 0.15f, LookHead = 0.9f, LookEyes = 1f, LookClamp = 0.6f;
    /// The fingers closed this far round the handle (0 open .. 1 a fist: the handle is thick).
    public const float GripCurl = 0.8f;
    /// The wrist rolls at most this far (degrees) to bring the thumb down the handle.
    public const float MaxWristRoll = 100f;
    /// The handle sits in the palm, not on the hand's centre line: the knuckles are aimed this far
    /// to the back of the hand from the handle's middle (about the handle's radius and half a palm).
    public const float PalmOffset = 0.02f;
    /// A finger's three joints bent this far (degrees) in a full fist, root to tip (as Celebration's hand)...
    public const float RootBend = 70f, MiddleBend = 95f, TipBend = 65f;
    /// ...and the thumb this share of a finger's bend.
    public const float ThumbBendShare = 0.5f;

    private static readonly HumanBodyBones[] RightRoots = { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
    private static readonly HumanBodyBones[] LeftRoots = { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal };

    private Animator animator;
    private Transform lHand, rHand, lShoulder, rShoulder;
    private readonly Transform[,] lFingers = new Transform[5, 3], rFingers = new Transform[5, 3];
    private float lCurl = 1f, rCurl = 1f;
    private Vector3 lPalm = Vector3.forward, rPalm = Vector3.forward;
    private bool calibrated;
    // Whether each hand held the bat last frame: a hand that lets go opens again (the rig maps
    // few finger joints, so the Animator does not straighten the rest).
    private bool lHeld, rHeld;

    private SwingPose pose;
    private bool hasPose;
    private Hold hold;
    private bool topHandLeft = true;
    private float lean;
    private Quaternion leftFootTurn = Quaternion.identity, rightFootTurn = Quaternion.identity;
    // Where each hand holds the handle this frame (set in the IK pass, used by the finger pass).
    private Vector3 lGrip, rGrip;
    private bool lHolds, rHolds;

    /// The hips' height above his feet standing on the idle, measured when he is built.
    public float StandingHipsY { get; private set; } = 0.95f;
    /// The head's height above his feet standing, measured when he is built.
    public float StandingHeadY { get; private set; } = 1.6f;
    /// What his head turns to (the ball in play, else the pose's own look).
    public Vector3 Look { get; set; }

    public void Init(Animator a)
    {
        animator = a;
        lHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
        rHand = a.GetBoneTransform(HumanBodyBones.RightHand);
        lShoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rShoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        Transform head = a.GetBoneTransform(HumanBodyBones.Head);
        if (hips != null) StandingHipsY = hips.position.y - transform.position.y;
        if (head != null) StandingHeadY = head.position.y - transform.position.y;
        Celebration.Fingers(lHand, LeftRoots, lFingers);
        Celebration.Fingers(rHand, RightRoots, rFingers);
    }

    /// How he holds the bat at this end: which hand is on top (the left for a right-hander), how
    /// far he bends over it, and which way each foot's toes point.
    public void Configure(Hold how, bool leftHandOnTop, float leanDegrees, Quaternion leftFoot, Quaternion rightFoot)
    {
        hold = how;
        topHandLeft = leftHandOnTop;
        lean = leanDegrees;
        leftFootTurn = leftFoot;
        rightFootTurn = rightFoot;
    }

    /// This frame's pose (world): the bat, the hands on it, the feet, the hips and the look.
    public void Pose(in SwingPose p)
    {
        pose = p;
        hasPose = true;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!hasPose || animator == null)
            return;
        // The hips: from where the idle has them to the pose's, turned with the shot, bent over the bat.
        // hipsYaw is the way his hips face (world, 0 = +z: SwingPose); his root faces his stance,
        // so the turn is the difference.
        Vector3 rest = transform.position + Vector3.up * StandingHipsY;
        float turn = Mathf.DeltaAngle(transform.eulerAngles.y, pose.hipsYaw);
        animator.bodyPosition += pose.hips - rest;
        animator.bodyRotation = Quaternion.AngleAxis(turn, Vector3.up) *
                                Quaternion.AngleAxis(lean, transform.right) * animator.bodyRotation;

        Quaternion feetTurn = Quaternion.AngleAxis(turn * FootYawShare, Vector3.up);
        Foot(AvatarIKGoal.LeftFoot, pose.leftFoot, feetTurn * leftFootTurn);
        Foot(AvatarIKGoal.RightFoot, pose.rightFoot, feetTurn * rightFootTurn);

        Vector3 axis = pose.batRotation * Vector3.forward;
        lHolds = rHolds = false;
        if (hold == Hold.BothHands)
        {
            HandOn(topHandLeft, pose.topHand, axis);
            HandOn(!topHandLeft, pose.bottomHand, axis);
        }
        else
        {
            HandOn(false, pose.topHand, axis);
            Release(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow);
        }

        animator.SetLookAtWeight(LookWeight, LookBody, LookHead, LookEyes, LookClamp);
        animator.SetLookAtPosition(Look);
    }

    private void Foot(AvatarIKGoal goal, Vector3 at, Quaternion turn)
    {
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKPosition(goal, at);
        animator.SetIKRotationWeight(goal, FootRotationWeight);
        animator.SetIKRotation(goal, turn);
    }

    /// One hand's wrist beside the handle at `grip`, on the side toward its shoulder, with its
    /// elbow hinted down and out; the finger pass then wraps the hand round the handle.
    private void HandOn(bool left, Vector3 grip, Vector3 axis)
    {
        Transform shoulder = left ? lShoulder : rShoulder;
        Vector3 from = shoulder != null ? shoulder.position : transform.position + Vector3.up * StandingHeadY;
        Vector3 across = Vector3.ProjectOnPlane(grip - from, axis);
        across = across.sqrMagnitude > 1e-6f ? across.normalized : transform.forward;
        Vector3 wrist = grip - across * WristToGrip;
        AvatarIKGoal goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
        animator.SetIKPositionWeight(goal, 1f);
        animator.SetIKPosition(goal, wrist);
        animator.SetIKRotationWeight(goal, 0f);     // the finger pass turns the hand

        AvatarIKHint elbow = left ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
        Vector3 side = left ? -transform.right : transform.right;
        animator.SetIKHintPositionWeight(elbow, ElbowHintWeight);
        animator.SetIKHintPosition(elbow, (from + wrist) * 0.5f + Vector3.down * ElbowDrop + side * ElbowOut);
        if (left) { lGrip = grip; lHolds = true; }
        else { rGrip = grip; rHolds = true; }
    }

    private void Release(AvatarIKGoal goal, AvatarIKHint hint)
    {
        animator.SetIKPositionWeight(goal, 0f);
        animator.SetIKRotationWeight(goal, 0f);
        animator.SetIKHintPositionWeight(hint, 0f);
    }

    private void LateUpdate()
    {
        if (!hasPose)
            return;
        if (!calibrated)
        {
            Celebration.Calibrate(lHand, lFingers, out lCurl, out lPalm);
            Celebration.Calibrate(rHand, rFingers, out rCurl, out rPalm);
            calibrated = true;
        }
        Vector3 axis = pose.batRotation * Vector3.forward;
        if (lHolds) Wrap(lHand, lFingers, lCurl, lPalm, lGrip, axis);
        else if (lHeld) Curl(lFingers, lCurl, 0f);
        if (rHolds) Wrap(rHand, rFingers, rCurl, rPalm, rGrip, axis);
        else if (rHeld) Curl(rFingers, rCurl, 0f);
        lHeld = lHolds;
        rHeld = rHolds;
    }

    /// The hand round the handle: knuckles across it, the thumb rolled down it toward the blade,
    /// the knuckles then moved off the handle's middle to the back of the hand (the handle in the
    /// palm), the fingers closed. The wrist stays where the IK put it.
    private static void Wrap(Transform hand, Transform[,] fingers, float curlSign, Vector3 palmLocal, Vector3 grip, Vector3 axis)
    {
        Transform knuckle = fingers[2, 0] != null ? fingers[2, 0] : fingers[1, 0];
        if (hand == null || knuckle == null)
            return;
        if (!AimKnuckles(hand, knuckle, grip, axis, out Vector3 across))
            return;
        Transform thumb = fingers[0, 0];
        if (thumb != null)
        {
            Vector3 t = Vector3.ProjectOnPlane(thumb.position - hand.position, across);
            if (t.sqrMagnitude > 1e-8f)
            {
                float roll = Mathf.Clamp(Vector3.SignedAngle(t, -axis, across), -MaxWristRoll, MaxWristRoll);
                hand.rotation = Quaternion.AngleAxis(roll, across) * hand.rotation;
            }
        }
        // The palm faces the handle now: aim the knuckles that much to the back of the hand.
        Vector3 palm = hand.TransformDirection(palmLocal);
        AimKnuckles(hand, knuckle, grip - palm * PalmOffset, axis, out _);
        Curl(fingers, curlSign, GripCurl);
    }

    /// The hand turned so its knuckles point at `at` across the handle (square to its axis).
    private static bool AimKnuckles(Transform hand, Transform knuckle, Vector3 at, Vector3 axis, out Vector3 across)
    {
        across = Vector3.ProjectOnPlane(at - hand.position, axis);
        Vector3 now = knuckle.position - hand.position;
        if (across.sqrMagnitude < 1e-6f || now.sqrMagnitude < 1e-8f)
            return false;
        across.Normalize();
        hand.rotation = Quaternion.FromToRotation(now, across) * hand.rotation;
        return true;
    }

    /// Each finger's joints bent to `curl` (0 open .. 1 a fist; as Celebration's hand).
    private static void Curl(Transform[,] fingers, float curlSign, float curl)
    {
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
            {
                Transform b = fingers[f, j];
                if (b == null) continue;
                Vector3 e = b.localEulerAngles;
                float bend = curlSign * curl * (j == 0 ? RootBend : j == 1 ? MiddleBend : TipBend) * (f == 0 ? ThumbBendShare : 1f);
                b.localRotation = Quaternion.Euler(bend, e.y, e.z);
            }
    }
}

/// <summary>
/// Where the partner stands and how he holds the bat when he is not swinging, as pure geometry
/// (no scene, so it is tested): at guard at the striker's crease, and backing up at the
/// non-striker's end; and the grip, from the bat's own markers.
///
/// The pitch runs along x, the bowler at -x. Off is +z for a right-hander (FieldSets), so
/// `offSign` is +1 for a right-hander and -1 for a left-hander, and every across-the-pitch number
/// here is mirrored by it. Every number is a constant to tune in the headset.
/// </summary>
public static class PartnerStance
{
    // ---- At guard (striker's crease) ---------------------------------------------------------
    /// His hips this far in front of the batting stumps, toward the bowler (the popping crease is 1.22).
    public const float HipsX = BallDelivery.BatsmanStumpsX - 0.75f;
    /// His hips this far to the leg side of middle stump (he stands beside his bat, not over it).
    public const float HipsAcross = 0.30f;
    /// Knees bent: his hips this much lower than standing.
    public const float Crouch = 0.10f;
    /// Bent over the bat at the waist (degrees, toward the off side he faces).
    public const float Lean = 16f;
    /// His feet: this far to the leg side, each this far either side of the hips along the pitch.
    public const float FeetAcross = 0.30f, HalfStance = 0.24f;
    /// The ankle (the foot IK target) above the ground.
    public const float AnkleHeight = 0.08f;
    /// The front foot's toes opened toward the bowler (degrees).
    public const float FrontFootOpen = 20f;
    /// Guard on middle stump: the bat grounded on the stumps' line...
    public const float GuardZ = 0f;
    /// ...this far behind his hips (toward the stumps), its toe just off the ground...
    public const float BatToeBehind = 0.05f, BatToeHeight = 0.01f;
    /// ...the handle leaning back from upright toward him (degrees).
    public const float BatLean = 12f;
    /// Where he watches from at guard: the bowler's hand at release.
    public static readonly Vector3 LookAtBowler = new Vector3(BallDelivery.BowlerStumpsX + 1f, 2.0f, 0.5f);

    // ---- Backing up (non-striker's end) ------------------------------------------------------
    /// Just inside the bowler's popping crease (it is 1.22 in front of the stumps)...
    public const float NonStrikerX = BallDelivery.BowlerStumpsX + 1.05f;
    /// ...on the side away from the bowler's run-up, whichever hand he bats (EndPose.NonStrikerSide:
    /// -z for a right-armer, +z for a left-armer); the career player's own spot is
    /// EndPose.NonStrikerHeadXZ (1.6 out).
    public static float NonStrikerZ => 1.3f * EndPose.NonStrikerSide;
    /// His feet this far either side of the middle.
    public const float NonStrikerFeet = 0.14f;
    /// The bat grounded this far in front of him (its toe still behind the popping crease, 0.17
    /// in front of his middle) and out to his right (away from the pitch), leaning back to his
    /// right hand on the top of the handle (degrees).
    public const float NonStrikerBatAhead = 0.12f, NonStrikerBatOut = 0.24f, NonStrikerBatLean = 12f;
    /// Backing up he faces down the pitch, toward the striker: +x, 90 degrees in SwingPose.hipsYaw's terms.
    public const float NonStrikerYaw = 90f;
    /// Where he watches from backing up: the striker.
    public static readonly Vector3 LookAtStriker = new Vector3(BallDelivery.BatsmanStumpsX - 0.8f, 1.5f, 0f);

    // ---- The grip (bat-local, along the long axis +Z: blade -Z, handle +Z) -------------------
    /// The top hand this far up the handle (0 at the shoulder, 1 at its end); the bottom hand just
    /// below it, a hand's width (~0.1 m on this bat) lower.
    public const float TopHandAlongHandle = 0.85f, BottomHandAlongHandle = 0.55f;
    /// No markers and no mesh to read: this bat's (Assets/Resources/Prefabs/Bat.prefab) own.
    public const float DefaultToeZ = -0.89f, DefaultSweetZ = -0.50f, DefaultShoulderZ = 0.167f, DefaultHandleEndZ = 0.89f;
    /// A handle shorter than this (local units) is a bad fit: the grip uses this length.
    public const float MinHandle = 0.2f;

    public static float OffSign(eBattingStyle hand) => hand == eBattingStyle.LeftHanded ? -1f : 1f;

    /// The grip from the bat's markers (BatGeometry: toe, sweet spot, shoulder) and the end of its
    /// handle (the mesh's bounds, readable on device). The sweet spot is on the hitting face.
    public static BatGrip GripFrom(BatGeometry geo, Mesh mesh, float scale)
    {
        float toeZ = DefaultToeZ, sweetZ = DefaultSweetZ, shoulderZ = DefaultShoulderZ, faceY = 0f;
        if (geo != null && geo.IsFitted)
        {
            toeZ = LocalTo(geo.toe, geo.transform).z;
            sweetZ = LocalTo(geo.sweetSpot, geo.transform).z;
            shoulderZ = LocalTo(geo.shoulder, geo.transform).z;
            faceY = geo.faceY;
        }
        else if (mesh != null && mesh.isReadable)
        {
            BatContact.Blade b = BatGeometry.FitBlade(mesh.vertices);
            toeZ = b.toeZ; sweetZ = b.sweetZ; shoulderZ = b.shoulderZ; faceY = b.faceY;
        }
        float endZ = mesh != null ? mesh.bounds.max.z : DefaultHandleEndZ;
        BatGrip g = Grip(toeZ, sweetZ, shoulderZ, endZ, scale);
        g.sweetLocal.y = faceY;
        return g;
    }

    /// The grip from the blade's numbers (bat-local) and the bat's scale.
    public static BatGrip Grip(float toeZ, float sweetZ, float shoulderZ, float handleEndZ, float scale)
    {
        float handle = Mathf.Max(handleEndZ - shoulderZ, MinHandle);
        return new BatGrip
        {
            toeLocal = new Vector3(0f, 0f, toeZ),
            sweetLocal = new Vector3(0f, 0f, sweetZ),
            topHandLocal = new Vector3(0f, 0f, shoulderZ + TopHandAlongHandle * handle),
            bottomHandLocal = new Vector3(0f, 0f, shoulderZ + BottomHandAlongHandle * handle),
            scale = scale,
        };
    }

    /// A point's position in `root`'s local frame, through the hierarchy's own local transforms
    /// (so it works on a prefab asset as well as in the scene).
    public static Vector3 LocalTo(Transform t, Transform root)
    {
        Vector3 p = Vector3.zero;
        for (Transform x = t; x != null && x != root; x = x.parent)
            p = x.localPosition + x.localRotation * Vector3.Scale(x.localScale, p);
        return p;
    }

    /// His hips at guard (world), from his standing hips height.
    public static Vector3 StanceHips(float offSign, float standingHipsY) =>
        new Vector3(HipsX, standingHipsY - Crouch, -HipsAcross * offSign);

    /// His head's z at guard before it is measured: over his hips, bent toward the off side by Lean.
    public static float StanceHeadZ(float offSign, float standingHipsY, float standingHeadY) =>
        StanceHips(offSign, standingHipsY).z + offSign * (standingHeadY - standingHipsY) * Mathf.Sin(Lean * Mathf.Deg2Rad);

    /// The way he faces at guard in SwingPose.hipsYaw's terms (degrees about up, 0 = +z): side-on,
    /// toward the off side - 0 for a right-hander, 180 for a left-hander.
    public static float StrikerYaw(float offSign) => offSign > 0f ? 0f : 180f;

    /// At guard he faces the off side, side-on to the bowler.
    public static Quaternion StrikerFacing(float offSign) => Quaternion.LookRotation(new Vector3(0f, 0f, offSign), Vector3.up);
    /// Backing up he faces down the pitch.
    public static Quaternion NonStrikerFacing => Quaternion.LookRotation(Vector3.right, Vector3.up);

    /// Which way each foot's toes point at guard: the back foot along the crease, the front opened
    /// toward the bowler. The front foot is the left for a right-hander.
    public static void StanceFeetTurn(float offSign, out Quaternion left, out Quaternion right)
    {
        float a = FrontFootOpen * Mathf.Deg2Rad;
        Quaternion back = StrikerFacing(offSign);
        Quaternion front = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, offSign * Mathf.Cos(a)), Vector3.up);
        left = offSign > 0f ? front : back;
        right = offSign > 0f ? back : front;
    }

    /// At guard: bat grounded on middle stump behind his toes, face to the bowler, both hands on it.
    public static SwingPose Stance(in BatGrip g, float offSign, float standingHipsY)
    {
        Vector3 hips = StanceHips(offSign, standingHipsY);
        Vector3 front = new Vector3(HipsX - HalfStance, AnkleHeight, -FeetAcross * offSign);
        Vector3 back = new Vector3(HipsX + HalfStance, AnkleHeight, -FeetAcross * offSign);
        float l = BatLean * Mathf.Deg2Rad;
        Vector3 axis = new Vector3(0f, Mathf.Cos(l), -offSign * Mathf.Sin(l));
        var p = new SwingPose
        {
            hips = hips,
            hipsYaw = StrikerYaw(offSign),
            leftFoot = offSign > 0f ? front : back,
            rightFoot = offSign > 0f ? back : front,
            look = LookAtBowler,
        };
        HoldBat(ref p, g, new Vector3(HipsX + BatToeBehind, BatToeHeight, GuardZ), axis, Vector3.left);
        return p;
    }

    /// Backing up: standing still beside the crease, bat grounded at his right side, his right hand
    /// on the top of the handle. The same for either hand (he is out of the bowler's way on -z).
    public static SwingPose NonStriker(in BatGrip g, float standingHipsY)
    {
        Vector3 right = NonStrikerFacing * Vector3.right;    // his right: -z
        // The bat out away from the pitch: at his right on -z, his left when he stands on +z.
        Vector3 outward = right * -EndPose.NonStrikerSide;
        float l = NonStrikerBatLean * Mathf.Deg2Rad;
        Vector3 axis = new Vector3(-Mathf.Sin(l), Mathf.Cos(l), 0f);
        var p = new SwingPose
        {
            hips = new Vector3(NonStrikerX, standingHipsY, NonStrikerZ),
            hipsYaw = NonStrikerYaw,
            leftFoot = new Vector3(NonStrikerX, AnkleHeight, NonStrikerZ) - right * NonStrikerFeet,
            rightFoot = new Vector3(NonStrikerX, AnkleHeight, NonStrikerZ) + right * NonStrikerFeet,
            look = LookAtStriker,
        };
        Vector3 toe = new Vector3(NonStrikerX + NonStrikerBatAhead, BatToeHeight, NonStrikerZ) + outward * NonStrikerBatOut;
        HoldBat(ref p, g, toe, axis, Vector3.right);
        p.bottomHand = p.topHand;
        return p;
    }

    /// The bat with its toe at `toe`, its long axis along `axis` and its face toward `face`; the
    /// hands where the grip holds it.
    private static void HoldBat(ref SwingPose p, in BatGrip g, Vector3 toe, Vector3 axis, Vector3 face)
    {
        p.batRotation = Quaternion.LookRotation(axis, face);
        p.batPosition = toe - p.batRotation * (g.toeLocal * g.scale);
        p.topHand = p.batPosition + p.batRotation * (g.topHandLocal * g.scale);
        p.bottomHand = p.batPosition + p.batRotation * (g.bottomHandLocal * g.scale);
    }

    /// A pose that has a bat in it (a default one, from a stub or a missing plan, does not).
    public static bool Valid(in SwingPose p)
    {
        Quaternion q = p.batRotation;
        return q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.5f;
    }

    /// A swing's pose with anything it left out (zero) taken from where it can be: the hands from
    /// the bat and the grip, the feet and the hips (with their turn) from the rest pose. A part
    /// left at the world origin would pull his limbs to the middle of the ground.
    public static void Complete(ref SwingPose p, in BatGrip g, in SwingPose rest)
    {
        if (Unset(p.topHand)) p.topHand = p.batPosition + p.batRotation * (g.topHandLocal * g.scale);
        if (Unset(p.bottomHand)) p.bottomHand = p.batPosition + p.batRotation * (g.bottomHandLocal * g.scale);
        if (Unset(p.leftFoot)) p.leftFoot = rest.leftFoot;
        if (Unset(p.rightFoot)) p.rightFoot = rest.rightFoot;
        if (Unset(p.hips)) { p.hips = rest.hips; p.hipsYaw = rest.hipsYaw; }
    }

    private static bool Unset(Vector3 v) => v.sqrMagnitude < 1e-8f;

    /// From one pose toward another by `k` (0..1): BackToStance's ease.
    public static SwingPose Blend(in SwingPose a, in SwingPose b, float k) => new SwingPose
    {
        batPosition = Vector3.Lerp(a.batPosition, b.batPosition, k),
        batRotation = Quaternion.Slerp(a.batRotation, b.batRotation, k),
        hips = Vector3.Lerp(a.hips, b.hips, k),
        hipsYaw = Mathf.LerpAngle(a.hipsYaw, b.hipsYaw, k),
        leftFoot = Vector3.Lerp(a.leftFoot, b.leftFoot, k),
        rightFoot = Vector3.Lerp(a.rightFoot, b.rightFoot, k),
        look = Vector3.Lerp(a.look, b.look, k),
        topHand = Vector3.Lerp(a.topHand, b.topHand, k),
        bottomHand = Vector3.Lerp(a.bottomHand, b.bottomHand, k),
    };
}
