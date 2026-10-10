using UnityEngine;

/// <summary>
/// A fielder's part in getting the ball back once it is dead (ThrowBack): he turns to the keeper
/// and throws it in with the fielders' throw animation, the ball leaving his hand as the arm
/// comes over (ReleaseTime into the clip); an inner fielder can be the relay the keeper throws
/// to. Having thrown, he jogs back to his fielding position.
/// </summary>
public partial class AnimatedFielder
{
    /// Into the Throw clip at which the arm comes over and the ball leaves the hand: the right
    /// hand's forward speed peaks there (7.3 m/s at 0.82 s, sampled from the clip).
    private const float ReleaseTime = 0.82f;
    /// After the release, the rest of the follow-through before he sets off back.
    private const float FollowThrough = 0.9f;
    /// Walking back to his position between balls, m/s.
    private const float JogSpeed = 3.2f;
    /// Further than this from his position when the next ball starts, he is put straight there.
    private const float FarFromStart = 12f;
    /// Chest height and how far in front the hands take a throw.
    private const float CatchHeight = 1.15f, CatchAhead = 0.35f;

    /// Every fielder in play (the bowler too, while he fields), for ThrowBack to look through each
    /// frame without searching the scene.
    public static readonly System.Collections.Generic.List<AnimatedFielder> All = new System.Collections.Generic.List<AnimatedFielder>();

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable() => All.Remove(this);

    private bool throwing, expecting, walkingBack;
    private float throwStart, walkBackAt = -1f, expectAt;
    private Vector3 throwAt, expectPoint, expectFrom;

    // ---- IThrowPlayer ----------------------------------------------------------------------------
    /// Not counting while he is down from a dive: he gets up before he throws.
    public float HeldFor => holdingBall && !diving ? Time.time - Mathf.Max(heldSince, diveUpAt) : 0f;
    public Vector3 ReleasePoint => ThrowHandPoint;
    public bool ReadyToRelease => throwing && Time.time - throwStart >= ReleaseTime;

    public Vector3 CatchPointFrom(Vector3 from)
    {
        Vector3 toThrower = Flat(from - transform.position);
        Vector3 facing = toThrower.sqrMagnitude > 1e-4f ? toThrower.normalized : transform.forward;
        return transform.position + Vector3.up * CatchHeight + facing * CatchAhead;
    }

    public void BeginThrow(Vector3 target)
    {
        throwing = true;
        throwStart = Time.time;
        throwAt = target;
        hasTarget = false;
        speed = 0f;
        if (reach != null)
            reach.LetSpineGo();   // the throw's own spine, not the one he held the ball with
        if (myAnimator != null)
        {
            myAnimator.speed = 1f;
            myAnimator.CrossFadeInFixedTime("5 Throw", 0.15f);
        }
    }

    public void Release()
    {
        holdingBall = false;
        throwing = false;
        walkBackAt = Time.time + FollowThrough;
    }

    public void ExpectBall(Vector3 point, Vector3 from, float arrivalTime)
    {
        expecting = true;
        expectPoint = point;
        expectFrom = from;
        expectAt = arrivalTime;
        hasTarget = false;
        walkingBack = false;
        speed = 0f;
    }

    public void TakeThrow(Vector3 incoming)
    {
        expecting = false;
        holdingBall = true;
        heldSince = Time.time;
        hasTarget = false;
        speed = 0f;
        Vector3 ball = Main.Instance.theBall.transform.position;
        bool ready = reach != null && reach.Ready;
        takenAt = ready ? reach.HoldPoint : ball;
        holdOffset = ready ? ball - reach.HoldPoint : Vector3.zero;
        takenAlong = incoming.sqrMagnitude > 1e-6f ? incoming.normalized : -transform.forward;
    }

    // ---- Hooks from Move / UpdateReach / LateUpdate / HandleGameState -----------------------------

    /// The right hand, where the ball sits through the wind-up and leaves from.
    private Vector3 ThrowHandPoint
    {
        get
        {
            Transform hand = myAnimator != null ? myAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null ? hand.position + hand.up * 0.03f : transform.position + Vector3.up * 1.5f;
        }
    }

    /// Standing to throw, to take a throw, or through the follow-through: face the right way and
    /// stay put. True while this owns his movement.
    private bool MoveForThrow()
    {
        if (throwing)
        {
            FaceTowards(throwAt, 1.5f);
            return true;
        }
        if (expecting)
        {
            SetAnimation(0);
            FaceTowards(expectFrom, 1.5f);
            return true;
        }
        if (walkBackAt >= 0f)
        {
            if (Time.time < walkBackAt)
                return true;
            walkBackAt = -1f;
            WalkBackToStart();
        }
        return false;
    }

    /// Arms free through the throw (the animation throws); hands up to where a throw will arrive.
    private bool ReachForThrow()
    {
        if (throwing)
        {
            reach.weight = 0f;
            reach.look = true;
            reach.lookTarget = throwAt;
            return true;
        }
        if (expecting)
        {
            reach.handTarget = expectPoint;
            reach.weight = Mathf.InverseLerp(0.9f, 0.3f, expectAt - Time.time);
            reach.handGap = 0.1f;
            reach.quickness = 1f;
            reach.look = true;
            reach.lookTarget = Main.Instance.theBall.transform.position;
            Vector3 toThrower = expectFrom - expectPoint;
            reach.palmFacing = toThrower.sqrMagnitude > 1e-4f ? toThrower.normalized : transform.forward;
            return true;
        }
        return false;
    }

    /// Back to his fielding position at a jog; the bowler back to his end, to take the ball there.
    public void WalkBackToStart()
    {
        if (holdingBall || throwing || expecting)
            return;
        if (FlatDistance(transform.position, StartPosition) < 0.3f)
            return;
        SetTarget(StartPosition);
        walkingBack = true;   // after SetTarget, which clears it
    }

    /// Someone else has it (or it is over the rope): stop chasing and stand, watching the ball.
    /// Not while he is diving - he finishes going down and gets up.
    public void CallOff()
    {
        if (walkingBack || throwing || expecting || backingUp)
            return;
        hasTarget = false;
        reachingLow = false;
        speed = 0f;
    }

    private void ClearThrow()
    {
        throwing = expecting = backingUp = false;
        walkBackAt = -1f;
    }

    // ---- Backing up a throw to the keeper ------------------------------------------------------

    private bool backingUp;

    /// Run to behind the keeper, in the line of a throw coming in to him (ThrowBack).
    public void BackUp(Vector3 spot)
    {
        SetTarget(spot);
        backingUp = true;
    }

    /// The keeper has it: back to his position.
    public void EndBackUp()
    {
        if (!backingUp)
            return;
        backingUp = false;
        WalkBackToStart();
    }
}
