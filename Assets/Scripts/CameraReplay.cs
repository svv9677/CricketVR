using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The replay on the big screen. Records the last few seconds of a delivery from the replay
/// camera and loops them on the screen after the ball is dead.
///
/// Built for the Quest's GPU, with nothing created at runtime:
///   * the camera is disabled and renders only while recording, once per CaptureInterval, into
///     the pre-made RenderTexture `view` (it used to render the whole stadium - with its own shadow
///     pass - every frame, into a texture nothing showed);
///   * each captured frame is copied into a slice of the pre-made Texture2DArray asset `frames`
///     (it used to allocate a new Texture2D for every captured frame);
///   * the screen's material (CricketVR/ReplayScreen) shows one slice via _Slice, -1 = off.
/// </summary>
public class CameraReplay : MonoBehaviour
{
    public static CameraReplay Instance = null;

    [SerializeField]
    private Material targetMat;
    [Tooltip("What the replay camera renders into (a RenderTexture asset).")]
    [SerializeField]
    private RenderTexture view;
    [Tooltip("Recorded frames: a Texture2DArray RenderTexture asset, same size as `view`.")]
    [SerializeField]
    private RenderTexture frames;
    [SerializeField]
    private float captureInterval = 0.06f;

    private Camera myCamera;
    private int frameCount;

    [HideInInspector]
    public bool record;
    [HideInInspector]
    public bool display;

    [HideInInspector]
    public Coroutine startCapturing;
    [HideInInspector]
    public Coroutine startDisplaying;

    private Vector3 startRot;
    private Vector3 startPos;
    private int viewSetting;

    private static readonly int SliceId = Shader.PropertyToID("_Slice");
    private static readonly int FramesId = Shader.PropertyToID("_Frames");

    // ---- Screen placement: at home in the stadium, or brought in front of the player -----------
    [Tooltip("The replay screen (the renderer using targetMat). Found by material if left empty.")]
    [SerializeField]
    private Transform screen;
    [Tooltip("How far in front of the player's head the screen comes, in metres.")]
    [SerializeField]
    private float frontDistance = 3.5f;
    [Tooltip("How far below eye level, in metres (0.3 m at 3.5 m is ~5 degrees down).")]
    [SerializeField]
    private float frontDrop = 0.3f;
    [Tooltip("Screen width in front, in metres. 2.9 m at 3.5 m fills ~45 degrees.")]
    [SerializeField]
    private float frontWidth = 2.9f;
    [SerializeField]
    private float moveSeconds = 0.8f;

    /// The player's choice: screen in front (true) or at home in the stadium (false). While a
    /// ball is live the screen goes home regardless, so it never blocks the delivery or the
    /// shot, and comes back once the ball is dead.
    public bool ScreenInFront => preferFront;
    /// Raised when ScreenInFront changes.
    public event System.Action ScreenMoved;

    private bool preferFront;
    private bool atFront;
    private bool frameCaptured;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private Vector3 homeScale;
    private Quaternion screenFrame;     // local frame: +Z = the face the player sees, +Y = up
    private float homeWidth;
    private Coroutine moving;
    private Main mainForScreen;

    void Start()
    {
        if (CameraReplay.Instance == null)
            CameraReplay.Instance = this;
        InitScreen();
        UseRuntimeMaterial();

        viewSetting = 0;
        startPos = transform.position;
        startRot = transform.rotation.eulerAngles;
        myCamera = GetComponent<Camera>();
        myCamera.enabled = false;               // renders only when a frame is captured
        myCamera.targetTexture = view;
        record = false;
        display = false;
        targetMat.SetTexture(FramesId, frames);
        targetMat.SetFloat(SliceId, -1f);
    }

