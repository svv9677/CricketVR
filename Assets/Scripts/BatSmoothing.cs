using UnityEngine;

/// <summary>
/// How the bat is drawn, separately from how it is simulated. Visual only: the contact sweep and
/// everything else still use the pose LateUpdate sets from the hand.
///
/// Two things made a hard swing look jittery:
///  * Staleness. The head is re-read just before rendering (the camera's TrackedPoseDriver), but
///    the controller was read once in Update, so the bat was drawn from a hand pose older than the
///    view by an amount that varied frame to frame - uneven steps in a fast swing. The controller
///    anchor is re-read here too (XRControllerTracker.Refresh) and the bat re-posed from it.
///  * Tracking noise and uneven steps. A One Euro filter: heavy smoothing when the bat is nearly
///    still (where noise shows), light when it moves fast (where lag would show). Even at swing
///    speed it keeps some smoothing (MaxCutoff), which is what evens out the steps.
/// Before the next Update the transform goes back to the simulated pose (LateUpdate sets it
/// again), so the physics never sees the smoothed one.
/// </summary>
public partial class Bat
{
    /// Hz. Cutoff at rest, how fast it rises with speed, and its ceiling. At the ceiling the
    /// drawn bat trails the hand by about 5 ms (1/(2 pi 30)) - a few degrees in a hard swing.
    private const float MinCutoff = 4f, PositionBeta = 6f, RotationBeta = 0.03f, MaxCutoff = 30f;

    private bool smoothedValid;
    private Vector3 smoothedPosition;
    private Quaternion smoothedRotation;
    private double lastRenderTime;

    private void OnEnable() => Application.onBeforeRender += PoseForRender;

    private void OnDisable()
    {
        Application.onBeforeRender -= PoseForRender;
        smoothedValid = false;
    }

    private void PoseForRender()
    {
        if (holdingStill || !grabbable || !grabbing || attachParent == null || myRigidBody == null)
        {
            smoothedValid = false;
            return;
        }
        XRControllerTracker tracker = attachParent.GetComponentInParent<XRControllerTracker>();
        if (tracker != null)
            tracker.Refresh();
        HeldPose(out Vector3 position, out Quaternion rotation);

        double now = Time.realtimeSinceStartupAsDouble;
        float dt = (float)(now - lastRenderTime);
        lastRenderTime = now;
        if (!smoothedValid || dt <= 0f || dt > 0.1f)
        {
            smoothedPosition = position;
            smoothedRotation = rotation;
            smoothedValid = true;
        }
        else
        {
            float speed = (position - smoothedPosition).magnitude / dt;
            float angularSpeed = Quaternion.Angle(smoothedRotation, rotation) / dt;
            smoothedPosition = Vector3.Lerp(smoothedPosition, position, Alpha(MinCutoff + PositionBeta * speed, dt));
            smoothedRotation = Quaternion.Slerp(smoothedRotation, rotation, Alpha(MinCutoff + RotationBeta * angularSpeed, dt));
        }
        transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);
    }

    /// One Euro: the blend toward the new sample for a first-order low-pass at `cutoff` Hz.
    private static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * Mathf.Min(cutoff, MaxCutoff));
        return 1f / (1f + tau / dt);
    }
}
