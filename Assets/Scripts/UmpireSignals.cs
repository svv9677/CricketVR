using UnityEngine;

/// <summary>
/// The umpire's signals, as the Laws have them, posed over his idle after the Animator runs:
///   * wide - both arms straight out to the sides;
///   * no-ball - one arm straight out to the side;
///   * bye - one open hand raised straight up;
///   * leg bye - a raised knee, touched with the hand;
///   * four - an arm waved across the chest;
///   * six - both arms straight up above the head;
///   * out - the arm raised in front, finger up.
/// Each comes up a moment after the ball is scored (HUD.BallScored), is held, and goes down.
/// The game has no no-balls or leg byes yet (no front-foot calls; the batter has no body for a
/// ball to hit): those poses are here for when it does - Signal(UmpireSignal.NoBall).
/// </summary>
[RequireComponent(typeof(Animator))]
public class UmpireSignals : MonoBehaviour
{
    private const float Delay = 0.4f, Rise = 0.3f, Hold = 1.6f, WaveHold = 2.4f, Fall = 0.45f;

    private Animator animator;
    private Transform rUpper, rLower, rHand, lUpper, lLower, lHand, rThigh, rShin, rFoot;
    private UmpireSignal signal;
    private float startAt = -100f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rUpper = Bone(HumanBodyBones.RightUpperArm);
        rLower = Bone(HumanBodyBones.RightLowerArm);
        rHand = Bone(HumanBodyBones.RightHand);
        lUpper = Bone(HumanBodyBones.LeftUpperArm);
        lLower = Bone(HumanBodyBones.LeftLowerArm);
        lHand = Bone(HumanBodyBones.LeftHand);
        rThigh = Bone(HumanBodyBones.RightUpperLeg);
        rShin = Bone(HumanBodyBones.RightLowerLeg);
        rFoot = Bone(HumanBodyBones.RightFoot);
    }

    private Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

    private void OnEnable() => HUD.BallScored += OnBallScored;
    private void OnDisable() => HUD.BallScored -= OnBallScored;

    private void OnBallScored(BallOutcome outcome)
    {
        if (outcome.Signal != UmpireSignal.None)
            Signal(outcome.Signal, Delay);
    }

    /// Make a signal, `after` seconds from now.
    public void Signal(UmpireSignal what, float after = 0f)
    {
        signal = what;
        startAt = Time.time + after;
    }

    private void LateUpdate()
    {
        float t = Time.time - startAt;
        float hold = signal == UmpireSignal.Four ? WaveHold : Hold;
        if (signal == UmpireSignal.None || t < 0f || t > Rise + hold + Fall || rUpper == null)
            return;
        float w = t < Rise ? Mathf.SmoothStep(0f, 1f, t / Rise)
                : t > Rise + hold ? Mathf.SmoothStep(1f, 0f, (t - Rise - hold) / Fall) : 1f;
        Pose(signal, t, w);
    }

    private void Pose(UmpireSignal what, float t, float w)
    {
        Vector3 f = transform.forward, r = transform.right, u = Vector3.up;
        switch (what)
        {
            case UmpireSignal.Wide:
                Arm(rUpper, rLower, rHand, r, r, w);
                Arm(lUpper, lLower, lHand, -r, -r, w);
                break;
            case UmpireSignal.NoBall:
                Arm(rUpper, rLower, rHand, r, r, w);
                break;
            case UmpireSignal.Bye:
                Arm(rUpper, rLower, rHand, u, u, w);
                break;
            case UmpireSignal.LegBye:
                LegBye(f, r, u, w);
                break;
            case UmpireSignal.Four:
                // Side to side across the chest, a little over once a second.
                Vector3 across = Quaternion.AngleAxis(Mathf.Sin((t - Rise) * Mathf.PI * 2.4f) * 50f, u) * (f * 0.85f + r * 0.15f - u * 0.1f);
                Arm(rUpper, rLower, rHand, across.normalized, across.normalized, w);
                break;
            case UmpireSignal.Six:
                Arm(rUpper, rLower, rHand, (u + r * 0.2f).normalized, u, w);
                Arm(lUpper, lLower, lHand, (u - r * 0.2f).normalized, u, w);
                break;
            case UmpireSignal.Out:
                Vector3 up = (f * 0.8f + u * 0.6f).normalized;   // forward and up, not straight up (that is a bye)
                Arm(rUpper, rLower, rHand, up, up, w);
                break;
        }
    }

    /// Knee raised in front, the hand down on it.
    private void LegBye(Vector3 f, Vector3 r, Vector3 u, float w)
    {
        if (rThigh == null || rShin == null || rFoot == null)
            return;
        Aim(rThigh, rShin, (f * 0.9f - u * 0.45f).normalized, w);
        Aim(rShin, rFoot, (-u + f * 0.1f).normalized, w);
        Aim(rUpper, rLower, (-u * 0.75f + f * 0.45f + r * 0.15f).normalized, w);
        Aim(rLower, rHand, (rShin.position - rLower.position).normalized, w);
    }

    private static void Arm(Transform upper, Transform lower, Transform hand, Vector3 upperDir, Vector3 lowerDir, float w)
    {
        Aim(upper, lower, upperDir, w);
        Aim(lower, hand, lowerDir, w);
    }

    /// Turn `bone` so the line from it to `child` points along `dir`, by `w`.
    private static void Aim(Transform bone, Transform child, Vector3 dir, float w)
    {
        if (bone == null || child == null)
            return;
        Vector3 now = child.position - bone.position;
        if (now.sqrMagnitude < 1e-8f || dir.sqrMagnitude < 1e-8f)
            return;
        Quaternion full = Quaternion.FromToRotation(now, dir) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, full, w);
    }
}
