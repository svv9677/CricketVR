using UnityEngine;

/// <summary>
/// The replay's one camera, worked like the main broadcast camera at a ground: high up behind the
/// bowler's arm, never moving, only panning, tilting and zooming. Through the run-up and the
/// delivery it holds a tight shot down the pitch (a long lens: the bowler, the batter, the
/// stumps), and keeps it a moment after the shot. Then the operator follows the ball - a pan that
/// lags a little, as a person does - pulling the zoom out while it races away, and back in as it
/// slows into a fielder's hands, so the take or the catch is seen close. A ball out of the ground
/// settles on a medium shot of where it came down. The replay is recorded, so the operator works
/// a little ahead of the ball, the way a good one anticipates.
///
/// Stepped every frame of the replay (Step), whether or not the frame is filmed, so the motion is
/// the same at any render rate; Reset at the start of each loop.
/// </summary>
public sealed class BroadcastCamera
{
    /// Tight on the pitch; as wide as the operator goes.
    private const float TightFov = 7f, WideFov = 42f;
    /// How quickly the pan catches the ball, and the zoom its target (per second): out briskly,
    /// back in gently.
    private const float PanRate = 5f, ZoomOutRate = 2.2f, ZoomInRate = 1.1f;
    /// The tight shot is held this long after the bat meets the ball.
    private const float HoldTight = 0.45f;
    /// The shot a slowing ball is followed into: a fielder taking it; a ball in the crowd.
    private const float FielderFov = 13f, CrowdFov = 24f, BoundaryRadius = 68f;
    /// How far ahead the operator judges the ball's pace (s), and the pace he goes tight for (m/s).
    private const float LookAhead = 0.6f, SlowPace = 5f, FastPace = 15f;
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
            Vector3 ball = shot.BallAt(time);
            Vector3 toBall = ball - position;
            if (toBall.sqrMagnitude > 1e-4f)
                targetAim = toBall.normalized;
            if (time - shot.ContactTime > HoldTight)
                targetFov = FollowFov(shot, time, ball, toBall);
        }
        aim = Vector3.Slerp(aim, targetAim, 1f - Mathf.Exp(-PanRate * dt)).normalized;
        float rate = targetFov > fov ? ZoomOutRate : ZoomInRate;
        fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-rate * dt));
    }

    /// Wide enough to hold the ball and where it was struck while it travels; tighter again as it
    /// is about to slow (taken, caught, stopped) - not as tight as the delivery, and only to a
    /// medium shot for a ball beyond the rope.
    private float FollowFov(ShotRecording shot, float time, Vector3 ball, Vector3 toBall)
    {
        float apart = Vector3.Angle(toBall, shot.ContactPoint - position);
        float wide = Mathf.Clamp(TightFov + apart * FramePerDegree, TightFov, WideFov);
        float ahead = Mathf.Min(time + LookAhead, shot.Duration);
        float pace = (shot.BallAt(ahead) - shot.BallAt(Mathf.Max(ahead - 0.1f, 0f))).magnitude / 0.1f;
        float settle = Mathf.InverseLerp(FastPace, SlowPace, pace);
        bool gone = new Vector2(ball.x, ball.z).magnitude > BoundaryRadius;
        float close = gone ? CrowdFov : FielderFov;
        return Mathf.Lerp(wide, Mathf.Min(wide, close), settle);
    }

    public void Apply()
    {
        mount.SetPositionAndRotation(position, Quaternion.LookRotation(aim, Vector3.up));
        lens.fieldOfView = fov;
    }
}
