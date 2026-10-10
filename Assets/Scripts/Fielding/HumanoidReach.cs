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
public partial class HumanoidReach : MonoBehaviour
{
    // A fielder picks the ball up with knees and hips bending together, back at about 45-60
    // degrees - down behind it. With the knees softest (and 0.55 m of drop) every reach down went
    // into a squat, and a low ball had fielders folding onto the grass, crawling; with the hips
    // first (0.6 against the knees' 1.0, 80 degrees of hinge, 0.35 m of drop) he folded into a
    // stiff-legged toe-touch, back flat over the grass (filmed from four sides). Knees and hips
    // even now, spine last; the keeper's prefab keeps his own squat values.
    [Header("Stiffness: lower bends sooner (effort units)")]
    [SerializeField] private float kneeStiffness = 0.8f;
    [SerializeField] private float hipStiffness = 0.8f;
    [SerializeField] private float spineStiffness = 1.6f;
    [Header("Range")]
    // With the hips at 70 degrees and 0.45 m of knee bend the shoulders are ~0.65 m up: a 0.62 m
    // arm, with the spine's flex, gets the hands to a ball on the grass in front.
    [SerializeField] private float maxPelvisDrop = 0.45f;
    [SerializeField] private float maxHipHinge = 70f;
    [SerializeField] private float maxSpineFlex = 20f;
    [Header("Smoothing: critically damped, settles in about 4 / omega seconds")]
    // Body and weight settle in ~0.4 s: softer (8, 7), a ground ball arrived before he was down.
    [SerializeField] private float poseOmega = 10f;
    [SerializeField] private float handOmega = 12f;
    [SerializeField] private float weightOmega = 9f;

    // ---- Inputs, set every frame by the owner ---------------------------------------------------
    /// World point the palms close on.
    [System.NonSerialized] public Vector3 handTarget;
    /// 0..1: how much of the reach to apply.
    [System.NonSerialized] public float weight;
    /// Multiplies every smoothing rate: above 1 he gets down and out to it faster (a fielder
    /// snapping down to a fast ground ball). Traced: at 1, a 20 m/s ball 2.5 m to his side passed
    /// with him 61% down, hands 0.55 m above it.
    [System.NonSerialized] public float quickness = 1f;
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
    private float footBlend;
    private const float FootBlendTime = 0.2f;
    [System.NonSerialized] public Vector3 leftFootTarget, rightFootTarget;

    // ---- State ------------------------------------------------------------------------------------
    private Animator animator;
    private bool ready;
    private ReachMath.Body body;
    private ReachMath.Limits limits;
    private float scale = 1f;
    private Transform hips, leftUpperArm, rightUpperArm, leftHand, rightHand, head, chest, neck;
    private Transform leftUpperLeg, leftLowerLeg, rightUpperLeg, rightLowerLeg;
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
        MeasureGuardBody();
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
        head = B(HumanBodyBones.Head);
        chest = B(HumanBodyBones.Chest) ?? B(HumanBodyBones.Spine);
        neck = B(HumanBodyBones.Neck) ?? head;
        leftUpperLeg = B(HumanBodyBones.LeftUpperLeg);
        leftLowerLeg = B(HumanBodyBones.LeftLowerLeg);
        rightUpperLeg = B(HumanBodyBones.RightUpperLeg);
        rightLowerLeg = B(HumanBodyBones.RightLowerLeg);
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
        float quick = Mathf.Max(1f, quickness);
        smoothedWeight = Mathf.Clamp01(ReachMath.Spring(smoothedWeight, ref weightVelocity, Mathf.Clamp01(weight), weightOmega * quick, dt));
        // Idle hands: follow the target exactly so a new reach starts from where it is asked for,
        // not from a stale point on the other side of the body.
        if (smoothedWeight < 0.01f)
        {
            handPos = handTarget;
            handVelocity = Vector3.zero;
        }
        handPos = ReachMath.Spring(handPos, ref handVelocity, handTarget, handOmega * quick, dt);

        ReachMath.Pose want = ReachMath.Pose.Lerp(default, ReachMath.Solve(body, limits, transform.InverseTransformPoint(handPos)), smoothedWeight);
        want.drop = Mathf.Min(limits.maxDrop, Mathf.Max(want.drop, minCrouch / scale));
        // Balance: crouched, the trunk leans forward from the hips to keep his weight over his
        // feet - the deeper the knees, the further over. Without it a reach to his chest while
        // still crouched (a keeper standing up with the ball) left the knees bent and the trunk
        // bolt upright: sitting back on his heels, leaning back (filmed).
        float crouched = Mathf.Clamp01(want.drop / Mathf.Max(1e-3f, limits.maxDrop));
        want.hinge = Mathf.Max(want.hinge, Mathf.Min(limits.maxHinge, BalanceHinge * crouched));
        SpringPose(want, dt * quick);

