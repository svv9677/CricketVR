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
///   * Feet: the body moves across at up to ShuffleSpeed with KeeperFootwork stepping the feet
///     (plant, lift, plant) so they never slide.
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
public class KeeperCatcher : MonoBehaviour
{
    /// Kept for anything reading the old keeper's envelope: lateral reach and height, metres.
    public const float Reach = 1.2f;
    public const float ReachHeight = 2.0f;

    [SerializeField] private HumanoidReach body;
    [SerializeField] private Transform bodyRoot;

    /// A ball this close to a glove's palm is in it (gloves are big: a little over a bare hand).
    public const float GatherDistance = 0.17f;
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
        // A hit changes everything about where the ball is going: read it again now.
        nextPredict = 0f;
        lastBall = inst.theBallRigidBody.position;
    }

    private void ResetStance()
    {
        hasIntercept = false;
        shuffleX = shuffleVelocity = 0f;
        if (bodyRoot != null)
        {
            bodyRoot.localPosition = new Vector3(0f, BodyDrop, 0f);
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

    private void FixedUpdate()
    {
        if (!Live(out _) || Time.time < nextPredict)
            return;
        nextPredict = Time.time + PredictInterval;
        Rigidbody rb = Main.Instance.theBallRigidBody;
        hasIntercept = Predict(rb.position, rb.linearVelocity, out intercept, out float eta);
        if (hasIntercept)
        {
            interceptAt = Time.time + eta;
            if (rb.linearVelocity.sqrMagnitude > 0.01f)
                incoming = rb.linearVelocity.normalized;
        }
    }

    /// Where the ball crosses the glove plane, HandsAhead in front of him, if it comes to him.
    private bool Predict(Vector3 position, Vector3 velocity, out Vector3 point, out float eta)
    {
        point = Vector3.zero;
        eta = 0f;
        Transform anchor = transform;
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, PredictTime, path,
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
        Vector3 localHit = hasIntercept ? transform.InverseTransformPoint(intercept) : Vector3.zero;
        float goal = holding ? shuffleX : hasIntercept && live ? Mathf.Clamp(localHit.x, -MaxShuffle, MaxShuffle) : shuffleX;
        shuffleX = ReachMath.Shuffle(shuffleX, ref shuffleVelocity, goal, ShuffleSpeed, ShuffleAccel, dt);
        bodyRoot.localPosition = new Vector3(shuffleX, BodyDrop, 0f);
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

    /// Before the take: crouch, and gloves low in front until the ball is coming, then on it.
    private void Keep(bool coming, eGameState state)
    {
        Vector3 fwd = bodyRoot.forward, up = bodyRoot.up;
        Vector3 rest = bodyRoot.position + fwd * 0.45f + up * 0.42f;
        float eta = interceptAt - Time.time;
        // Down in the crouch until the delivery pitches (keepers rise with the bounce); an edge
        // or a ball already bounced is read straight away.
        bool readable = coming && (state != eGameState.InGame_DeliverBallLoop || Main.Instance.theBallScript.bounced || eta < 0.25f);
        float onIt = readable ? Mathf.InverseLerp(ReachLead, ReachFull, eta) : 0f;
        float arriveY = transform.InverseTransformPoint(intercept).y;
        float rise = readable ? Mathf.Clamp01((arriveY - 0.45f) / 0.9f) : 0f;
        body.minCrouch = Crouch * (1f - rise);
        body.handTarget = Vector3.Lerp(rest, intercept, onIt);
        body.weight = Mathf.Max(0.85f, onIt);
        body.leftHandShare = 1f;   // both gloves to take it
        // Gloves face the ball coming in, or straight up the pitch while waiting.
        Vector3 face = Vector3.Slerp(fwd, -incoming, onIt);
        body.palmFacing = face.sqrMagnitude > 1e-4f ? face : fwd;
    }

    /// After the take: give with both gloves, then stand up holding it in the right glove, the left
    /// arm dropping away, and look up the pitch at the batter - not down at his hands.
    private void Hold()
    {
        float t = Time.time - heldSince;
        float give = Mathf.Clamp01(t / GiveTime);
        Vector3 back = contact + incoming * (GiveDistance * (1f - (1f - give) * (1f - give)));
        float stand = Mathf.Clamp01((t - StandUpAfter) / 0.4f);
        float s = stand * stand * (3f - 2f * stand);
        // Ball in the right glove, a little out to the side at the waist.
        Vector3 held = bodyRoot.position + bodyRoot.up * 1.0f + bodyRoot.right * 0.28f + bodyRoot.forward * 0.3f;
        body.handTarget = Vector3.Lerp(back, held, s);
        body.weight = 1f;
        body.leftHandShare = 1f - s;
        body.minCrouch = Crouch * (1f - stand);
        body.palmFacing = Vector3.Slerp(-incoming, bodyRoot.up, s);
        body.lookTarget = Vector3.Lerp(Main.Instance.theBall.transform.position, BatterHead(), s);
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
            if (body != null && body.Ready)
                ball.position = body.HoldPoint + ReachMath.Residual(holdOffset, Time.time - heldSince, HoldSettle);
        }
        else if (body != null && body.Ready && Live(out eGameState state) && body.SmoothedWeight > 0.3f &&
                 // A delivery must clear the wide boxes at the stumps (x 9.8-10.8) first, or a keeper
                 // standing up to the spinners would take it inside them and no wide could be called.
                 !(state == eGameState.InGame_DeliverBallLoop && now.x < WideBoxesEndX) &&
                 body.ClosestPalm(lastBall, now) <= GatherDistance)
        {
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
