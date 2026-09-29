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
        {
            EndCatch();
            Begin();
        }
        // The next ball: back to bowling. Not before - he may be holding the ball, like any fielder
        // until then, and let go early it would hang in the air where he dropped it.
        else if (state == eGameState.InGame_SelectDelivery)
        {
            EndCatch();
            if (Fielding)
                StopFielding();
        }
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

    // ---- Taking the throw back while he walks to his mark (ThrowBack) ------------------------------

    /// Hands up to it from this long before it arrives; holding it in both hands this long before
    /// it goes to his bowling hand; the arms back to the walk over the last part.
    private const float CatchLead = 0.8f, CatchFull = 0.25f, HoldTime = 0.45f, LetGo = 0.3f;
    private bool expecting, catchHolding;
    private float arriveAt, heldAt;
    private Vector3 catchPoint, catchFrom;

    /// A throw is coming to him at `point` from `from`, arriving at `arrival` (Time.time).
    public void ExpectThrow(Vector3 point, Vector3 from, float arrival)
    {
        if (Fielding || reach == null)
            return;
        expecting = true;
        catchHolding = false;
        catchPoint = point;
        catchFrom = from;
        arriveAt = arrival;
        if (!reach.enabled)
        {
            reach.enabled = true;
            reach.ResetPose();
        }
    }

    /// He has it: both hands a moment, then into his bowling hand.
    public void TookThrow()
    {
        expecting = false;
        catchHolding = true;
        heldAt = Time.time;
    }

    /// Where the ball is while he takes it in both hands (then ThrowBack puts it in his hand).
    public bool HoldingInBothHands(out Vector3 at)
    {
        at = reach != null && reach.Ready ? reach.HoldPoint : transform.position;
        return catchHolding && Time.time - heldAt < HoldTime && reach != null && reach.Ready;
    }

    private void Update()
    {
        if (Fielding || reach == null || (!expecting && !catchHolding))
            return;
        Vector3 toThrower = Vector3.ProjectOnPlane(catchFrom - transform.position, Vector3.up);
        reach.look = true;
        reach.lookTarget = Main.Instance.theBall.transform.position;
        reach.handGap = 0.1f;
        reach.leftHandShare = 1f;
        reach.minCrouch = 0f;
        if (expecting)
        {
            reach.handTarget = catchPoint;
            reach.weight = Mathf.InverseLerp(CatchLead, CatchFull, arriveAt - Time.time);
            reach.palmFacing = toThrower.sqrMagnitude > 1e-4f ? toThrower.normalized : transform.forward;
            return;
        }
        // Holding: in to his chest, then the arms fade back to the walk.
        float t = Time.time - heldAt;
        reach.palmFacing = Vector3.zero;
        reach.handTarget = transform.position + Vector3.up * 1.15f + transform.forward * 0.3f;
        reach.weight = t < HoldTime ? 1f : Mathf.Clamp01(1f - (t - HoldTime) / LetGo);
        if (t >= HoldTime + LetGo)
        {
            catchHolding = false;
            reach.weight = 0f;
            reach.ResetPose();
            reach.enabled = false;
        }
    }

    private void EndCatch()
    {
        expecting = catchHolding = false;
        if (reach != null && !Fielding)
        {
            reach.weight = 0f;
            reach.ResetPose();
            reach.enabled = false;
        }
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
