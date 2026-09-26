using UnityEngine;

public class BoundaryCollider : MonoBehaviour
{
    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name != "Ball") return;

        // Teleporting the ball (StopTheBall, re-parenting to the bowler's hand) also fires
        // OnTriggerExit, so confirm the ball really is outside the rope before acting on it.
        var capsule = GetComponent<CapsuleCollider>();
        float boundaryRadius = capsule != null ? capsule.radius * transform.lossyScale.x : 0f;
        Vector3 p = other.transform.position;
        float ballRadius = new Vector2(p.x - transform.position.x, p.z - transform.position.z).magnitude;
        if (boundaryRadius > 0f && ballRadius < boundaryRadius * 0.9f)
            return;   // spurious exit from a teleport, not a real boundary

        eGameState state = Main.Instance.gameState;

        if (state == eGameState.InGame_BallHitLoop)
        {
            // A shot that cleared the rope: four or six.
            if (!Main.Instance.theBallScript.bounced && other.attachedRigidbody != null)
                ShotDistance.Instance.OnSix(other.attachedRigidbody.position, other.attachedRigidbody.linearVelocity);
            Main.Instance.resetDelay = 4f;
            Main.Instance.gameState = eGameState.InGame_BallPastBoundary;
            return;
        }

        // Anything else still in play - most often a ball the batsman missed - used to be ignored
        // here, so it kept rolling until it reached the stadium stands, bounced off the
        // architecture and lodged in the crowd. Take it out of play at the rope instead. The game
        // state is deliberately left alone so scoring and the reset timer are unaffected.
        if (state == eGameState.InGame_BallMissedLoop ||
            state == eGameState.InGame_DeliverBallLoop ||
            state == eGameState.InGame_BallFielded_Loop)
        {
            var rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }
}
