using System;
using UnityEngine;

/// <summary>
/// One fielder. FieldingController decides where each fielder goes and when the ball is taken;
/// this moves the body there and holds the ball.
///
/// The old version waited on animation states before moving (Idle -> Start Running -> Run),
/// moved by root motion at ~4 m/s while planning with ~10-12 m/s, and only stopped the ball after
/// a stop-then-pick-up animation had played - so fielders arrived late and the ball rolled on
/// through them. Now the body is moved in code, straight away, at the speed the plan assumed, and
/// the ball is held the instant it is in reach. Animation is only for looks.
/// </summary>
public class AnimatedFielder : MonoBehaviour
{
    // Serialized names kept so the scene's nine fielders stay wired up.
    [SerializeField]
    AnimatedFielderManagement animatedFielderManagementScript;
    [SerializeField]
    protected GameObject theObject;
    [SerializeField]
    private GameObject fielderHand;
    [SerializeField]
    protected bool isKeeper;
    [SerializeField]
    private Animator myAnimator;
    [SerializeField]
    private float pickUpDistance;

    [HideInInspector]
    public Material JerseyMaterial;
    [HideInInspector]
    public Vector3 StartPosition;

    /// Top running speed, m/s, before the B-menu "Fielder Speed" slider (default 1.5 -> 7.5 m/s).
    public const float BaseRunSpeed = 5f;
    public const float Acceleration = 10f;
    public const float TurnRate = 720f;
    /// Forward speed baked into the run animation, used to keep the feet from sliding.
    private const float AnimatedRunSpeed = 4f;

    private Transform holdBallOffset;
    private bool hasTarget;
    private Vector3 target;
    private float speed;
    private bool holdingBall;

    public float RunSpeed => BaseRunSpeed * Mathf.Max(0.5f, Main.Instance != null ? Main.Instance.fielderSpeed : 1.5f);
    public bool HoldingBall => holdingBall;
    public float CurrentSpeed => speed;
    public Vector3 Target => target;
    public bool HasTarget => hasTarget;

    void Start()
    {
        if (theObject != null)
        {
            foreach (Material mat in theObject.GetComponent<Renderer>().materials)
            {
                if (mat.name.Contains("Material.001"))
                {
                    JerseyMaterial = mat;
                    break;
                }
            }
        }
        StartPosition = Constants.fieldingPositions[Convert.ToInt32(gameObject.name[gameObject.name.Length - 1].ToString()) - 1];
        ReturnToStart();
        if (myAnimator != null)
            myAnimator.applyRootMotion = false;
        if (fielderHand != null && fielderHand.transform.childCount > 5)
            holdBallOffset = fielderHand.transform.GetChild(5);
        Main.Instance.onGameStateChanged += HandleGameState;
    }

