using UnityEngine;

/// <summary>
/// Full-body reach for a humanoid (Mecanim IK, no extra packages). The owner sets a world point
/// for the hands and a weight every frame; this poses the whole body to get them there:
///   1. the pelvis is lowered and shifted (Animator.bodyPosition) and pitched forward at the hips
///      (Animator.bodyRotation),
///   2. both feet are held on their animated ground positions with foot IK and knee hints forward
///      and slightly out, so lowering the pelvis BENDS THE KNEES instead of sinking the feet,
///   3. the spine bones flex, turn and lean toward the target (SetBoneLocalRotation),
///   4. the hands close either side of the target with elbow hints out and down, and the head
///      looks at it.
/// How much each joint gives is ReachMath's stiffness chain (knees soft, hips moderate, spine
/// stiff); every output goes through a critically damped spring so nothing pops.
///
/// After the animator has run (LateUpdate) the hands are turned so the fingers point along the
/// reach and the palms face the ball, and <see cref="PalmCentre"/> reports where the palms really
/// are - the owners gather the ball only when it actually reaches them.
///
/// Needs the controller layer's IK Pass on (Fielder Controller's base layer has it).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Animator))]
public class HumanoidReach : MonoBehaviour
{
    [Header("Stiffness: lower bends sooner (effort units)")]
    [SerializeField] private float kneeStiffness = 0.6f;
    [SerializeField] private float hipStiffness = 1.0f;
    [SerializeField] private float spineStiffness = 2.0f;
    [Header("Range")]
    // A ball on the grass needs a deep squat and a real hip bend: at 0.45 m and 60 degrees it was
    // only just reachable, and only straight in front - one beside a fielder stayed on the ground.
    [SerializeField] private float maxPelvisDrop = 0.55f;
    [SerializeField] private float maxHipHinge = 75f;
    [SerializeField] private float maxSpineFlex = 25f;
    [Header("Smoothing: critically damped, settles in about 4 / omega seconds")]
    [SerializeField] private float poseOmega = 10f;
    [SerializeField] private float handOmega = 16f;
    [SerializeField] private float weightOmega = 9f;

    // ---- Inputs, set every frame by the owner ---------------------------------------------------
    /// World point the palms close on.
    [System.NonSerialized] public Vector3 handTarget;
    /// 0..1: how much of the reach to apply.
    [System.NonSerialized] public float weight;
    /// Pelvis drop (world metres) held whatever the reach - the keeper's crouch.
    [System.NonSerialized] public float minCrouch;
    /// Distance between the palms (a ball is 0.072 m across).
    [System.NonSerialized] public float handGap = 0.1f;
    /// How much the left hand takes part, 1 = both hands on the target, 0 = the right hand alone
    /// (the left arm goes back to the animation). A keeper holds a taken ball in one glove.
    [System.NonSerialized] public float leftHandShare = 1f;
    /// Zero: palms face each other across the target (gathering). Otherwise the palms face this
    /// world direction (a keeper's gloves toward the incoming ball) with fingers up, or down for a
    /// target below the hips.
    [System.NonSerialized] public Vector3 palmFacing;
    /// Extra distance from the palm to where the ball sits (big keeping gloves), metres.
    [System.NonSerialized] public float palmExtra;
    [System.NonSerialized] public bool look;
    [System.NonSerialized] public Vector3 lookTarget;
    /// Feet placed by the owner (keeper footwork) instead of by the animation.
    [System.NonSerialized] public bool feetOverride;
    [System.NonSerialized] public Vector3 leftFootTarget, rightFootTarget;

    // ---- State ------------------------------------------------------------------------------------
    private Animator animator;
    private bool ready;
    private ReachMath.Body body;
    private ReachMath.Limits limits;
    private float scale = 1f;
    private Transform hips, leftUpperArm, rightUpperArm, leftHand, rightHand;
    private Transform leftMiddle, rightMiddle, leftIndex, rightIndex, leftLittle, rightLittle;
    private Transform[] spineBones;
    private readonly Quaternion[] spineBase = new Quaternion[3];
    private bool spineFrozen;
    private float palmLength = 0.09f;

