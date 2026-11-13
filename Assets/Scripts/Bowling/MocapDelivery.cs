using UnityEngine;

/// <summary>
/// Bowling actions captured from video (Tools/mocap, imported by MocapBowlerImport) in place of a
/// pace or medium bowler's hand-made one, while they are being tried out. Off unless switched on
/// (the editor's CricketVR/Bowling menu, or <see cref="Enabled"/> from code); spinners keep Lyon.
/// When several are in the bowling controller, the first in <see cref="Captured"/> is used.
/// </summary>
public static class MocapDelivery
{
    /// <summary>
    /// A captured delivery: its state in the bowling controller (its clip's name), the controller's
    /// Action value that leads to it (Steyn 2, Lyon 3, Bumrah 4), and how far his mark moves for it so
    /// his front foot lands where Steyn's does and the ball leaves from about the same place.
    /// </summary>
    public struct Delivery
    {
        public string state;
        public int action;
        public float markForward, markWide;
        /// How much faster the run-up plays into it (the Run state's RunPace): the game's run is
        /// 3.7 m/s, a captured action may start much faster (Anderson 5.1-6.9 m/s), and the jump
        /// at the handover is a lurch. The run covers the same ground per stride, only sooner.
        public float runPace;
    }

    /// The bowling controller's multiplier on the Run state's speed (1: as it always was).
    public const string RunPaceParam = "RunPace";

    /// <summary>
    /// Marks measured with CricketVR/Bowling/Measure Delivery Clips (DeliveryClipProbe) and in play
    /// (Nets): see each entry. Re-measure when a clip changes. Mirrored, every clip covers exactly its
    /// own ground, so a left-armer's extra set-back (AnimatedBowlerArm.MirrorMark) is left as it is.
    /// </summary>
    public static readonly Delivery[] Captured =
    {
        // Two-view fusion of fan footage, hand-corrected and smoothed (Tools/mocap, 2026-11-13).
        // Run into Steyn Cleaned's run-in and load-up, then this clip from back-foot landing (its sidecar).
        // In play (Nets, 2026-11-13) with the mark 1.5 m on his front toe landed at x -6.90 to -7.03 at the
        // release: 2.45 m past where it should be (-9.40, just behind the popping crease at -9.14).
        new Delivery { state = "MocapAnderson", action = 6, markForward = -0.95f, markWide = 0f, runPace = 1f },
        // From IMG_9676. Front toe 2.22 m on at the release and hand 0.14 m right, against Steyn's
        // 3.75 and 0.36: 1.53 m nearer the stumps, 0.22 m wider.
        new Delivery { state = "MocapPace", action = 5, markForward = 1.53f, markWide = 0.22f, runPace = 1f },
    };

    /// The first captured delivery's Action (kept for MocapBowlerImport's default).
    public const int Action = 5;
    private const string Pref = "MocapDelivery";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Pref, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(Pref, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// The captured delivery this bowler would use, if any (switched on, a pace (2) or medium (4)
    /// action, and the controller has the state).
    public static bool TryChoose(Animator animator, int own, out Delivery chosen)
    {
        chosen = default;
        if (!Enabled || (own != 2 && own != 4) || animator == null || animator.runtimeAnimatorController == null)
            return false;
        foreach (Delivery d in Captured)
        {
            if (animator.HasState(0, Animator.StringToHash(d.state)))
            {
                chosen = d;
                return true;
            }
        }
        return false;
    }

    /// The Action to run in with: the captured delivery's when there is one, otherwise his own.
    public static int ActionFor(Animator animator, int own) =>
        TryChoose(animator, own, out Delivery d) ? d.action : own;

    /// The run-up's pace for this ball (RunPaceParam), set with the Action: the captured delivery's,
    /// or 1. Nothing happens if the controller has no such parameter.
    public static void SetRunPace(Animator animator, int own)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        float pace = TryChoose(animator, own, out Delivery d) && d.runPace > 0f ? d.runPace : 1f;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == RunPaceParam && p.type == AnimatorControllerParameterType.Float)
                animator.SetFloat(RunPaceParam, pace);
    }

    /// His mark for this ball: moved for the captured action's own ground when he will bowl it.
    public static AFInfo Mark(AFInfo mark, bool leftArm, Animator animator)
    {
        if (mark == null || !TryChoose(animator, mark.myIndex, out Delivery d))
            return mark;
        Quaternion facing = Quaternion.Euler(mark.startRot);
        return mark.ShiftedCopy(facing * new Vector3(leftArm ? -d.markWide : d.markWide, 0f, d.markForward));
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("CricketVR/Bowling/Use Mocap Delivery For Pace")]
    private static void Toggle() => Enabled = !Enabled;

    [UnityEditor.MenuItem("CricketVR/Bowling/Use Mocap Delivery For Pace", true)]
    private static bool ToggleCheck()
    {
        UnityEditor.Menu.SetChecked("CricketVR/Bowling/Use Mocap Delivery For Pace", Enabled);
        return true;
    }
#endif
}
