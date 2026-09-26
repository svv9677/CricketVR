using UnityEngine;

/// <summary>
/// The wicketkeeper. He does not move; anything that passes within reach of where he stands is
/// taken and held.
///   * An edge (the ball was hit): taken as a catch if it has not bounced, otherwise fielded -
///     HUD scores it from Ball.bounced like any other fielder.
///   * A ball the batter missed: gathered, and the delivery ends as missed. Wides are already
///     decided at the stumps (WideCollider), which the ball passes before it reaches him.
/// Lives on Main.theKeeper, whose position HUD sets per bowler type (up to the stumps for spin).
/// </summary>
public class KeeperCatcher : MonoBehaviour
{
    public const float Reach = 1.2f;
    public const float ReachHeight = 2.0f;
    /// Glove height when holding the ball.
    private const float HoldHeight = 0.9f;
    private const float WideBoxesEndX = 10.85f;

    private Vector3 previousBall;
    private bool holding;

    private void Start()
    {
        Main.Instance.onGameStateChanged += HandleGameState;

        // The placeholder body used to be solid, so a missed ball bounced back off it up the pitch.
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (!c.isTrigger)
                c.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        if (Main.Instance.gameState == eGameState.InGame_SelectDelivery)
            holding = false;
        previousBall = Main.Instance.theBallRigidBody.position;
    }

    private void FixedUpdate()
    {
        Main inst = Main.Instance;
        Rigidbody ball = inst.theBallRigidBody;
        Vector3 now = ball.position;
        eGameState state = inst.gameState;
        bool live = !ball.isKinematic && !holding &&
                    (state == eGameState.InGame_BallHitLoop || state == eGameState.InGame_DeliverBallLoop ||
                     state == eGameState.InGame_BallMissed || state == eGameState.InGame_BallMissedLoop);
        // A delivery must clear the wide boxes at the stumps (x 9.8-10.8) before he takes it, or a
        // keeper standing up to the spinners would stop it inside them and no wide could be called.
        if (state == eGameState.InGame_DeliverBallLoop && now.x < WideBoxesEndX)
            live = false;
        if (live && AnimatedFielderManagement.WithinReach(previousBall, now, transform.position, Reach, ReachHeight))
        {
            holding = true;
            if (state == eGameState.InGame_BallHitLoop)
            {
                AnimatedFielderManagement.Take(null, name);
            }
            else
            {
                ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                ball.linearVelocity = Vector3.zero;
                ball.angularVelocity = Vector3.zero;
                ball.isKinematic = true;
                ball.interpolation = RigidbodyInterpolation.None;
                if (state == eGameState.InGame_DeliverBallLoop)
                    inst.gameState = eGameState.InGame_BallMissed;
            }
        }
        previousBall = now;
    }

    private void LateUpdate()
    {
        if (holding)
            Main.Instance.theBall.transform.position = new Vector3(transform.position.x, HoldHeight, transform.position.z);
    }
}