    private float smoothedWeight, weightVelocity;
    private Vector3 handPos, handVelocity;
    private ReachMath.Pose pose;
    private ReachMath.Pose poseVelocity;

    public bool Ready => ready;
    public float SmoothedWeight => smoothedWeight;
    /// Current pelvis drop, world metres.
    public float CurrentDrop => pose.drop * scale;
    public Transform LeftHandBone => leftHand;
    public Transform RightHandBone => rightHand;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman || animator.avatar == null)
            return;
        // Pose once per rendered frame. On the physics clock (Fixed, 100 Hz) a rendered frame can
        // get no animator step at all - measured: 94 of 150 frames in the editor - and then the
        // hand keeps last frame's wrist turn and LateUpdate's AlignHand stacks another on it, which
        // the next step snaps back: gloves flicking 20-60 degrees with no animation behind it.
        animator.updateMode = AnimatorUpdateMode.Normal;
        CacheBones();
        if (hips == null || leftHand == null || rightHand == null || leftUpperArm == null || rightUpperArm == null)
            return;
        Measure();
        limits = ReachMath.Limits.Default;
        limits.kneeStiffness = kneeStiffness; limits.hipStiffness = hipStiffness; limits.spineStiffness = spineStiffness;
        limits.maxDrop = maxPelvisDrop / scale; limits.maxHinge = maxHipHinge; limits.maxSpine = maxSpineFlex;
        handPos = leftHand.position;
        ready = true;
    }

    private void CacheBones()
    {
        Transform B(HumanBodyBones b) => animator.GetBoneTransform(b);
        hips = B(HumanBodyBones.Hips);
        leftUpperArm = B(HumanBodyBones.LeftUpperArm);
        rightUpperArm = B(HumanBodyBones.RightUpperArm);
        leftHand = B(HumanBodyBones.LeftHand);
        rightHand = B(HumanBodyBones.RightHand);
        leftMiddle = B(HumanBodyBones.LeftMiddleProximal);
        rightMiddle = B(HumanBodyBones.RightMiddleProximal);
        leftIndex = B(HumanBodyBones.LeftIndexProximal);
        rightIndex = B(HumanBodyBones.RightIndexProximal);
        leftLittle = B(HumanBodyBones.LeftLittleProximal);
        rightLittle = B(HumanBodyBones.RightLittleProximal);
        spineBones = new[] { B(HumanBodyBones.Spine), B(HumanBodyBones.Chest), B(HumanBodyBones.UpperChest) };
    }

    /// Body proportions from the rig as it stands (character space, so a scaled model still
    /// solves in its own units).
    private void Measure()
    {
        scale = Mathf.Max(0.01f, transform.lossyScale.y);
        Vector3 L(Transform t) => transform.InverseTransformPoint(t.position);
        Vector3 shoulders = 0.5f * (L(leftUpperArm) + L(rightUpperArm));
        Vector3 hip = L(hips);
        Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        float arm = lower != null ? Vector3.Distance(L(leftUpperArm), L(lower)) + Vector3.Distance(L(lower), L(leftHand)) : 0.55f / scale;
        if (leftMiddle != null)
            palmLength = Vector3.Distance(leftHand.position, leftMiddle.position);
        body = new ReachMath.Body
        {
            hipHeight = Mathf.Max(0.3f / scale, hip.y),
            torsoLength = Mathf.Max(0.2f / scale, shoulders.y - hip.y),
            // Shoulder to palm centre, less 5% so the elbow never locks straight.
            armReach = (arm + 0.6f * palmLength / scale) * 0.95f,
            shoulderWidth = Vector3.Distance(L(leftUpperArm), L(rightUpperArm)),
        };
    }

    // ---- IK pass ------------------------------------------------------------------------------------

    private void OnAnimatorIK(int layerIndex)
    {
        if (!ready || layerIndex != 0)
            return;
        float dt = Time.deltaTime;
        smoothedWeight = Mathf.Clamp01(ReachMath.Spring(smoothedWeight, ref weightVelocity, Mathf.Clamp01(weight), weightOmega, dt));
        // Idle hands: follow the target exactly so a new reach starts from where it is asked for,
        // not from a stale point on the other side of the body.
        if (smoothedWeight < 0.01f)
        {
            handPos = handTarget;
            handVelocity = Vector3.zero;
        }
        handPos = ReachMath.Spring(handPos, ref handVelocity, handTarget, handOmega, dt);

        ReachMath.Pose want = ReachMath.Pose.Lerp(default, ReachMath.Solve(body, limits, transform.InverseTransformPoint(handPos)), smoothedWeight);
        want.drop = Mathf.Min(limits.maxDrop, Mathf.Max(want.drop, minCrouch / scale));
        SpringPose(want, dt);

        ApplyBodyAndFeet();
        ApplySpine();
        ApplyHands();
        animator.SetLookAtWeight(look ? Mathf.Max(0.6f, smoothedWeight) * 0.9f : 0f, 0.1f, 0.8f, 1f, 0.5f);
        if (look)
            animator.SetLookAtPosition(lookTarget);
    }

    private void SpringPose(ReachMath.Pose want, float dt)
    {
        pose.drop = ReachMath.Spring(pose.drop, ref poseVelocity.drop, want.drop, poseOmega, dt);
        pose.hinge = ReachMath.Spring(pose.hinge, ref poseVelocity.hinge, want.hinge, poseOmega, dt);
        pose.spine = ReachMath.Spring(pose.spine, ref poseVelocity.spine, want.spine, poseOmega, dt);
        pose.shift = ReachMath.Spring(pose.shift, ref poseVelocity.shift, want.shift, poseOmega, dt);
        pose.twist = ReachMath.Spring(pose.twist, ref poseVelocity.twist, want.twist, poseOmega, dt);
        pose.roll = ReachMath.Spring(pose.roll, ref poseVelocity.roll, want.roll, poseOmega, dt);
    }

    /// Lower, shift and pitch the pelvis, with the feet held where they were: the knees bend.
    private void ApplyBodyAndFeet()
    {
        Vector3 up = transform.up, fwd = transform.forward, right = transform.right;
        // Read the animated foot goals before the body moves - they are world positions, so they
        // stay on the ground while the pelvis comes down.
        Vector3 lf = feetOverride ? leftFootTarget : animator.GetIKPosition(AvatarIKGoal.LeftFoot);
        Vector3 rf = feetOverride ? rightFootTarget : animator.GetIKPosition(AvatarIKGoal.RightFoot);
        Quaternion lr = feetOverride ? transform.rotation : animator.GetIKRotation(AvatarIKGoal.LeftFoot);
        Quaternion rr = feetOverride ? transform.rotation : animator.GetIKRotation(AvatarIKGoal.RightFoot);

        float active = Mathf.Clamp01(Mathf.Max(smoothedWeight, pose.drop * scale / 0.03f));
        if (active < 0.001f && !feetOverride)
        {
            SetFootWeights(0f);
            return;
        }
        animator.bodyPosition += (-up * pose.drop - fwd * ReachMath.HipsBack(body, pose.hinge) + right * pose.shift) * scale;
        animator.bodyRotation = Quaternion.AngleAxis(pose.twist * 0.3f, up) *
                                Quaternion.AngleAxis(pose.hinge, right) *
                                Quaternion.AngleAxis(-pose.roll * 0.5f, fwd) * animator.bodyRotation;

        SetFootWeights(feetOverride ? 1f : active);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, lf);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rf);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, lr);
        animator.SetIKRotation(AvatarIKGoal.RightFoot, rr);
        // Knees forward and a little out, the deeper the squat the further out (a keeper's knees
        // splay; nobody squats knock-kneed).
        float outward = (0.1f + 0.25f * pose.drop / Mathf.Max(0.01f, limits.maxDrop)) * scale;
        Vector3 pelvis = animator.bodyPosition;
        animator.SetIKHintPosition(AvatarIKHint.LeftKnee, 0.5f * (pelvis + lf) + (fwd * 0.6f * scale - right * outward));
        animator.SetIKHintPosition(AvatarIKHint.RightKnee, 0.5f * (pelvis + rf) + (fwd * 0.6f * scale + right * outward));
    }

    private void SetFootWeights(float w)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, w);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, w);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, w);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, w);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, w);
    }

    /// Flex, turn and lean the spine, spread over its bones. SetBoneLocalRotation replaces the
    /// animated rotation, and in the IK pass the bone transforms still hold last frame's result,
    /// so the animated spine is captured once when a reach starts and the flex applied on top of
    /// that; it is released back to the animation when the reach is over.
    private void ApplySpine()
    {
        bool active = smoothedWeight > 0.005f || Mathf.Abs(pose.spine) > 0.05f || Mathf.Abs(pose.twist) > 0.05f;
        if (!active)
        {
            spineFrozen = false;
            return;
        }
        if (!spineFrozen)
        {
            for (int i = 0; i < spineBones.Length; i++)
                if (spineBones[i] != null) spineBase[i] = spineBones[i].localRotation;
            spineFrozen = true;
        }
        float twist = pose.twist * 0.7f;   // the pelvis already took 30%
        for (int i = 0; i < spineBones.Length; i++)
        {
            Transform bone = spineBones[i];
            if (bone == null || bone.parent == null)
                continue;
            float share = ReachMath.SpineShare[i];
            Transform parent = bone.parent;
            Quaternion delta = Quaternion.AngleAxis(pose.spine * share, parent.InverseTransformDirection(transform.right)) *
                               Quaternion.AngleAxis(twist * share, parent.InverseTransformDirection(transform.up)) *
                               Quaternion.AngleAxis(-pose.roll * share, parent.InverseTransformDirection(transform.forward));
            animator.SetBoneLocalRotation(SpineBone(i), delta * spineBase[i]);
        }
    }

    private static HumanBodyBones SpineBone(int i) =>
        i == 0 ? HumanBodyBones.Spine : i == 1 ? HumanBodyBones.Chest : HumanBodyBones.UpperChest;

    private void ApplyHands()
    {
        float w = smoothedWeight, share = Mathf.Clamp01(leftHandShare);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w * share);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w * share);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, w);
        if (w < 0.001f)
            return;
        Frame(out Vector3 finger, out Vector3 side);
        // IK moves the wrist; put it so the palm centre, not the wrist, lands on the target.
        float reachBack = (0.6f * palmLength + palmExtra);
        Vector3 facing = smoothFacing;
        // Two hands sit either side of the target; one hand alone goes right onto it.
        Vector3 lPalm = handPos - side * (handGap * 0.5f), rPalm = handPos + side * (handGap * 0.5f * share);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, lPalm - finger * reachBack - facing * 0.02f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rPalm - finger * reachBack - facing * 0.02f);
        Vector3 up = transform.up, right = transform.right, fwd = transform.forward;
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftUpperArm.position + (-right * 0.35f - up * 0.3f - fwd * 0.1f) * scale);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightUpperArm.position + (right * 0.35f - up * 0.3f - fwd * 0.1f) * scale);
    }

    /// Fastest the hands' frame (finger, side and palm directions) turns, degrees per second.
    private const float FrameTurnSpeed = 360f;
    private int frameStamp = -1;
    private bool frameSet;
    private Vector3 smoothFinger, smoothSide, smoothFacing;

    /// Finger direction and the across-the-hands direction for the current target, worked out once
    /// a frame and turned toward the raw answer at FrameTurnSpeed. The raw answer can jump: the
    /// palms face back along the ball's velocity, which turns sharply at the bounce, and the finger
    /// rule has an either-or fallback. Fed straight to the arm IK, a jump in these moved the wrist
    /// target and the solver swung the elbow to a new side - measured at the keeper, forearm turns
    /// of 22-70 degrees and the elbow moving up to 10 cm in one frame, the gloves flicking with them.
    private void Frame(out Vector3 finger, out Vector3 side)
    {
        if (frameStamp != Time.frameCount)
        {
            frameStamp = Time.frameCount;
            RawFrame(out Vector3 f, out Vector3 s);
            Vector3 face = palmFacing.sqrMagnitude > 1e-6f ? palmFacing.normalized : Vector3.zero;
            if (!frameSet)
            {
                smoothFinger = f; smoothSide = s; smoothFacing = face;
                frameSet = true;
            }
            else
            {
                float step = FrameTurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
                smoothFinger = Vector3.RotateTowards(smoothFinger, f, step, 0f).normalized;
                smoothSide = Vector3.RotateTowards(smoothSide, s, step, 0f).normalized;
                smoothFacing = face == Vector3.zero ? Vector3.zero
                             : smoothFacing == Vector3.zero ? face
                             : Vector3.RotateTowards(smoothFacing, face, step, 0f).normalized;
            }
        }
        finger = smoothFinger;
        side = smoothSide;
    }

    private void RawFrame(out Vector3 finger, out Vector3 side)
    {
        Vector3 shoulders = 0.5f * (leftUpperArm.position + rightUpperArm.position);
        Vector3 reach = handPos - shoulders;
        if (palmFacing.sqrMagnitude > 1e-6f)
        {
            // Gloves up for a ball above the waist, down (fingers to the ground) below it.
            // Palms facing up (cradling the ball): fingers point forward instead.
            Vector3 facing = palmFacing.normalized;
            bool low = transform.InverseTransformPoint(handPos).y < body.hipHeight - pose.drop;
            finger = Vector3.ProjectOnPlane(low ? -transform.up : transform.up, facing);
            if (finger.sqrMagnitude < 0.04f)
                finger = Vector3.ProjectOnPlane(transform.forward, facing);
            finger = finger.normalized;
            side = Vector3.Cross(facing, finger).normalized;
            if (Vector3.Dot(side, transform.right) < 0f) side = -side;
            return;
        }
        finger = reach.sqrMagnitude > 1e-6f ? reach.normalized : transform.forward;
        side = Vector3.ProjectOnPlane(transform.right, finger);
        side = side.sqrMagnitude > 1e-6f ? side.normalized : transform.right;
    }

    // ---- After the animator: wrists, and where the palms really are -------------------------------

    private void LateUpdate()
    {
        if (!ready || smoothedWeight < 0.01f)
        {
            leftWrist = rightWrist = Quaternion.identity;
            frameSet = false;   // start the next reach from its own frame, not a stale one
            return;
        }
        Frame(out Vector3 finger, out Vector3 side);
        bool facing = smoothFacing != Vector3.zero;
        Vector3 lPalm = facing ? smoothFacing : side, rPalm = facing ? smoothFacing : -side;
        float dt = Time.deltaTime;
        leftWrist = AlignHand(leftHand, leftMiddle, leftIndex, leftLittle, true, finger, lPalm,
                              smoothedWeight * Mathf.Clamp01(leftHandShare), leftWrist, dt);
        rightWrist = AlignHand(rightHand, rightMiddle, rightIndex, rightLittle, false, finger, rPalm, smoothedWeight, rightWrist, dt);
    }

    /// Most a wrist turns from the animated hand, and how fast it gets there.
    private const float MaxWristTurn = 100f, WristSpeed = 720f;
    /// Each wrist's turn on top of the animated hand, carried frame to frame.
    private Quaternion leftWrist = Quaternion.identity, rightWrist = Quaternion.identity;

    /// Turn a hand toward fingers along `finger` and palm facing `palm`, as one rotation from the
    /// animated hand, capped at what a wrist can do and moving at a wrist's speed. Returns the turn
    /// applied, for the next frame to continue from.
    ///
    /// It used to bend the fingers over, then twist by a signed angle clamped to +-90. Where the
    /// twist wanted was near half a turn, that angle crossed +-180 and flipped sign, and the clamp
    /// threw the glove from +90 to -90 in one frame - measured 109 and 180 degree single-frame jumps
    /// at the keeper's gloves. Now the target is a whole orientation and the glove can only travel
    /// WristSpeed toward it, so there is nothing to flip.
    private static Quaternion AlignHand(Transform hand, Transform middle, Transform index, Transform little, bool left,
                                        Vector3 finger, Vector3 palm, float w, Quaternion current, float dt)
    {
        if (hand == null || middle == null)
            return Quaternion.identity;
        Vector3 have = (middle.position - hand.position).normalized;
        Quaternion wanted;
        if (index != null && little != null)
        {
            Vector3 haveNormal = PalmNormal(have, index.position - little.position, left);
            Vector3 wantNormal = Vector3.ProjectOnPlane(palm, finger);
            if (wantNormal.sqrMagnitude < 1e-4f)
                wantNormal = Vector3.ProjectOnPlane(haveNormal, finger);
            wanted = Quaternion.LookRotation(finger, wantNormal) * Quaternion.Inverse(Quaternion.LookRotation(have, haveNormal));
        }
        else
        {
            wanted = Quaternion.FromToRotation(have, finger);
        }
        wanted = Quaternion.RotateTowards(Quaternion.identity, wanted, MaxWristTurn);
        wanted = Quaternion.Slerp(Quaternion.identity, wanted, w);
        Quaternion turn = Quaternion.RotateTowards(current, wanted, WristSpeed * dt);
        hand.rotation = turn * hand.rotation;
        return turn;
    }

    /// Palm normal from the finger direction and the little-to-index direction across the
    /// knuckles (right hand palm-down, fingers +Z: across is -X and the palm faces -Y).
    public static Vector3 PalmNormal(Vector3 finger, Vector3 across, bool left) =>
        (left ? Vector3.Cross(across, finger) : Vector3.Cross(finger, across)).normalized;

    /// Where the ball sits in this hand right now (after IK and the wrist turn).
    public Vector3 PalmCentre(bool left)
    {
        Transform hand = left ? leftHand : rightHand, middle = left ? leftMiddle : rightMiddle;
        Transform index = left ? leftIndex : rightIndex, little = left ? leftLittle : rightLittle;
        if (hand == null)
            return transform.position + transform.up * scale;
        if (middle == null)
            return hand.position;
        Vector3 along = (middle.position - hand.position).normalized;
        Vector3 c = hand.position + along * (0.6f * palmLength + palmExtra);
        if (index != null && little != null)
            c += PalmNormal(along, index.position - little.position, left) * (0.03f * scale);
        return c;
    }

    /// Between the two palms: where a two-handed take holds the ball.
    public Vector3 HoldPoint => Vector3.Lerp(PalmCentre(false), 0.5f * (PalmCentre(true) + PalmCentre(false)), Mathf.Clamp01(leftHandShare));

    /// Closest palm to a ball that moved from a to b this frame, and how close it came.
    public float ClosestPalm(Vector3 a, Vector3 b) =>
        Mathf.Min(ReachMath.SegmentDistance(a, b, PalmCentre(true)), ReachMath.SegmentDistance(a, b, PalmCentre(false)));

    /// Drop the reach at once (a new delivery): no springs back from a stale pose.
    public void ResetPose()
    {
        smoothedWeight = weightVelocity = 0f;
        pose = default;
        poseVelocity = default;
        handVelocity = Vector3.zero;
        spineFrozen = false;
        leftWrist = rightWrist = Quaternion.identity;
        frameSet = false;
    }
}
