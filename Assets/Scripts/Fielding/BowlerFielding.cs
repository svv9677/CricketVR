using UnityEngine;

/// <summary>
/// The bowler fields after he has bowled: once the ball is hit he joins the fielding plan
/// (AnimatedFielderManagement) like any fielder, and is sent after it when he is the man best
/// placed - a straight drive back past him, a ball popped up the pitch.
///
/// He becomes an AnimatedFielder for the rest of the ball: the fielders' animation controller
/// in place of his bowling one (it has the run the fielder code drives; his own "Jog" leads
/// into a run-up and a delivery), root motion off (the fielder code moves him), and the same
/// full-body reach and take. On the next delivery he is handed back to his bowling
/// (AnimatedBowler), which puts him on his mark as before.
///
/// His HumanoidReach is added at the start, while he stands at his mark, and left switched
/// off until needed: it measures the body when it is added, and mid follow-through - bent over
/// the crease - it would measure him as a shorter man.
/// </summary>
public class BowlerFielding : MonoBehaviour
{
    private AnimatedBowler bowler;
    private Animator animator;
    private RuntimeAnimatorController bowlingController, fieldingController;
    private HumanoidReach reach;
    private AnimatedFielderManagement field;
    private AnimatedFielder fielder;

    public bool Fielding => fielder != null;

    private void Start()
    {
        bowler = GetComponent<AnimatedBowler>();
        animator = GetComponent<Animator>();
        field = FindFirstObjectByType<AnimatedFielderManagement>();
        fieldingController = Resources.Load<RuntimeAnimatorController>("Animations/Fielder Controller");
        // The nets have no field to join.
        if (bowler == null || animator == null || !animator.isHuman || field == null || fieldingController == null ||
            Main.Instance == null)
        {
            enabled = false;
            return;
        }
        bowlingController = animator.runtimeAnimatorController;
        reach = GetComponent<HumanoidReach>();
        if (reach == null)
            reach = gameObject.AddComponent<HumanoidReach>();
        reach.enabled = false;
        Main.Instance.onGameStateChanged += HandleGameState;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        eGameState state = Main.Instance.gameState;
        // A hit: he has let the ball go by now, so he is free to field it.
        if (state == eGameState.InGame_BallHit && !Fielding)
            Begin();
        // The next ball: back to bowling. Not before - he may be holding the ball, like any fielder
        // until then, and let go early it would hang in the air where he dropped it.
        else if (state == eGameState.InGame_SelectDelivery && Fielding)
            StopFielding();
    }

    private void Begin()
    {
        bowler.BeginFielding();
        animator.runtimeAnimatorController = fieldingController;
        animator.applyRootMotion = false;
        reach.enabled = true;
        reach.ResetPose();
        fielder = gameObject.AddComponent<AnimatedFielder>();
        fielder.InitAsBowler(field, animator);
        field.Join(fielder);
    }

    /// Back to bowling (the next delivery; the fielding film's reset between shots).
    public void StopFielding()
    {
        if (!Fielding)
            return;
        field.Leave(fielder);
        // He may be holding the ball: let go of it; the bowling code takes it for the next ball.
        Destroy(fielder);
        fielder = null;
        reach.weight = 0f;
        reach.ResetPose();
        reach.enabled = false;
        bowler.EndFielding(bowlingController);
    }
}