    private void Update()
    {
        if (viewSetting == 0)
        {
            transform.position = startPos;
            transform.rotation = Quaternion.Euler(startRot);
            myCamera.fieldOfView = 7f;
        }
        else if (viewSetting == 1)
        {
            transform.position = new Vector3(-14f, 1.78f, 0f);
            transform.LookAt(Main.Instance.theBall.transform);
            transform.rotation = Quaternion.Euler(new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0f));
            myCamera.fieldOfView = 60f;
        }
    }

    public void StartRecording(float delay)
    {
        if (startCapturing == null)
            startCapturing = StartCoroutine(StartCapturing(delay));
    }

    private IEnumerator StartCapturing(float delay)
    {
        yield return new WaitForSeconds(delay);

        frameCount = 0;
        record = true;
        display = false;
        var wait = new WaitForSeconds(captureInterval);
        while (record)
        {
            Capture(frameCount % frames.volumeDepth);
            frameCount++;
            yield return wait;
        }
    }

    private void Capture(int slice)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = view };
        if (RenderPipeline.SupportsRenderRequest(myCamera, request))
            RenderPipeline.SubmitRenderRequest(myCamera, request);
        else
            myCamera.Render();
        Graphics.CopyTexture(view, 0, 0, frames, slice, 0);
    }

    public void StopRecording(float delay)
    {
        if (startDisplaying == null)
        {
            viewSetting = 0;
            startDisplaying = StartCoroutine(StartDisplaying(delay));
        }
    }

    private IEnumerator StartDisplaying(float delay)
    {
        yield return new WaitForSeconds(delay);

        record = false;
        display = true;
        // Only the most recent `volumeDepth` frames survive the ring; play them oldest first.
        int count = Mathf.Min(frameCount, frames.volumeDepth);
        int first = frameCount - count;
        var wait = new WaitForSeconds(captureInterval);
        while (display && count > 0)
        {
            for (int i = 0; i < count && display; i++)
            {
                targetMat.SetFloat(SliceId, (first + i) % frames.volumeDepth);
                yield return wait;
            }
            if (display)
                yield return new WaitForSeconds(1f);
        }
        targetMat.SetFloat(SliceId, -1f);
    }

    public void StopDisplaying()
    {
        display = false;
        targetMat.SetFloat(SliceId, -1f);
    }

    public void setViewSetting(int setting, float delay = 0f)
    {
        if (delay == 0)
            viewSetting = setting;
        else
            StartCoroutine(setViewSettingDelay(setting, delay));
    }

    private IEnumerator setViewSettingDelay(int setting, float delay = 0f)
    {
        yield return new WaitForSeconds(delay);
        viewSetting = setting;
    }

    // ---- Screen placement ----------------------------------------------------------------------

    /// Bring the replay screen in front of the player (true) or send it back to its home in the
    /// stadium (false). Placed once, where the player is looking when it arrives - not
    /// head-locked, which is uncomfortable in a headset.
    public void SetScreenInFront(bool front)
    {
        if (preferFront == front)
            return;
        preferFront = front;
        UpdateScreenPlacement();
        ScreenMoved?.Invoke();
    }

    private void InitScreen()
    {
        if (screen == null && targetMat != null)
        {
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial == targetMat) { screen = r.transform; break; }
        }
        if (screen != null)
        {
            homePosition = screen.position;
            homeRotation = screen.rotation;
            homeScale = screen.localScale;
            var renderer = screen.GetComponent<Renderer>();
            // A statically batched mesh is baked in place: moving its transform moves nothing.
            if (renderer != null && renderer.isPartOfStaticBatch)
                Debug.LogWarning("[CameraReplay] The replay screen is statically batched and cannot move - run Tools > CricketVR > Build Shot Replay.", screen);
        }
        mainForScreen = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        if (mainForScreen != null)
            mainForScreen.onGameStateChanged += UpdateScreenPlacement;
    }

    private void OnDestroy()
    {
        if (mainForScreen != null)
            mainForScreen.onGameStateChanged -= UpdateScreenPlacement;
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private Material runtimeMaterial;

    /// Play on a copy of the screen material. Writing _Slice / _Frames into targetMat itself
    /// wrote into the ReplayDisplay.mat asset, so every play session left it changed in git.
    private void UseRuntimeMaterial()
    {
        if (targetMat == null)
            return;
        Material asset = targetMat;
        runtimeMaterial = new Material(asset) { name = asset.name + " (runtime)" };
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r.sharedMaterial == asset)
                r.sharedMaterial = runtimeMaterial;
        }
        targetMat = runtimeMaterial;
    }

    /// While a ball is live - being bowled, or struck and still in play - a screen 3.5 m in front
    /// of the player would hide it.
    private static bool LiveBall(eGameState state)
    {
        switch (state)
        {
            case eGameState.InGame_SelectDelivery:
            case eGameState.InGame_SelectDeliveryLoop:
            case eGameState.InGame_DeliverBall:
            case eGameState.InGame_DeliverBallLoop:
            case eGameState.InGame_BallHit:
            case eGameState.InGame_BallHitLoop:
                return true;
            default:
                return false;
        }
    }

    private void UpdateScreenPlacement()
    {
        if (screen == null)
            return;
        bool live = mainForScreen != null && LiveBall(mainForScreen.gameState);
        bool want = preferFront && !live;
        if (want == atFront)
            return;
        if (want)
        {
            if (!FrontPose(out Vector3 position, out Quaternion rotation, out Vector3 scale))
                return;
            MoveScreen(position, rotation, scale);
        }
        else
        {
            MoveScreen(homePosition, homeRotation, homeScale);
        }
        atFront = want;
    }

    /// 3.5 m ahead of the head along its level gaze, a little below the eyes, turned to face the
    /// player the same way round it faces them at home, and scaled to `frontWidth`.
    private bool FrontPose(out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = homePosition;
        rotation = homeRotation;
        scale = homeScale;
        Camera eye = Camera.main;
        if (eye == null || eye == myCamera)
            return false;
        Transform head = eye.transform;
        if (!frameCaptured)
            CaptureScreenFrame(head.position);

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.ProjectOnPlane(head.up, Vector3.up);   // looking straight down/up
        forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;

        position = head.position + forward * frontDistance + Vector3.down * frontDrop;
        Vector3 toViewer = head.position - position;
        rotation = Quaternion.LookRotation(toViewer, Vector3.up) * Quaternion.Inverse(screenFrame);
        scale = homeScale * (homeWidth > 1e-4f ? frontWidth / homeWidth : 1f);
        return true;
    }

    /// Work out, from the mesh and its home pose, which local axis is the face the player sees
    /// (the thinnest axis of a plane/quad, signed toward the player), which is up, and how wide
    /// the screen is - so it keeps its orientation and aspect whatever mesh the scene uses.
    private void CaptureScreenFrame(Vector3 viewer)
    {
        var filter = screen.GetComponent<MeshFilter>();
        Vector3 size = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : new Vector3(1f, 1f, 0f);
        int normal = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;

        Vector3 frontLocal = Vector3.zero;
        frontLocal[normal] = 1f;
        if (Vector3.Dot(homeRotation * frontLocal, viewer - homePosition) < 0f)
            frontLocal[normal] = -1f;

        Vector3 upRaw = Quaternion.Inverse(homeRotation) * Vector3.up;
        upRaw[normal] = 0f;
        int up = normal == 0 ? (Mathf.Abs(upRaw.y) >= Mathf.Abs(upRaw.z) ? 1 : 2)
               : normal == 1 ? (Mathf.Abs(upRaw.x) >= Mathf.Abs(upRaw.z) ? 0 : 2)
               : (Mathf.Abs(upRaw.x) >= Mathf.Abs(upRaw.y) ? 0 : 1);
        Vector3 upLocal = Vector3.zero;
        upLocal[up] = upRaw[up] >= 0f ? 1f : -1f;

        int across = 3 - normal - up;
        homeWidth = Mathf.Abs(size[across] * screen.lossyScale[across]);
        screenFrame = Quaternion.LookRotation(frontLocal, upLocal);
        frameCaptured = true;
    }

    private void MoveScreen(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (moving != null)
            StopCoroutine(moving);
        moving = StartCoroutine(MovingScreen(position, rotation, scale));
    }

    private IEnumerator MovingScreen(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Vector3 fromPosition = screen.position;
        Quaternion fromRotation = screen.rotation;
        Vector3 fromScale = screen.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(moveSeconds, 1e-3f))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            screen.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, s), Quaternion.Slerp(fromRotation, rotation, s));
            screen.localScale = Vector3.Lerp(fromScale, scale, s);
            yield return null;
        }
        screen.SetPositionAndRotation(position, rotation);
        screen.localScale = scale;
        moving = null;
    }
}
