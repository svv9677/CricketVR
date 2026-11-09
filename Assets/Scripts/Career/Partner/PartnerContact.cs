using UnityEngine;

/// <summary>
/// The partner's bat meeting the ball (phase 3). His bat has no collider - the swing is played to
/// the plan PartnerDirector made at release - so the hit is applied here, in the physics step the
/// ball reaches the plan's contact plane: the ball is put where it crosses it, given the planned
/// exit velocity, and the game is told exactly as Bat.OnBallHit tells it of the player's hits (the
/// same order: the ball, hasHitBall, InGame_BallHit, the speeds, the sound, ShotStruck, the shot's
/// distance). No haptics: the player did not hit it. From there the game's own fielding, catching,
/// boundary and scoring decide what the shot was worth, as for any ball.
///
/// One step ahead: the hit goes on in the step BEFORE the ball would cross, at the crossing point, so
/// the ball never flies through the bat for a step. Before Ball (order -100): Ball.FixedUpdate's swing
/// force would otherwise be applied once more on top of the new velocity.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PartnerContact : MonoBehaviour
{
    /// The ball must cross the contact plane within this of where the plan met it (m): further off,
    /// the flight was not the one planned for (it kept low, it turned) and the bat misses it.
    public const float ContactWindow = 0.35f;
    /// How far the ball is put out from the face along the contact normal (m), as Bat does.
    public const float FaceNudge = 0.002f;

    private ShotPlan plan;
    private bool armed;

    /// The bat met the ball this ball (for the log).
    public bool Struck { get; private set; }
    /// The ball crossed the contact plane too far from the plan's contact point (for the log, m).
    public float MissedBy { get; private set; }

    /// This ball's plan, at release. A plan that makes no contact (a leave, a play and miss) arms nothing.
    public void Arm(in ShotPlan p)
    {
        plan = p;
        armed = p.makesContact;
        Struck = false;
        MissedBy = 0f;
    }

    public void Disarm() => armed = false;

    private void FixedUpdate()
    {
        if (!armed)
            return;
        Main inst = Main.Instance;
        if (!PartnerWatch.ThisBall || inst == null || inst.theBallRigidBody == null || inst.theBatScript == null)
        {
            armed = false;
            return;
        }
        // Only the delivery itself: not once it is called wide, has hit the stumps or is played on
        // for byes, and never twice.
        Rigidbody ball = inst.theBallRigidBody;
        if (inst.gameState != eGameState.InGame_DeliverBallLoop || Extras.InPlay || inst.theBatScript.hasHitBall || ball.isKinematic)
            return;
        if (ball.position.x > plan.contactX)
        {
            armed = false;   // past the bat already
            return;
        }
        if (!CrossesNextStep(ball.position, ball.linearVelocity, Time.fixedDeltaTime, plan.contactX, out Vector3 at))
            return;
        armed = false;
        MissedBy = (at - plan.contactPoint).magnitude;
        if (MissedBy > ContactWindow)
        {
            Debug.Log($"[Partner] no contact: crossed {MissedBy:F2} m from the planned point");
            return;
        }
        Strike(inst, ball, at);
    }

    /// Where the ball crosses the plane x = contactX during the coming step of `dt`, if it does. The
    /// step is short enough (10 ms) to take as a straight line.
    public static bool CrossesNextStep(Vector3 p, Vector3 v, float dt, float contactX, out Vector3 at)
    {
        at = p;
        if (v.x <= 1e-3f || p.x > contactX || p.x + v.x * dt < contactX)
            return false;
        at = p + v * ((contactX - p.x) / v.x);
        return true;
    }

    /// The direction the bat pushed the ball (the change in its velocity): the contact normal, from
    /// the face out to the ball.
    public static Vector3 FaceNormal(Vector3 incoming, Vector3 exit)
    {
        Vector3 push = exit - incoming;
        return push.sqrMagnitude > 1e-6f ? push.normalized : -incoming.normalized;
    }

    /// The shot as ShotInfo reports a contact. The partner's bat has no shield: the real-blade
    /// contact is the contact.
    public static ShotInfo Info(in ShotPlan p, Vector3 incoming, Vector3 contact)
    {
        Vector3 exit = p.exitVelocity;
        return new ShotInfo
        {
            ballSpeedIn = incoming.magnitude,
            batSpeed = p.batVelocity.magnitude,
            exitSpeed = exit.magnitude,
            quality = p.quality,
            contactLabel = BatContact.Label(p.kind),
            edge = p.edge,
            kind = p.kind,
            impactSpeed = (p.batVelocity - incoming).magnitude,
            exitVelocity = exit,
            contactPoint = contact,
            realQuality = p.quality,
            realContact = p.edge ? eRealContact.Edge : eRealContact.Clean,
            realKind = p.kind,
            realExitVelocity = exit,
        };
    }

    /// The hit, in Bat.OnBallHit's order (see the summary).
    private void Strike(Main inst, Rigidbody ball, Vector3 at)
    {
        Vector3 incoming = ball.linearVelocity;
        Vector3 centre = at + FaceNormal(incoming, plan.exitVelocity) * FaceNudge;
        ball.position = centre;
        ball.transform.position = centre;
        ball.linearVelocity = plan.exitVelocity;
        Struck = true;

        inst.theBatScript.hasHitBall = true;
        inst.gameState = eGameState.InGame_BallHit;
        ShotInfo info = Info(plan, incoming, centre);
        if (BallSpeed.Instance != null)
            BallSpeed.Instance.updateBatAndFinalSpeed(info.batSpeed, info.exitSpeed);
        BatSounds sounds = inst.theBat != null ? inst.theBat.GetComponent<BatSounds>() : null;
        if (sounds != null)
            sounds.Play(plan.kind, info.impactSpeed, centre);
        Debug.Log($"[Partner] contact {plan.shot} {plan.kind} in={info.ballSpeedIn:F1} bat={info.batSpeed:F1} out={info.exitSpeed:F1} " +
                  $"at={centre.ToString("F2")} off={MissedBy:F2}");
        Bat.ReportShot(info);
        if (ShotDistance.Instance != null)
            ShotDistance.Instance.RecordHit(inst.theBall.transform.position);
    }
}
