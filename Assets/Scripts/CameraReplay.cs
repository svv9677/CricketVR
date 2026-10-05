using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The replay on the big screen. Nothing is filmed while the ball is live: ShotRecorder records
/// the delivery once, as data - the ball, the bat, every player's bones, the stumps - the same
/// recording the holographic ShotReplay plays. Once the ball is dead, the replay rebuilds the
/// delivery on stand-ins (ReplayActors) standing in the real stadium, and the replay camera
/// films them into `view`, which the screen shows: a live in-game render at the full resolution
/// of `view`, never a stored, compressed frame.
///
/// For the Quest:
///   * while the ball is live, the replay costs a few hundred transform reads every other frame;
///     the old one rendered the whole stadium a second time ~16 times a second during the
///     delivery and the shot, and copied every frame into a 64-slice texture array (75 MB);
///   * the camera renders only while the replay plays, `renderRate` times a second, and not at
///     all through the pause between loops (the screen keeps the last frame);
///   * the stand-ins are visible to the replay camera alone, and the live players are hidden from
///     it only for the length of its render (ReplayActors.SetReplayView).
/// </summary>
public partial class CameraReplay : MonoBehaviour
{
    public static CameraReplay Instance = null;

    [SerializeField]
    private Material targetMat;
    [Tooltip("What the replay camera renders into and the screen shows (a RenderTexture asset).")]
    [SerializeField]
    private RenderTexture view;
    [Tooltip("Replay frames rendered per second while the replay plays. The camera is off otherwise.")]
    [SerializeField]
    private float renderRate = 30f;
    [Tooltip("Seconds of the bowler's run-up shown before the release.")]
    [SerializeField]
    private float leadIn = 1.2f;
    [Tooltip("Seconds the last frame holds before the replay loops.")]
    [SerializeField]
    private float loopPause = 1f;

    private static readonly int ViewId = Shader.PropertyToID("_View");
    private static readonly int OnId = Shader.PropertyToID("_On");

    private Camera myCamera;
    private BroadcastCamera broadcast;
    private ReplayOverlay overlay;
    private ReplayBallTrail trail;
    private ShotRecorder recorder;
    private ReplayActors actors;
    private bool othersHidden;
    private bool warmed;
    private Renderer screenRenderer;
    private readonly Plane[] eyePlanes = new Plane[6];

    private bool pending;
    private float showAt;
    private System.Func<bool> waitFor;      // ShowReplayWhen: the moment the play is over
    private float waitUntil, waitThen;
    private bool displaying;
    private bool screenLit;
    private ShotRecording shot;
    private float playFrom, playTo, playTime, holdLeft, nextRender;

    public bool IsShowing => displaying;

