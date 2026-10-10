using UnityEngine;

/// <summary>
/// Two uses of the same trigger. The wide boxes at the stumps (named ...WideCollider): the ball
/// leaving one is a wide. The box behind the keeper: a ball still free when it leaves has beaten
/// him - it is played on for byes (Extras), or wides if it was already a wide.
/// </summary>
public class KeeperCollider : MonoBehaviour
{
    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name != "Ball")
            return;
        Main inst = Main.Instance;
        eGameState state = inst.gameState;

        if (gameObject.name.Contains("WideCollider"))
        {
            if (state == eGameState.InGame_DeliverBallLoop)
            {
                inst.resetDelay = Main.NextBallDelay;
                inst.theBallScript.wide = true;
                inst.gameState = eGameState.InGame_BallMissed;
            }
            return;
        }

        // Behind the keeper: past him and still loose (a ball he holds is kinematic).
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null || rb.isKinematic)
            return;
        if (state == eGameState.InGame_DeliverBallLoop)
            Extras.Begin(false);
        else if (state == eGameState.InGame_BallMissed || state == eGameState.InGame_BallMissedLoop)
            Extras.Begin(inst.theBallScript.wide);
    }
}
