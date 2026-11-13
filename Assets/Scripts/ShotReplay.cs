using UnityEngine;

/// <summary>
/// The holographic shot replay: plays the last delivery (ShotRecorder) back as a ghost bat and
/// ghost ball at their real positions in the world, so the player, standing at the crease, sees
/// their own swing and the ball's flight from where they are now - at any speed, looping. The
/// same recording the big screen (CameraReplay) plays: each delivery is recorded once.
///
/// Built for the Quest, with nothing created at runtime (Tools > CricketVR > Build Shot Replay
/// makes the ghosts):
///   * per frame: one interpolated pose for the bat and ball, 48 interpolated trail points
///     written into a preallocated array, and while the contact pulse runs 32 ring points;
///     no allocations;
///   * the ghosts are one additive unlit material (CricketVR/Hologram), 4 draw calls in all,
///     and their GameObject is inactive unless a replay is playing;
///   * never touches the real ball or bat.
/// </summary>
public class ShotReplay : MonoBehaviour
{
    public static ShotReplay Instance { get; private set; }

    private static readonly float[] SpeedPresets = { 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f };
    private const int TrailPoints = 48;
    private const int RingPoints = 32;

    [SerializeField]
    private ShotRecorder recorder;
    [SerializeField]
    private Main main;
    [Tooltip("Parent of every ghost; active only while playing.")]
    [SerializeField]
    private GameObject ghosts;
    [SerializeField]
    private Transform ghostBat;
    [SerializeField]
    private Transform ghostBall;
    [SerializeField]
    private LineRenderer trail;
    [SerializeField]
    private LineRenderer contactRing;

    [Tooltip("Seconds of the delivery shown before contact (the ball coming in).")]
    [SerializeField]
    private float leadIn = 0.8f;
    [Tooltip("Seconds shown after contact (the ball's flight).")]
    [SerializeField]
    private float followThrough = 4f;
    [Tooltip("Pause at the end of each loop, in real seconds.")]
    [SerializeField]
    private float endPause = 0.8f;
    [Tooltip("Length of the ball's trail, in replay seconds.")]
    [SerializeField]
    private float trailSeconds = 0.5f;
    [SerializeField]
    private float ringRadius = 0.12f;
    [Tooltip("How long the contact ring takes to open, in replay seconds.")]
    [SerializeField]
    private float pulseSeconds = 0.35f;

    /// What Play plays: a saved record clip when one is loaded (the replay theatre), else the last
    /// delivery - the one still being recorded once it has been bowled. It was the last *finished*
    /// recording, and the recorder keeps a ball open for the big screen for up to 6 s after it is
    /// dead (a miss: 2 + 6 s asked for at the end of the play), long after the next-ball card is
    /// up, so Replay showed the ball before, then jumped to this one when it finished (2026-11-09:
    /// "it still shows the previous replay ... then it snaps to the correct latest ball").
    public ShotRecording Source
    {
        get
        {
            if (loaded != null)
                return loaded;
            if (recorder == null)
                return null;
            ShotRecording latest = recorder.Latest;
            return latest != null && latest.Released ? latest : recorder.Last;
        }
    }
    public bool HasShot => Source != null && Source.Count > 1;
    public bool IsPlaying => playing;
    public bool IsPaused => playing && paused;
    public float Speed
    {
        get => speed;
        set
        {
            float clamped = Mathf.Clamp(value, 0.1f, 2f);
            if (Mathf.Approximately(clamped, speed))
                return;
            speed = clamped;
            StateChanged?.Invoke();
        }
    }
    public event System.Action StateChanged;

    private ShotRecording shot;
    private ShotRecording loaded;       // PlayRecording's clip, until the next delivery
    private bool playing;
    private bool paused;
    private float speed = 1f;
    private float time;
    private float startTime;
    private float endTime;
    private float holdLeft;
    private bool ringSettled;
    private bool subscribed;
    private Camera viewer;
    private readonly Vector3[] trailBuffer = new Vector3[TrailPoints];
    private readonly Vector3[] ringBuffer = new Vector3[RingPoints];

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        if (trail != null)
            trail.positionCount = TrailPoints;
        if (contactRing != null)
        {
            contactRing.positionCount = RingPoints;
            contactRing.loop = true;
            contactRing.useWorldSpace = false;
        }
        if (ghosts != null)
            ghosts.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable() => Subscribe();

