using UnityEngine;

/// <summary>
/// Bowling actions made in Blender or captured from video (Tools/mocap, imported by
/// MocapBowlerImport), bowled alongside the hand-made ones. Each quick and medium-pacer gets one
/// action for the match, drawn at random when the side is picked (Bowler.ActionRoll): his own
/// (Steyn for a quick, Bumrah for a medium-pacer) or any clip here that the bowling controller has
/// (2026-11-17: "randomize the bowler for each type of bowling"). Spinners keep Lyon.
/// Until 2026-11-17 a clip replaced every quick's and medium-pacer's action behind an editor
/// switch ("Use Mocap Delivery For Pace"), while it was being tried out.
///
/// One clip serves a quick and a medium-pacer alike (2026-11-17: "he could be both a fast or medium
/// pace"). The medium-pacer runs in from his own, shorter mark, and the clip's sidecar can give him
/// his own mark trims, a gentler run-up and a slower action (<see cref="Delivery.medium"/>).
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
        /// A left-armer's mark set back this much more than a right-armer's (metres): the mirrored
        /// clip, steered onto his line (AnimatedBowlerArm), does not always cover the right-armer's ground.
        public float leftBack;
        /// How much faster the run-up plays into it (the Run state's RunPace): the game's run is
        /// 3.7 m/s, a captured action may start much faster (Anderson 5.1-6.9 m/s), and the jump
        /// at the handover is a lurch. The run covers the same ground per stride, only sooner.
        public float runPace;
        /// The action's speed against the game's own for this ball (the controller's Time
        /// parameter, which also plays his follow-through): 1 for a quick.
        public float actionSpeed;
        /// The same clip bowled by a medium-pacer (Action 4): his own mark trims, run-up pace and
        /// action speed, from the sidecar's <c>medium_*</c> fields. Null when the clip has none:
        /// a medium-pacer then bowls it with the quick's values (as before 2026-11-17).
        public Variant medium;
    }

    /// A medium-pacer's mark trims, run-up and action for a clip a quick bowls too.
    public class Variant
    {
        public float markForward, markWide, leftBack, runPace, actionSpeed;
    }

    /// The bowling controller's multiplier on the Run state's speed (1: as it always was).
    public const string RunPaceParam = "RunPace";

    /// <summary>
    /// The deliveries in the project: one per clip in Resources/Animations/Bowling/Mocap, read from the
    /// clip's own sidecar (<c>&lt;Name&gt;.mocap.json</c>), so a clip exported from Blender
    /// (Tools/mocap/handanim) is picked up without a code change. Highest <c>priority</c> first: hand
    /// animation (10) over the captured clips (0).
    ///
    /// His mark is the export's (<c>mark_forward</c>, <c>mark_wide</c>: the clip's front toe against
    /// Steyn's, worked out in Blender) plus what play showed it still needed (<c>trim_forward</c>,
    /// <c>trim_wide</c>, <c>left_back</c>, set by hand after watching where his front foot lands;
    /// an export keeps them). The trims cover what Blender cannot see: the Animator's own transition
    /// into the clip, and the left-armer's steered run-up. HandAnderson, measured 2026-11-15 in the
    /// Nets against Steyn's front toe: right-arm 0.23 m short and 0.81 m wide; left-arm 1.66 m over.
    /// </summary>
    public static Delivery[] Captured => captured ??= Load();
    private static Delivery[] captured;

    [System.Serializable]
    private class Sidecar
    {
        public int action;
        public float mark_forward, mark_wide, run_pace = 1f;
        public float trim_forward, trim_wide, left_back;
        public int priority;
        // A medium-pacer bowling the clip (Delivery.medium); medium_run_pace 0 means none.
        public float medium_run_pace, medium_action_speed = 1f;
        public float medium_trim_forward, medium_trim_wide, medium_left_back;
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
                markForward = s.mark_forward + s.trim_forward, markWide = s.mark_wide + s.trim_wide,
                leftBack = s.left_back, runPace = s.run_pace > 0f ? s.run_pace : 1f, actionSpeed = 1f,
                medium = s.medium_run_pace > 0f ? new Variant
                {
                    markForward = s.mark_forward + s.medium_trim_forward, markWide = s.mark_wide + s.medium_trim_wide,
                    leftBack = s.medium_left_back, runPace = s.medium_run_pace,
                    actionSpeed = s.medium_action_speed > 0f ? s.medium_action_speed : 1f,
                } : null,
            }));
        }
        list.Sort((x, y) => y.priority != x.priority ? y.priority.CompareTo(x.priority) : string.CompareOrdinal(x.d.state, y.d.state));
        return list.ConvertAll(x => x.d).ToArray();
    }

    /// Read the sidecars again (the editor, after an export from Blender).
    public static void Reload() => captured = null;

    /// The first captured delivery's Action (kept for MocapBowlerImport's default).
    public const int Action = 5;

    /// For trying an action out (the editor, a probe): true, every quick and medium-pacer bowls a
    /// clip from here; false, every one bowls his own; null (the game), each his own draw. Not saved.
    public static bool? Force;

    /// The pace bowler's own Action (Steyn) and the medium-pacer's (Bumrah): a captured delivery
    /// stands in for either. Spinners (Lyon, 3) keep their own.
    public const int PaceAction = 2, MediumAction = 4;

    /// The clip this bowler bowls, if not his own action: a pace (2) or medium (4) bowler whose
    /// draw (Bowler.ActionRoll) fell on one of the clips the controller has. A medium-pacer's comes
    /// with the clip's medium values in place of the quick's (its mark, run-up and action speed),
    /// when it has them. No bowler (none on yet): his own action.
    public static bool TryChoose(Animator animator, int own, Bowler bowler, out Delivery chosen)
    {
        chosen = default;
        if (Force == false || (own != PaceAction && own != MediumAction) || animator == null ||
            animator.runtimeAnimatorController == null || (bowler == null && Force != true))
            return false;
        var usable = new System.Collections.Generic.List<Delivery>();
        foreach (Delivery d in Captured)
            if (animator.HasState(0, Animator.StringToHash(d.state)))
                usable.Add(d);
        int pick = Pick(Force == true ? 0 : 1, usable.Count, bowler != null ? bowler.ActionRoll : 0);
        if (pick < 0)
            return false;
        chosen = own == MediumAction ? AsMedium(usable[pick]) : usable[pick];
        return true;
    }

    /// Which of `clips` clips a draw of `roll` falls on, with `own` (0 or 1) more choices for the
    /// bowler's own action ahead of them: -1 for his own. Every choice is equally likely.
    public static int Pick(int own, int clips, int roll)
    {
        if (clips <= 0)
            return -1;
        int i = (roll & int.MaxValue) % (own + clips);
        return i < own ? -1 : i - own;
    }

    /// The delivery as a medium-pacer bowls it (unchanged if the clip has no medium values).
    public static Delivery AsMedium(Delivery d)
    {
        if (d.medium == null)
            return d;
        d.markForward = d.medium.markForward;
        d.markWide = d.medium.markWide;
        d.leftBack = d.medium.leftBack;
        d.runPace = d.medium.runPace;
        d.actionSpeed = d.medium.actionSpeed;
        return d;
    }

    /// What the bowling controller's Time (the action's speed) is multiplied by for this ball: the
    /// captured delivery's action speed, or 1.
    public static float ActionSpeed(Animator animator, int own, Bowler bowler) =>
        TryChoose(animator, own, bowler, out Delivery d) && d.actionSpeed > 0f ? d.actionSpeed : 1f;

    /// The Action to run in with: the captured delivery's when there is one, otherwise his own.
    public static int ActionFor(Animator animator, int own, Bowler bowler) =>
        TryChoose(animator, own, bowler, out Delivery d) ? d.action : own;

    /// The run-up's pace for this ball (RunPaceParam), set with the Action: the captured delivery's,
    /// or 1. Nothing happens if the controller has no such parameter.
    public static void SetRunPace(Animator animator, int own, Bowler bowler)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        float pace = TryChoose(animator, own, bowler, out Delivery d) && d.runPace > 0f ? d.runPace : 1f;
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == RunPaceParam && p.type == AnimatorControllerParameterType.Float)
                animator.SetFloat(RunPaceParam, pace);
    }

    /// His mark for this ball: moved for the captured action's own ground when he will bowl it.
    public static AFInfo Mark(AFInfo mark, bool leftArm, Animator animator, Bowler bowler)
    {
        if (mark == null || !TryChoose(animator, mark.myIndex, bowler, out Delivery d))
            return mark;
        Quaternion facing = Quaternion.Euler(mark.startRot);
        return mark.ShiftedCopy(facing * new Vector3(leftArm ? -d.markWide : d.markWide, 0f,
                                                     leftArm ? d.markForward - d.leftBack : d.markForward));
    }
}
