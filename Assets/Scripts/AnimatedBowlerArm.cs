using UnityEngine;

/// <summary>
/// The bowler's arm, ball by ball (BowlerArm: the player's setting, the bowler's own, the nets' spell). A left-armer is the world mirror image of a
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
/// right-armer from the same mark lets go (RunUpEnd), on the clip's own strides from a mark set back
/// for the extra ground (MirrorMark), turning smoothly onto his line.
/// After the release the clip moves him, its turns mirrored.
/// </summary>
public partial class AnimatedBowler
{
    /// The bowling controller's bool that mirrors every state (BowlerMirrorSetup puts it there).
    public const string MirrorParam = "Mirror";

    /// He bowls left-arm this ball.
    public bool LeftArm { get; private set; }

    private AFInfo baseBowlerInfo;
    /// The nets: this spell's arm, swapped each time a pace bowler comes on (BowlerArm).
    private bool spellLeft;
    private int lastBowlerIndex = -1;

    /// A bowler has come on (HUD.SetBowler, through UpdateInfo): in the nets a new pace spell is
    /// the other arm from the last, so Change bowler brings on a left-armer every other time.
    private void BowlerCameOn(int index)
    {
        bool changed = lastBowlerIndex >= 0 && index != lastBowlerIndex;
        lastBowlerIndex = index;
        Bowler b = HUD.Instance != null && HUD.Instance.Bowlers != null && index >= 0 && index < HUD.Instance.Bowlers.Count
            ? HUD.Instance.Bowlers[index] : null;
        if (changed && b != null)
            spellLeft = !spellLeft;
        // The non-striker's side for this bowler (EndPose): away from his run-up.
        if (b != null)
            EndPose.SetBowlerArm(WillBowlLeftArm(b));
    }
    private Transform rightHand, leftHand;
    private Vector3 handLocal;

    /// Right before the run-up: this ball's arm - the bowler's own in a match, drawn per ball in
    /// the nets - and his mark, action and hand to go with it.
    private void ChooseArm()
    {
        // A career's sides are its own: the player's choice is for quick play and the free nets.
        SetArm(WillBowlLeftArm(HUD.Instance != null ? HUD.Instance.CurrentBowler : null));
    }

    /// The arm he will bowl his next ball with, without choosing it (the next-ball card's "left-arm
    /// over"): what ChooseArm will decide, for the bowler on now.
    public bool WillBowlLeftArm(Bowler b)
    {
        if (b == null)
            return false;
        bool nets = gameObject.scene.name == NetsSession.NetsScene;
        bool career = CareerMatchSession.Active || NetsSession.Career;
        BowlerArm.eSetting setting = career ? BowlerArm.eSetting.Both : BowlerArm.Setting;
        HUD hud = HUD.Instance;
        int index = hud != null && hud.Bowlers != null ? hud.Bowlers.IndexOf(b) : -1;
        string first = hud != null && hud.Bowlers != null && hud.Bowlers.Count > 0 ? hud.Bowlers[0].Name : "";
        return BowlerArm.Force ?? BowlerArm.Decide(setting, nets, spellLeft, BowlerArm.IsLeftArm(index, first));
    }

    public void SetArm(bool left)
    {
        LeftArm = left;
        EndPose.SetBowlerArm(left);
        if (baseBowlerInfo == null)
            baseBowlerInfo = currentBowlerInfo;
        if (baseBowlerInfo != null)
            currentBowlerInfo = left ? MirrorMark(baseBowlerInfo) : baseBowlerInfo;
        ApplyMirror();
        MoveHand(left);
    }


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

    /// Where a right-armer's root is as he lets go, by his mark's width (traced 2026-11-06: the
    /// quick's mark at z 2.32 lets go at x -9.18, z 0.90). A left-armer is steered to its mirror,
    /// never nearer the stumps than LeftClear: the medium-pacer's let go over the stumps
    /// ("he's basically on the stumps"), so his is held out wider than his mark gives.
    public const float RunUpIn = 1.42f, RunUpEndX = -9.18f, LeftClear = 0.75f;
    /// The mirrored clips cover more ground than the right-arm ones, by about the same distance
    /// whatever the run (traced 2026-11-06, the right-armer's run to his release against the
    /// left-armer's: the quick 14.3 m + 1.6, the medium-pacer 11.0 + 1.6, the spinner 4.5 + 1.9).
    /// Scaling the stride down to fit made his feet slide ("not as smooth as the righty"); his mark
    /// is set back by it instead (MirrorMark), so every stride is the clip's own.
    /// Each 0.1 m more, so his front toe lands where a right-armer's does (traced: 8 cm further over).
    public const float ExtraGroundPace = 1.7f, ExtraGroundSpin = 2.0f, ShortRun = 6f;
    /// How fast he turns onto his line (deg/s): smooth, not snapped each frame.
    private const float SteerTurnRate = 120f;

    public static Vector3 RunUpEnd(Vector3 mark) =>
        new Vector3(RunUpEndX, 0f, Mathf.Sign(mark.z) * Mathf.Max(LeftClear, Mathf.Abs(mark.z) - RunUpIn));

    /// A left-armer's mark: the right-armer's mirrored across the pitch, and set back by the extra
    /// ground the mirrored run-up covers, so he arrives at the crease on the clip's own strides.
    private static AFInfo MirrorMark(AFInfo a)
    {
        if (a == null)
            return null;
        AFInfo m = a.MirroredCopy();
        float run = RunUpEndX - a.startPos.x;
        m.startPos.x -= run < ShortRun ? ExtraGroundSpin : ExtraGroundPace;
        return m;
    }

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
        // The clip's own stride along his facing (its sideways sway cancels out over a run).
        float stride = Mathf.Max(0f, (Quaternion.Inverse(transform.rotation) * step).z);
        Vector3 end = RunUpEnd(currentBowlerInfo.startPos);
        Vector3 to = new Vector3(end.x - transform.position.x, 0f, end.z - transform.position.z);
        Vector3 dir = to.x > 0.05f ? to.normalized : Vector3.right;
        transform.position += dir * stride + Vector3.up * step.y;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up),
                                                      SteerTurnRate * Time.deltaTime);
    }
}