    public void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        eGameState state = Main.Instance.gameState;
        // A new delivery: drop the ball (Main parks it) and go back to the fielding position.
        if (state == eGameState.InGame_SelectDelivery || state == eGameState.InGame_ResetToReadyLoop)
        {
            if (state == eGameState.InGame_SelectDelivery)
                holdingBall = false;
            ReturnToStart();
        }
    }

    public void ReturnToStart()
    {
        hasTarget = false;
        speed = 0f;
        transform.position = StartPosition;
        transform.LookAt(new Vector3(0f, transform.position.y, 0f));
        SetAnimation(0);
    }

    /// Run to this point (on the ground).
    public void SetTarget(Vector3 point)
    {
        point.y = transform.position.y;
        target = point;
        hasTarget = true;
    }

    public void Stop()
    {
        hasTarget = false;
    }

    /// The ball is in reach: take it and hold it. Stop dead - the controller has no transition from
    /// the run to the pick-up, so asking for it left the fielder running on with the ball.
    public void TakeBall()
    {
        holdingBall = true;
        hasTarget = false;
        speed = 0f;
        SetAnimation(0);
        if (myAnimator != null)
            myAnimator.CrossFadeInFixedTime("0 Idle", 0.12f);
    }

    // ---- Hands to the ball (humanoid IK; the controller's base layer has IK Pass on) -----------
    /// The hands start reaching at this distance from the chest and are fully on the ball here.
    private const float ReachStart = 3f, ReachFull = 1.2f;
    /// Keep the hands this far in front of the chest, and let them cross the body's midline by at
    /// most this much, so the arms never pass through the torso.
    private const float MinInFront = 0.2f, MaxAcrossMidline = 0.12f;
    /// Fraction of the arm's length a hand may reach, so the elbow never locks straight.
    private const float MaxExtension = 0.95f;
    private float ikWeight;
    private Transform leftUpperArm, rightUpperArm, chest;
    private float leftArmLength, rightArmLength;

    private void CacheBones()
    {
        if (myAnimator == null || !myAnimator.isHuman || chest != null)
            return;
        leftUpperArm = myAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = myAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        chest = myAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? myAnimator.GetBoneTransform(HumanBodyBones.Spine);
        leftArmLength = ArmLength(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
        rightArmLength = ArmLength(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
    }

    private float ArmLength(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand)
    {
        Transform u = myAnimator.GetBoneTransform(upper), l = myAnimator.GetBoneTransform(lower), h = myAnimator.GetBoneTransform(hand);
        return u != null && l != null && h != null ? Vector3.Distance(u.position, l.position) + Vector3.Distance(l.position, h.position) : 0.6f;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        CacheBones();
        if (chest == null)
            return;
        Main inst = Main.Instance;
        Vector3 focus;
        float want;
        if (holdingBall)
        {
            // Both hands on the ball in front of the chest.
            focus = chest.position + transform.forward * 0.32f - transform.up * 0.1f;
            want = 1f;
        }
        else
        {
            focus = inst.theBall.transform.position;
            bool live = !inst.theBallRigidBody.isKinematic &&
                        (inst.gameState == eGameState.InGame_BallHit || inst.gameState == eGameState.InGame_BallHitLoop);
            want = live ? Mathf.InverseLerp(ReachStart, ReachFull, Vector3.Distance(focus, chest.position)) : 0f;
        }
        ikWeight = Mathf.MoveTowards(ikWeight, want, Time.deltaTime * 5f);

        myAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
        myAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
        myAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, ikWeight);
        myAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, ikWeight);
        myAnimator.SetLookAtWeight(ikWeight * 0.8f, 0.15f, 0.8f, 0.4f, 0.5f);
        if (ikWeight <= 0.001f)
            return;

        // Cupped hands either side of the ball.
        Vector3 apart = transform.right * 0.07f;
        myAnimator.SetIKPosition(AvatarIKGoal.LeftHand, Reachable(focus - apart, true));
        myAnimator.SetIKPosition(AvatarIKGoal.RightHand, Reachable(focus + apart, false));
        // Elbows out and down, never folded in against the ribs.
        myAnimator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftUpperArm.position + (-transform.right * 0.35f - transform.up * 0.35f - transform.forward * 0.05f));
        myAnimator.SetIKHintPosition(AvatarIKHint.RightElbow, rightUpperArm.position + (transform.right * 0.35f - transform.up * 0.35f - transform.forward * 0.05f));
        myAnimator.SetLookAtPosition(focus);
    }

    /// Clamp a hand target to where that arm can really go.
    private Vector3 Reachable(Vector3 target, bool left)
    {
        Vector3 local = transform.InverseTransformPoint(target);
        Vector3 chestLocal = transform.InverseTransformPoint(chest.position);
        local.z = Mathf.Max(local.z, chestLocal.z + MinInFront);
        local.x = left ? Mathf.Max(local.x, chestLocal.x - MaxAcrossMidline) : Mathf.Min(local.x, chestLocal.x + MaxAcrossMidline);
        Vector3 world = transform.TransformPoint(local);
        Transform shoulder = left ? leftUpperArm : rightUpperArm;
        float reach = (left ? leftArmLength : rightArmLength) * MaxExtension;
        Vector3 fromShoulder = world - shoulder.position;
        return fromShoulder.magnitude > reach ? shoulder.position + fromShoulder.normalized * reach : world;
    }

    private void Update()
    {
        if (!hasTarget)
        {
            speed = 0f;
            if (!holdingBall)
                SetAnimation(0);
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.05f)
        {
            speed = 0f;
            SetAnimation(0);
            FaceBall();
            return;
        }

        // Accelerate to full speed, and ease off just in time to stop on the spot.
        float stopping = Mathf.Sqrt(2f * Acceleration * distance);
        speed = Mathf.Min(RunSpeed, speed + Acceleration * Time.deltaTime, stopping);
        Vector3 step = to / distance * Mathf.Min(distance, speed * Time.deltaTime);
        transform.position += step;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnRate * Time.deltaTime);
        SetAnimation(1);
        if (myAnimator != null)
            myAnimator.speed = Mathf.Clamp(speed / AnimatedRunSpeed, 0.6f, 2f);
    }

    private void FaceBall()
    {
        Vector3 ball = Main.Instance.theBall.transform.position - transform.position;
        ball.y = 0f;
        if (ball.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(ball), TurnRate * Time.deltaTime);
    }

    private void SetAnimation(int action)
    {
        if (myAnimator == null)
            return;
        myAnimator.SetInteger("Action", action);
        if (action != 1)
            myAnimator.speed = 1f;
    }

    public void LateUpdate()
    {
        if (holdingBall)
        {
            Transform ball = Main.Instance.theBall.transform;
            ball.position = holdBallOffset != null ? holdBallOffset.position : transform.position + Vector3.up;
        }
    }
}
