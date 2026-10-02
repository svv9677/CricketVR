using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The wicketkeeper: a humanoid in a crouch who reads the ball, shuffles across behind its line,
/// rises with the bounce, puts his gloves where it will arrive and gives with it on the take.
///
/// Lives on Main.theKeeper (built by Tools/CricketVR/Build Keeper). HUD sets that object's
/// position per bowler type (up to the stumps for spin) on every state change, so this root is
/// only the anchor: the body is the child that shuffles sideways from it.
///
///   * Reading it: from the ball's live state, BallFlight predicts where it crosses the glove
///     plane (HandsAhead in front of him), re-run every PredictInterval so swing and the bounce
///     are picked up as they happen.
///   * Feet: only as far as the gloves need (KeeperDive.FootworkGoal), at up to ShuffleSpeed with
///     KeeperFootwork stepping the feet (plant, lift, plant) so they never slide; a ball his feet
///     cannot get him to in time, he dives for (KeeperDive).
///   * Height: he sits in a deep crouch (Crouch of pelvis drop) until the delivery pitches, then
///     rises to the height the ball will arrive at.
///   * Gloves: to the predicted point from ReachLead before it arrives, tracking it; the ball is
///     his only when it reaches a glove (GatherDistance, after IK - no snap). On the take the
///     gloves give back toward the body over GiveTime (soft hands), then he stands up with it.
///   * An edge (the ball was hit): a catch if it has not bounced, otherwise fielded - HUD scores it
///     from Ball.bounced like any other fielder. A ball the batter missed and he takes ends the
///     delivery as missed. One that beats him goes on through (byes); KeeperCollider behind him
///     calls it missed. Wides are decided at the stumps (WideCollider) before he can take it.
/// </summary>
public partial class KeeperCatcher : MonoBehaviour, IFielder
{
    /// Kept for anything reading the old keeper's envelope: lateral reach and height, metres.
    public const float Reach = 1.2f;
    public const float ReachHeight = 2.0f;

    [SerializeField] private HumanoidReach body;
    [SerializeField] private Transform bodyRoot;

    /// A ball this close to a glove's palm is in it. Keeping gloves are big - webbed, well past
    /// the fingers - and at 0.17 head-high balls went through glove-high hands 0.19-0.28 m off
    /// (KeeperGrid).
    public const float GatherDistance = 0.2f;
    private const float WideBoxesEndX = 10.85f;
    private const float HandsAhead = 0.5f;
    private const float MaxShuffle = 1.6f, ShuffleSpeed = 3f, ShuffleAccel = 20f;
    private const float Crouch = 0.42f;
    private const float PredictInterval = 0.05f, PredictTime = 2.5f, PredictStep = 0.01f;
    /// Gloves go to the ball from this long before it arrives, fully there by ReachFull.
    private const float ReachLead = 0.8f, ReachFull = 0.3f;
    private const float GiveTime = 0.15f, GiveDistance = 0.22f, HoldSettle = 0.1f, StandUpAfter = 0.35f;
    private const float GloveExtra = 0.03f;

    private readonly List<BallFlight.Sample> path = new List<BallFlight.Sample>(256);
    private readonly KeeperFootwork feet = new KeeperFootwork();
    private float nextPredict;
    private bool hasIntercept;
    private Vector3 intercept;        // world
    private float interceptAt;        // Time.time it arrives
    private Vector3 incoming = Vector3.right;
    private float shuffleX, shuffleVelocity;
    private Vector3 lastRoot, lastBody, lastBall;
    private bool holding;
    private float heldSince;
    private Vector3 holdOffset, contact;

