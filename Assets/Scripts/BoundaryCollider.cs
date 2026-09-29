using UnityEngine;

/// <summary>
/// The rope: a circle RopeRadius from the middle (the CapsuleCollider's radius), as high as you
/// like. A ball in play whose centre is past it is over the boundary - four if it has bounced,
/// six if not - wherever it is in the air.
///
/// Checked directly every physics step (FixedUpdate), not only by the trigger: a ball leaving
/// the trigger is not the only way over the rope, and a fielder who ran on beyond the rope used to
/// "catch" it out there and the boundary never counted. Fielders cannot take a ball beyond the
/// rope (Beyond, in AnimatedFielderManagement.Gather) - it is a boundary whoever touches it.
/// </summary>
public class BoundaryCollider : MonoBehaviour
{
    private static BoundaryCollider instance;
    private float ropeRadius = 56.6f;

    private void Awake()
    {
        instance = this;
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
            ropeRadius = capsule.radius * transform.lossyScale.x;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// Over the rope (horizontally from the middle), at any height.
    public static bool Beyond(Vector3 p)
    {
        if (instance == null)
            return false;
        Vector3 c = instance.transform.position;
        return new Vector2(p.x - c.x, p.z - c.z).magnitude > instance.ropeRadius;
    }

    private void FixedUpdate()
    {
        Main inst = Main.Instance;
        if (inst == null || inst.gameState != eGameState.InGame_BallHitLoop)
            return;
        Rigidbody rb = inst.theBallRigidBody;
        if (rb == null || rb.isKinematic || !Beyond(rb.position))
            return;
        Boundary(rb);
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name != "Ball") return;

        // Teleporting the ball (StopTheBall, re-parenting to the bowler's hand) also fires
        // OnTriggerExit, so confirm the ball really is outside the rope before acting on it.
        if (!Beyond(other.transform.position))
            return;   // spurious exit from a teleport, not a real boundary

        eGameState state = Main.Instance.gameState;

        if (state == eGameState.InGame_BallHitLoop)
        {
            if (other.attachedRigidbody != null)
                Boundary(other.attachedRigidbody);
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

    /// A shot (or byes) that cleared the rope: four or six.
    private static void Boundary(Rigidbody rb)
    {
        Main inst = Main.Instance;
        if (inst.gameState != eGameState.InGame_BallHitLoop)
            return;
        if (!inst.theBallScript.bounced)
            ShotDistance.Instance.OnSix(rb.position, rb.linearVelocity);
        inst.resetDelay = Main.BoundaryDelay;
        inst.gameState = eGameState.InGame_BallPastBoundary;
    }
}
