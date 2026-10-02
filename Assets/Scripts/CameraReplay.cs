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
    private float renderRate = 36f;
    [Tooltip("Seconds of the bowler's run-up shown before the release.")]
    [SerializeField]
    private float leadIn = 1.2f;
    [Tooltip("Seconds the last frame holds before the replay loops.")]
    [SerializeField]
    private float loopPause = 1f;

    // The two views: from behind the bowler's arm (the TV view, a long lens from the start pose),
    // then, from a moment after a hit, side on behind the bowler's stumps, following the ball.
    private static readonly Vector3 FollowFrom = new Vector3(-14f, 1.78f, 0f);
    private const float TvFov = 7f, FollowFov = 60f;

    private static readonly int ViewId = Shader.PropertyToID("_View");
    private static readonly int OnId = Shader.PropertyToID("_On");

    private Camera myCamera;
    private ShotRecorder recorder;
    private ReplayActors actors;
    private bool othersHidden;
    private Vector3 startPos;
    private Quaternion startRot;
    private float followAt = float.MaxValue;     // Time.time the replay view starts following the ball

    private bool pending;
    private float showAt;
    private bool displaying;
    private bool screenLit;
    private ShotRecording shot;
    private float playFrom, playTo, playTime, followTime, holdLeft, nextRender;

    public bool IsShowing => displaying;

    void Start()
    {
        if (CameraReplay.Instance == null)
            CameraReplay.Instance = this;
        InitScreen();
        UseRuntimeMaterial();

        startPos = transform.position;
        startRot = transform.rotation;
        myCamera = GetComponent<Camera>();
        myCamera.enabled = false;               // on only for the frames a replay frame is filmed
        myCamera.targetTexture = view;
        myCamera.stereoTargetEye = StereoTargetEyeMask.None;
        int ui = LayerMask.NameToLayer("UI");
        if (ui >= 0)
            myCamera.cullingMask &= ~(1 << ui);
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
        {
            actors.SetReplayView(false);
            actors.HidePanels(false);
        }
        ReleaseScreen();
    }

    // ---- The game's calls ----------------------------------------------------------------------

    /// The ball is dead (or about to be): show the replay `delay` seconds from now, ending there.
    public void ShowReplayIn(float delay)
    {
        if (pending || displaying)
            return;
        pending = true;
        showAt = Time.time + delay;
        if (recorder == null)
            recorder = ShotRecorder.Instance;
        if (recorder != null)
            recorder.RecordAtLeastUntil(showAt);
    }

    /// After a hit: the replay's view follows the ball from `delay` seconds after this moment.
    public void FollowBallFrom(float delay)
    {
        followAt = Time.time + delay;
    }

    /// The next ball: the screen off, the views back to the start.
    public void ResetForNextBall()
    {
        pending = false;
        followAt = float.MaxValue;
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
        if (actors != null)
            actors.ShowStage(false);
    }

    // ---- Playback ------------------------------------------------------------------------------

    private void LateUpdate()
    {
        if (pending && Time.time >= showAt)
        {
            pending = false;
            BeginDisplay();
        }
        bool film = displaying && Advance(Time.deltaTime);
        if (film)
        {
            actors.Apply(shot, playTime);
            PoseCamera(playTime);
            actors.SkinThisFrame(true);
            actors.HidePanels(true);
        }
        if (myCamera.enabled != film)
            myCamera.enabled = film;
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
        // From the bowler's last strides to the moment the game asked for the replay.
        playFrom = Mathf.Max(0f, shot.ReleaseTime - leadIn);
        playTo = Mathf.Min(shot.Duration, showAt - shot.StartedAt);
        if (playTo <= playFrom)
            return;
        followTime = followAt < float.MaxValue ? followAt - shot.StartedAt : float.MaxValue;
        playTime = playFrom;
        holdLeft = 0f;
        nextRender = 0f;
        screenLit = false;
        displaying = true;
        actors.ShowStage(true);
    }

    /// Moves the replay on by `dt`; true when this frame should be filmed.
    private bool Advance(float dt)
    {
        if (holdLeft > 0f)
        {
            holdLeft -= dt;     // the screen keeps showing the last frame: nothing to render
            if (holdLeft > 0f)
                return false;
            playTime = playFrom;
            return Paced();
        }
        playTime += dt;
        if (playTime >= playTo)
        {
            playTime = playTo;
            holdLeft = Mathf.Max(loopPause, 1e-3f);
            return true;        // the last frame, to hold
        }
        return Paced();
    }

    /// `renderRate` frames a second, give or take half a frame (every other frame at 72 Hz).
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

    private void PoseCamera(float time)
    {
        if (time < followTime)
        {
            transform.SetPositionAndRotation(startPos, startRot);
            myCamera.fieldOfView = TvFov;
            return;
        }
        Vector3 look = shot.BallAt(time) - FollowFrom;
        transform.position = FollowFrom;
        if (look.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(look, Vector3.up);
        myCamera.fieldOfView = FollowFov;
    }

    /// The replay camera must not see what is live now and has no place in the replay: the
    /// screen itself (it would film its own picture), the hologram, the player's hands and the
    /// menu pointer.
    private void HideOthersOnce()
    {
        if (othersHidden)
            return;
        othersHidden = true;
        if (screen != null)
            actors.HideAlso(screen.GetComponentsInChildren<Renderer>(true));
        if (ShotReplay.Instance != null)
            actors.HideAlso(ShotReplay.Instance.GetComponentsInChildren<Renderer>(true));
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
            actors.HideCanvasesAlso(panels, ui);
    }

    // ---- The replay camera's render ------------------------------------------------------------

    private void BeginCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera == myCamera && actors != null)
            actors.SetReplayView(true);
    }

    private void EndCamera(ScriptableRenderContext context, Camera camera)
    {
        if (camera != myCamera || actors == null)
            return;
        actors.SetReplayView(false);
        actors.SkinThisFrame(false);
        actors.HidePanels(false);
        if (!screenLit && displaying && targetMat != null)
        {
            targetMat.SetFloat(OnId, 1f);   // only now: before its first frame `view` holds the last replay
            screenLit = true;
        }
    }
}
