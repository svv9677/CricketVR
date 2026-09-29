using UnityEngine;

/// <summary>
/// How far the keeper moves for a ball, and his dive when his feet cannot get him there.
///
/// Footwork: a keeper does not shuffle his whole body behind every ball. One arriving within
/// GloveReach of his middle he takes with the gloves where he is; further out he moves just enough
/// to bring it ComfortReach from his middle, not all the way behind it.
///
/// Diving, as a person can: a dive takes DiveTime from the push to landing on his side, however
/// soon the ball arrives - he is at full stretch only CatchPoint of the way through it, and then
/// carries on down and lands, ball or no ball. He goes when he sees (after his reaction time,
/// KeeperCatcher.ReactionTime) that his feet will not get him there. Seen late, the ball arrives
/// before he is stretched out and goes past the gloves: he still dives, and misses. The dive used
/// to take exactly as long as the ball did - a 2 m dive in 0.2 s when it had to - and he took
/// balls no one could. A glove on it in a dive holds most of the time, less at full stretch
/// (DiveTakeSticks);
/// otherwise it pops out (Spill).
/// </summary>
public partial class KeeperCatcher
{
    /// The ball is taken where he stands if it arrives within this of his middle, metres.
    private const float GloveReach = 0.5f;
    /// Moving for a wider one, he goes until it is this far from his middle.
    private const float ComfortReach = 0.35f;
    /// A new footwork goal replaces the old only if it differs by more than this: no dithering.
    private const float GoalHysteresis = 0.12f;
    /// Dives only for balls within this of his middle, and never for one within a lunge's reach.
    private const float MaxDive = 2.4f, LungeReach = 1.1f;
    /// Push-off to landing, and how far through it he is stretched out to take the ball.
    private const float DiveTime = 0.6f, CatchPoint = 0.7f;
    /// He launches this long before the ball arrives, to meet it at CatchPoint.
    private const float DiveLead = DiveTime * CatchPoint;
    /// Standing height of his shoulders above his feet, for choosing how far over he goes.
    private const float ShoulderHeight = 1.4f;
    /// How far the push-off carries his feet, at most, and how long he stays down and takes to rise.
    private const float MaxPush = 0.9f, LieTime = 0.45f, LieWithBall = 0.6f, RiseTime = 0.6f;
    /// Chance a dive take sticks: a short dive, and one at full stretch.
    private const float StickShort = 0.9f, StickFull = 0.55f;
    /// After a spill, not his again for this long (it is popping out of the gloves).
    private const float SpillBlock = 0.4f;

    private float footGoal;
    private bool diving, diveCommitted;
    private float diveStart, diveSide, diveFrom, diveTo, diveRoll, diveReach, landedAt = -1f;
    private float spilledUntil, diveUpAt = -1f;

    /// Where his feet go for a ball arriving `ballX` across from his mark (both anchor-local).
    private float FootworkGoal(float ballX)
    {
        // Going to dive for it: stay set and go from where he is.
        if (diveCommitted)
            return shuffleX;
        float offset = ballX - shuffleX;
        float wanted = Mathf.Abs(offset) <= GloveReach ? shuffleX : ballX - Mathf.Sign(offset) * ComfortReach;
        wanted = Mathf.Clamp(wanted, -MaxShuffle, MaxShuffle);
        if (Mathf.Abs(wanted - footGoal) > GoalHysteresis)
            footGoal = wanted;
        return footGoal;
    }

    private void ClearDive()
    {
        diving = false;
        diveCommitted = false;
        landedAt = -1f;
        footGoal = 0f;
        spilledUntil = 0f;
    }

    /// Before the stance code each frame. True while he is diving, down, or getting up.
    private bool UpdateDive(bool live, float dt)
    {
        if (!diving)
        {
            if (!live || holding || away || !hasIntercept)
                return false;
            // Decided once he has read it: a keeper sees he has to go, stays set, and goes.
            if (!diveCommitted && CannotGetThere())
                diveCommitted = true;
            if (!diveCommitted || interceptAt - Time.time > DiveLead)
                return false;
            StartDive();
        }
        float t = Time.time - diveStart;
        float s = Mathf.Clamp01(t / DiveTime);
        float out1 = 1f - (1f - s) * (1f - s);                     // quick off the mark, easing in
        float x = Mathf.Lerp(diveFrom, diveTo, out1);
        float roll = ReachMath.DiveRoll(s, CatchPoint, diveRoll);
        if (s >= 1f && landedAt < 0f)
            landedAt = Time.time;
        // Down on his side a moment, then up: roll back over the rise, the feet where he landed.
        float stayDown = holding ? LieWithBall : LieTime;
        float rising = -1f;
        if (landedAt >= 0f && Time.time - landedAt > stayDown)
        {
            float up = Mathf.Clamp01((Time.time - landedAt - stayDown) / RiseTime);
            rising = up;
            roll = ReachMath.DiveLanded * (1f - up * up * (3f - 2f * up));
            if (up >= 1f)
            {
                diving = false;
                diveCommitted = false;
                diveUpAt = Time.time;
                landedAt = -1f;
                shuffleX = footGoal = diveTo;
                shuffleVelocity = 0f;
                bodyRoot.localRotation = Quaternion.identity;
                bodyRoot.localPosition = new Vector3(diveTo, BodyDrop, 0f);
                feet.StanceHalfWidth = StandStance;
                feet.Plant(Ground(bodyRoot.position), bodyRoot.right);
                return false;
            }
        }
        bodyRoot.localPosition = new Vector3(x, BodyDrop, 0f);
        // Topple about his feet toward the ball: a roll about his forward axis.
        bodyRoot.localRotation = Quaternion.AngleAxis(-diveSide * roll, Vector3.forward);
        lastBody = bodyRoot.position;

        body.feetOverride = false;             // the legs go with the body
        body.minCrouch = 0f;
        body.palmExtra = GloveExtra;
        body.handGap = 0.11f;
        body.look = true;
        body.lookTarget = Main.Instance.theBall.transform.position;
        if (holding)
        {
            // Ball in the gloves, still going down: hug it in as he lands.
            Hold();
        }
        else if (landedAt < 0f && Time.time < interceptAt + 0.1f)
        {
            // Arms out along the dive to where the ball will be - flung out, not eased (at the
            // normal ease the arms were still 12% short of the target at the take: 0.3 m).
            body.handTarget = intercept;
            body.weight = 1f;
            body.quickness = 2.5f;
            body.leftHandShare = 1f;
            body.palmFacing = -incoming;
        }
        else
        {
            // Gone past him: the arms come down with the fall.
            body.weight = Mathf.MoveTowards(body.weight, 0.3f, dt * 3f);
        }
        if (rising >= 0f)
        {
            // Getting up: feet down on the grass where he landed and the hips tucked in, so he
            // comes up through a crouch - knees bending, pushing up - instead of swinging upright
            // stiff as a plank about his feet (filmed).
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 under = Ground(transform.TransformPoint(new Vector3(x, 0f, 0f)));
            body.feetOverride = true;
            body.leftFootTarget = under - flatRight * StandStance + Vector3.up * feet.AnkleHeight;
            body.rightFootTarget = under + flatRight * StandStance + Vector3.up * feet.AnkleHeight;
            body.minCrouch = Mathf.Max(body.minCrouch, 0.35f * Mathf.Sin(rising * Mathf.PI));
        }
        return true;
    }

