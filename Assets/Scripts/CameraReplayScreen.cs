using System.Collections;
using UnityEngine;

/// <summary>
/// The replay screen itself (CameraReplay): its material, and where it stands - at home in the
/// stadium, or brought in front of the player.
/// </summary>
public partial class CameraReplay
{
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
    private Material runtimeMaterial;

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

    private void ReleaseScreen()
    {
        if (mainForScreen != null)
            mainForScreen.onGameStateChanged -= UpdateScreenPlacement;
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    /// Play on a copy of the screen material. Writing into targetMat itself wrote into the
    /// ReplayDisplay.mat asset, so every play session left it changed in git.
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

        // Straight ahead of the player's fixed spot (MenuLayout), not wherever they are looking.
        if (!MenuLayout.TryGetEye(out Vector3 eyePoint, out Vector3 forward))
        {
            eyePoint = head.position;
            forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);   // looking straight down/up
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        position = eyePoint + forward * frontDistance + Vector3.down * frontDrop;
        Vector3 toViewer = eyePoint - position;
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
