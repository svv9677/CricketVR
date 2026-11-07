using UnityEngine;

/// <summary>
/// The bowler's arm, ball by ball (BowlerArm). A left-armer is the world mirror image of a
/// right-armer across the pitch's centre line: his mark on the other side of the stumps, his
/// action the humanoid's own mirror (the controller's Mirror bool on every state), the ball in his
/// real left hand (BowlerGrip binds it), his right foot over the line (FrontFoot), and his run-up
/// along the mirror of a right-armer's line.
///
/// Why not a negative scale (2026-11-06, both tried): flipping the bowler's own X scale, or putting
/// him under a reflection, renders the skinned mesh inside out on this pipeline - the far side's
/// inner faces, or the right faces lit from inside: "the hands and legs are not flipped, or
/// somewhat weird". The humanoid mirror draws a real body.
///
/// Its catch is the run-up: the mirrored clip's root motion turns him the wrong way and carries
/// him further (traced: yaw about 105 deg against a right-armer's 98, out to z -5.6, and 13% more
/// ground). So until he lets go he is steered (OnAnimatorMove): toward the mirror of where a
/// right-armer from the same mark lets go (RunUpEnd), covering the clip's stride scaled to a
/// right-armer's ground (StrideScale), facing that way with the clip's sway mirrored about it.
/// After the release the clip moves him, its turns mirrored.
/// </summary>
public partial class AnimatedBowler
{
    /// The bowling controller's bool that mirrors every state (BowlerMirrorSetup puts it there).
    public const string MirrorParam = "Mirror";

    /// He bowls left-arm this ball.
    public bool LeftArm { get; private set; }

    private AFInfo baseBowlerInfo;
    private Transform rightHand, leftHand;
    private Vector3 handLocal;

    /// Right before the run-up: this ball's arm - the bowler's own in a match, drawn per ball in
    /// the nets - and his mark, action and hand to go with it.
    private void ChooseArm()
    {
        ResetSway();
        Bowler b = HUD.Instance != null ? HUD.Instance.CurrentBowler : null;
        bool nets = gameObject.scene.name == NetsSession.NetsScene;
        bool left = BowlerArm.Force ?? (b != null && (nets ? BowlerArm.NetsBall(b.Kind, Random.value) : BowlerArm.IsLeftArm(b.Name, b.Kind)));
        SetArm(left);
    }

    public void SetArm(bool left)
    {
        LeftArm = left;
        if (baseBowlerInfo == null)
            baseBowlerInfo = currentBowlerInfo;
        if (baseBowlerInfo != null)
            currentBowlerInfo = left ? MirrorMark(baseBowlerInfo) : baseBowlerInfo;
        ApplyMirror();
        MoveHand(left);
    }

    private static AFInfo MirrorMark(AFInfo a) => a == null ? null : a.MirroredCopy();

    /// The controller's Mirror to this ball's arm (again after fielding swapped the controller).
    private void ApplyMirror()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        // Looked up each time (once a ball): the controller in place may be the fielding one.
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == MirrorParam && p.type == AnimatorControllerParameterType.Bool)
                animator.SetBool(MirrorParam, LeftArm);
    }

    /// The ball's holder (`hand`, made under the right hand) onto the bowling hand.
    private void MoveHand(bool left)
    {
        if (hand == null || animator == null || !animator.isHuman)
            return;
        if (rightHand == null)
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            handLocal = hand.transform.localPosition;
        }
        Transform to = left ? leftHand : rightHand;
        if (to == null || hand.transform.parent == to)
            return;
        hand.transform.SetParent(to, false);
        hand.transform.localPosition = left ? new Vector3(-handLocal.x, handLocal.y, handLocal.z) : handLocal;
    }

    // ---- The left-armer's run-up ------------------------------------------------------------------

    /// Where a right-armer's root is as he lets go, from his mark (traced 2026-11-06: from z 2.32
    /// he lets go at x -9.18, z 0.90); a left-armer's, mirrored.
    public const float RunUpIn = 1.42f, RunUpEndX = -9.18f, MinEndZ = 0.3f;
    /// The mirrored clip covers this much more ground than the right-arm one (traced: 15.9 m
    /// against 14.0 from the same mark), so its stride is scaled back by it.
    public const float StrideScale = 0.88f;
    /// The clip's sway is measured about its own running mean over about this long (s).
    private const float SwayWindow = 0.6f;

    private float clipYaw, clipYawMean;

    public static Vector3 RunUpEnd(Vector3 mark) =>
        new Vector3(RunUpEndX, 0f, Mathf.Sign(mark.z) * Mathf.Max(MinEndZ, Mathf.Abs(mark.z) - RunUpIn));

    private void ResetSway() => clipYaw = clipYawMean = 0f;

    /// Root motion, applied here: a right-armer, and anyone with it off, exactly as the Animator
    /// would; a left-armer steered until he lets go, then the clip's step with its turn mirrored.
    private void OnAnimatorMove()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null || !animator.applyRootMotion)
            return;
        Vector3 step = animator.rootPosition - transform.position;
        Quaternion turn = animator.rootRotation * Quaternion.Inverse(transform.rotation);
        if (!LeftArm || Fielding)
        {
            transform.position += step;
            transform.rotation = turn * transform.rotation;
            return;
        }
        if (ballInHand && currentBowlerInfo != null)
        {
            Steer(step, turn);
            return;
        }
        transform.position += step;
        transform.rotation = MirrorTurn(turn) * transform.rotation;
    }

    /// A turn about the vertical, the other way (mirrored across the pitch).
    private static Quaternion MirrorTurn(Quaternion q) => new Quaternion(-q.x, -q.y, q.z, q.w);

    private void Steer(Vector3 step, Quaternion turn)
    {
        clipYaw += Mathf.DeltaAngle(0f, turn.eulerAngles.y);
        clipYawMean = Mathf.Lerp(clipYawMean, clipYaw, 1f - Mathf.Exp(-Time.deltaTime / SwayWindow));
        float sway = Mathf.DeltaAngle(clipYawMean, clipYaw);
        // The stride along his facing (its sideways sway cancels over a run), scaled to a right-armer's ground.
        float stride = Mathf.Max(0f, (Quaternion.Inverse(transform.rotation) * step).z) * StrideScale;
        Vector3 end = RunUpEnd(currentBowlerInfo.startPos);
        Vector3 to = new Vector3(end.x - transform.position.x, 0f, end.z - transform.position.z);
        Vector3 dir = to.x > 0.05f ? to.normalized : Vector3.right;
        transform.position += dir * stride + Vector3.up * step.y;
        float heading = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, heading - sway, 0f);
    }
}
