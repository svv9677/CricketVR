using UnityEngine;

/// <summary>
/// One recorded delivery, the single source for both replays: the holographic one (ShotReplay,
/// a ghost bat and ball) and the big screen (CameraReplay, every player, the ball, the bat and
/// the stumps rebuilt on stand-ins and filmed by the replay camera).
///
/// Two tracks, both rings that drop their oldest entries only before the release (the pre-roll,
/// so the screen can show the bowler's last strides) and never after it:
///   * the ball and the bat at a fixed rate, sampled by interpolation, so a lookup is an index;
///   * everything else (ReplayActors) as poses at a lower rate, stamped with the time they were
///     taken (PoseTrack).
/// Times are seconds from the first sample still held.
/// </summary>
public sealed class ShotRecording
{
    public struct Sample
    {
        public Vector3 ball;
        public Vector3 batPosition;
        public Quaternion batRotation;
    }   // 40 bytes

    private readonly Sample[] samples;
    private int first;

    public int Count { get; private set; }
    public readonly float Interval;
    /// Poses of the players and the stumps; null when there is nothing to pose (the nets).
    public readonly PoseTrack Poses;

    /// Time.time of the first sample still held.
    public float StartedAt { get; private set; }
    /// Seconds from Begin to the first sample still held: what the pre-roll has dropped.
    private float dropped;

    public bool Released;
    public float ReleaseTime;       // when the bowler let go of it
    public bool HasContact;
    public float ContactTime;
    public Vector3 ContactPoint;    // where the ball was when the bat struck it

    public ShotRecording(int capacity, float interval, PoseTrack poses)
    {
        samples = new Sample[capacity];
        Interval = interval;
        Poses = poses;
    }

    public int Capacity => samples.Length;
    public float Duration => Count > 1 ? (Count - 1) * Interval : 0f;

    public void Clear(float startedAt)
    {
        first = 0;
        Count = 0;
        dropped = 0f;
        StartedAt = startedAt;
        Released = false;
        ReleaseTime = 0f;
        HasContact = false;
        ContactTime = 0f;
        ContactPoint = Vector3.zero;
        Poses?.Clear();
    }

    public Sample this[int index] => samples[(first + index) % samples.Length];

    /// Appends a sample; false when full.
    public bool Add(Sample sample)
    {
        if (Count >= samples.Length)
            return false;
        samples[(first + Count) % samples.Length] = sample;
        Count++;
        return true;
    }

    /// Before the release: forget everything older than `keep` seconds before the newest sample.
    public void DropOlderThan(float keep)
    {
        int over = Count - (Mathf.CeilToInt(keep / Interval) + 1);
        if (over <= 0)
            return;
        first = (first + over) % samples.Length;
        Count -= over;
        dropped += over * Interval;
        StartedAt += over * Interval;
        Poses?.DropBefore(dropped);
    }

    /// Seconds from Begin (what the recorder stamps poses with) for a time on this recording.
    public float SinceBegin(float time) => time + dropped;

    /// Ball and bat at `time`, interpolated between the two nearest samples so slow motion is
    /// smooth rather than stepping at the sample rate.
    public void Evaluate(float time, out Vector3 ball, out Vector3 batPosition, out Quaternion batRotation)
    {
        Locate(time, out int i, out float f);
        Sample a = this[i], b = this[i + 1 < Count ? i + 1 : i];
        ball = Vector3.LerpUnclamped(a.ball, b.ball, f);
        batPosition = Vector3.LerpUnclamped(a.batPosition, b.batPosition, f);
        batRotation = Quaternion.Slerp(a.batRotation, b.batRotation, f);
    }

    public Vector3 BallAt(float time)
    {
        Locate(time, out int i, out float f);
        return Vector3.LerpUnclamped(this[i].ball, this[i + 1 < Count ? i + 1 : i].ball, f);
    }

    private void Locate(float time, out int index, out float fraction)
    {
        float x = Mathf.Clamp(time / Interval, 0f, Mathf.Max(0, Count - 1));
        index = Mathf.Min((int)x, Mathf.Max(0, Count - 1));
        fraction = x - index;
    }
}

/// <summary>
/// Poses of the replay's actors (ReplayActors) over a delivery: a ring of time-stamped frames,
/// each a fixed block of rotations and positions laid out by ReplayActors. Preallocated; nothing
/// is allocated while recording or playing.
/// </summary>
public sealed class PoseTrack
{
    public readonly int Rotations, Positions;
    public readonly Quaternion[] rotations;
    public readonly Vector3[] positions;
    private readonly float[] times;     // seconds from the recording's Begin
    private int first;

    public int Count { get; private set; }

    public PoseTrack(int capacity, int rotationsPerPose, int positionsPerPose)
    {
        Rotations = rotationsPerPose;
        Positions = positionsPerPose;
        times = new float[capacity];
        rotations = new Quaternion[capacity * rotationsPerPose];
        positions = new Vector3[capacity * positionsPerPose];
    }

    public void Clear()
    {
        first = 0;
        Count = 0;
    }

    /// The slot for a pose taken at `time` (fill rotations[slot * Rotations ...] and
    /// positions[slot * Positions ...]), or -1 when full.
    public int Add(float time)
    {
        if (Count >= times.Length)
            return -1;
        int slot = (first + Count) % times.Length;
        times[slot] = time;
        Count++;
        return slot;
    }

    /// Forget poses before `time`, keeping the last one at or before it to blend from.
    public void DropBefore(float time)
    {
        while (Count > 1 && times[(first + 1) % times.Length] <= time)
        {
            first = (first + 1) % times.Length;
            Count--;
        }
    }

    /// The slots either side of `time` and how far between them it is.
    public bool Locate(float time, out int slotA, out int slotB, out float blend)
    {
        slotA = slotB = 0;
        blend = 0f;
        if (Count == 0)
            return false;
        int lo = 0, hi = Count - 1;
        if (time <= At(lo)) { hi = lo; }
        else if (time >= At(hi)) { lo = hi; }
        else
        {
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (At(mid) <= time) lo = mid;
                else hi = mid;
            }
            float span = At(hi) - At(lo);
            blend = span > 1e-6f ? (time - At(lo)) / span : 0f;
        }
        slotA = (first + lo) % times.Length;
        slotB = (first + hi) % times.Length;
        return true;
    }

    private float At(int index) => times[(first + index) % times.Length];
}