        ApplyBodyAndFeet();
        ApplySpine();
        PoseGuardBody();   // the body as posed this pass, for keeping the hands out of it
        ApplyHands();
        // The head turns to what he looks at as a head does - at most LookTurnSpeed - and looking
        // on or off fades in and out. Set straight to the target, it snapped: at a keeper's take
        // his head jerked 16-19 cm in a frame from the ball on the grass up to the batter.
        float wantLook = look ? Mathf.Max(0.6f, smoothedWeight) * 0.9f : 0f;
        lookWeight = Mathf.MoveTowards(lookWeight, wantLook, dt / LookFadeTime);
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 eyes = headBone != null ? headBone.position : transform.position + transform.up * 1.6f * scale;
        if (look)
        {
            Vector3 toTarget = lookTarget - eyes;
            if (toTarget.sqrMagnitude > 1e-4f)
            {
                if (lookDir == Vector3.zero)
                    lookDir = toTarget.normalized;
                lookDir = Vector3.RotateTowards(lookDir, toTarget.normalized, LookTurnSpeed * Mathf.Deg2Rad * dt, 0f).normalized;
                lookDistance = toTarget.magnitude;
            }
        }
        animator.SetLookAtWeight(lookWeight, 0.1f, 0.8f, 1f, 0.5f);
        if (lookWeight > 0f && lookDir != Vector3.zero)
            animator.SetLookAtPosition(eyes + lookDir * Mathf.Max(lookDistance, 1f));
    }

    /// Forward lean of the trunk (degrees at the hips) in a full crouch, scaled down as he rises.
    private const float BalanceHinge = 35f;

    private const float LookTurnSpeed = 270f, LookFadeTime = 0.3f;
    private float lookWeight, lookDistance = 5f;
    private Vector3 lookDir;

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
        // Between the animation's own feet and the planted ones (feetOverride), blended over
        // FootBlendTime: switched in one frame - a keeper getting up to throw, setting off to fetch,
        // settling for the next ball - the legs and the hips over them jumped (measured 14-17 cm at
        // the head in a frame).
        footBlend = Mathf.MoveTowards(footBlend, feetOverride ? 1f : 0f, Time.deltaTime / FootBlendTime);
        Vector3 lf = Vector3.Lerp(animator.GetIKPosition(AvatarIKGoal.LeftFoot), leftFootTarget, footBlend);
        Vector3 rf = Vector3.Lerp(animator.GetIKPosition(AvatarIKGoal.RightFoot), rightFootTarget, footBlend);
        Quaternion lr = Quaternion.Slerp(animator.GetIKRotation(AvatarIKGoal.LeftFoot), transform.rotation, footBlend);
        Quaternion rr = Quaternion.Slerp(animator.GetIKRotation(AvatarIKGoal.RightFoot), transform.rotation, footBlend);

        float active = Mathf.Clamp01(Mathf.Max(smoothedWeight, pose.drop * scale / 0.03f));
        kneesHinted = false;
        if (active < 0.001f && footBlend < 0.001f)
        {
            SetFootWeights(0f);
            return;
        }
        animator.bodyPosition += (-up * pose.drop - fwd * ReachMath.HipsBack(body, pose.hinge) + right * pose.shift) * scale;
        animator.bodyRotation = Quaternion.AngleAxis(pose.twist * 0.3f, up) *
                                Quaternion.AngleAxis(pose.hinge, right) *
                                Quaternion.AngleAxis(-pose.roll * 0.5f, fwd) * animator.bodyRotation;
        KeepBodyOverFeet(lf, rf);
        KeepFeetReachable(lf, rf);

        SetFootWeights(Mathf.Max(footBlend, active));
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, lf);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, rf);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, lr);
        animator.SetIKRotation(AvatarIKGoal.RightFoot, rr);
        // Knees forward and a little out, the deeper the squat the further out (a keeper's knees
        // splay; nobody squats knock-kneed).
        float outward = (0.1f + 0.25f * pose.drop / Mathf.Max(0.01f, limits.maxDrop)) * scale;
        Vector3 pelvis = animator.bodyPosition;
        kneeHintL = 0.5f * (pelvis + lf) + (fwd * 0.6f * scale - right * outward);
        kneeHintR = 0.5f * (pelvis + rf) + (fwd * 0.6f * scale + right * outward);
        kneesHinted = true;
        animator.SetIKHintPosition(AvatarIKHint.LeftKnee, kneeHintL);
        animator.SetIKHintPosition(AvatarIKHint.RightKnee, kneeHintR);
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
        if (spineLetGo && weight > 0.05f)
            spineLetGo = false;   // a new reach
        if (!active || spineLetGo)
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

    /// Bend like a fielder picking a ball up - hips first, knees soft, spine last - whatever this
    /// body's own settings are. The keeper's are for sitting in his crouch (knees softest, 0.45 m
    /// of drop); fetching a loose ball with them folded him onto his heels, head between his
    /// knees, gloves short of the ball (filmed). RestoreBend puts his own back.
    public void UsePickUpBend()
    {
        if (!ready) return;
        // Mostly from the hips, knees part bent: the chest stays above the knees and the arms go
        // down in front of them. Knees as free as the hips, he sank 0.38 m into a squat with his
        // chest folded down between his knees - no room left there for his arms, which went
        // through his thighs or tucked into his chest whichever way they were steered (measured).
        limits.kneeStiffness = 1.4f; limits.hipStiffness = 0.6f; limits.spineStiffness = 1.6f;
        limits.maxDrop = 0.35f / scale; limits.maxHinge = 80f; limits.maxSpine = 25f;
    }

    public void RestoreBend()
    {
        if (!ready) return;
        limits.kneeStiffness = kneeStiffness; limits.hipStiffness = hipStiffness; limits.spineStiffness = spineStiffness;
        limits.maxDrop = maxPelvisDrop / scale; limits.maxHinge = maxHipHinge; limits.maxSpine = maxSpineFlex;
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

    /// How a take is made, from where the hands meet the ball against the body as it is now:
    /// fingers up (palms to the ball) above the chest, fingers down (cupped) below it, fingers out
    /// to the side for one wide of the body at waist-to-chest height. Measured from the shoulders
    /// in his current posture - a keeper rising out of his crouch, a fielder bent to a ball - not
    /// from fixed heights: those had a keeper taking waist-high balls overhand and chest-high ones
    /// underhand, depending on the last ball.
    private enum Grip { Up, Down, Out }
    /// Below the shoulders by this much (x scale) is the chest line; the margin either side that
    /// must be crossed to change grip mid-reach, so it cannot flicker on the line.
    private const float ChestBelowShoulders = 0.22f, GripHysteresis = 0.08f;
    /// A ball this far (x scale) out to the side of the shoulder is taken fingers out.
    private const float WideOfShoulder = 0.3f;
    private Grip grip;
    private bool gripChosen;

    private void RawFrame(out Vector3 finger, out Vector3 side)
    {
        Vector3 shoulders = 0.5f * (leftUpperArm.position + rightUpperArm.position);
        Vector3 reach = handPos - shoulders;
        if (palmFacing.sqrMagnitude > 1e-6f)
        {
            Vector3 facing = palmFacing.normalized;
            Vector3 up = transform.up;
            float h = Vector3.Dot(handPos - shoulders, up) + ChestBelowShoulders * scale;   // + above the chest line
            float halfShoulders = 0.5f * Vector3.Distance(leftUpperArm.position, rightUpperArm.position);
            float across = Vector3.Dot(handPos - shoulders, transform.right);
            bool wide = Mathf.Abs(across) > halfShoulders + WideOfShoulder * scale && h > -0.45f * scale && h < 0.1f * scale;
            float margin = gripChosen ? GripHysteresis * scale : 0f;
            Grip wanted = wide ? Grip.Out
                        : h > (grip == Grip.Up ? -margin : margin) ? Grip.Up : Grip.Down;
            if (!gripChosen || wanted != grip)
            {
                grip = wanted;
                gripChosen = true;
            }
            Vector3 fingerWay = grip == Grip.Up ? up
                              : grip == Grip.Down ? -up
                              : transform.right * Mathf.Sign(across);
            finger = Vector3.ProjectOnPlane(fingerWay, facing);
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
        CalibrateGuardBody();
        if (!ready || smoothedWeight < 0.01f)
        {
            leftWrist = rightWrist = Quaternion.identity;
            frameSet = false;   // start the next reach from its own frame, not a stale one
            gripChosen = false; // and choose its grip afresh
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

    /// Most a wrist turns from the animated hand, and how fast it gets there. A real wrist turns
    /// well under 90 degrees; at 100 (and 720 deg/s) the keeper's gloves visibly corkscrewed on
    /// chest-high and higher takes.
    private const float MaxWristTurn = 65f, WristSpeed = 400f;
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
        gripChosen = false;
    }
}
