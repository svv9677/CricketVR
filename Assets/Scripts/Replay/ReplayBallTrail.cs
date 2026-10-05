using UnityEngine;

/// <summary>
/// The ball's trail in the replay, and a ball that can be seen in it. The live ball's trail is a
/// TrailRenderer, which only draws where the live ball has been; the replay's ball is a stand-in
/// posed from the recording, so its trail is drawn from the recording too: the last
/// `TrailSeconds` of its path, in the live trail's material and colours.
///
/// Zoomed out to follow a shot, a 7 cm ball and its trail are under a pixel of the replay's
/// picture and the eye loses it. Both are kept at least a few pixels across for the camera
/// filming them - true size when it is tight on the pitch, bigger only when it is wide.
/// Seen by the replay camera alone (Show, around its render).
/// </summary>
public sealed class ReplayBallTrail
{
    private const int Points = 24;
    private const float TrailSeconds = 0.35f;
    private const float TrailPixels = 3f, BallPixels = 5f;

    private readonly LineRenderer line;
    private readonly Vector3[] buffer = new Vector3[Points];
    private readonly Transform ball;
    private readonly Vector3 ballScale;
    private readonly float ballDiameter, trueWidth;

    public ReplayBallTrail(Main main, Transform ballStandIn)
    {
        ball = ballStandIn;
        ballScale = ballStandIn != null ? ballStandIn.localScale : Vector3.one;
        Renderer live = main != null && main.theBall != null ? main.theBall.GetComponent<MeshRenderer>() : null;
        ballDiameter = live != null ? Mathf.Max(0.02f, live.bounds.size.x) : 0.072f;
        TrailRenderer liveTrail = main != null && main.theBall != null ? main.theBall.GetComponent<TrailRenderer>() : null;

        var go = new GameObject("Replay Ball Trail");
        line = go.AddComponent<LineRenderer>();
        line.positionCount = Points;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.widthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);   // thin at the tail
        if (liveTrail != null)
        {
            line.sharedMaterial = liveTrail.sharedMaterial;
            line.colorGradient = liveTrail.colorGradient;
            trueWidth = Mathf.Max(0.02f, liveTrail.widthMultiplier * liveTrail.widthCurve.Evaluate(0f));
        }
        else
        {
            line.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trueWidth = ballDiameter;
        }
        // The gradient as it is, but the tail always fades out (a trail, not a bar).
        Gradient g = line.colorGradient;
        g.SetKeys(g.colorKeys, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f) });
        line.colorGradient = g;
        line.forceRenderingOff = true;
    }

    /// The trail up to `time` (never from before `from`, the release), sized for `camera`.
    public void Apply(ShotRecording shot, float time, float from, Camera camera)
    {
        float start = Mathf.Max(from, time - TrailSeconds);
        for (int i = 0; i < Points; i++)
            buffer[i] = shot.BallAt(Mathf.Lerp(start, time, i / (float)(Points - 1)));
        line.SetPositions(buffer);

        float distance = Vector3.Distance(camera.transform.position, buffer[Points - 1]);
        float metresPerPixel = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) /
                               Mathf.Max(1, camera.pixelHeight);
        line.widthMultiplier = Mathf.Max(trueWidth, TrailPixels * metresPerPixel);
        if (ball != null)
            ball.localScale = ballScale * Mathf.Max(1f, BallPixels * metresPerPixel / ballDiameter);
    }

    public void Show(bool on) => line.forceRenderingOff = !on;
}
