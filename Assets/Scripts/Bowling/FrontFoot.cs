using UnityEngine;

/// <summary>
/// The bowler's front foot: on a ball he oversteps (NoBalls.Overstep) his whole run-up starts
/// NoBalls.OverstepBy further on, from the moment he sets off - so the foot that lands at the
/// crease really is over the line, where the umpire (and the player) can see it. Runs after
/// AnimatedBowler has put him on his mark; root motion carries the shift through the run-up.
/// LastFrontFootX is where his front foot was at the release, for checks.
/// </summary>
[DefaultExecutionOrder(5)]
public class FrontFoot : MonoBehaviour
{
    private AnimatedBowler bowler;
    private Animator animator;
    private bool shifted, wasCarrying;
    private Transform leftFoot, leftToes;

    /// World X of the front (left) foot's heel and toe at the last release. A left-armer is this
    /// bowler mirrored (BowlerArm), so the same bone is then his right foot - his front foot too.
    public float LastHeelX { get; private set; } = float.NaN;
    public float LastToeX { get; private set; } = float.NaN;

    private void Awake()
    {
        bowler = GetComponent<AnimatedBowler>();
        animator = GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
        }
    }

    private void LateUpdate()
    {
        if (bowler == null || animator == null)
            return;
        bool carrying = bowler.CarryingBall;
        if (!carrying)
        {
            if (wasCarrying && leftFoot != null)
            {
                LastHeelX = leftFoot.position.x;
                LastToeX = leftToes != null ? leftToes.position.x : leftFoot.position.x;
            }
            shifted = false;
        }
        else if (NoBalls.Overstep && !shifted && animator.GetCurrentAnimatorStateInfo(0).IsName("Jog"))
        {
            Vector3 travel = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            transform.position += travel * NoBalls.OverstepBy;
            shifted = true;
        }
        wasCarrying = carrying;
    }
}
