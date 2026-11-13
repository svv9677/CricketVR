using UnityEngine;

/// <summary>
/// Bowling actions captured from video (Tools/mocap, imported by MocapBowlerImport) in place of a
/// pace or medium bowler's hand-made one, while they are being tried out. Off unless switched on
/// (the editor's CricketVR/Bowling menu, or <see cref="Enabled"/> from code); spinners keep Lyon.
/// When several are in the bowling controller, the first in <see cref="Captured"/> (by priority) is used.
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
    /// The deliveries in the project: one per clip in Resources/Animations/Bowling/Mocap, read from the
    /// clip's own sidecar (<c>&lt;Name&gt;.mocap.json</c>), so a clip exported from Blender
    /// (Tools/mocap/handanim) is picked up without a code change. Highest <c>priority</c> first: hand
    /// animation (10) over the captured clips (0). Mirrored, every clip covers exactly its own ground,
    /// so a left-armer's extra set-back (AnimatedBowlerArm.MirrorMark) is left as it is.
    /// </summary>
    public static Delivery[] Captured => captured ??= Load();
    private static Delivery[] captured;

    [System.Serializable]
    private class Sidecar
    {
        public int action;
        public float mark_forward, mark_wide, run_pace = 1f;
        public int priority;
    }

    private static Delivery[] Load()
    {
        var list = new System.Collections.Generic.List<(int priority, Delivery d)>();
        foreach (TextAsset t in Resources.LoadAll<TextAsset>("Animations/Bowling/Mocap"))
        {
            if (!t.name.EndsWith(".mocap"))
                continue;
            Sidecar s;
            try { s = JsonUtility.FromJson<Sidecar>(t.text); }
            catch (System.Exception) { continue; }
            if (s == null || s.action <= 0)
                continue;
            list.Add((s.priority, new Delivery
            {
                state = t.name.Substring(0, t.name.Length - ".mocap".Length), action = s.action,
                markForward = s.mark_forward, markWide = s.mark_wide, runPace = s.run_pace > 0f ? s.run_pace : 1f,
            }));
        }
        list.Sort((x, y) => y.priority != x.priority ? y.priority.CompareTo(x.priority) : string.CompareOrdinal(x.d.state, y.d.state));
        return list.ConvertAll(x => x.d).ToArray();
    }

    /// Read the sidecars again (the editor, after an export from Blender).
    public static void Reload() => captured = null;

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
