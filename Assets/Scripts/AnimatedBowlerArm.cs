using UnityEngine;

/// <summary>
/// The bowler's arm, ball by ball (BowlerArm): a left-armer is the right-armer flipped - the whole
/// bowler mirrored left for right (his X scale negative), so his bowling arm, the ball in that hand
/// (BowlerGrip), his front foot (FrontFoot) and his action all show on the other side - with his
/// mark across the pitch and his turns mirrored (OnAnimatorMove).
///
/// Why the person and not the animation: the humanoid Mirror flips the pose, but under it the
/// clip's root motion turned him the wrong way and carried him on; he let go from z -5.6, or
/// metres over the crease once steered (traced 2026-11-06). Flipped, the clip's step mirrors with
/// him and only the turn needs mirroring.
/// </summary>
public partial class AnimatedBowler
{
    /// He bowls left-arm this ball.
    public bool LeftArm { get; private set; }

    private AFInfo baseBowlerInfo;

    /// Right before the run-up: this ball's arm - the bowler's own in a match, drawn per ball in
    /// the nets - and his mark and mirror to go with it.
    private void ChooseArm()
    {
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
            currentBowlerInfo = left ? Mirrored(baseBowlerInfo) : baseBowlerInfo;
        Vector3 s = transform.localScale;
        s.x = left ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        transform.localScale = s;
    }

    private static AFInfo Mirrored(AFInfo a) => a == null ? null : a.MirroredCopy();

    /// Root motion, applied here so a left-armer's turns can be mirrored: flipped, the clip's step
    /// mirrors with him, but its turn is a rotation and comes through as the right-armer's, which
    /// swung him out wide. A right-armer, and anyone with root motion off, moves exactly as the
    /// Animator would have moved him.
    private void OnAnimatorMove()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null || !animator.applyRootMotion)
            return;
        Vector3 step = animator.rootPosition - transform.position;
        Quaternion turn = animator.rootRotation * Quaternion.Inverse(transform.rotation);
        if (LeftArm)
            turn = new Quaternion(-turn.x, -turn.y, turn.z, turn.w);   // about up: the other way
        transform.position += step;
        transform.rotation = turn * transform.rotation;
    }
}
