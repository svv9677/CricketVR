using UnityEngine;

/// <summary>
/// The keeper's part in getting the ball back once it is dead (ThrowBack): he takes the throw
/// in from the field standing, gloves up to it and facing it, then turns and passes it on - to
/// the bowler, or to an inner fielder when the bowler is too far off - with the fielders' throw
/// animation.
/// </summary>
public partial class KeeperCatcher : IThrowPlayer
{
    private const float KeeperReleaseTime = 0.82f, KeeperFollowThrough = 0.9f;
    private const float KeeperCatchHeight = 1.1f, KeeperCatchAhead = 0.4f;

    private bool throwingOut, expectingThrow;
    private float throwOutStart, expectThrowAt, standAfterThrowUntil = -1f;
    private Vector3 throwOutAt, expectThrowPoint, expectThrowFrom;

    // ---- IThrowPlayer ----------------------------------------------------------------------------
    public bool HoldingBall => holding;
    public float HeldFor => holding ? Time.time - heldSince : 0f;
    public Vector3 ReleasePoint => KeeperHandPoint;
    public bool ReadyToRelease => throwingOut && Time.time - throwOutStart >= KeeperReleaseTime;

    public Vector3 CatchPointFrom(Vector3 from)
    {
        Vector3 at = bodyRoot != null ? bodyRoot.position : transform.position;
        Vector3 toThrower = Vector3.ProjectOnPlane(from - at, Vector3.up);
        Vector3 facing = toThrower.sqrMagnitude > 1e-4f ? toThrower.normalized : -transform.forward;
        return at + Vector3.up * KeeperCatchHeight + facing * KeeperCatchAhead;
    }

    public void BeginThrow(Vector3 target)
    {
        throwingOut = true;
        throwOutStart = Time.time;
        throwOutAt = target;
        Animator a = body != null ? body.GetComponent<Animator>() : null;
        if (a != null)
        {
            a.speed = 1f;
            a.CrossFadeInFixedTime("5 Throw", 0.15f);
        }
    }

    public void Release()
    {
        holding = false;
        throwingOut = false;
        standAfterThrowUntil = Time.time + KeeperFollowThrough;
    }

    public void ExpectBall(Vector3 point, Vector3 from, float arrivalTime)
    {
        expectingThrow = true;
        expectThrowPoint = point;
        expectThrowFrom = from;
        expectThrowAt = arrivalTime;
    }

    public void TakeThrow(Vector3 incomingDir)
    {
        expectingThrow = false;
        holding = true;
        heldSince = Time.time;
        if (incomingDir.sqrMagnitude > 1e-6f)
            incoming = incomingDir.normalized;
        contact = body != null && body.Ready ? body.HoldPoint : Main.Instance.theBall.transform.position;
        holdOffset = Main.Instance.theBall.transform.position - contact;
        crouchAtTake = body != null ? body.CurrentDrop : 0f;
    }

    // ---- Hooks from Update / LateUpdate / ResetStance ---------------------------------------------

    private Vector3 KeeperHandPoint
    {
        get
        {
            Animator a = body != null ? body.GetComponent<Animator>() : null;
            Transform hand = a != null ? a.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null ? hand.position + hand.up * 0.04f : transform.position + Vector3.up * 1.5f;
        }
    }

    /// Throwing it on, taking a throw, or through the follow-through: stood up, facing the right
    /// way. True while this owns his body this frame.
    private bool UpdateThrowRole(float dt)
    {
        if (!throwingOut && !expectingThrow && standAfterThrowUntil < 0f)
            return false;
        if (standAfterThrowUntil >= 0f && !throwingOut && !expectingThrow && Time.time >= standAfterThrowUntil)
        {
            standAfterThrowUntil = -1f;
            return false;
        }
        Vector3 faceTo = throwingOut ? throwOutAt : expectingThrow ? expectThrowFrom : Main.Instance.theBall.transform.position;
        Vector3 flat = Vector3.ProjectOnPlane(faceTo - bodyRoot.position, Vector3.up);
        if (flat.sqrMagnitude > 1e-4f)
            bodyRoot.rotation = Quaternion.RotateTowards(bodyRoot.rotation, Quaternion.LookRotation(flat), 540f * dt);
        body.feetOverride = false;   // standing: the animation's feet
        body.minCrouch = 0f;
        body.look = true;
        body.lookTarget = faceTo;
        if (expectingThrow)
        {
            body.handTarget = expectThrowPoint;
            body.weight = Mathf.InverseLerp(0.9f, 0.3f, expectThrowAt - Time.time);
            body.leftHandShare = 1f;
            body.handGap = 0.11f;
            body.palmFacing = flat.sqrMagnitude > 1e-4f ? flat.normalized : bodyRoot.forward;
        }
        else
        {
            body.weight = 0f;   // arms free for the throw
        }
        return true;
    }

    private void ClearThrowRole()
    {
        throwingOut = expectingThrow = false;
        standAfterThrowUntil = -1f;
    }
}
