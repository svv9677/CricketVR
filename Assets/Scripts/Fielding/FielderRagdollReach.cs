using UnityEngine;

/// <summary>
/// Still reaching for the ball after a dive has been handed to physics in the air. The dive hands
/// over at 74% of its flight, about when the ball reaches his hands; when it comes a little late,
/// his arms were plain ragdoll by then - flung about by the fall, nowhere near it, and the take was
/// off as well.
///
/// The IK cannot pose a body physics is moving, so the reach is made of forces: each palm is pulled
/// toward its own side of the ball (ReachFor, set every frame by the dive while the ball is still
/// coming) at a capped speed and acceleration, through the arm's joints, and the arms are held up
/// against gravity. The rest of him falls as he was falling.
/// </summary>
public partial class FielderRagdoll
{
    /// Palm speed toward the ball: proportional to the distance (per second), at most MaxReachSpeed
    /// (m/s); reached within ReachResponse (s), with at most MaxReachAccel (m/s^2) of pull.
    private const float ReachGain = 14f, MaxReachSpeed = 7f, ReachResponse = 0.04f, MaxReachAccel = 150f;
    /// A reach not renewed for this long has ended (the dive stops asking once the ball is past).
    private const float ReachLapse = 0.05f;

    private Vector3 reachTarget;
    private float reachGap, reachUntil = -1f;
    private Rigidbody upperArmL, upperArmR, lowerArmL, lowerArmR;

    /// Down under physics and reaching for the ball: the dives allow the take then.
    public bool Reaching => state == State.Down && Time.time <= reachUntil;

    /// Hands to `target` this frame, `gap` apart either side of it. Called every frame it applies.
    public void ReachFor(Vector3 target, float gap)
    {
        reachTarget = target;
        reachGap = gap;
        reachUntil = Time.time + ReachLapse;
    }

    /// Holding the ball as he goes down: gloves in together in front of his chest, as the animated
    /// dive hugs it in. Handed over with it (at half the dive), his arms flopped about with the
    /// ball in them - still flailing.
    public void HugBall()
    {
        Transform hipL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), hipR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform neck = anim.GetBoneTransform(HumanBodyBones.Neck), chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        if (hipL == null || hipR == null || neck == null || chest == null)
            return;
        // His own frame as he lies, from the bones physics is moving: across the hips, up the spine.
        Vector3 right = (hipR.position - hipL.position).normalized;
        Vector3 up = (neck.position - hips.position).normalized;
        Vector3 front = Vector3.Cross(right, up).normalized;
        float scale = transform.lossyScale.y;
        ReachFor(chest.position + front * (HugAhead * scale) - up * (HugBelow * scale), HugGap * scale);
    }

    /// The hug: this far in front of the chest and below it, the gloves this far apart, metres.
    private const float HugAhead = 0.3f, HugBelow = 0.1f, HugGap = 0.1f;

    private int ballHand;   // 0 not chosen this fall, 1 left, 2 right

    /// Where a ball he holds sits while physics has him: in one glove, the one it was nearest when
    /// he went down. Kept between his two palms (HoldPoint), as when they are together round it,
    /// it hung in mid-air under his chest whenever the fall swung them apart (filmed: 1.5 m apart).
    public Vector3 HeldBallPoint(Vector3 ball)
    {
        Vector3 left = Reach.PalmCentre(true), right = Reach.PalmCentre(false);
        if (ballHand == 0)
            ballHand = (ball - left).sqrMagnitude < (ball - right).sqrMagnitude ? 1 : 2;
        return ballHand == 1 ? left : right;
    }

    private void CacheReachBodies()
    {
        Rigidbody R(HumanBodyBones bone)
        {
            Transform t = anim.GetBoneTransform(bone);
            return t != null ? t.GetComponent<Rigidbody>() : null;
        }
        upperArmL = R(HumanBodyBones.LeftUpperArm);
        upperArmR = R(HumanBodyBones.RightUpperArm);
        lowerArmL = R(HumanBodyBones.LeftLowerArm);
        lowerArmR = R(HumanBodyBones.RightLowerArm);
    }

    private void FixedUpdate()
    {
        if (!Reaching || Reach == null)
            return;
        Vector3 left = Reach.PalmCentre(true), right = Reach.PalmCentre(false);
        Vector3 across = right - left;
        across = across.sqrMagnitude > 1e-4f ? across.normalized : transform.right;
        Pull(lowerArmL, left, reachTarget - across * (reachGap * 0.5f));
        Pull(lowerArmR, right, reachTarget + across * (reachGap * 0.5f));
        HoldUp(upperArmL);
        HoldUp(upperArmR);
        HoldUp(lowerArmL);
        HoldUp(lowerArmR);
    }

    /// Accelerate the body so its point `palm` heads for `target`.
    private static void Pull(Rigidbody body, Vector3 palm, Vector3 target)
    {
        if (body == null || body.isKinematic)
            return;
        Vector3 want = Vector3.ClampMagnitude((target - palm) * ReachGain, MaxReachSpeed);
        Vector3 accel = Vector3.ClampMagnitude((want - body.GetPointVelocity(palm)) / ReachResponse, MaxReachAccel);
        body.AddForceAtPosition(accel * body.mass, palm, ForceMode.Force);
    }

    private static void HoldUp(Rigidbody body)
    {
        if (body != null && !body.isKinematic)
            body.AddForce(-Physics.gravity, ForceMode.Acceleration);
    }
}