    void Start()
    {
        if (CameraReplay.Instance == null)
            CameraReplay.Instance = this;
        InitScreen();
        UseRuntimeMaterial();
        screenRenderer = screen != null ? screen.GetComponent<Renderer>() : null;

        myCamera = GetComponent<Camera>();
        myCamera.enabled = false;               // on only for the frames a replay frame is filmed
        myCamera.targetTexture = view;
        myCamera.stereoTargetEye = StereoTargetEyeMask.None;
        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0)
            myCamera.cullingMask &= ~(1 << ui);
        broadcast = new BroadcastCamera(myCamera);
        overlay = new ReplayOverlay(myCamera);
        if (targetMat != null)
        {
            targetMat.SetTexture(ViewId, view);
            targetMat.SetFloat(OnId, 0f);
        }
        RenderPipelineManager.beginCameraRendering += BeginCamera;
        RenderPipelineManager.endCameraRendering += EndCamera;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCamera;
        RenderPipelineManager.endCameraRendering -= EndCamera;
        if (actors != null)
            actors.SetReplayView(false);
        ReleaseScreen();
    }

    // ---- The game's calls ----------------------------------------------------------------------

    /// The ball is dead: show the replay `delay` seconds from now - it runs up to then. A later
    /// call replaces one still waiting (a wide that beats the keeper is played on, then fielded).
    public void ShowReplayIn(float delay) => ShowReplayWhen(null, delay, 0f);

    /// Show the replay `delay` seconds after `done` comes true (the keeper has the wide; the six
    /// has come down), or after `maxWait` if it never does.
    public void ShowReplayWhen(System.Func<bool> done, float delay, float maxWait)
    {
        if (displaying)
            return;
        waitFor = done;
        waitThen = delay;
        waitUntil = Time.time + maxWait;
        pending = false;
        if (recorder == null)
            recorder = ShotRecorder.Instance;
        if (recorder != null)
            recorder.RecordAtLeastUntil(Time.time + maxWait + delay);   // whenever it comes, it is recorded
        if (done == null)
            Schedule(delay);
    }

    private void Schedule(float delay)
    {
        waitFor = null;
        pending = true;
        showAt = Time.time + delay;
        if (recorder != null)
            recorder.RecordAtLeastUntil(showAt);
    }

    /// The next ball: the screen off.
    public void ResetForNextBall()
    {
        pending = false;
        waitFor = null;
        StopDisplaying();
    }

    public void StopDisplaying()
    {
        displaying = false;
        shot = null;
        if (myCamera != null)
            myCamera.enabled = false;
        if (targetMat != null)
            targetMat.SetFloat(OnId, 0f);
        overlay?.Hide();
    }

    // ---- Playback ------------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (!warmed && WarmUp())
            return;
        if (waitFor != null && (waitFor() || Time.time >= waitUntil))
            Schedule(waitThen);
        if (pending && Time.time >= showAt)
        {
            pending = false;
            BeginDisplay();
        }
        bool film = displaying && Advance(Time.deltaTime) && ScreenInView();
        if (film)
        {
            actors.Apply(shot, playTime);
            broadcast.Apply();
            trail?.Apply(shot, playTime, shot.ReleaseTime, myCamera);
            actors.SkinThisFrame(true);
        }
        if (myCamera.enabled != film)
            myCamera.enabled = film;
    }

    /// One render at load, with the stand-ins in, the screen still dark: the shaders, the MSAA
    /// target and the stand-ins' skinning are set up then, not in the frame the first replay
    /// starts (on the headset, that first frame was a stall).
    private bool WarmUp()
    {
        if (recorder == null)
            recorder = ShotRecorder.Instance;
        if (recorder == null || recorder.Actors == null)
            return false;
        actors = recorder.Actors;
        trail = new ReplayBallTrail(Main.Instance, actors.BallStandIn);
        HideOthersOnce();
        broadcast.Apply();
        actors.SkinThisFrame(true);
        myCamera.enabled = true;
        warmed = true;
        return true;
    }

    private void BeginDisplay()
    {
        if (recorder == null)
            recorder = ShotRecorder.Instance;
        actors = recorder != null ? recorder.Actors : null;
        shot = recorder != null ? recorder.Latest : null;
        if (actors == null || shot == null || !shot.Released || shot.Count < 2)
            return;
        HideOthersOnce();
        // From the bowler's last strides to the end of the play (ShowReplayWhen).
        playFrom = Mathf.Max(0f, shot.ReleaseTime - leadIn);
        playTo = Mathf.Min(shot.Duration, showAt - shot.StartedAt);
        if (playTo <= playFrom)
            return;
        Restart();
        nextRender = 0f;
        screenLit = false;
        displaying = true;
        overlay.Show(ReleaseSpeedKmh(shot));
    }

    private void Restart()
    {
        playTime = playFrom;
        holdLeft = 0f;
        broadcast.Reset();
        broadcast.Step(shot, playTime, 0f);
    }

    /// Moves the replay on by `dt`; true when this frame should be filmed.
    private bool Advance(float dt)
    {
        if (holdLeft > 0f)
        {
            holdLeft -= dt;     // the screen keeps showing the last frame: nothing to render
            if (holdLeft > 0f)
                return false;
            Restart();
            return Paced();
        }
        playTime = Mathf.Min(playTime + dt, playTo);
        broadcast.Step(shot, playTime, dt);
        if (playTime >= playTo)
        {
            holdLeft = Mathf.Max(loopPause, 1e-3f);
            return true;        // the last frame, to hold
        }
        return Paced();
    }

    /// `renderRate` frames a second, give or take half a frame.
    private bool Paced()
    {
        float interval = 1f / Mathf.Max(1f, renderRate);
        if (Time.time < nextRender - 0.5f * Time.deltaTime)
            return false;
        nextRender += interval;
        if (nextRender < Time.time)
            nextRender = Time.time + interval;
        return true;
    }

    /// No replay frames for a screen the player is not looking at: the replay plays on unseen
    /// and is filmed again the moment it comes back into view.
    private bool ScreenInView()
    {
        Camera eye = Camera.main;
        if (screenRenderer == null || eye == null || eye == myCamera || !screenLit)
            return true;
        GeometryUtility.CalculateFrustumPlanes(eye, eyePlanes);
        return GeometryUtility.TestPlanesAABB(eyePlanes, screenRenderer.bounds);
    }

    /// The delivery's speed just out of the hand, from the recorded ball.
    private static float ReleaseSpeedKmh(ShotRecording shot)
    {
        float t = shot.ReleaseTime + 0.05f, dt = 0.1f;
        if (t + dt > shot.Duration)
            return 0f;
        return (shot.BallAt(t + dt) - shot.BallAt(t)).magnitude / dt * UIFormat.KmhPerMps;
    }

    /// The replay camera must not see what is live now and has no place in the replay: the
    /// screen itself (it would film its own picture), the hologram, the player's hands, the menu
    /// pointer and the menus.
    private void HideOthersOnce()
    {
        if (othersHidden)
            return;
        othersHidden = true;
        if (screen != null)
            actors.HideAlso(screen.GetComponentsInChildren<Renderer>(true));
        if (ShotReplay.Instance != null)
            actors.HideAlso(ShotReplay.Instance.GetComponentsInChildren<Renderer>(true));
        // The live result over the stumps belongs to now, not to the ball being replayed.
        BallResultText result = FindFirstObjectByType<BallResultText>();
        if (result != null)
            actors.HideAlso(result.GetComponentsInChildren<Renderer>(true));
        // The hands, under the XR rig's own root - never a root that holds the game itself.
        Camera eye = Camera.main;
        if (eye != null && eye != myCamera && eye.transform.root.GetComponentInChildren<Main>(true) == null)
            actors.HideAlso(eye.transform.root.GetComponentsInChildren<Renderer>(true));
        foreach (LaserPointer pointer in FindObjectsByType<LaserPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // The pointer and its dot, grouped under their own root (UIHelpers).
            Transform group = pointer.transform.parent != null && pointer.transform.parent.parent == null
                ? pointer.transform.parent : pointer.transform;
            actors.HideAlso(group.GetComponentsInChildren<Renderer>(true));
        }
        // The player's menus and overlays: the world-space canvases outside the game's own root
        // (the stadium's scorecard is inside it, and stays in the picture).
        int ui = LayerMask.NameToLayer("UI");
        var panels = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponentInParent<Main>(true) == null)
                panels.Add(canvas);
        if (ui >= 0)
            ReplayActors.MoveCanvases(panels, ui);
    }

    // ---- The replay camera's render ------------------------------------------------------------

    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != myCamera || actors == null)
            return;
        actors.SetReplayView(true);
        trail?.Show(displaying);
    }

    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != myCamera || actors == null)
            return;
        actors.SetReplayView(false);
        actors.SkinThisFrame(false);
        trail?.Show(false);
        if (!screenLit && displaying && targetMat != null)
        {
            targetMat.SetFloat(OnId, 1f);   // only now: before its first frame `view` holds the last replay
            screenLit = true;
        }
    }
}