    private void Start()
    {
        if (recorder == null)
            recorder = GetComponent<ShotRecorder>();
        if (main == null)
            main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        Subscribe();
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            if (recorder != null) recorder.Recorded -= OnRecorded;
            if (main != null) main.onGameStateChanged -= OnGameStateChanged;
        }
        subscribed = false;
        Stop();
    }

    private void Subscribe()
    {
        if (subscribed || recorder == null || main == null)
            return;
        recorder.Recorded += OnRecorded;
        main.onGameStateChanged += OnGameStateChanged;
        subscribed = true;
    }

    // ---- Public API (the UI calls these) -------------------------------------------------------

    /// Plays the last delivery from a little before contact (or from release on a miss), looping.
    public void Play()
    {
        if (!HasShot || ghosts == null)
            return;
        shot = Source;
        Frame();
        time = startTime;
        holdLeft = 0f;
        ringSettled = false;
        playing = true;
        paused = false;
        ghosts.SetActive(true);
        Apply();
        StateChanged?.Invoke();
    }

    /// Plays `recording` (a saved record clip, ClipRemap) in place of the last delivery, looping like
    /// any replay, until the next delivery.
    public void PlayRecording(ShotRecording recording)
    {
        loaded = recording;
        Play();
    }

    /// The loop's start and end in the recording. The recording starts in the run-up (for the big
    /// screen); the ghosts start at the release. A recording still running ends where it has got
    /// to, and the loop grows as it does (each pass, Advance).
    private void Frame()
    {
        if (shot.HasContact)
        {
            startTime = Mathf.Max(shot.ReleaseTime, shot.ContactTime - leadIn);
            endTime = Mathf.Min(shot.Duration, shot.ContactTime + followThrough);
        }
        else
        {
            startTime = shot.ReleaseTime;
            endTime = Mathf.Min(shot.Duration, shot.ReleaseTime + leadIn + followThrough);
        }
    }

    public void Stop()
    {
        bool was = playing;
        playing = false;
        paused = false;
        shot = null;
        if (ghosts != null)
            ghosts.SetActive(false);
        if (was)
            StateChanged?.Invoke();
    }

    public void TogglePause()
    {
        if (!playing)
            return;
        paused = !paused;
        StateChanged?.Invoke();
    }

    /// Steps through 0.1, 0.25, 0.5, 1, 1.5, 2x from the preset nearest the current speed,
    /// wrapping at either end so one button can cycle them.
    public void StepSpeed(int dir)
    {
        int nearest = 0;
        for (int i = 1; i < SpeedPresets.Length; i++)
            if (Mathf.Abs(SpeedPresets[i] - speed) < Mathf.Abs(SpeedPresets[nearest] - speed))
                nearest = i;
        int step = dir > 0 ? 1 : dir < 0 ? -1 : 0;
        int n = SpeedPresets.Length;
        Speed = SpeedPresets[((nearest + step) % n + n) % n];
    }

    // ---- Game events ---------------------------------------------------------------------------

    private void OnRecorded()
    {
        // The ball on show finishing recording is the same ball (the recorder's buffer becomes its
        // Last): keep playing, to its full length. A different one (on show from before) is replaced.
        if (playing && loaded == null && shot != null && shot == recorder.Last)
            Frame();
        else if (playing && loaded == null)
            Play();
        StateChanged?.Invoke();
    }

    private void OnGameStateChanged()
    {
        // Ghosts flying about while a real ball is bowled would be confusing: clear them. A real
        // ball is the one to replay from now on, not a loaded clip.
        if (main.gameState != eGameState.InGame_SelectDelivery)
            return;
        loaded = null;
        if (playing)
            Stop();
    }

    // ---- Playback ------------------------------------------------------------------------------

    private void Update()
    {
        if (!playing || shot == null)
            return;
        if (!paused)
            Advance(Time.deltaTime);
        Apply();
    }

    private void Advance(float dt)
    {
        if (holdLeft > 0f)
        {
            holdLeft -= dt;         // the end-of-loop pause is real time, whatever the speed
            if (holdLeft <= 0f)
            {
                time = startTime;
                ringSettled = false;
            }
            return;
        }
        time += dt * speed;
        if (time >= endTime && shot.Duration > endTime + 1e-3f)
            Frame();            // still being recorded: the loop runs on as far as it has got
        if (time >= endTime)
        {
            time = endTime;
            holdLeft = Mathf.Max(endPause, 1e-3f);
        }
    }

    private void Apply()
    {
        shot.Evaluate(time, out Vector3 ball, out Vector3 batPosition, out Quaternion batRotation);
        if (ghostBall != null)
            ghostBall.position = ball;
        if (ghostBat != null)
            ghostBat.SetPositionAndRotation(batPosition, batRotation);
        UpdateTrail();
        UpdateRing();
    }

    /// The last `trailSeconds` of the ball's path, resampled to a fixed point count so the
    /// LineRenderer never resizes; the material fades it toward the tail.
    private void UpdateTrail()
    {
        if (trail == null)
            return;
        float from = Mathf.Max(startTime, time - trailSeconds);
        for (int i = 0; i < TrailPoints; i++)
            trailBuffer[i] = shot.BallAt(Mathf.Lerp(from, time, i / (float)(TrailPoints - 1)));
        trail.SetPositions(trailBuffer);
    }

    /// A ring that opens on the contact point when the replay reaches the moment of contact.
    private void UpdateRing()
    {
        if (contactRing == null)
            return;
        bool show = shot.HasContact && time >= shot.ContactTime;
        contactRing.enabled = show;
        if (!show)
        {
            ringSettled = false;
            return;
        }
        Transform ring = contactRing.transform;
        ring.position = shot.ContactPoint;
        if (viewer == null)
            viewer = Camera.main;
        if (viewer != null)
            ring.rotation = Quaternion.LookRotation(ring.position - viewer.transform.position);

        float age = Mathf.Clamp01((time - shot.ContactTime) / Mathf.Max(pulseSeconds, 1e-3f));
        float eased = 1f - (1f - age) * (1f - age);
        Color c = contactRing.startColor;
        c.a = Mathf.Lerp(1f, 0.45f, eased);
        contactRing.startColor = c;
        contactRing.endColor = c;
        if (ringSettled)
            return;
        float radius = Mathf.Lerp(ringRadius * 0.2f, ringRadius, eased);
        for (int i = 0; i < RingPoints; i++)
        {
            float a = i * (2f * Mathf.PI / RingPoints);
            ringBuffer[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        contactRing.SetPositions(ringBuffer);
        ringSettled = age >= 1f;
    }
}
