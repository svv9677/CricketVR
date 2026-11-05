using UnityEngine;

/// <summary>
/// The batter's head while one ball comes down (BallLedger, for the career's drills): how far he
/// went down the pitch to it (Step: using his feet to a spinner) and how close the ball's centre
/// came to his head (Clearance: swaying out of a bouncer, or wearing it). Pure - the head (the
/// camera) and the ball are passed in each frame - so it can be tested without a scene.
///
///   * Begin: a new ball, nothing seen yet (Step 0, Clearance NeverNear).
///   * Release: where the head was as the ball left the hand; Step is measured from there.
///   * Sample: one frame - the ball's path since the last frame and the head now. Clearance is only
///     measured while the ball is within Near of the head and not yet past it, along Steps points of
///     the path (a quick moves ~0.5 m a frame, so the nearest frame can be well off the nearest
///     point), as BallLedger measures the bat's closest approach.
/// </summary>
public sealed class HeadWatch
{
    /// The ball is watched for clearance this close to the head (m); never that close: NeverNear.
    public const float Near = 3f, NeverNear = 99f;
    /// Points sampled along the ball's path each frame (as BallLedger.ApproachSteps).
    public const int Steps = 4;
    /// A ball whose path starts this far behind the head (+X, toward the keeper) has gone past.
    public const float Past = 0.5f;

    /// Furthest the head has moved toward the bowler since release (m, 0 if never forward).
    public float Step { get; private set; }
    /// Closest the ball's centre has come to the head (m), NeverNear if it never came near.
    public float Clearance { get; private set; } = NeverNear;

    private Vector3 atRelease;
    private bool released;

    public void Begin()
    {
        Step = 0f;
        Clearance = NeverNear;
        released = false;
    }

    public void Release(Vector3 head)
    {
        atRelease = head;
        released = true;
    }

    public void Sample(Vector3 ballFrom, Vector3 ballTo, Vector3 head)
    {
        if (released)
            Step = Mathf.Max(Step, BallLedgerMath.HeadStep(atRelease, head));
        if ((ballTo - head).sqrMagnitude > Near * Near || ballFrom.x > head.x + Past)
            return;
        for (int i = 0; i <= Steps; i++)
        {
            Vector3 p = Vector3.Lerp(ballFrom, ballTo, i / (float)Steps);
            Clearance = Mathf.Min(Clearance, (p - head).magnitude);
        }
    }
}
