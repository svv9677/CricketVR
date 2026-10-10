using UnityEngine;

/// <summary>
/// A take near the rope is only complete once the fielder has the ball under control wholly
/// inside the field (Law 19, 33): a fielder who takes it and then carries over the boundary - a
/// dive that lands beyond it, a step back onto the rope - has not fielded it; it is a boundary,
/// six if it had not bounced (a "catch" taken over the rope), four if it had.
///
/// AnimatedFielderManagement.Take hands a take within `NearRope` of the boundary here instead of
/// calling it fielded on the spot. From then until the fielder has settled - at least `Settle`
/// seconds, on his feet (not diving, down or getting up), at most `GiveUp` - every part of him
/// (feet, hands, head, hips) is watched: any of it over the rope makes it a boundary; otherwise
/// it is fielded (a catch, if it had not bounced), as before. Takes in the middle of the field
/// are called at once, as they always were.
/// </summary>
public class CatchJudge : MonoBehaviour
{
    /// Takes closer than this to the rope wait for the fielder to settle.
    private const float NearRope = 6f;
    private const float Settle = 0.6f, GiveUp = 2.5f;

    private static CatchJudge instance;
    public static float Boundary { get; set; } = 56f;

    private static readonly HumanBodyBones[] Extremities =
    {
        HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.RightHand,
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
    };

    private AnimatedFielder fielder;
    private FielderRagdoll ragdoll;
    private readonly Transform[] parts = new Transform[Extremities.Length];
    private string fielderName;
    private float since;

    private void Awake() => instance = this;

    /// True when the take is near enough the rope to be judged here (the caller then does not
    /// call it fielded).
    public static bool Begin(AnimatedFielder fielder, string name)
    {
        if (instance == null || fielder == null)
            return false;
        Vector3 p = fielder.transform.position;
        if (new Vector2(p.x, p.z).magnitude < Boundary - NearRope)
            return false;
        instance.Watch(fielder, name);
        return true;
    }

    private void Watch(AnimatedFielder who, string name)
    {
        fielder = who;
        fielderName = name;
        since = Time.time;
        ragdoll = who.GetComponent<FielderRagdoll>();
        Animator animator = who.GetComponent<Animator>();
        for (int i = 0; i < Extremities.Length; i++)
            parts[i] = animator != null && animator.isHuman ? animator.GetBoneTransform(Extremities[i]) : null;
    }

    private void LateUpdate()
    {
        if (fielder == null)
            return;
        Main inst = Main.Instance;
        if (inst == null || inst.gameState != eGameState.InGame_BallHitLoop)
        {
            fielder = null;     // the ball was decided some other way (or the next ball began)
            return;
        }
        if (OverTheRope())
        {
            fielder = null;
            // Carried over the rope with the ball: a boundary. HUD scores six if it never bounced.
            inst.gameState = eGameState.InGame_BallPastBoundary;
            return;
        }
        float held = Time.time - since;
        bool settled = held >= Settle && (ragdoll == null || !ragdoll.Busy);
        if (settled || held >= GiveUp)
        {
            fielder = null;
            inst.currentFielderName = fielderName;
            inst.gameState = eGameState.InGame_BallFielded;
        }
    }

    private bool OverTheRope()
    {
        if (Over(fielder.transform.position))
            return true;
        foreach (Transform part in parts)
            if (part != null && Over(part.position))
                return true;
        return false;
    }

    private static bool Over(Vector3 p) => new Vector2(p.x, p.z).magnitude > Boundary;
}
