using UnityEngine;

/// <summary>
/// The keeper going after a ball that is not coming to his gloves: off the stumps after a bowled
/// ball, one that beat him, or a hit ball the fielding plan gives him (he calls it "Keeper's!").
/// He gets up out of his stance and runs to where he can meet it, like a fielder, then bends and
/// picks it up with both gloves (HumanoidReach) - and holds it in one, looking at the batter.
///
/// Away from his mark the stance code (shuffle, planted feet) is off: his feet are the run
/// animation's. He goes back to his mark for the next delivery (ResetStance).
/// </summary>
public partial class KeeperCatcher
{
    /// A slow or stopped ball is picked up by either glove from this close.
    private const float PickUpDistance = 0.22f;
    /// Keepers run a little slower than fielders, in pads.
    private const float FetchSpeedShare = 0.85f;
    private const float FetchAccel = 8f, FetchTurnRate = 540f, AnimatedRun = 4f;
    /// Gloves go down to the ball from this far and are on it by the second.
    private const float PickUpReachStart = 2.2f, PickUpReachFull = 0.7f;
    private const float RefetchInterval = 0.1f;

    private bool fetching;      // running to fetchTarget
    private bool away;          // off his mark this delivery (stance code off)
    private Vector3 fetchTarget;
    private float fetchSpeed, nextRefetch;
    private Animator anim;

    // ---- IFielder: the fielding plan can send him too ----------------------------------------------
    public Vector3 Position => bodyRoot != null ? bodyRoot.position : transform.position;
    public float RunSpeed => AnimatedFielder.BaseRunSpeed * FetchSpeedShare *
                             Mathf.Max(0.5f, Main.Instance != null ? Main.Instance.fielderSpeed : 1.5f);
    public bool HasTarget => fetching;
    public bool Available => body != null && body.Ready && !holding;
    public bool IsKeeper => true;
    public string Name => name;

    public void SetTarget(Vector3 point)
    {
        // Already reading it into the gloves: stay down and take it there.
        if (hasIntercept && !away)
            return;
        fetching = away = true;
        fetchTarget = point;
    }

    public void Stop() => fetching = false;

    private void ClearFetch()
    {
        fetching = away = false;
        fetchSpeed = 0f;
        if (anim != null)
            anim.SetInteger("Action", 0);
    }

    /// A ball he can go and pick up without a hit: loose after a miss or off the stumps.
    private static bool LooseState(eGameState s) =>
        s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop ||
        s == eGameState.InGame_Bowled || s == eGameState.InGame_BowledLoop;

    private bool CanPickUp(eGameState state) =>
        away && !holding && !Main.Instance.theBallRigidBody.isKinematic &&
        (LooseState(state) || state == eGameState.InGame_BallHitLoop);

    /// Once a frame, before the stance code. True when he is away and this has moved him.
    private bool UpdateFetch(eGameState state, float dt)
    {
        Rigidbody rb = Main.Instance.theBallRigidBody;
        // A loose ball not coming to his gloves: go and get it himself (no plan runs without a hit).
        if (!holding && !rb.isKinematic && LooseState(state) && !(hasIntercept && !away) && Time.time >= nextRefetch)
        {
            nextRefetch = Time.time + RefetchInterval;
            fetching = away = true;
            fetchTarget = LooseBallMeet(rb.position, rb.linearVelocity);
        }
        if (!away)
            return false;
        if (anim == null)
            anim = body.GetComponent<Animator>();
        body.feetOverride = false;   // the run animation's feet
        body.palmExtra = GloveExtra;
        body.handGap = 0.11f;
        body.look = true;
        body.lookTarget = Main.Instance.theBall.transform.position;
        RunTo(dt);
        if (holding)
            Hold();
        else
            ReachForLooseBall();
        return true;
    }

    /// Where he can meet a loose ball: the first point of its predicted roll he can reach in time,
    /// or where it stops.
    private Vector3 LooseBallMeet(Vector3 position, Vector3 velocity)
    {
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, 6f, path, null, 0.02f);
        Vector3 from = Position;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i].position;
            if (p.y > 1.2f)
                continue;
            float run = Mathf.Max(0f, new Vector2(p.x - from.x, p.z - from.z).magnitude - 0.5f);
            if (run / RunSpeed <= path[i].time)
                return p;
        }
        return path.Count > 0 ? path[path.Count - 1].position : position;
    }

    /// Run to the target, stopping just short so the ball ends up in front of him.
    private void RunTo(float dt)
    {
        Vector3 at = bodyRoot.position;
        Vector3 to = fetchTarget - at;
        to.y = 0f;
        float distance = to.magnitude - 0.45f;
        if (fetching && !holding && distance > 0.05f)
        {
            float stopping = Mathf.Sqrt(2f * FetchAccel * distance);
            fetchSpeed = Mathf.Min(RunSpeed, fetchSpeed + FetchAccel * dt, stopping);
            at += to.normalized * Mathf.Min(distance, fetchSpeed * dt);
            bodyRoot.position = new Vector3(at.x, BallFlight.GroundY(at), at.z);
            bodyRoot.rotation = Quaternion.RotateTowards(bodyRoot.rotation, Quaternion.LookRotation(to), FetchTurnRate * dt);
            anim.SetInteger("Action", 1);
            anim.speed = Mathf.Clamp(fetchSpeed / AnimatedRun, 0.6f, 2f);
            return;
        }
        fetchSpeed = 0f;
        anim.SetInteger("Action", 0);
        anim.speed = 1f;
        Vector3 look = Main.Instance.theBall.transform.position - at;
        look.y = 0f;
        if (!holding && look.sqrMagnitude > 0.01f)
            bodyRoot.rotation = Quaternion.RotateTowards(bodyRoot.rotation, Quaternion.LookRotation(look), FetchTurnRate * dt);
    }

    /// Both gloves down to the ball as he gets to it: palms toward each other to scoop it.
    private void ReachForLooseBall()
    {
        Vector3 ball = Main.Instance.theBall.transform.position;
        Vector3 hands = bodyRoot.position + bodyRoot.forward * 0.45f;
        float d = new Vector2(ball.x - hands.x, ball.z - hands.z).magnitude;
        body.handTarget = ball;
        body.weight = Mathf.InverseLerp(PickUpReachStart, PickUpReachFull, d);
        body.minCrouch = 0f;
        body.leftHandShare = 1f;
        body.palmFacing = Vector3.zero;
    }
}
