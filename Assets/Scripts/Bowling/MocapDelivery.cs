using UnityEngine;

/// <summary>
/// Bowling actions captured from video (Tools/mocap, imported by MocapBowlerImport) in place of a
/// pace or medium bowler's hand-made one, while they are being tried out. Off unless switched on
/// (the editor's CricketVR/Bowling menu, or <see cref="Enabled"/> from code); spinners keep Lyon.
/// </summary>
public static class MocapDelivery
{
    /// The controller's Action value for the captured delivery (Steyn 2, Lyon 3, Bumrah 4).
    public const int Action = 5;
    /// The captured delivery's state in the bowling controller (its clip's name).
    public const string StateName = "MocapPace";
    private const string Pref = "MocapDelivery";

    /// <summary>
    /// How far his mark moves for the captured action, so his front foot lands where Steyn's does
    /// and the ball leaves from about the same place. Measured 2026-11-13 with
    /// CricketVR/Bowling/Measure Delivery Clips (DeliveryClipProbe), from each clip's start at
    /// the release: Steyn's front toe 3.75 m on and his hand 0.36 m to his right; MocapPace's
    /// 2.22 m and 0.14 m (the joint-limited, leg-swap-fixed rebuild; earlier cuts measured 2.40 /
    /// 0.11 and 2.46 / 0.14). Hence 1.53 m nearer the stumps and 0.22 m wider. Mirrored, every clip
    /// covers exactly its own ground (Steyn, Bumrah and MocapPace alike), so a left-armer's extra
    /// set-back (AnimatedBowlerArm.MirrorMark) comes from the steering, not the clip, and is left
    /// as it is - not yet checked in play for the captured action. Re-measure when the clip changes.
    /// </summary>
    public const float MarkForward = 1.53f, MarkWide = 0.22f;

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Pref, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(Pref, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// The Action to run in with: the captured delivery for a pace (2) or medium (4) action when it
    /// is switched on and the controller has it, otherwise the bowler's own.
    public static int ActionFor(Animator animator, int own)
    {
        if (!Enabled || (own != 2 && own != 4) || animator == null || animator.runtimeAnimatorController == null)
            return own;
        return animator.HasState(0, Animator.StringToHash(StateName)) ? Action : own;
    }

    /// His mark for this ball: moved for the captured action's own ground when he will bowl it.
    public static AFInfo Mark(AFInfo mark, bool leftArm, Animator animator)
    {
        if (mark == null || ActionFor(animator, mark.myIndex) != Action)
            return mark;
        Quaternion facing = Quaternion.Euler(mark.startRot);
        return mark.ShiftedCopy(facing * new Vector3(leftArm ? -MarkWide : MarkWide, 0f, MarkForward));
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
