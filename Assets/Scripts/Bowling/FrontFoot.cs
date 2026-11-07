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
    private Transform leftFoot, leftToes, rightFoot, rightToes;

    /// World X of the front foot's heel and toe at the last release (the left; a left-armer's right).
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
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
        }
    }

    private void LateUpdate()
    {
        if (bowler == null || animator == null)
            return;
        bool carrying = bowler.CarryingBall;
        if (!carrying)
        {
            // A left-armer's front foot is his right (his action is the humanoid's mirror).
            Transform foot = bowler.LeftArm ? rightFoot : leftFoot, toes = bowler.LeftArm ? rightToes : leftToes;
            if (wasCarrying && foot != null)
            {
                LastHeelX = foot.position.x;
                LastToeX = toes != null ? toes.position.x : foot.position.x;
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
