using UnityEngine;

/// <summary>
/// Records each delivery once, for both replays (ShotRecording): the holographic shot replay
/// (ShotReplay) and the big screen (CameraReplay). Starts with the run-up, keeping only the last
/// `preRoll` seconds before the release; marks the release and the contact; stops shortly after
/// the ball is dead.
///
/// Sized for the Quest, with nothing allocated after Start:
///   * two recordings (the delivery being recorded, and the last finished one, swapped on
///     finish), each a fixed-capacity ring;
///   * the ball and bat at `sampleRate`, by interpolating between this frame and the last, so
///     72/90 Hz frames and hitches both give evenly spaced samples;
///   * the players and stumps (ReplayActors) at `poseRate`, as they stand on the frame each pose
///     is due, stamped with its time - a few hundred transform reads every other frame or so,
///     where the old screen replay rendered the whole stadium a second time;
///   * runs after Bat's LateUpdate (execution order), so the bat is where the player saw it, and
///     reads the ball's interpolated transform rather than the physics body for the same reason.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ShotRecorder : MonoBehaviour
{
    public static ShotRecorder Instance { get; private set; }

    [SerializeField]
    private Main main;
    [Tooltip("Ball and bat samples per second. 60 is plenty: playback interpolates between samples.")]
    [SerializeField]
    private float sampleRate = 60f;
    [Tooltip("Player and stump poses per second, for the big screen.")]
    [SerializeField]
    private float poseRate = 30f;
    [Tooltip("Longest delivery kept, release to dead ball. Recording stops when full.")]
    [SerializeField]
    private float maxSeconds = 12f;
    [Tooltip("Seconds of the run-up kept from before the release.")]
    [SerializeField]
    private float preRoll = 2f;
    [Tooltip("Keep recording this long after the ball is dead (fielded, bowled, boundary...).")]
    [SerializeField]
    private float deadBallTail = 0.5f;

    /// The last finished delivery, or null before the first one.
    public ShotRecording Last { get; private set; }
    /// The delivery being recorded if there is one, else the last finished one.
    public ShotRecording Latest => active ? current : Last;
    /// Raised when a delivery finishes recording and becomes Last.
    public event System.Action Recorded;
    /// The players, ball, bat and stumps, with the stand-ins the big screen films.
    public ReplayActors Actors { get; private set; }

    public bool IsRecording => active;

    private ShotRecording current;
    private ShotRecording spare;
    private bool active;
    private bool posing;
    private bool subscribed;
    private float startTime;
    private float stopAt;
    private float keepUntil;
    private float nextTick;
    private float nextPose;
    private bool havePrevious;
    private float previousTime;
    private ShotRecording.Sample previous;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        if (main == null)
            main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        Allocate();
        Subscribe();
    }

    /// After every other Start (execution order), so the players are set up before they are copied.
    private void Allocate()
    {
        if (main != null)
            Actors = ReplayActors.Build(main);
        float interval = 1f / Mathf.Max(1f, sampleRate);
        float seconds = maxSeconds + preRoll;
        int capacity = Mathf.CeilToInt(seconds * sampleRate) + 2;
        int poses = Mathf.CeilToInt(seconds * poseRate * 1.1f) + 2;   // on schedule on average (Pose), with a margin
        current = new ShotRecording(capacity, interval, PoseTrackFor(poses));
        spare = new ShotRecording(capacity, interval, PoseTrackFor(poses));
    }

    private PoseTrack PoseTrackFor(int capacity) =>
        Actors != null && Actors.Rotations > 0 ? new PoseTrack(capacity, Actors.Rotations, Actors.Positions) : null;

    private void OnEnable() => Subscribe();

    private void OnDisable()
    {
        if (subscribed && main != null)
            main.onGameStateChanged -= OnGameStateChanged;
        subscribed = false;
        active = false;
    }

    private void Subscribe()
    {
        if (subscribed || main == null || current == null)
            return;
        main.onGameStateChanged += OnGameStateChanged;
        subscribed = true;
    }

    private void OnGameStateChanged()
    {
        switch (main.gameState)
        {
            case eGameState.InGame_SelectDeliveryLoop:  // the run-up
                Begin();
                break;
            case eGameState.InGame_DeliverBallLoop:     // the ball is on its release point, moving
                MarkRelease();
                break;
            case eGameState.InGame_BallHit:             // raised from Bat.OnBallHit, mid-LateUpdate
                MarkContact();
                break;
            case eGameState.InGame_BallFielded:
            case eGameState.InGame_BallMissed:
            case eGameState.InGame_Bowled:
            case eGameState.InGame_BallPastBoundary:
                if (active)
                    stopAt = Mathf.Min(stopAt, Time.time - startTime + deadBallTail);
                break;
            case eGameState.InGame_ResetToReady:
            case eGameState.InGame_Ready:
                // Done - once the big screen has as much as it asked for (RecordAtLeastUntil).
                if (active)
                    stopAt = Mathf.Min(stopAt, Time.time - startTime);
                break;
            case eGameState.InGame_SelectDelivery:     // the next ball: whatever is left over
                if (active)
                    Finish();
                break;
        }
    }

    /// Keep recording until at least `time` (Time.time), dead ball or not: the big screen's
    /// replay runs on past the end of the play.
    public void RecordAtLeastUntil(float time)
    {
        if (active)
            keepUntil = Mathf.Max(keepUntil, time - startTime);
    }

    private void Begin()
    {
        startTime = Time.time;
        current.Clear(startTime);
        stopAt = float.MaxValue;
        keepUntil = 0f;
        nextTick = 0f;
        nextPose = 0f;
        havePrevious = false;
        posing = current.Poses != null && Actors.StillLive();
        active = true;
    }

    private void MarkRelease()
    {
        if (!active)
            Begin();    // no run-up seen (a scene without one): from the release, then
        current.Released = true;
        current.ReleaseTime = Time.time - current.StartedAt;
    }

    private void MarkContact()
    {
        if (!active || !current.Released || current.HasContact || main.theBall == null)
            return;
        current.HasContact = true;
        current.ContactTime = Time.time - current.StartedAt;
        // Bat.TrySweep has just put the ball back on the face of the bat.
        current.ContactPoint = main.theBall.transform.position;
    }

    private void LateUpdate()
    {
        if (!active)
            return;
        if (main == null || main.theBall == null || main.theBat == null)
        {
            active = false;
            return;
        }
        // Backstop for the event: the Nets scene or a future path may set hasHitBall directly.
        if (!current.HasContact && main.theBatScript != null && main.theBatScript.hasHitBall)
            MarkContact();

        float now = Time.time - startTime;
        Transform bat = main.theBat.transform;
        Camera eye = Camera.main;
        var sample = new ShotRecording.Sample
        {
            ball = main.theBall.transform.position,
            batPosition = bat.position,
            batRotation = bat.rotation,
            // The head the batter plays the ball with: shown in the replay as a helmet.
            headPosition = eye != null ? eye.transform.position : Vector3.zero,
            headRotation = eye != null ? eye.transform.rotation : new Quaternion(0f, 0f, 0f, 0f),
        };
        if (!Record(now, sample) || !Pose(now))
        {
            Finish();   // full: keep what we have rather than overwrite the contact
            return;
        }
        if (!current.Released)
            current.DropOlderThan(preRoll);
        else if (now >= stopAt && now >= keepUntil)
            Finish();
    }

    /// Write every fixed-rate tick that falls between the previous frame and this one.
    private bool Record(float now, ShotRecording.Sample sample)
    {
        if (!havePrevious)
        {
            current.Add(sample);
            nextTick = current.Interval;
        }
        else
        {
            float span = now - previousTime;
            while (nextTick <= now)
            {
                float f = span > 1e-6f ? (nextTick - previousTime) / span : 1f;
                bool added = current.Add(new ShotRecording.Sample
                {
                    ball = Vector3.LerpUnclamped(previous.ball, sample.ball, f),
                    batPosition = Vector3.LerpUnclamped(previous.batPosition, sample.batPosition, f),
                    batRotation = Quaternion.Slerp(previous.batRotation, sample.batRotation, f),
                    headPosition = Vector3.LerpUnclamped(previous.headPosition, sample.headPosition, f),
                    headRotation = sample.headRotation.w == 0f && sample.headRotation.x == 0f && sample.headRotation.y == 0f && sample.headRotation.z == 0f
                        ? sample.headRotation : Quaternion.Slerp(previous.headRotation, sample.headRotation, f),
                });
                if (!added)
                    return false;
                nextTick += current.Interval;
            }
        }
        previous = sample;
        previousTime = now;
        havePrevious = true;
        return true;
    }

    /// The players and stumps, when a pose is due: on schedule on average, every second or third
    /// frame at 72 Hz.
    private bool Pose(float now)
    {
        if (!posing || now < nextPose)
            return true;
        int slot = current.Poses.Add(now);
        if (slot < 0)
            return false;
        Actors.Capture(current.Poses, slot);
        nextPose += 1f / Mathf.Max(1f, poseRate);
        if (nextPose <= now)
            nextPose = now + 1f / Mathf.Max(1f, poseRate);   // after a hitch, not a burst
        return true;
    }

    private void Finish()
    {
        active = false;
        if (current.Count < 2 || !current.Released)
            return;     // never released (the run-up was abandoned): nothing to replay
        // Swap: the finished delivery becomes Last, the old Last is recycled for the next one.
        // Both replays stop before the next delivery starts, so nothing is reading the spare.
        ShotRecording finished = current;
        current = Last ?? spare;
        Last = finished;
        Recorded?.Invoke();
    }
}
