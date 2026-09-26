using System;
using UnityEngine;

/// <summary>
/// One fielder. AnimatedFielderManagement decides where each fielder goes; this moves the body
/// there, reaches for the ball with the whole body and holds it once it is in the hands.
///
/// The body is moved in code, straight away, at the speed the plan assumed (animation is only for
/// looks - root motion ran at ~4 m/s against a ~10 m/s plan and fielders arrived late).
///
/// Taking the ball: there is no snap. HumanoidReach squats, hinges and reaches the hands to where
/// the ball's predicted path comes closest (AnimatedFielderManagement.PredictPass), tracking it as
/// it comes; the ball is his only when it passes within GatherDistance of a palm after IK, tested
/// over the ball's whole path that frame so a fast one cannot slip between frames. Then it is held
/// between the palms, the last few centimetres faded out over HoldSettle, and he stands up with it
/// (the reach springs settle in ~0.4 s) and turns to the stumps.
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

    /// The ball is in the hands when it passes this close to a palm centre.
    public const float GatherDistance = 0.15f;
    /// A slow ball (a roller, one that has stopped) is scooped up from a little further.
    private const float SlowGatherDistance = 0.2f, SlowBall = 2f;
    /// Time for the gap between ball and palms at the take to close.
    private const float HoldSettle = 0.1f;
    /// Hands work this far in front of the feet; the path is searched this far around that spot.
    private const float HandsAhead = 0.45f, ReachRadius = 1.1f;
    /// The reach starts this long before the ball arrives and is full by the second value.
    private const float ReachStartTime = 1.1f, ReachFullTime = 0.35f;
    /// Holding: ball in front of the chest (above the feet, in front of them), and the pause
    /// before turning to throw it in.
    private const float HoldHeight = 1.15f, HoldAhead = 0.32f, TurnAfter = 0.5f;

    private Transform holdBallOffset;
    private HumanoidReach reach;
    private bool hasTarget;
    private Vector3 target;
    private float speed;
    private bool holdingBall;
    private float heldSince;
    private Vector3 holdOffset;
    private Vector3 lastBall;

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
        if (animatedFielderManagementScript == null)
            animatedFielderManagementScript = GetComponentInParent<AnimatedFielderManagement>();
        StartPosition = Constants.fieldingPositions[Convert.ToInt32(gameObject.name[gameObject.name.Length - 1].ToString()) - 1];
        if (myAnimator != null)
        {
            myAnimator.applyRootMotion = false;
            reach = myAnimator.GetComponent<HumanoidReach>();
            if (reach == null)
                reach = myAnimator.gameObject.AddComponent<HumanoidReach>();
        }
        ReturnToStart();
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
        if (reach != null && !holdingBall)
        {
            reach.weight = 0f;
            reach.ResetPose();
        }
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

    /// The ball is in his hands (AnimatedFielderManagement.Take): hold it from where it was met.
    /// Stop dead - the controller has no transition from the run to the pick-up.
    public void TakeBall()
    {
        holdingBall = true;
        heldSince = Time.time;
        hasTarget = false;
        speed = 0f;
        Vector3 ball = Main.Instance.theBall.transform.position;
        holdOffset = reach != null && reach.Ready ? ball - reach.HoldPoint : Vector3.zero;
        SetAnimation(0);
        if (myAnimator != null)
            myAnimator.CrossFadeInFixedTime("0 Idle", 0.12f);
    }

    private void Update()
    {
        Move();
        UpdateReach();
    }

    private void Move()
    {
        if (!hasTarget)
        {
            speed = 0f;
            if (!holdingBall)
                SetAnimation(0);
            if (holdingBall && Time.time - heldSince > TurnAfter)
                FaceTowards(Main.Instance.theStumps != null ? Main.Instance.theStumps.transform.position : Vector3.zero, 0.35f);
            else
                FaceTowards(Main.Instance.theBall.transform.position, 1f);
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.05f)
        {
            speed = 0f;
            SetAnimation(0);
            FaceTowards(Main.Instance.theBall.transform.position, 1f);
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

    /// Tell the body where the hands should be this frame (HumanoidReach applies it in the IK pass).
    private void UpdateReach()
    {
        if (reach == null || !reach.Ready)
            return;
        Main inst = Main.Instance;
        Vector3 ball = inst.theBall.transform.position;
        if (holdingBall)
        {
            // Ball to the chest: the reach target comes up, so the springs stand him up (~0.4 s).
            reach.handTarget = transform.position + transform.up * HoldHeight + transform.forward * HoldAhead;
            reach.weight = 1f;
            reach.handGap = 0.09f;
            reach.look = false;
            return;
        }
        bool live = !inst.theBallRigidBody.isKinematic &&
                    (inst.gameState == eGameState.InGame_BallHit || inst.gameState == eGameState.InGame_BallHitLoop);
        reach.look = live && (ball - transform.position).sqrMagnitude < 60f * 60f;
        reach.lookTarget = ball;
        reach.handGap = 0.1f;
        Vector3 spot = transform.position + transform.forward * HandsAhead;
        if (live && animatedFielderManagementScript != null &&
            animatedFielderManagementScript.PredictPass(spot, ReachRadius, out Vector3 point, out float eta))
        {
            // Close in, the live ball is the better target than a prediction up to 0.1 s old.
            float close = Mathf.InverseLerp(1.5f, 0.4f, Vector3.Distance(ball, spot));
            reach.handTarget = Vector3.Lerp(point, ball, close * 0.5f);
            reach.weight = Mathf.InverseLerp(ReachStartTime, ReachFullTime, eta);
        }
        else
        {
            reach.weight = 0f;
        }
        KeepAnimating(live && (hasTarget || reach.weight > 0f));
    }

    /// A fielder behind the camera is culled, and a culled animator does not move the hands, so
    /// the ball could never reach them: keep him animating while he is in the play.
    private void KeepAnimating(bool inPlay)
    {
        AnimatorCullingMode mode = inPlay || holdingBall ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
        if (myAnimator.cullingMode != mode)
            myAnimator.cullingMode = mode;
    }

    private void FaceTowards(Vector3 point, float rateScale)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), TurnRate * rateScale * Time.deltaTime);
    }

    private void SetAnimation(int action)
    {
        if (myAnimator == null)
            return;
        myAnimator.SetInteger("Action", action);
        if (action != 1)
            myAnimator.speed = 1f;
    }

    /// After the animator and the IK: take the ball if it has reached the hands, carry it if held.
    public void LateUpdate()
    {
        Main inst = Main.Instance;
        Transform ball = inst.theBall.transform;
        Vector3 now = ball.position;
        if (holdingBall)
        {
            if (reach != null && reach.Ready)
                ball.position = reach.HoldPoint + ReachMath.Residual(holdOffset, Time.time - heldSince, HoldSettle);
            else
                ball.position = holdBallOffset != null ? holdBallOffset.position : transform.position + Vector3.up;
        }
        else if (reach != null && reach.Ready && reach.SmoothedWeight > 0.3f && !inst.theBallRigidBody.isKinematic &&
                 inst.gameState == eGameState.InGame_BallHitLoop && animatedFielderManagementScript != null)
        {
            bool slow = inst.theBallRigidBody.linearVelocity.sqrMagnitude < SlowBall * SlowBall;
            if (reach.ClosestPalm(lastBall, now) <= (slow ? SlowGatherDistance : GatherDistance))
                animatedFielderManagementScript.Gather(this);   // -> Take -> TakeBall
        }
        lastBall = holdingBall ? ball.position : now;
    }
}
