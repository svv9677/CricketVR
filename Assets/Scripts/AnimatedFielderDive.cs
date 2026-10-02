using UnityEngine;

/// <summary>
/// A fielder's dive: for a ball going past him out of reach - a catch or a ground ball - that his
/// feet will not get him to in time but a dive might.
///
/// A real dive, not a fall: he pushes off and his hips fly out toward the ball - up for a high
/// one, skimming low for a ground ball - the body going near-flat in the air with the arms
/// stretched out ahead, meeting the ball DiveCatchPoint of the way through the flight. He lands on
/// his side and front and slides on along the grass, stays down a moment, and gets up. (It used
/// to be a roll about his feet: he toppled over like a felled tree and never left the ground.)
/// The body is posed about the hips (HipHeight up from the feet), so turning him flat moves his
/// feet off the ground too, not his head into it.
///
/// Launched late, he is not stretched out yet when it arrives, and it goes past. A hand on it in a
/// dive holds it most of the time, less at full stretch (DiveTakeSticks).
/// </summary>
public partial class AnimatedFielder
{
    /// How far beyond his normal reach a dive gets his hands, metres.
    private const float DiveReach = 2.5f;
    /// Push-off to landing, and how far through it the arms meet the ball.
    private const float DiveFlight = 0.55f, DiveCatchPoint = 0.7f;
    private const float DiveLead = DiveFlight * DiveCatchPoint;
    /// Sliding on along the grass after landing: how long, and how far at most.
    private const float DiveSlideTime = 0.35f, DiveMaxSlide = 0.9f;
    /// Hips above the feet standing; hips above the grass lying on his side; hips to fingertips
    /// with the arms stretched out (shoulders 0.5 m up the body, then the arm).
    private const float HipHeight = 0.95f, LyingHips = 0.2f, HipsToHands = 1.0f;
    private const float DiveLie = 0.45f, DiveLieWithBall = 0.55f, DiveRise = 0.6f;
    /// Only for a ball this low - not one over his head.
    private const float DiveMaxHeight = 2.2f;
    /// Chance a diving take sticks: just out of reach, and at full stretch.
    private const float DiveStickShort = 0.85f, DiveStickFull = 0.5f;
    private const float DiveSpillBlock = 0.4f;

    private bool diving;
    private float diveStart, diveSide, diveRoll, diveStretch, diveLanded = -1f, diveSpilledUntil, diveUpAt = -1f;
    private float diveHipsAtCatch, diveReachOut, diveSlide;
    private Vector3 divePoint, diveFrom, diveDir, diveIncoming, lastDiveHips;
    /// Through the flight at which physics takes over - in the air, as the ball reaches (or passes)
    /// his hands: from there it is a body falling, not a dive being aimed. Sooner if he has it.
    private const float RagdollFrom = 0.74f, RagdollWithBall = 0.5f;
    private FielderRagdoll ragdoll;
    private bool ragdollDown;
    private Quaternion diveFacing;
    /// Down under physics, he goes on reaching for it until this long after it was due (s).
    private const float DiveReachOnAfter = 0.25f;

