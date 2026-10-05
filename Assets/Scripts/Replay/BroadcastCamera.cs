using UnityEngine;

/// <summary>
/// The replay's one camera, worked like the main broadcast camera at a ground: high up behind the
/// bowler's arm, never moving, only panning, tilting and zooming. Through the run-up and the
/// delivery it holds a tight shot down the pitch (a long lens: the bowler, the batter, the
/// stumps). Once the ball is hit the operator follows it - a pan that lags a little, as a person
/// does - and pulls the zoom out smoothly as it goes further from the bat, so the ball stays in a
/// picture that widens to show where it went and who chases it.
///
/// Stepped every frame of the replay (Step), whether or not the frame is filmed, so the motion is
/// the same at any render rate; Reset at the start of each loop.
/// </summary>
public sealed class BroadcastCamera
{
    /// Tight on the pitch; as wide as the operator goes.
    private const float TightFov = 7f, WideFov = 42f;
    /// How quickly the pan catches the ball, and the zoom its target (per second).
    private const float PanRate = 5f, ZoomRate = 2.2f;
    /// Degrees of field of view per degree the ball is from where it was struck.
    private const float FramePerDegree = 2.4f;

    private readonly Transform mount;
    private readonly Camera lens;
    private readonly Vector3 position;
    private readonly Vector3 home;      // looking down the pitch
    private Vector3 aim;
    private float fov;

    public BroadcastCamera(Camera camera)
    {
        lens = camera;
        mount = camera.transform;
        position = mount.position;
        home = mount.forward;
        Reset();
    }

    public void Reset()
    {
        aim = home;
        fov = TightFov;
    }

    public void Step(ShotRecording shot, float time, float dt)
    {
        Vector3 targetAim = home;
        float targetFov = TightFov;
        if (shot.HasContact && time > shot.ContactTime)
        {
            Vector3 toBall = shot.BallAt(time) - position;
            if (toBall.sqrMagnitude > 1e-4f)
                targetAim = toBall.normalized;
            float apart = Vector3.Angle(toBall, shot.ContactPoint - position);
            targetFov = Mathf.Clamp(TightFov + apart * FramePerDegree, TightFov, WideFov);
        }
        aim = Vector3.Slerp(aim, targetAim, 1f - Mathf.Exp(-PanRate * dt)).normalized;
        fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-ZoomRate * dt));
    }

    public void Apply()
    {
        mount.SetPositionAndRotation(position, Quaternion.LookRotation(aim, Vector3.up));
        lens.fieldOfView = fov;
    }
}
