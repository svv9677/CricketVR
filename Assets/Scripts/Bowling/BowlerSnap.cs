using UnityEngine;

/// <summary>
/// A pace or medium bowler's action, quicker, and quickest at the end: the arm snaps over into
/// the release (2026-11-09: "the arm feels slow and then the ball comes out much quicker than I
/// expect ... like a curve, the speed, just a slight curve towards the end, so that the arm comes
/// down quick relative to the ball speed. For spinners, it looks good though").
///
/// The whole delivery clip plays Base times as fast as before; from the start of the final arm
/// swing (the hand dropping behind him, sampled from the clips) to the release it speeds up on an
/// ease-in curve to Base x (1 + Snap), then eases back over the first of the follow-through. The
/// release event, the ball's speed and where he lets it go are as before: only when.
/// The spinner's action (Lyon) is left as it was.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Animator))]
public class BowlerSnap : MonoBehaviour
{
    /// The whole action this much quicker; at the release, this much more on top. Halfway back from
    /// the first cut (1.1 and 0.55: 1.7x at the release), which was "a little bit too much ... in
    /// between what it was before and now, that would be perfect" (2026-11-09): 1.05 and 0.29, so
    /// 1.35x at the release, halfway between the old 1.0 and 1.7.
    public const float Base = 1.05f, Snap = 0.29f;
    /// After the release, back to Base over this much of the clip (normalised).
    private const float EaseBack = 0.06f;

    /// A delivery clip's final arm swing: where it starts and the release (normalised clip time),
    /// sampled from the hand's path (the hand at its lowest, behind the hip, then over the top).
    public struct Swing
    {
        public string state;
        public float armFrom, release;
    }

    public static readonly Swing[] Swings =
    {
        new Swing { state = "Steyn", armFrom = 0.45f, release = 5.46f / 8.29f },      // the quick
        new Swing { state = "Bumrah", armFrom = 0.43f, release = 4.30f / 6.90f },     // the medium-pacer
    };

    private Animator animator;
    private AnimatedBowler bowler;
    private bool driving;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        bowler = GetComponent<AnimatedBowler>();
    }

    /// The speed multiplier at normalised time `t` of a delivery with this swing.
    public static float SpeedAt(Swing s, float t)
    {
        if (t <= s.armFrom)
            return Base;
        if (t <= s.release)
        {
            float u = (t - s.armFrom) / (s.release - s.armFrom);
            return Base * (1f + Snap * u * u * (3f - 2f * u) * u);      // eased in, steepest at the end
        }
        float back = Mathf.Clamp01((t - s.release) / EaseBack);
        return Base * (1f + Snap * (1f - back * back * (3f - 2f * back)));
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null || (bowler != null && bowler.Fielding))
        {
            driving = false;            // the fielding controller sets its own speed
            return;
        }
        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        foreach (Swing s in Swings)
        {
            if (!st.IsName(s.state))
                continue;
            animator.speed = SpeedAt(s, st.normalizedTime % 1f);
            driving = true;
            return;
        }
        if (driving)
            animator.speed = 1f;
        driving = false;
    }
}