    private void Start()
    {
        Main.Instance.onGameStateChanged += HandleGameState;
        if (body == null)
            body = GetComponentInChildren<HumanoidReach>();
        if (bodyRoot == null && body != null)
            bodyRoot = body.transform;
        incoming = -transform.forward;   // a delivery comes straight at him
        // Nothing solid on the keeper: a missed ball must go through to KeeperCollider, not bounce
        // back up the pitch off a collider.
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (!c.isTrigger)
                c.enabled = false;
        }
        if (body != null)
        {
            Animator a = body.GetComponent<Animator>();
            Transform foot = a != null ? a.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            if (foot != null)
                feet.AnkleHeight = Mathf.Clamp(foot.position.y - body.transform.position.y, 0.04f, 0.15f);
        }
        ResetStance();
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        Main inst = Main.Instance;
        eGameState state = inst.gameState;
        if (state == eGameState.InGame_SelectDelivery)
        {
            holding = false;
            ResetStance();
        }
        // Someone else has it, or it has gone for four: stop where he is.
        if (state == eGameState.InGame_BallFielded || state == eGameState.InGame_BallPastBoundary)
            fetching = false;
        // Off the stumps it is not coming to his gloves any more, whatever he read before; nothing
        // re-reads it in this state (Live), so clear it or he would wait for it instead of fetching.
        if (state == eGameState.InGame_Bowled)
            hasIntercept = false;
        // Off the bat: a new line he has not seen yet - he reacts to it a reaction time later.
        if (state == eGameState.InGame_BallHit)
            deflectedAt = Time.time;
        else if (state == eGameState.InGame_SelectDelivery)
            deflectedAt = -1f;
        nextPredict = 0f;
        lastBall = inst.theBallRigidBody.position;
    }

    private void ResetStance()
    {
        hasIntercept = false;
        jumpY = 0f;
        shuffleX = shuffleVelocity = 0f;
        ClearFetch();
        ClearDive();
        ClearThrowRole();
        if (bodyRoot != null)
        {
            bodyRoot.localRotation = Quaternion.identity;
            bodyRoot.localPosition = new Vector3(0f, BodyDrop, 0f);
            feet.StanceHalfWidth = CrouchStance;
            feet.Settle = false;
            feet.Plant(Ground(bodyRoot.position), bodyRoot.right);
        }
        if (body != null)
            body.ResetPose();
        lastRoot = transform.position;
    }

    /// A point on the ground under p: the real surface, as the ball sees it. HUD stands him at
    /// y 0, but the outfield where he keeps is at -0.047 - measured in play, his toes hovered 5 cm
    /// above the grass.
    private static Vector3 Ground(Vector3 p) => new Vector3(p.x, BallFlight.GroundY(p), p.z);

    /// The body sits on that surface, whatever height HUD gave the anchor.
    private float BodyDrop => BallFlight.GroundY(transform.position) - transform.position.y;

    private bool Live(out eGameState state)
    {
        Main inst = Main.Instance;
        state = inst.gameState;
        return !inst.theBallRigidBody.isKinematic && !holding &&
               (state == eGameState.InGame_BallHitLoop || state == eGameState.InGame_DeliverBallLoop ||
                state == eGameState.InGame_BallMissed || state == eGameState.InGame_BallMissedLoop);
    }

    // ---- Reading the ball ------------------------------------------------------------------------

    /// How long after a deflection he can act on it: a human's reaction time. A delivery he reads
    /// from the moment it leaves the bowler's hand - its swing too - and keeps reading as it comes:
    /// nothing about it surprises him. Off the bat it changes line without warning, and for this
    /// long he keeps going for where it was going (that is what beats real keepers); then he reads
    /// the new line. Delaying every read, as it did, had him missing easy straight deliveries.
    private const float ReactionTime = 0.22f;
    private float deflectedAt = -1f;

    private void FixedUpdate()
    {
        if (!Live(out eGameState state) || Time.time < nextPredict)
            return;
        // Just off the bat: carry on with the read he had until he has reacted.
        if (deflectedAt >= 0f && Time.time - deflectedAt < ReactionTime)
            return;
        nextPredict = Time.time + PredictInterval;
        Rigidbody rb = Main.Instance.theBallRigidBody;
        // A delivery still in the air before it pitches swings, and will turn off the pitch: he
        // reads both, as a keeper reads the bowler's hand. (Without the turn a spinner's ball was
        // read up to half a metre off its line until it pitched.)
        Ball ball = Main.Instance.theBallScript;
        BallFlight.DeliveryEffects effects = state == eGameState.InGame_DeliverBallLoop && ball.fresh
            ? ball.deliveryEffects
            : BallFlight.DeliveryEffects.None;
        hasIntercept = Predict(rb.position, rb.linearVelocity, effects, out intercept, out float eta);
        if (hasIntercept)
        {
            interceptAt = Time.time + eta;
            if (rb.linearVelocity.sqrMagnitude > 0.01f)
                incoming = rb.linearVelocity.normalized;
        }
    }

    /// Where the ball crosses the glove plane, HandsAhead in front of him, if it comes to him.
    private bool Predict(Vector3 position, Vector3 velocity, BallFlight.DeliveryEffects effects, out Vector3 point, out float eta)
    {
        point = Vector3.zero;
        eta = 0f;
        Transform anchor = transform;
        BallFlight.Simulate(position, velocity, effects, PredictTime, path,
            s => anchor.InverseTransformPoint(s.position).z < HandsAhead - 0.5f || s.position.y < -1f, PredictStep);
        Vector3 prev = anchor.InverseTransformPoint(position);
        float prevTime = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 local = anchor.InverseTransformPoint(path[i].position);
            if (prev.z > HandsAhead && local.z <= HandsAhead)
            {
                float f = Mathf.InverseLerp(prev.z, local.z, HandsAhead);
                Vector3 hit = Vector3.Lerp(prev, local, f);
                if (Mathf.Abs(hit.x) > MaxShuffle + Reach || hit.y > ReachHeight + 0.3f)
                    return false;
                point = anchor.TransformPoint(hit);
                eta = Mathf.Lerp(prevTime, path[i].time, f);
                return true;
            }
            prev = local;
            prevTime = path[i].time;
        }
        return false;
    }

    // ---- Body: shuffle, crouch, gloves -------------------------------------------------------------

    private void Update()
    {
        if (body == null || bodyRoot == null || !body.Ready)
            return;
        float dt = Time.deltaTime;
        // HUD moved the keeper (new bowler type): he walks there between balls, so just re-plant.
        if ((transform.position - lastRoot).sqrMagnitude > 0.25f)
            ResetStance();
        lastRoot = transform.position;

        bool live = Live(out eGameState state);
        body.quickness = 1f;   // the dive speeds his arms up; everything else eases
        // The ball is dead and coming back to him, or he is throwing it on (KeeperThrow).
        if (UpdateThrowRole(dt))
            return;
        // Diving for a wide one (KeeperDive), down, or getting back up: nothing else moves him.
        if (UpdateDive(live, dt))
            return;
        // Gone after a loose ball (KeeperCatcher.Fetch): on his feet, running, not in his stance.
        if (UpdateFetch(state, dt))
            return;
        Vector3 localHit = hasIntercept ? transform.InverseTransformPoint(intercept) : Vector3.zero;
        // Only as far as the gloves need (KeeperDive.FootworkGoal), not behind every line.
        float goal = holding ? shuffleX : hasIntercept && live ? FootworkGoal(localHit.x) : shuffleX;
        shuffleX = ReachMath.Shuffle(shuffleX, ref shuffleVelocity, goal, ShuffleSpeed, ShuffleAccel, dt);
        // Off the ground for a high one (Keep's jump), coming back down once it is over.
        if (!jumping)
            jumpY = Mathf.MoveTowards(jumpY, 0f, JumpFall * dt);
        jumping = false;
        bodyRoot.localPosition = new Vector3(shuffleX, BodyDrop + jumpY, 0f);
        Vector3 velocity = dt > 0f ? (bodyRoot.position - lastBody) / dt : Vector3.zero;
        lastBody = bodyRoot.position;
        feet.Tick(Ground(bodyRoot.position), bodyRoot.right, velocity, dt);
        body.feetOverride = true;
        body.leftFootTarget = feet.Left;
        body.rightFootTarget = feet.Right;
        body.palmExtra = GloveExtra;
        body.handGap = 0.11f;
        body.look = true;
        body.lookTarget = Main.Instance.theBall.transform.position;

        if (holding)
            Hold();
        else
            Keep(live && hasIntercept, state);
    }

    /// Between balls: a half-crouch with the back up and the gloves resting just above the knees,
    /// head up the pitch. The full crouch with gloves at the shins, held all the time, made the
    /// reach fold him at the hips and spine to get the hands that low - hunched over.
    private const float RestCrouch = 0.26f, RestHandsAhead = 0.3f, RestHandsUp = 0.62f, RestWeight = 0.6f;

    /// Before the take: set in the rest stance, down into the crouch as the bowler runs in, gloves
    /// low in front until the ball is coming, then on it.
    private void Keep(bool coming, eGameState state)
    {
        Vector3 fwd = bodyRoot.forward, up = bodyRoot.up;
        bool between = !coming && state != eGameState.InGame_SelectDeliveryLoop &&
                       state != eGameState.InGame_DeliverBall && state != eGameState.InGame_DeliverBallLoop;
        // Nothing on: stand easy until the bowler sets off.
        if (between && !InPlay(state))
        {
            StandEasy();
            return;
        }
        Vector3 rest = between ? bodyRoot.position + fwd * RestHandsAhead + up * RestHandsUp
                               : bodyRoot.position + fwd * 0.45f + up * 0.42f;
        float eta = interceptAt - Time.time;
        // His gloves go to it from the moment he reads it - out of the bowler's hand - easing in
        // over ReachLead..ReachFull. He stays down in his crouch until the delivery pitches
        // (keepers rise with the bounce), or until it is nearly on him. This used to wait for
        // Ball.bounced, which is only ever set after a hit, so for every delivery the gloves and
        // the rise jumped in 0.25 s before it arrived and were still ~0.2 m short (review).
        bool readable = coming;
        bool pitched = state != eGameState.InGame_DeliverBallLoop || !Main.Instance.theBallScript.fresh || eta < 0.35f;
        float onIt = readable ? Mathf.InverseLerp(ReachLead, ReachFull, eta) : 0f;
        float arriveY = transform.InverseTransformPoint(intercept).y;
        float rise = readable && pitched ? Mathf.Clamp01((arriveY - 0.45f) / 0.9f) : 0f;
        body.minCrouch = between ? RestCrouch : Crouch * (1f - rise);
        // Above his standing reach: jump for it, at the top of the jump as it arrives.
        float high = readable ? Mathf.Clamp01((arriveY - JumpAbove) / JumpFullAt) : 0f;
        if (high > 0f && eta < JumpLead && eta > -0.1f)
        {
            float phase = Mathf.Clamp01((JumpLead - eta) / (2f * JumpLead));
            jumpY = MaxJump * high * Mathf.Sin(Mathf.PI * phase);
            jumping = true;
            body.feetOverride = false;   // the legs go up with him
        }
        body.handTarget = Vector3.Lerp(rest, intercept, onIt);
        body.weight = between ? RestWeight : Mathf.Max(0.85f, onIt);
        if (between && AnimatedBowler.Instance != null)
            body.lookTarget = AnimatedBowler.Instance.transform.position + Vector3.up * 1.6f;   // head up the pitch
        body.leftHandShare = 1f;   // both gloves to take it
        // Gloves face the ball coming in, or straight up the pitch while waiting.
        Vector3 face = Vector3.Slerp(fwd, -incoming, onIt);
        body.palmFacing = face.sqrMagnitude > 1e-4f ? face : fwd;
    }

    /// Jumping for a high one: balls arriving above JumpAbove (standing, his gloves reach about
    /// that), as high as MaxJump for one JumpFullAt above it; he leaves the ground JumpLead before
    /// it arrives and falls back at JumpFall m/s. Standing, every head-high ball missed his gloves
    /// by 0.2-0.3 m and every one above it by 0.45 m (KeeperGrid).
    private const float JumpAbove = 1.6f, JumpFullAt = 0.6f, MaxJump = 0.4f, JumpLead = 0.3f, JumpFall = 2.5f;
    private float jumpY;
    private bool jumping;

    /// A ball is being bowled or is in play - no standing easy.
    private static bool InPlay(eGameState s) =>
        s == eGameState.InGame_SelectDelivery || s == eGameState.InGame_SelectDeliveryLoop ||
        s == eGameState.InGame_DeliverBall || s == eGameState.InGame_DeliverBallLoop ||
        s == eGameState.InGame_BallHit || s == eGameState.InGame_BallHitLoop;

    /// How often his eyes move on, seconds (a random time in this range), and how fast they get there.
    private static readonly Vector2 GlanceEvery = new Vector2(1.8f, 4f);
    private const float GlanceSpeed = 2.5f;
    /// Standing easy: where the gloves rest (in front, at the waist) and how much of the reach.
    private const float EasyHandsAhead = 0.25f, EasyHandsUp = 1.0f, EasyWeight = 0.85f;
    private float nextGlance;
    private Vector3 glanceAt, lookNow;

    /// Between balls: stood up, feet in, arms down (the standing idle), looking about - at the
    /// bowler walking back, the batter, the field, the crowd - the way a keeper waits. The
    /// half-crouch with his gloves on his knees, held between every ball, looked set and awkward.
    private void StandEasy()
    {
        body.minCrouch = 0f;
        // Gloves loosely together in front of the waist, a light touch over the standing idle:
        // that clip on its own holds the keeper's arms stiffly out from his sides (filmed).
        body.handTarget = bodyRoot.position + bodyRoot.forward * EasyHandsAhead + bodyRoot.up * EasyHandsUp;
        body.weight = EasyWeight;
        body.leftHandShare = 1f;
        body.handGap = 0.12f;
        body.palmFacing = Vector3.zero;
        feet.StanceHalfWidth = StandStance;
        feet.Settle = true;
        if (Time.time >= nextGlance)
        {
            nextGlance = Time.time + Random.Range(GlanceEvery.x, GlanceEvery.y);
            glanceAt = Glance();
        }
        if (lookNow == Vector3.zero)
            lookNow = glanceAt;
        lookNow = Vector3.Lerp(lookNow, glanceAt, 1f - Mathf.Exp(-GlanceSpeed * Time.deltaTime));
        body.look = true;
        body.lookTarget = lookNow;
    }

    private Vector3 Glance()
    {
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float pick = Random.value;
        if (pick < 0.35f && AnimatedBowler.Instance != null)
            return AnimatedBowler.Instance.transform.position + Vector3.up * 1.6f;
        if (pick < 0.6f)
            return BatterHead();
        // Somewhere out in the field, or up at the stands, in front and to the sides.
        return transform.position + fwd * Random.Range(15f, 60f) + right * Random.Range(-40f, 40f) +
               Vector3.up * Random.Range(1f, 12f);
    }

    /// Standing up with it: how long it takes, how much of the reach stays on (the right glove
    /// holding the ball; the rest is the standing idle), and the feet's width stood up and crouched.
    private const float StandTime = 0.5f, HeldWeight = 0.6f;
    public const float CrouchStance = 0.24f, StandStance = 0.14f;
    private float crouchAtTake;

    /// After the take: give with both gloves, then stand up into a normal stance - feet in to
    /// standing width, the ball in the right glove in front of his chest, the left arm and the rest
    /// of him back to the standing idle - and look up at the batter.
    ///
    /// Filmed from four sides, the old hold was the "weird pose": the full keeping crouch put back
    /// on during the give (a high take dropped him into a squat with his arms over his head), then
    /// stood "up" with his feet still planted wide, knees bent and splayed, hunched over a glove
    /// hanging at his waist, his head ducked into his gloves.
    private void Hold()
    {
        float t = Time.time - heldSince;
        float give = Mathf.Clamp01(t / GiveTime);
        Vector3 back = contact + incoming * (GiveDistance * (1f - (1f - give) * (1f - give)));
        float stand = Mathf.Clamp01((t - StandUpAfter) / StandTime);
        float s = stand * stand * (3f - 2f * stand);
        // Ball in the right glove in front of his chest, measured from his torso as it is now (a
        // standing-waist point was inside his thigh while he was still crouched).
        Vector3 held = body.ChestPosition + body.TorsoFront * 0.28f - body.TorsoUp * 0.12f + bodyRoot.right * 0.12f;
        body.handTarget = Vector3.Lerp(back, held, s);
        body.weight = Mathf.Lerp(1f, HeldWeight, s);
        body.leftHandShare = 1f - s;
        // No deeper than he was when he took it, then up.
        body.minCrouch = crouchAtTake * (1f - s);
        feet.StanceHalfWidth = Mathf.Lerp(CrouchStance, StandStance, s);
        feet.Settle = stand > 0f;
        body.palmFacing = Vector3.Slerp(-incoming, bodyRoot.up, s);
        // Eyes off the gloves at once and up to the batter.
        float lookUp = Mathf.Clamp01(t / 0.3f);
        Vector3 ball = Main.Instance.theBall.transform.position;
        body.lookTarget = Vector3.Lerp(ball + bodyRoot.forward * 2f + Vector3.up * 0.5f, BatterHead(), lookUp * lookUp * (3f - 2f * lookUp));
    }

    /// Where the batter's eyes are: the player's head, or a standing batter at the crease.
    private static Vector3 BatterHead()
    {
        Camera eye = Camera.main;
        return eye != null ? eye.transform.position : new Vector3(BallDelivery.BatsmanStumpsX - 1f, 1.6f, 0f);
    }

    // ---- The take ------------------------------------------------------------------------------------

    private void LateUpdate()
    {
        Main inst = Main.Instance;
        Transform ball = inst.theBall.transform;
        Vector3 now = ball.position;
        if (holding)
        {
            if (throwingOut)
                ball.position = KeeperHandPoint;   // in the throwing hand through the wind-up
            else if (body != null && body.Ready)
                ball.position = body.HoldPoint + ReachMath.Residual(holdOffset, Time.time - heldSince, HoldSettle);
        }
        else if (body != null && body.Ready && (Live(out eGameState state) || CanPickUp(state)) && body.SmoothedWeight > 0.3f &&
                 // A delivery must clear the wide boxes at the stumps (x 9.8-10.8) first, or a keeper
                 // standing up to the spinners would take it inside them and no wide could be called.
                 !(state == eGameState.InGame_DeliverBallLoop && now.x < WideBoxesEndX) &&
                 Time.time >= spilledUntil && !ragdollDown &&
                 body.ClosestPalm(lastBall, now) <= (away ? PickUpDistance : GatherDistance))
        {
            // At full stretch in a dive, it only sometimes sticks (KeeperDive.DiveTakeSticks).
            if (diving && !DiveTakeSticks())
                Spill();
            else
                TakeIt(state, now);
        }
        lastBall = holding ? ball.position : now;
    }

    private void TakeIt(eGameState state, Vector3 ballNow)
    {
        Main inst = Main.Instance;
        Rigidbody rb = inst.theBallRigidBody;
        if (rb.linearVelocity.sqrMagnitude > 0.01f)
            incoming = rb.linearVelocity.normalized;
        holding = true;
        heldSince = Time.time;
        contact = body.HoldPoint;
        crouchAtTake = body.CurrentDrop;
        holdOffset = ballNow - contact;
        if (state == eGameState.InGame_BallHitLoop)
        {
            AnimatedFielderManagement.Take(null, name);
            return;
        }
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        // A kinematic body with interpolation overwrites the transform writes that carry the ball.
        rb.interpolation = RigidbodyInterpolation.None;
        if (state == eGameState.InGame_DeliverBallLoop)
            inst.gameState = eGameState.InGame_BallMissed;
    }
}
