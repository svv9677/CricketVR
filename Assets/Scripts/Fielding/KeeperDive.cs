using UnityEngine;

/// <summary>
/// How far the keeper moves for a ball, and his dive when his feet cannot get him there.
///
/// Footwork: a keeper does not shuffle his whole body behind every ball. One arriving within
/// GloveReach of his middle he takes with the gloves where he is; further out he moves just enough
/// to bring it ComfortReach from his middle, not all the way behind it. He used to shuffle to
/// directly behind every predicted line (up to 1.6 m), re-aiming every 0.05 s - always on his toes.
///
/// Diving: when a ball is further out than the shuffle can cover before it arrives (and within
/// MaxDive), he launches DiveLead before it gets there: a push off the near foot sideways, then he
/// topples toward it about his feet, lower for a lower ball, arms out to it (HumanoidReach does the
/// arms). He lands on his side, stays down a moment (longer if he has it), and gets back up.
/// </summary>
public partial class KeeperCatcher
{
    /// The ball is taken where he stands if it arrives within this of his middle, metres.
    private const float GloveReach = 0.5f;
    /// Moving for a wider one, he goes until it is this far from his middle.
    private const float ComfortReach = 0.35f;
    /// A new footwork goal replaces the old only if it differs by more than this: no dithering.
    private const float GoalHysteresis = 0.12f;
    /// Dives only for balls within this of his middle, launching this long before it arrives.
    private const float MaxDive = 2.4f, DiveLead = 0.38f;
    /// Standing height of his shoulders above his feet, for choosing how far over he goes.
    private const float ShoulderHeight = 1.4f;
    /// How far the push-off carries his feet, at most, and how long he stays down and takes to rise.
    private const float MaxPush = 0.9f, LieTime = 0.35f, LieWithBall = 0.5f, RiseTime = 0.55f;

    private float footGoal;
    private bool diving, diveCommitted;
    private float diveStart, diveFlight, diveSide, diveFrom, diveTo, diveRoll, landedAt = -1f, liftedAt;

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
    }

    /// Before the stance code each frame. True while he is diving, down, or getting up.
    private bool UpdateDive(bool live, float dt)
    {
        if (!diving)
        {
            if (!live || holding || away || !hasIntercept)
                return false;
            // Decided once, at the first read: shuffling part of the way and then finding he could
            // not make it left him in a long lunge, legs split (filmed); a keeper sees early that
            // he has to go, stays set, and goes.
            if (!diveCommitted && CannotGetThere())
                diveCommitted = true;
            if (!diveCommitted || interceptAt - Time.time > DiveLead)
                return false;
            StartDive();
        }
        float t = Time.time - diveStart;
        float s = Mathf.Clamp01(t / diveFlight);
        float out1 = 1f - (1f - s) * (1f - s);                     // quick off the mark, easing in
        float x = Mathf.Lerp(diveFrom, diveTo, out1);
        float roll = diveRoll * s * s * (3f - 2f * s);
        if (s >= 1f && landedAt < 0f)
            landedAt = Time.time;
        // Down on his side a moment, then up: roll back over the rise, the feet where he landed.
        float stayDown = holding ? LieWithBall : LieTime;
        float rising = -1f;
        if (landedAt >= 0f && Time.time - landedAt > stayDown)
        {
            float up = Mathf.Clamp01((Time.time - landedAt - stayDown) / RiseTime);
            rising = up;
            roll = diveRoll * (1f - up * up * (3f - 2f * up));
            if (up >= 1f)
            {
                diving = false;
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
            Hold();
        }
        else
        {
            body.handTarget = intercept;
            body.weight = 1f;
            body.leftHandShare = 1f;
            body.palmFacing = -incoming;
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
        if (Mathf.Abs(offset) <= GloveReach + 0.1f || Mathf.Abs(offset) > MaxDive)
            return false;
        // Shuffling from rest at ShuffleAccel up to ShuffleSpeed: how far he gets in eta, leaving
        // DiveLead's worth to stretch for it at the end.
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
        diveFlight = Mathf.Max(0.2f, interceptAt - Time.time + 0.05f);
        diveFrom = shuffleX;
        // Toppled over, his gloves get out about 1.3 m from his feet; the push off his near foot
        // carries him the rest. Allowing 1.6 there, a ball 2 m wide went past his gloves 0.25 m
        // beyond them - short sideways, not in height (traced).
        float push = Mathf.Clamp(Mathf.Abs(offset) - (ShoulderHeight - 0.1f), 0.2f, MaxPush);
        diveTo = Mathf.Clamp(shuffleX + diveSide * push, -MaxShuffle - MaxPush, MaxShuffle + MaxPush);
        // Over far enough that his shoulders come down to a little above the ball's height, the
        // arms reaching the rest. With only 0.15 of margin a 0.4 m ball at 2 m had his gloves at
        // 0.08 m, on the grass, as it went over them (traced; missed by 0.25 m).
        float height = Mathf.Max(0.1f, local.y + 0.3f);
        diveRoll = Mathf.Clamp(Mathf.Acos(Mathf.Clamp01(height / ShoulderHeight)) * Mathf.Rad2Deg, 25f, 80f);
    }
}