    /// Too wide for his feet in the time there is (and within a dive): he will have to dive.
    private bool CannotGetThere()
    {
        float eta = interceptAt - Time.time;
        if (eta < 0.05f)
            return false;
        Vector3 local = transform.InverseTransformPoint(intercept);
        float offset = local.x - shuffleX;
        // Within a lunge he takes it on his feet, stretching (filmed: he dived full length for a
        // ball 1 m wide at the waist).
        if (Mathf.Abs(offset) <= LungeReach || Mathf.Abs(offset) > MaxDive)
            return false;
        // Shuffling from rest at ShuffleAccel up to ShuffleSpeed: how far he gets in eta, leaving
        // a moment to stretch for it at the end.
        float move = Mathf.Max(0f, eta - 0.1f);
        float accelTime = ShuffleSpeed / ShuffleAccel;
        float covered = move <= accelTime ? 0.5f * ShuffleAccel * move * move
                                          : 0.5f * ShuffleSpeed * accelTime + ShuffleSpeed * (move - accelTime);
        return Mathf.Abs(offset) - ComfortReach > covered;
    }

    private void StartDive()
    {
        diving = true;
        diveStart = Time.time;
        landedAt = -1f;
        Vector3 local = transform.InverseTransformPoint(intercept);
        float offset = local.x - shuffleX;
        diveSide = Mathf.Sign(offset);
        diveReach = Mathf.Abs(offset);
        diveFrom = shuffleX;
        // Stretched out at CatchPoint of the dive: toppled over, his gloves get out about 1.3 m from
        // his feet and the push off his near foot carries him the rest. The push goes on past
        // CatchPoint, so he lands beyond the ball; the roll goes on down to the grass
        // (ReachMath.DiveRoll).
        float atCatchPush = 1f - (1f - CatchPoint) * (1f - CatchPoint);
        float push = Mathf.Clamp(diveReach - (ShoulderHeight - 0.1f), 0.2f, MaxPush);
        diveTo = Mathf.Clamp(shuffleX + diveSide * push / atCatchPush, -MaxShuffle - MaxPush - 0.3f, MaxShuffle + MaxPush + 0.3f);
        // Over far enough at the take that his shoulders come down to a little above the ball's
        // height, the arms reaching the rest; then on down to the ground.
        float height = Mathf.Max(0.1f, local.y + 0.3f);
        float rollAtCatch = Mathf.Acos(Mathf.Clamp01(height / ShoulderHeight)) * Mathf.Rad2Deg;
        diveRoll = Mathf.Clamp(rollAtCatch, 30f, ReachMath.DiveLanded);
    }

    /// Whether a glove on the ball in a dive holds it: more often on a short dive than at full
    /// stretch, and less when he had to go early (seen late) - never certain.
    private bool DiveTakeSticks()
    {
        float stretch = Mathf.InverseLerp(GloveReach, MaxDive, diveReach);
        float chance = Mathf.Lerp(StickShort, StickFull, stretch);
        return Random.value < chance;
    }

    /// The ball hits the gloves and pops out: most of its pace gone, up and off to one side.
    private void Spill()
    {
        Rigidbody rb = Main.Instance.theBallRigidBody;
        Vector3 v = rb.linearVelocity;
        Vector3 across = Vector3.Cross(Vector3.up, v.sqrMagnitude > 1e-4f ? v.normalized : transform.forward);
        rb.linearVelocity = v * Random.Range(0.1f, 0.25f) + Vector3.up * Random.Range(1f, 2.5f) +
                            across * Random.Range(-1.5f, 1.5f);
        spilledUntil = Time.time + SpillBlock;
        hasIntercept = false;
        // Loose off the gloves: the batters can run byes (or wides) until someone gathers it.
        eGameState s = Main.Instance.gameState;
        if (s == eGameState.InGame_DeliverBallLoop)
            Extras.Begin(false);
        else if (s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop)
            Extras.Begin(Main.Instance.theBallScript.wide);
    }
}
