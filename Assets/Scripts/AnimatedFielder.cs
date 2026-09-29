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
/// it comes; the ball is his only when it comes into the pocket between his palms after IK, tested
/// over the ball's whole path that frame so a fast one cannot slip between frames. Then it is held
/// between the palms, the last few centimetres faded out over HoldSettle, and he stands up with it
/// (the reach springs settle in ~0.4 s) and turns to the stumps.
/// </summary>
public class AnimatedFielder : MonoBehaviour, IFielder
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

    /// A moving ball is caught when it comes into the pocket between the palms - this close to the
    /// middle of the two hands - not merely near one of them.
    /// 0.13 caught a straight high catch only some runs: it passed the palms' middle at 0.13-0.16 m
    /// (filmed and traced), in the hands but read as a miss, and he fumbled it.
    public const float CatchPocket = 0.15f;
    /// A slow ball (a roller, one that has stopped) is scooped up by either hand from a little further.
    private const float SlowGatherDistance = 0.2f, SlowBall = 2f;
    /// Hands must be this far into the reach to take it: out and set, not still on the way.
    private const float TakeWeight = 0.6f;
    /// Time for the gap between ball and palms at the take to close.
    private const float HoldSettle = 0.1f;
    /// Hands work this far in front of the feet; the path is searched this far around that spot.
    private const float HandsAhead = 0.45f, HandsHeight = 1.1f, ReachRadius = 1.1f;
    /// The reach starts this long before the ball arrives and is full by the second value. For a
    /// catch the hands go up sooner and further, out toward the ball: a fielder sets his hands
    /// early and meets it with his arms extended rather than grabbing at it as it arrives.
    private const float ReachStartTime = 1.1f, ReachFullTime = 0.35f;
    /// Filmed from four sides, 1.6 s / 0.5 s / 0.25 m had him run in with both arms held straight
    /// out in front for over a second and take it at full stretch, arms locked - "zombie arms".
    /// A fielder runs with his arms free, brings the hands up late, and takes it in front of his
    /// chest, elbows bent.
    private const float CatchStartTime = 0.9f, CatchFullTime = 0.3f, CatchOut = 0.08f;
    /// A ground ball: down and set early, waiting for it. Starting at 1.1 s and full at 0.35 s, a
    /// 7-8 m/s roller arrived with the hands still ~0.5 m off the grass (filmed and traced) and
    /// went under them.
    /// Later now he snaps down to it (SnapDown): set from 1.8 s he stood folded over for 1.4 s
    /// before the ball came (filmed), which no fielder does.
    private const float GroundStartTime = 1.1f, GroundFullTime = 0.45f;
    /// Soft hands: on the take the hands ride back along the ball's line this far, this fast.
    private const float GiveDistance = 0.2f, GiveTime = 0.15f;
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
    private Vector3 takenAt, takenAlong;

    public float RunSpeed => BaseRunSpeed * Mathf.Max(0.5f, Main.Instance != null ? Main.Instance.fielderSpeed : 1.5f);
    public bool HoldingBall => holdingBall;
    public float CurrentSpeed => speed;
    public Vector3 Target => target;
    public bool HasTarget => hasTarget;

    // ---- IFielder ------------------------------------------------------------------------------
    public Vector3 Position => transform.position;
    public bool Available => isActiveAndEnabled && !holdingBall;
    public bool IsKeeper => false;
    public string Name => name;

    /// The bowler, fielding after his delivery (BowlerFielding): set up in code rather than placed
    /// in the scene, and with no fielding position of his own - he fields from where he finished.
    public bool IsBowler { get; private set; }

    /// Call straight after AddComponent, before Start runs.
    public void InitAsBowler(AnimatedFielderManagement management, Animator animator)
    {
        IsBowler = true;
        animatedFielderManagementScript = management;
        myAnimator = animator;
        StartPosition = transform.position;
    }

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
        if (!IsBowler)
            StartPosition = Constants.fieldingPositions[Convert.ToInt32(gameObject.name[gameObject.name.Length - 1].ToString()) - 1];
        // Stand on the grass, not at y 0: the outfield is at -0.047, so the feet hovered 5 cm up
        // and the foot IK pinned them there.
        StartPosition.y = BallFlight.GroundY(StartPosition);
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
        reachingLow = false;
        speed = 0f;
        // The bowler stays where he is; BowlerFielding hands him back to his bowling for the next ball.
        if (!IsBowler)
        {
            transform.position = StartPosition;
            transform.LookAt(new Vector3(0f, transform.position.y, 0f));
        }
        headingVelocity = 0f;
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
        takenAt = reach != null && reach.Ready ? reach.HoldPoint : ball;
        Vector3 v = ball - lastBall;
        takenAlong = v.sqrMagnitude > 1e-6f ? v.normalized : -transform.forward;
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
        // Down low for the ball, the feet stay set and he faces it. Running on with the hands down
        // at the grass, the legs still striding, was a crawl (filmed: on all fours after a roller).
        if (reachingLow)
        {
            speed = 0f;
            SetAnimation(0);
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
        // Run facing the way he runs; settling into his spot, turn to face the ball and side-step
        // in. He used to keep facing his run until he was on the spot, so a catch dropping in from
        // behind (measured: ball 3.9 m behind and 2.7 m up) had his hands reaching back past his head.
        Vector3 face = distance < SettleDistance ? Flat(Main.Instance.theBall.transform.position - transform.position) : to;
        TurnToward(face, 1f);
        // The run cycle only while he is actually running: settled under a catch he had a target a
        // few centimetres away and ran on the spot (measured: run clip playing at speed 0).
        bool running = speed > MinRunSpeed;
        SetAnimation(running ? 1 : 0);
        if (myAnimator != null && running)
            myAnimator.speed = Mathf.Clamp(speed / AnimatedRunSpeed, 0.6f, 2f);
    }

    private const float MinRunSpeed = 0.6f;
    /// Reaching this low (hands below the knee), he stops to take it rather than running on.
    private const float LowReach = 0.6f;
    /// m/s of the ball's speed toward him above which he sets his feet and lets it come.
    private const float ComingAtMe = 1f;
    /// Seconds of a slow ball's travel the hands lead it by.
    private const float SlowLead = 0.12f;
    private bool reachingLow;

    /// Tell the body where the hands should be this frame (HumanoidReach applies it in the IK pass).
    private void UpdateReach()
    {
        if (reach == null || !reach.Ready)
            return;
        Main inst = Main.Instance;
        Vector3 ball = inst.theBall.transform.position;
        if (holdingBall)
        {
            // Soft hands: ride back along the ball's line, then bring it to the chest - the reach
            // target comes up, so the springs stand him up (~0.4 s).
            float t = Time.time - heldSince;
            float give = Mathf.Clamp01(t / GiveTime);
            Vector3 back = takenAt + takenAlong * (GiveDistance * (1f - (1f - give) * (1f - give)));
            // Where his chest will be stood up: the reach solver unbends him toward it (a point in
            // front of the chest as it is, tried instead, left him bent over for good - the solver
            // only bends as far as it must, and that point never asks him to stand). While he is still
            // bent over it lies above his back; HumanoidReach's body guard (HumanoidReachBody) keeps
            // the hands out of the torso and on its front meanwhile, so they rise with him - without
            // it they swept up through his chest (measured 12 cm in).
            Vector3 chest = transform.position + transform.up * HoldHeight + transform.forward * HoldAhead;
            float up = Mathf.Clamp01((t - GiveTime) / 0.3f);
            reach.handTarget = Vector3.Lerp(back, chest, up * up * (3f - 2f * up));
            reach.weight = 1f;
            reach.handGap = 0.09f;
            reach.look = false;
            return;
        }
        bool live = !inst.theBallRigidBody.isKinematic &&
                    (inst.gameState == eGameState.InGame_BallHit || inst.gameState == eGameState.InGame_BallHitLoop);
        reach.quickness = 1f;
        reach.look = live && (ball - transform.position).sqrMagnitude < 60f * 60f;
        reach.lookTarget = ball;
        reach.handGap = 0.1f;
        // Where the hands naturally work: in front of the chest. Searching the path for the point
        // nearest a spot on the ground picked the lowest pass - for a catch, a point already
        // behind him once the ball had dropped past his hands.
        Vector3 spot = transform.position + transform.forward * HandsAhead + transform.up * HandsHeight;
        if (live && animatedFielderManagementScript != null &&
            animatedFielderManagementScript.PredictPass(spot, ReachRadius, out Vector3 point, out float eta))
        {
            // The hands go to where the ball will be and wait for it - never chase the live ball,
            // which at 30 m/s dragged them round in the last tenth of a second.
            bool catching = !inst.theBallScript.bounced && point.y > 0.5f;
            Vector3 incoming = inst.theBallRigidBody.linearVelocity;
            if (catching && incoming.sqrMagnitude > 0.01f)
                point -= incoming.normalized * CatchOut;   // arms out to meet it
            reach.handTarget = point;
            bool ground = !catching && point.y - transform.position.y < LowReach;
            reach.weight = catching ? Mathf.InverseLerp(CatchStartTime, CatchFullTime, eta)
                         : ground ? Mathf.InverseLerp(GroundStartTime, GroundFullTime, eta) * GetDownAllowance(eta)
                                  : Mathf.InverseLerp(ReachStartTime, ReachFullTime, eta);
            // Close to it on the ground, he snaps down rather than easing (HumanoidReach.quickness).
            reach.quickness = ground ? Mathf.Lerp(1f, SnapDown, Mathf.InverseLerp(0.9f, 0.4f, eta)) : 1f;
        }
        else if (live && inst.theBallRigidBody.linearVelocity.sqrMagnitude < SlowBall * SlowBall &&
                 FlatDistance(ball, transform.position) < ReachRadius + HandsAhead && InFrontOfFeet(ball))
        {
            // A ball that has stopped (or all but) beside him: the prediction for it is a single
            // point already in the past by the time it is read, so PredictPass finds nothing and
            // he stood over it with his hands at his sides. Go down to the ball itself - a slow
            // one cannot drag the hands round the way a fast one did.
            // A little ahead of a rolling ball: the hands' spring trails its target, and at 2 m/s it
            // left them ~0.17 m behind, just outside the scoop.
            reach.handTarget = ball + Vector3.ClampMagnitude(inst.theBallRigidBody.linearVelocity * SlowLead, 0.25f);
            // ...and only as he slows to it: run up upright, then bend - not down at the grass on the run.
            reach.weight = Mathf.InverseLerp(ReachRadius + HandsAhead, HandsAhead, FlatDistance(ball, transform.position))
                         * Mathf.InverseLerp(2.5f, 0.8f, speed);
        }
        else
        {
            reach.weight = 0f;
        }
        // Set and let it come only while it IS coming. A ball that had stopped 0.77 m away left him
        // frozen, reaching, 0.29 m short with his feet planted - the play never ended. A stopped,
        // slow or departing ball he walks up to.
        Vector3 toMe = transform.position - ball; toMe.y = 0f;
        bool coming = toMe.sqrMagnitude > 1e-4f && Vector3.Dot(inst.theBallRigidBody.linearVelocity, toMe.normalized) > ComingAtMe;
        // ...and only once he is on his mark in its line. Planting as soon as the hands started down
        // stopped him short of the line: filmed, a ground ball 2.5 m to his side went past his
        // outstretched hands and he chased it for 2 s.
        reachingLow = coming && OnHisMark && reach.SmoothedWeight > 0.35f && reach.handTarget.y - transform.position.y < LowReach;
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

    /// Within this of the ball's line he is in it: he plants and lets a ground ball come to him.
    private const float LineTolerance = 0.35f;
    /// In the ball's line, however far along it: a ball coming straight at him comes to him. His
    /// mark itself moves toward him as he attacks the ball (the plan re-picks the meeting point), so
    /// "on his mark" never held for a ball he ran in at - filmed, it went under his hands, half bent.
    private bool OnHisMark => !hasTarget || OffTheLine() < LineTolerance;

    /// Sideways distance from the ball's path along the ground.
    private float OffTheLine()
    {
        Rigidbody rb = Main.Instance.theBallRigidBody;
        Vector3 along = Flat(rb.linearVelocity);
        if (along.sqrMagnitude < 0.25f)
            return FlatDistance(transform.position, rb.position);
        return Mathf.Abs(Vector3.Cross(along.normalized, Flat(transform.position - rb.position)).y);
    }

    /// How far down he may get for a ground ball: fully once he is in its line, nearly on his mark,
    /// has slowed, or it is nearly on him; not sprinting across well short of it with time to
    /// spare - bent to the grass at full stride is a crawl. Without the last, a ball 2.5 m to his
    /// side held him upright until 0.37 s before it arrived (traced) and it went under his hands.
    private float GetDownAllowance(float eta)
    {
        if (!hasTarget)
            return 1f;
        float inLine = Mathf.InverseLerp(1.2f, LineTolerance, OffTheLine());
        float near = Mathf.InverseLerp(2f, 0.5f, FlatDistance(transform.position, target));
        float slow = Mathf.InverseLerp(4f, 1.5f, speed);
        float onHim = Mathf.InverseLerp(0.8f, 0.4f, eta);
        return Mathf.Max(Mathf.Max(inLine, near), Mathf.Max(slow, onHim));
    }

    /// How much faster than his normal ease he gets down to a ground ball that is nearly on him.
    private const float SnapDown = 1.8f;

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    /// A ball he can bend down for where he stands: in front of his feet, not under or beside
    /// them. Anywhere else the plan walks him round to it first (ShortOf), upright.
    private bool InFrontOfFeet(Vector3 ball)
    {
        Vector3 local = transform.InverseTransformPoint(ball);
        return local.z > 0.25f && local.z < ReachRadius + HandsAhead && Mathf.Abs(local.x) < 0.5f;
    }
    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    /// Within this of his spot he faces the ball rather than his run.
    private const float SettleDistance = 2f;

    private void FaceTowards(Vector3 point, float rateScale)
    {
        TurnToward(Flat(point - transform.position), rateScale);
    }

    /// Seconds for the body's heading to settle onto a new direction.
    private const float HeadingSmoothing = 0.15f;
    private float headingVelocity;

    /// Every frame the body turns toward one overall heading, eased rather than snapped. The plan
    /// re-aims the run several times a second and each re-aim used to swing him straight onto the
    /// new line at TurnRate, a visible jerk on top of the run cycle's own sway. The run clip's
    /// body yaw is centred on this heading by its orientation offset (Standard Run.anim, +4.9
    /// degrees, measured: the body ran 4.9 degrees right of the root before), so the animation
    /// no longer steers him.
    private void TurnToward(Vector3 direction, float rateScale)
    {
        if (direction.sqrMagnitude <= 0.01f)
            return;
        float current = transform.eulerAngles.y;
        float wanted = Quaternion.LookRotation(direction).eulerAngles.y;
        float yaw = Mathf.SmoothDampAngle(current, wanted, ref headingVelocity, HeadingSmoothing / Mathf.Max(0.1f, rateScale),
                                          TurnRate * rateScale, Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
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
        else if (reach != null && reach.Ready && reach.SmoothedWeight > TakeWeight && !inst.theBallRigidBody.isKinematic &&
                 inst.gameState == eGameState.InGame_BallHitLoop && animatedFielderManagementScript != null)
        {
            // Caught means in the hands: a moving ball has to come into the pocket between the
            // palms (tested over its whole path this frame). A slow roller can be scooped by either.
            bool slow = inst.theBallRigidBody.linearVelocity.sqrMagnitude < SlowBall * SlowBall;
            bool inHands = slow ? reach.ClosestPalm(lastBall, now) <= SlowGatherDistance
                                : ReachMath.SegmentDistance(lastBall, now, reach.HoldPoint) <= CatchPocket;
            if (inHands)
                animatedFielderManagementScript.Gather(this);   // -> Take -> TakeBall
        }
        lastBall = holdingBall ? ball.position : now;
    }
}