    /// Called first in Move: starts a dive when one is on, and runs it. True while it owns him.
    private bool UpdateDive()
    {
        if (!diving)
        {
            if (!ShouldDive(out Vector3 point, out float eta))
                return false;
            StartDive(point, eta);
        }
        // Diving: nothing of him into the ground (FielderRagdoll's guard, off the rest of the time).
        if (ragdoll != null)
            ragdoll.GuardGroundThisFrame();
        // Handed to physics for the landing: down, then the get-up clip (FielderRagdoll).
        if (ragdollDown)
        {
            speed = 0f;
            if (ragdoll != null && ragdoll.Busy)
            {
                // Handed over before the ball got to him: his arms still go for it (and he can
                // still take it) until it is past. With it: hugged in as he lands.
                Main inst = Main.Instance;
                if (holdingBall)
                    ragdoll.HugBall();
                else if (!inst.theBallRigidBody.isKinematic && inst.gameState == eGameState.InGame_BallHitLoop &&
                         Time.time < diveStart + DiveFlight * DiveCatchPoint + DiveReachOnAfter)
                {
                    Vector3 ball = inst.theBall.transform.position;
                    ragdoll.ReachFor((ball - divePoint).sqrMagnitude < 1f ? ball : divePoint, 0.1f);
                }
                return true;
            }
            ragdollDown = false;
            diving = false;
            diveLanded = -1f;
            diveUpAt = Time.time;
            headingVelocity = 0f;
            return false;
        }
        float t = Time.time - diveStart;
        float stayDown = holdingBall ? DiveLieWithBall : DiveLie;
        float roll;
        Vector3 hips;
        if (t < DiveFlight)
        {
            // In the air.
            float s = t / DiveFlight;
            float c = DiveCatchPoint;
            roll = ReachMath.DiveRoll(s, c, diveRoll);
            float across = diveReachOut * (1f - (1f - s) * (1f - s)) / (1f - (1f - c) * (1f - c));
            float y = s <= c ? Mathf.Lerp(HipHeight, diveHipsAtCatch, Smooth(s / c))
                             : Mathf.Lerp(diveHipsAtCatch, LyingHips, Sq((s - c) / (1f - c)));
            hips = diveFrom + diveDir * across + Vector3.up * y;
            // Past the take and nearly down: physics has the landing, the slide and the tumble,
            // moving as he was (a get-up clip follows). Without a ragdoll, the scripted landing below.
            if ((s >= RagdollFrom || (holdingBall && s >= RagdollWithBall)) && ragdoll != null && ragdoll.Ready)
            {
                Quaternion pose = diveFacing * Quaternion.AngleAxis(-diveSide * roll, Vector3.forward);
                Vector3 velocity = t > 0.02f ? (hips - lastDiveHips) / Mathf.Max(Time.deltaTime, 1e-3f) : diveDir * 4f;
                transform.position = hips - pose * (Vector3.up * HipHeight);
                transform.rotation = pose;
                ragdoll.Fall(velocity, Vector3.zero);
                ragdollDown = true;
                speed = 0f;
                return true;
            }
            lastDiveHips = hips;
        }
        else
        {
            // Down, sliding on, lying there, then up.
            if (diveLanded < 0f)
                diveLanded = Time.time;
            float since = Time.time - diveLanded;
            float slid = diveSlide * (1f - Sq(1f - Mathf.Clamp01(since / DiveSlideTime)));
            float landed = diveReachOut / (1f - (1f - DiveCatchPoint) * (1f - DiveCatchPoint));
            hips = diveFrom + diveDir * (landed + slid) + Vector3.up * LyingHips;
            roll = ReachMath.DiveLanded;
            float rise = since - DiveSlideTime - stayDown;
            if (rise > 0f)
            {
                // Up about his feet, where they lie.
                float up = Mathf.Clamp01(rise / DiveRise);
                roll = ReachMath.DiveLanded * (1f - Smooth(up));
                Quaternion lying = diveFacing * Quaternion.AngleAxis(-diveSide * ReachMath.DiveLanded, Vector3.forward);
                Vector3 feet = hips - lying * (Vector3.up * HipHeight);
                feet.y = BallFlight.GroundY(feet);
                if (up >= 1f)
                {
                    transform.position = feet;
                    EndDive();
                    return false;
                }
                transform.position = feet;
                transform.rotation = diveFacing * Quaternion.AngleAxis(-diveSide * roll, Vector3.forward);
                speed = 0f;
                SetAnimation(0);
                return true;
            }
        }
        // Posed about the hips: the feet go wherever the body's angle puts them.
        Quaternion body = diveFacing * Quaternion.AngleAxis(-diveSide * roll, Vector3.forward);
        transform.position = hips - body * (Vector3.up * HipHeight);
        transform.rotation = body;
        speed = 0f;
        SetAnimation(0);
        return true;
    }

    private static float Smooth(float a) { a = Mathf.Clamp01(a); return a * a * (3f - 2f * a); }
    private static float Sq(float a) { a = Mathf.Clamp01(a); return a * a; }

    /// The arms during a dive. True while diving (UpdateReach leaves them alone).
    private bool ReachForDive()
    {
        if (!diving)
            return false;
        if (holdingBall)
            return false;   // the normal hold: give, then in to the chest
        bool reaching = diveLanded < 0f && Time.time < diveStart + DiveFlight * DiveCatchPoint + 0.15f;
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
        if (ragdoll == null)
            ragdoll = GetComponent<FielderRagdoll>();
        ragdollDown = false;
        lastDiveHips = transform.position + Vector3.up * HipHeight;
        Vector3 v = inst.theBallRigidBody.linearVelocity;
        diveIncoming = v.sqrMagnitude > 0.01f ? v.normalized : -transform.forward;
        // Face back along the ball's line (toward where it comes from), and go out sideways to it.
        Vector3 face = Flat(-diveIncoming);
        if (face.sqrMagnitude < 1e-4f)
            face = Flat(transform.forward);
        diveFacing = Quaternion.LookRotation(face.normalized, Vector3.up);
        Vector3 right = diveFacing * Vector3.right;
        Vector3 offset = Flat(point - transform.position);
        diveSide = Mathf.Sign(Vector3.Dot(offset, right));
        diveStretch = Mathf.InverseLerp(ReachRadius, ReachRadius + DiveReach, offset.magnitude);
        diveDir = offset.sqrMagnitude > 1e-4f ? offset.normalized : right * diveSide;
        diveFrom = transform.position;
        // How the body meets it: flat out for a low ball, more upright (and off the ground) for a
        // high one - the hips at the height that puts the outstretched hands on the ball.
        float ballUp = point.y - transform.position.y;
        float atCatch = Mathf.Lerp(82f, 40f, Mathf.InverseLerp(0.3f, 1.9f, ballUp));
        diveRoll = atCatch;
        float a = atCatch * Mathf.Deg2Rad;
        diveHipsAtCatch = Mathf.Clamp(ballUp - HipsToHands * Mathf.Cos(a), 0.3f, 1.45f);
        // The hips travel the rest of the way out: the hands reach HipsToHands along the body.
        diveReachOut = Mathf.Max(0.2f, offset.magnitude - HipsToHands * Mathf.Sin(a));
        // Carried on along the grass by the speed of the dive.
        diveSlide = Mathf.Min(DiveMaxSlide, 0.25f * diveReachOut / DiveFlight * DiveSlideTime + 0.15f);
        hasTarget = false;
        speed = 0f;
    }

    private void EndDive()
    {
        if (!diving)
            return;
        if (ragdollDown && ragdoll != null)
            ragdoll.Recover();
        ragdollDown = false;
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
