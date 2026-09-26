using UnityEngine;

/// <summary>
/// One recorded delivery: the ball and the bat at a fixed rate from release until the ball is
/// dead, plus the moment of contact. Samples are evenly spaced (see ShotRecorder), so no
/// timestamps are stored and a lookup is an index, not a search.
/// </summary>
public sealed class ShotRecording
{
    public struct Sample
    {
        public Vector3 ball;
        public Vector3 batPosition;
        public Quaternion batRotation;
    }   // 40 bytes

    public readonly Sample[] samples;
    public int Count;
    public float Interval;
    public bool HasContact;
    public float ContactTime;       // seconds from release
    public Vector3 ContactPoint;    // where the ball was when the bat struck it

    public ShotRecording(int capacity, float interval)
    {
        samples = new Sample[capacity];
        Interval = interval;
    }

    public float Duration => Count > 1 ? (Count - 1) * Interval : 0f;

    public void Clear()
    {
        Count = 0;
        HasContact = false;
        ContactTime = 0f;
        ContactPoint = Vector3.zero;
    }

    /// Ball and bat at `time` seconds from release, interpolated between the two nearest samples
    /// so slow motion is smooth rather than stepping at the sample rate.
    public void Evaluate(float time, out Vector3 ball, out Vector3 batPosition, out Quaternion batRotation)
    {
        Locate(time, out int i, out float f);
        Sample a = samples[i], b = samples[i + 1 < Count ? i + 1 : i];
        ball = Vector3.LerpUnclamped(a.ball, b.ball, f);
        batPosition = Vector3.LerpUnclamped(a.batPosition, b.batPosition, f);
        batRotation = Quaternion.Slerp(a.batRotation, b.batRotation, f);
    }

    public Vector3 BallAt(float time)
    {
        Locate(time, out int i, out float f);
        return Vector3.LerpUnclamped(samples[i].ball, samples[i + 1 < Count ? i + 1 : i].ball, f);
    }

    private void Locate(float time, out int index, out float fraction)
    {
        float x = Mathf.Clamp(time / Interval, 0f, Mathf.Max(0, Count - 1));
        index = Mathf.Min((int)x, Mathf.Max(0, Count - 1));
        fraction = x - index;
    }
}

/// <summary>
/// Records each delivery for the holographic shot replay (ShotReplay). Starts when the ball is
/// released, marks the contact, stops shortly after the ball is dead.
///
/// Sized for the Quest, with nothing allocated after Awake:
///   * two fixed-capacity sample arrays (the delivery being recorded, and the last finished one,
///     swapped on finish), 12 s x 60 Hz = 720 samples x 40 bytes = 28 KB each, 56 KB in all;
///   * sampled at a fixed rate by interpolating between this frame and the last, so 72/90 Hz
///     frames and hitches both give evenly spaced samples;
///   * runs after Bat's LateUpdate (execution order), so the bat is where the player saw it, and
///     reads the ball's interpolated transform rather than the physics body for the same reason.
/// </summary>
[DefaultExecutionOrder(1000)]
public class ShotRecorder : MonoBehaviour
{
    [SerializeField]
    private Main main;
    [Tooltip("Samples per second. 60 is plenty: playback interpolates between samples.")]
    [SerializeField]
    private float sampleRate = 60f;
    [Tooltip("Longest delivery kept, release to dead ball. Recording stops when full.")]
    [SerializeField]
    private float maxSeconds = 12f;
    [Tooltip("Keep recording this long after the ball is dead (fielded, bowled, boundary...).")]
    [SerializeField]
    private float deadBallTail = 0.5f;

    /// The last finished delivery, or null before the first one.
    public ShotRecording Last { get; private set; }
    /// Raised when a delivery finishes recording and becomes Last.
    public event System.Action Recorded;

    public bool IsRecording => active;

    private ShotRecording current;
    private ShotRecording spare;
    private bool active;
    private bool subscribed;
    private float startTime;
    private float stopAt;
    private float nextTick;
    private bool havePrevious;
    private float previousTime;
    private ShotRecording.Sample previous;

    private void Awake()
    {
        float interval = 1f / Mathf.Max(1f, sampleRate);
        int capacity = Mathf.CeilToInt(maxSeconds * sampleRate) + 1;
        current = new ShotRecording(capacity, interval);
        spare = new ShotRecording(capacity, interval);
    }

    private void Start()
    {
        if (main == null)
            main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        Subscribe();
    }

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
        if (subscribed || main == null)
            return;
        main.onGameStateChanged += OnGameStateChanged;
        subscribed = true;
    }

    private void OnGameStateChanged()
    {
        switch (main.gameState)
        {
            case eGameState.InGame_DeliverBallLoop:     // the ball is on its release point, moving
                Begin();
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
            case eGameState.InGame_SelectDelivery:
                if (active)
                    Finish();
                break;
        }
    }

    private void Begin()
    {
        current.Clear();
        startTime = Time.time;
        stopAt = float.MaxValue;
        nextTick = 0f;
        havePrevious = false;
        active = true;
    }

    private void MarkContact()
    {
        if (!active || current.HasContact || main.theBall == null)
            return;
        current.HasContact = true;
        current.ContactTime = Time.time - startTime;
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
        var sample = new ShotRecording.Sample
        {
            ball = main.theBall.transform.position,
            batPosition = bat.position,
            batRotation = bat.rotation,
        };
        Record(now, sample);

        if (active && now >= stopAt)
            Finish();
    }

    /// Write every fixed-rate tick that falls between the previous frame and this one.
    private void Record(float now, ShotRecording.Sample sample)
    {
        ShotRecording.Sample[] buffer = current.samples;
        if (!havePrevious)
        {
            buffer[0] = sample;
            current.Count = 1;
            nextTick = current.Interval;
        }
        else
        {
            float span = now - previousTime;
            while (nextTick <= now)
            {
                if (current.Count >= buffer.Length)
                {
                    Finish();   // full: keep what we have rather than overwrite the contact
                    return;
                }
                float f = span > 1e-6f ? (nextTick - previousTime) / span : 1f;
                buffer[current.Count++] = new ShotRecording.Sample
                {
                    ball = Vector3.LerpUnclamped(previous.ball, sample.ball, f),
                    batPosition = Vector3.LerpUnclamped(previous.batPosition, sample.batPosition, f),
                    batRotation = Quaternion.Slerp(previous.batRotation, sample.batRotation, f),
                };
                nextTick += current.Interval;
            }
        }
        previous = sample;
        previousTime = now;
        havePrevious = true;
    }

    private void Finish()
    {
        active = false;
        if (current.Count < 2)
            return;
        // Swap: the finished delivery becomes Last, the old Last is recycled for the next one.
        // ShotReplay stops before the next delivery starts, so nothing is reading the spare.
        ShotRecording finished = current;
        current = Last ?? spare;
        Last = finished;
        Recorded?.Invoke();
    }
}
