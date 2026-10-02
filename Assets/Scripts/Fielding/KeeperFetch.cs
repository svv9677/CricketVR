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
    /// The run clip's speed (3.41 m/s at human scale 1, measured) times his scale: matched to how
    /// fast he really moves, so his feet do not slide. It was taken as 4.
    private const float FetchAccel = 8f, FetchTurnRate = 540f, RunClipSpeed = 3.41f, MinRunMove = 1f;
    /// Times his avatar's human scale and his model's own scale (the keeper is scaled up 11%):
    /// the human scale alone left his strides 11% short of the ground covered.
    private float AnimatedRun => RunClipSpeed * (anim != null ? anim.humanScale * anim.transform.lossyScale.y : 1f);
    /// Gloves go down to the ball from this far and are on it by the second.
    private const float PickUpReachStart = 2.2f, PickUpReachFull = 0.7f;
    private const float RefetchInterval = 0.1f;
    /// Closer than RunFrom he walks it on planted steps (KeeperFootwork), at up to StepSpeed;
    /// further, he runs. Every fetch used to run: for a ball 2 m away he glided off on the idle,
    /// slid his planted foot at 3 m/s through the Start Running blend, and ran on the spot once
    /// he had stopped (measured) - nobody sprints two metres.
    private const float RunFrom = 4f, StepSpeed = 1.4f;
    /// Into and out of the run, seconds; the run's legs slowed while it blends out.
    private const float RunBlend = 0.2f, StopBlend = 0.25f, StoppingAnimSpeed = 0.4f;

    private bool fetching;      // running to fetchTarget
    private bool running;       // on the run animation (else stepping, or stood)
    private bool away;          // off his mark this delivery (stance code off)
    private Vector3 fetchTarget;
    private float fetchSpeed, nextRefetch;
    private Animator anim;

    // ---- IFielder: the fielding plan can send him too ----------------------------------------------
    public Vector3 Position => bodyRoot != null ? bodyRoot.position : transform.position;
    public float RunSpeed => AnimatedFielder.BaseRunSpeed * FetchSpeedShare *
                             Mathf.Max(0.5f, Main.Instance != null ? Main.Instance.fielderSpeed : 1.5f);
    public bool HasTarget => fetching;
    public bool Available => body != null && body.Ready && !holding && !diving;
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
        fetching = away = running = false;
        fetchSpeed = 0f;
        if (anim != null)
            anim.SetInteger("Action", 0);
        if (body != null)
            body.RestoreBend();   // back to his keeping crouch
    }

    /// He stops this far short of a loose ball, so it lies in front of his knees when he bends.
    /// At 0.5 it ended up 0.13-0.3 m ahead of his knees in the squat and his arms went down
    /// through his thighs to it, and came back up through them holding it (measured, filmed).
    private const float stopShort = 0.7f;
    /// Closer than stopShort by this much (rolled in under him), he steps back from it, slowly.
    private const float BackOffBy = 0.15f, StepBackSpeed = 0.8f;
    /// A ball slower than this (m/s) is one to walk up to and pick up, not to cut off.
    private const float StillBall = 0.5f;
    /// Crouched less than this (metres of pelvis drop), he is up: feet in to standing width.
    private const float StoodDrop = 0.08f;

    /// A ball he can go and pick up without a hit: loose after a miss or off the stumps.
    private static bool LooseState(eGameState s) =>
        s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop ||
        s == eGameState.InGame_Bowled || s == eGameState.InGame_BowledLoop;

    /// Also once the delivery is over and the game is getting ready for the next: the ball is dead
    /// and he is only tidying it up, but at 1.75 s from dead ball to ready he is often still on
    /// his way. Refused then, he stood bent over it with his gloves 0.10 m from it, never taking it.
    private bool CanPickUp(eGameState state) =>
        away && !holding && !Main.Instance.theBallRigidBody.isKinematic &&
        (LooseState(state) || state == eGameState.InGame_BallHitLoop ||
         state == eGameState.InGame_ResetToReady || state == eGameState.InGame_ResetToReadyLoop ||
         state == eGameState.InGame_Ready);

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
        body.UsePickUpBend();   // a fielder's bend for a ball on the ground, not his crouch
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
            float run = Mathf.Max(0f, new Vector2(p.x - from.x, p.z - from.z).magnitude - stopShort);
            if (run / RunSpeed <= path[i].time)
                return p;
        }
        return path.Count > 0 ? path[path.Count - 1].position : position;
    }

    /// Go to the target - running, or stepping when it is close - stopping just short so the ball
    /// ends up in front of him.
    private void RunTo(float dt)
    {
        Vector3 at = bodyRoot.position;
        // Close to a ball that has all but stopped, he goes to the ball itself. The fielding plan's
        // meeting point (for cutting off a moving ball) is 0.4 m on his side of it: re-set every
        // replan against the step up to it, he went back and forth a centimetre, never settled to
        // bend, and the ball was never finished (GameLoopTest, stuck).
        Rigidbody rb = Main.Instance.theBallRigidBody;
        Vector3 ball = Main.Instance.theBall.transform.position;
        if (fetching && !holding && !rb.isKinematic && rb.linearVelocity.sqrMagnitude < StillBall * StillBall &&
            new Vector2(ball.x - at.x, ball.z - at.z).magnitude < PickUpReachStart + stopShort)
            fetchTarget = ball;
        Vector3 to = fetchTarget - at;
        to.y = 0f;
        float distance = to.magnitude - stopShort;
        bool moving = fetching && !holding && distance > 0.05f;
        if (moving && !running && distance > RunFrom)
            StartRun();
        if (moving)
        {
            float stopping = Mathf.Sqrt(2f * FetchAccel * distance);
            fetchSpeed = Mathf.Min(running ? RunSpeed : StepSpeed, fetchSpeed + FetchAccel * dt, stopping);
            at += to.normalized * Mathf.Min(distance, fetchSpeed * dt);
            bodyRoot.position = new Vector3(at.x, BallFlight.GroundY(at), at.z);
            bodyRoot.rotation = Quaternion.RotateTowards(bodyRoot.rotation, Quaternion.LookRotation(to), FetchTurnRate * dt);
            // Off his stance: the reach he had there lets go quickly, not over his first strides
            // (filmed: he set off with his arms held out in front of him).
            body.quickness = 2.5f;
        }
        else
        {
            fetchSpeed = 0f;
            // Too close to bend for it in front of him: a step back. He only ever stepped toward
            // it, and a ball under his feet left him stood over it.
            if (fetching && !holding && distance < -BackOffBy && to.sqrMagnitude > 1e-4f)
            {
                at -= to.normalized * Mathf.Min(-distance, StepBackSpeed * dt);
                bodyRoot.position = new Vector3(at.x, BallFlight.GroundY(at), at.z);
            }
            Vector3 look = Main.Instance.theBall.transform.position - at;
            look.y = 0f;
            if (!holding && look.sqrMagnitude > 0.01f)
                bodyRoot.rotation = Quaternion.RotateTowards(bodyRoot.rotation, Quaternion.LookRotation(look), FetchTurnRate * dt);
        }
        // Slowed below a run: out of it, the last of the way on his feet.
        if (running && fetchSpeed < MinRunMove)
            StopRun();
        if (running)
        {
            // The run's cycle matched to his speed: its feet carry him.
            anim.speed = Mathf.Clamp(fetchSpeed / AnimatedRun, 0.3f, 2.4f);
            body.feetOverride = false;
        }
        else
        {
            anim.speed = anim.IsInTransition(0) ? StoppingAnimSpeed : 1f;
            StepFeet(dt);
        }
    }

    private void StartRun()
    {
        running = true;
        anim.SetInteger("Action", 1);
        // Straight into the run: the controller's Idle > Start Running > Run chain is fixed
        // seconds, stretched by the slow animator speed at a standing start to 0.7 s of blending
        // while he was already moving at 3 m/s.
        anim.CrossFadeInFixedTime("1 Run", RunBlend);
    }

    private void StopRun()
    {
        running = false;
        anim.SetInteger("Action", 0);
        anim.CrossFadeInFixedTime("0 Idle", StopBlend);
    }

    /// Walking it, or stood: feet planted and stepping with him (KeeperFootwork).
    private void StepFeet(float dt)
    {
        Vector3 velocity = dt > 0f ? (bodyRoot.position - lastBody) / dt : Vector3.zero;
        lastBody = bodyRoot.position;
        // Bending for the ball, feet apart to his keeping width first: squatting on his standing
        // width his knees closed together and both forearms went down through his thighs, gloves
        // between the pads (filmed from in front, 5-6 cm in). And they stay apart until he is up
        // again: stepped in at the take, still crouched, his left arm let go of the ball and hung
        // down through his closed-up thigh (7 cm, measured).
        bool low = !holding ? body.weight > 0.5f : body.CurrentDrop > StoodDrop;
        feet.StanceHalfWidth = low ? CrouchStance : StandStance;
        feet.Tick(Ground(bodyRoot.position), bodyRoot.right, velocity, dt);
        FeetPlanted();
        body.leftFootTarget = feet.Left;
        body.rightFootTarget = feet.Right;
    }

    /// Both gloves down to the ball as he gets to it: palms toward each other to scoop it.
    private void ReachForLooseBall()
    {
        Vector3 ball = Main.Instance.theBall.transform.position;
        Vector3 hands = bodyRoot.position + bodyRoot.forward * 0.45f;
        float d = new Vector2(ball.x - hands.x, ball.z - hands.z).magnitude;
        body.minCrouch = 0f;
        body.leftHandShare = 1f;
        body.palmFacing = Vector3.zero;
        // As a person picks a ball up: walk up to it upright, stop with it in front of him, then
        // bend for it. Only then - never on the move, never for a ball under or beside his feet.
        // Reaching for it wherever it was had the ball end up under him and him folded and twisted
        // over it, head at the grass, gloves never on it (filmed).
        Vector3 local = bodyRoot.InverseTransformPoint(ball);
        bool inFront = local.z > PickUpNear && local.z < PickUpFar && Mathf.Abs(local.x) < PickUpSide;
        bool settled = fetchSpeed < 0.3f;
        if (!inFront && d < PickUpReachStart)
        {
            // Not where his hands can take it: step to it (upright), to have it in front again.
            fetching = true;
            fetchTarget = ball;
        }
        body.handTarget = ball;
        body.weight = inFront && settled ? 1f : 0f;
    }

    /// Where a ball on the ground can be picked up from: in front of his feet, metres.
    /// Near enough that it is ahead of his knees once he is down (they come 0.2 m forward), and no
    /// further than his gloves reach bending from the hips: at 1.0 he stood bent over a ball 0.99 m
    /// out, gloves 0.3 m short of it, never stepping in - the game never finished the ball
    /// (GameLoopTest, stuck). Further than this he steps up to it (to stopShort).
    private const float PickUpNear = 0.45f, PickUpFar = 0.8f, PickUpSide = 0.45f;
}
