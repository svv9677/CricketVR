using UnityEngine;

/// <summary>
/// A fielder's dive: for a ball going past him out of reach - a catch or a ground ball - that his
/// feet will not get him to in time but a dive might. Same model as the keeper's (KeeperDive): a
/// dive takes DiveTime from the push to landing, he is stretched out CatchPoint of the way
/// through, then goes on down, lies a moment and gets up. Launched late, he is not out far enough
/// yet and it goes past. A glove on it in a dive only sometimes holds (DiveTakeSticks) - a diving
/// take is always the harder one.
/// </summary>
public partial class AnimatedFielder
{
    /// How far beyond his normal reach a dive gets his hands, metres.
    private const float DiveReach = 2.5f;
    private const float DiveTime = 0.6f, DiveCatchPoint = 0.7f;
    private const float DiveLead = DiveTime * DiveCatchPoint;
    /// Shoulders above his feet standing, for how far over he goes.
    private const float DiveShoulder = 1.4f;
    private const float DiveMaxPush = 1.1f, DiveLie = 0.5f, DiveLieWithBall = 0.6f, DiveRise = 0.6f;
    /// Only for a ball this low - not one over his head.
    private const float DiveMaxHeight = 2.1f;
    /// Chance a diving take sticks: just out of reach, and at full stretch.
    private const float DiveStickShort = 0.6f, DiveStickFull = 0.2f;
    private const float DiveSpillBlock = 0.4f;

    private bool diving;
    private float diveStart, diveSide, diveRoll, diveStretch, diveLanded = -1f, diveSpilledUntil, diveUpAt = -1f, diveLeap;
    /// Highest a fielder gets off the ground leaping for a high one, metres.
    private const float MaxLeap = 0.45f;
    private Vector3 divePoint, diveFrom, diveTo, diveIncoming;
    private Quaternion diveFacing;

    /// Called first in Move: starts a dive when one is on, and runs it. True while it owns him.
    private bool UpdateDive()
    {
        if (!diving)
        {
            if (!ShouldDive(out Vector3 point, out float eta))
                return false;
            StartDive(point, eta);
        }
        float t = Time.time - diveStart;
        float s = Mathf.Clamp01(t / DiveTime);
        float out1 = 1f - (1f - s) * (1f - s);
        float roll = ReachMath.DiveRoll(s, DiveCatchPoint, diveRoll);
        if (s >= 1f && diveLanded < 0f)
            diveLanded = Time.time;
        float stayDown = holdingBall ? DiveLieWithBall : DiveLie;
        Vector3 at = Vector3.Lerp(diveFrom, diveTo, out1);
        if (diveLanded >= 0f && Time.time - diveLanded > stayDown)
        {
            float up = Mathf.Clamp01((Time.time - diveLanded - stayDown) / DiveRise);
            roll = ReachMath.DiveLanded * (1f - up * up * (3f - 2f * up));
            if (up >= 1f)
            {
                EndDive();
                return false;
            }
        }
        // Off the ground for a high one: a leap sideways, up and down again over the dive.
        at.y += diveLeap * Mathf.Sin(Mathf.PI * s);
        transform.position = at;
        transform.rotation = diveFacing * Quaternion.AngleAxis(-diveSide * roll, Vector3.forward);
        speed = 0f;
        SetAnimation(0);
        return true;
    }

    /// The arms during a dive. True while diving (UpdateReach leaves them alone).
    private bool ReachForDive()
    {
        if (!diving)
            return false;
        if (holdingBall)
            return false;   // the normal hold: give, then in to the chest
        bool reaching = diveLanded < 0f && Time.time < diveStart + DiveTime * DiveCatchPoint + 0.15f;
        reach.handTarget = divePoint;
        reach.weight = reaching ? 1f : Mathf.MoveTowards(reach.weight, 0.3f, Time.deltaTime * 3f);
        reach.quickness = 2.5f;   // arms flung out, not eased (eased, a keeper's were 0.3 m short)
        reach.handGap = 0.1f;
        reach.palmFacing = -diveIncoming;
        reach.look = true;
        reach.lookTarget = Main.Instance.theBall.transform.position;
        return true;
    }

    /// A live ball passing him out of reach, now or never, and too far to run to.
    private bool ShouldDive(out Vector3 point, out float eta)
    {
        point = Vector3.zero;
        eta = 0f;
        Main inst = Main.Instance;
        // Whoever it passes near, not only the man sent after it: it went 3.4 m past a fielder
        // standing on his mark, who lunged and watched it go (filmed).
        if (holdingBall || reachingLow || walkingBack || throwing || expecting || Time.time < diveSpilledUntil ||
            inst.theBallRigidBody.isKinematic || inst.gameState != eGameState.InGame_BallHitLoop ||
            animatedFielderManagementScript == null || reach == null || !reach.Ready)
            return false;
        Vector3 body = transform.position + Vector3.up * 1f;
        if (!animatedFielderManagementScript.PredictPass(body, ReachRadius + DiveReach, out point, out eta))
            return false;
        if (eta > DiveLead || eta < 0.05f || point.y - transform.position.y > DiveMaxHeight)
            return false;
        float away = FlatDistance(point, transform.position);
        // Within reach on his feet - less when he is still running and cannot stop and stretch
        // (filmed: 1 m off the ball at the end of a chase, he ran on and it went 0.5 m past his hands).
        if (away <= (speed > 2f ? 1.2f : ReachRadius + 0.2f))
            return false;
        // His run is taking him there in time: no need.
        if (hasTarget && FlatDistance(target, point) < 0.6f && FlatDistance(transform.position, target) < RunSpeed * eta * 0.8f)
            return false;
        return true;
    }

    private void StartDive(Vector3 point, float eta)
    {
        Main inst = Main.Instance;
        diving = true;
        diveStart = Time.time;
        diveLanded = -1f;
        divePoint = point;
        Vector3 v = inst.theBallRigidBody.linearVelocity;
        diveIncoming = v.sqrMagnitude > 0.01f ? v.normalized : -transform.forward;
        // Face back along the ball's line (toward where it comes from), and go over sideways.
        Vector3 face = Flat(-diveIncoming);
        if (face.sqrMagnitude < 1e-4f)
            face = Flat(transform.forward);
        diveFacing = Quaternion.LookRotation(face.normalized, Vector3.up);
        Vector3 right = diveFacing * Vector3.right;
        Vector3 offset = Flat(point - transform.position);
        diveSide = Mathf.Sign(Vector3.Dot(offset, right));
        float lateral = Mathf.Abs(Vector3.Dot(offset, right));
        diveStretch = Mathf.InverseLerp(ReachRadius, ReachRadius + DiveReach, offset.magnitude);
        float atCatchPush = 1f - (1f - DiveCatchPoint) * (1f - DiveCatchPoint);
        float push = Mathf.Clamp(lateral - (DiveShoulder - 0.1f), 0.2f, DiveMaxPush) / atCatchPush;
        diveFrom = transform.position;
        // Along the ball's line too, as much as he needs (up to half a metre).
        float along = Mathf.Clamp(Vector3.Dot(offset, face.normalized), -0.5f, 0.5f);
        diveTo = diveFrom + right * (diveSide * push) + face.normalized * along;
        diveTo.y = BallFlight.GroundY(diveTo);
        float height = Mathf.Max(0.1f, point.y - transform.position.y + 0.3f);
        // Over as far as the take needs (and at least into a real dive), then on to the ground.
        diveRoll = Mathf.Clamp(Mathf.Acos(Mathf.Clamp01(height / DiveShoulder)) * Mathf.Rad2Deg, 40f, ReachMath.DiveLanded);
        // A ball above the shoulders: he leaps for it, up to MaxLeap off the ground.
        diveLeap = Mathf.Clamp01((point.y - transform.position.y - 1.1f) / 0.8f) * MaxLeap;
        hasTarget = false;
        speed = 0f;
    }

    private void EndDive()
    {
        if (!diving)
            return;
        diving = false;
        diveLanded = -1f;
        diveUpAt = Time.time;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Vector3 p = transform.position;
        p.y = BallFlight.GroundY(p);
        transform.position = p;
        headingVelocity = 0f;
    }

    /// Whether a glove on the ball in a dive holds it.
    private bool DiveTakeSticks() => Random.value < Mathf.Lerp(DiveStickShort, DiveStickFull, diveStretch);

    /// It hits his hands and pops out: most of its pace gone, up and off to one side.
    private void DiveSpill()
    {
        Rigidbody rb = Main.Instance.theBallRigidBody;
        Vector3 v = rb.linearVelocity;
        Vector3 across = Vector3.Cross(Vector3.up, v.sqrMagnitude > 1e-4f ? v.normalized : transform.forward);
        rb.linearVelocity = v * Random.Range(0.1f, 0.25f) + Vector3.up * Random.Range(1f, 2.5f) +
                            across * Random.Range(-1.5f, 1.5f);
        diveSpilledUntil = Time.time + DiveSpillBlock;
    }
}
