using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>What a record clip says about itself: which record and shot it is, the match it came
/// from (so the theatre can set the same ground, light, crowd and hand), and the delivery's release
/// and contact, in the clip's own time. Saved as JSON at the head of the file (ClipCodec), so
/// ReadHeader can answer without reading the poses.</summary>
[Serializable]
public class ClipHeader
{
    public int format = ClipCodec.Format;
    /// The clip's name on disk and in the record book (RecordEntry.clipStamp).
    public string stamp = "";
    public eRecord record = eRecord.LongestSix;
    /// The record's value: for the longest six, its real-blade carry in metres.
    public float value;
    public string team = "", teamShort = "", opponent = "", date = "";
    public eRung rung;
    public int season;
    public eVenue venue = eVenue.Stadium;
    public eTimeOfDay time = eTimeOfDay.Afternoon;
    public eCrowd crowd = eCrowd.Empty;
    public ePitch pitch = ePitch.Flat;
    public eBowlingLevel bowling = eBowlingLevel.Easy;
    public eBattingStyle hand = eBattingStyle.RightHanded;
    public int overs = 6;
    public bool released, hasContact;
    public float releaseTime, contactTime;
    public Vector3 contactPoint;
}

/// <summary>A record clip read back (or about to be written): the ball, bat and head samples, and the
/// players' poses with the name of every channel (ReplayActors.ChannelKeys), in the layout of the
/// scene it was recorded in. ClipRemap puts the poses into the layout of the scene playing it.</summary>
public sealed class SavedClip
{
    public ClipHeader header;
    public float interval;
    public ShotRecording.Sample[] samples;
    public string[] rotKeys, posKeys;
    /// Seconds from the clip's first sample, one per pose.
    public float[] poseTimes;
    /// Pose after pose: rotKeys.Length rotations, then posKeys.Length positions, each.
    public Quaternion[] rotations;
    public Vector3[] positions;

    public int Poses => poseTimes != null ? poseTimes.Length : 0;

    /// The ball, bat and head as a recording the replays can play, with `poses` (null: the hologram
    /// only). Release and contact come from the header.
    public ShotRecording ToRecording(PoseTrack poses = null)
    {
        var s = new ShotRecording(samples.Length, interval, poses);
        s.Clear(0f);
        foreach (ShotRecording.Sample sample in samples)
            s.Add(sample);
        s.Released = header.released;
        s.ReleaseTime = header.releaseTime;
        s.HasContact = header.hasContact;
        s.ContactTime = header.contactTime;
        s.ContactPoint = header.contactPoint;
        return s;
    }
}

/// <summary>
/// Record clips to and from bytes. Format 1:
///   "CVRC", u16 format, i32 header length, the header (UTF-8 JSON, ClipHeader);
///   f32 interval, i32 samples, each sample 17 floats (ball, bat position, bat rotation, head
///   position, head rotation); i32 + the rotation keys, i32 + the position keys (BinaryWriter
///   strings); i32 poses, their times; then every pose's rotations (4 floats) and positions (3).
/// Every number is little-endian (BinaryWriter always is) and raw float32: about 3-4 MB a clip,
/// measured on the first device run. Quantising and compressing are format 2's job.
///
/// The clip is the shot, not the whole recording: from 1.2 s before the release (the big screen's
/// lead-in, CameraReplay) for at most 8 s, with its times re-based to start at 0.
///
/// Capture copies on the main thread; Pack and Unpack touch nothing of Unity's but its structs, so
/// they run on a worker thread. Reading never throws: a short, corrupt or unknown file is null.
/// </summary>
public static class ClipCodec
{
    public const int Format = 1;
    public const float LeadIn = 1.2f, MaxSeconds = 8f;
    private const int SampleFloats = 17;
    private static readonly byte[] Magic = { (byte)'C', (byte)'V', (byte)'R', (byte)'C' };

    /// The shot's window of `s`, copied (main thread: the recorder reuses its recordings). The keys
    /// name the pose channels in `s.Poses`' layout; when they do not fit it, the clip has no poses.
    /// Null when there is nothing to keep.
    public static SavedClip Capture(ShotRecording s, IList<string> rotKeys, IList<string> posKeys, ClipHeader h)
    {
        if (s == null || h == null || s.Count < 2 || s.Interval <= 0f)
            return null;
        float from = s.Released ? Mathf.Max(0f, s.ReleaseTime - LeadIn) : 0f;
        int first = Mathf.Clamp(Mathf.FloorToInt(from / s.Interval), 0, s.Count - 2);
        int count = Mathf.Min(s.Count - first, Mathf.FloorToInt(MaxSeconds / s.Interval) + 1);
        float offset = first * s.Interval;
        var clip = new SavedClip { header = h, interval = s.Interval, samples = new ShotRecording.Sample[count] };
        for (int i = 0; i < count; i++)
            clip.samples[i] = s[first + i];
        h.format = Format;
        h.released = s.Released;
        h.releaseTime = s.ReleaseTime - offset;
        h.hasContact = s.HasContact;
        h.contactTime = s.HasContact ? s.ContactTime - offset : 0f;
        h.contactPoint = s.ContactPoint;
        CapturePoses(s, rotKeys, posKeys, clip, s.SinceBegin(offset), (count - 1) * s.Interval);
        return clip;
    }

    /// The poses from the last one at or before the clip's start to the first at or after its end
    /// (`zero` is the clip's start in the pose track's time).
    private static void CapturePoses(ShotRecording s, IList<string> rotKeys, IList<string> posKeys, SavedClip clip, float zero, float end)
    {
        PoseTrack p = s.Poses;
        bool fits = p != null && p.Count > 0 && rotKeys != null && posKeys != null &&
                    rotKeys.Count == p.Rotations && posKeys.Count == p.Positions && p.Rotations > 0;
        clip.rotKeys = fits ? new List<string>(rotKeys).ToArray() : new string[0];
        clip.posKeys = fits ? new List<string>(posKeys).ToArray() : new string[0];
        if (!fits)
        {
            clip.poseTimes = new float[0];
            clip.rotations = new Quaternion[0];
            clip.positions = new Vector3[0];
            return;
        }
        int lo = 0, hi = p.Count - 1;
        while (lo + 1 < p.Count && p.TimeAt(lo + 1) - zero <= 0f) lo++;
        for (int i = lo; i < p.Count; i++)
            if (p.TimeAt(i) - zero >= end) { hi = i; break; }
        int n = hi - lo + 1, r = p.Rotations, v = p.Positions;
        clip.poseTimes = new float[n];
        clip.rotations = new Quaternion[n * r];
        clip.positions = new Vector3[n * v];
        for (int j = 0; j < n; j++)
        {
            int slot = p.SlotAt(lo + j);
            clip.poseTimes[j] = p.TimeAt(lo + j) - zero;
            Array.Copy(p.rotations, slot * r, clip.rotations, j * r, r);
            Array.Copy(p.positions, slot * v, clip.positions, j * v, v);
        }
    }

    /// The whole clip as bytes (main thread: the header goes through JsonUtility).
    public static byte[] Encode(ShotRecording s, IList<string> rotKeys, IList<string> posKeys, ClipHeader h)
    {
        SavedClip clip = Capture(s, rotKeys, posKeys, h);
        return clip != null ? Pack(HeaderJson(clip.header), clip) : null;
    }

    public static string HeaderJson(ClipHeader h) => JsonUtility.ToJson(h ?? new ClipHeader());

    /// The clip as bytes, with `headerJson` as its header. Any thread.
    public static byte[] Pack(string headerJson, SavedClip c)
    {
        if (c?.samples == null || c.samples.Length < 2)
            return null;
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, true))
        {
            w.Write(Magic);
            w.Write((ushort)Format);
            byte[] json = Encoding.UTF8.GetBytes(headerJson ?? "{}");
            w.Write(json.Length);
            w.Write(json);
            w.Write(c.interval);
            w.Write(c.samples.Length);
            foreach (ShotRecording.Sample s in c.samples)
                WriteSample(w, s);
            WriteKeys(w, c.rotKeys);
            WriteKeys(w, c.posKeys);
            int poses = c.Poses;
            w.Write(poses);
            for (int i = 0; i < poses; i++)
                w.Write(c.poseTimes[i]);
            foreach (Quaternion q in c.rotations) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
            foreach (Vector3 p in c.positions) { w.Write(p.x); w.Write(p.y); w.Write(p.z); }
        }
        return ms.ToArray();
    }

    private static void WriteSample(BinaryWriter w, ShotRecording.Sample s)
    {
        Write(w, s.ball);
        Write(w, s.batPosition);
        Write(w, s.batRotation);
        Write(w, s.headPosition);
        Write(w, s.headRotation);
    }

    private static void Write(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }

    private static void Write(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }

    private static void WriteKeys(BinaryWriter w, string[] keys)
    {
        w.Write(keys != null ? keys.Length : 0);
        if (keys != null)
            foreach (string k in keys)
                w.Write(k ?? "");
    }

    // ---- Reading -------------------------------------------------------------------------------

    /// The header alone (the poses are not read). Null for anything that is not a format-1 clip.
    public static ClipHeader ReadHeader(byte[] data)
    {
        string json = HeaderText(data, out _);
        if (json == null)
            return null;
        try { return JsonUtility.FromJson<ClipHeader>(json); }
        catch { return null; }
    }

    /// The whole clip, or null.
    public static SavedClip Decode(byte[] data)
    {
        ClipHeader h = ReadHeader(data);
        return h != null ? Unpack(data, h) : null;
    }

    /// The header's JSON and where the body starts; null when the bytes are not a format-1 clip.
    private static string HeaderText(byte[] data, out int bodyAt)
    {
        bodyAt = 0;
        if (data == null || data.Length < 10)
            return null;
        for (int i = 0; i < Magic.Length; i++)
            if (data[i] != Magic[i])
                return null;
        if (BitConverter.ToUInt16(Little(data, 4, 2), 0) != Format)
            return null;
        int length = BitConverter.ToInt32(Little(data, 6, 4), 0);
        if (length < 0 || length > data.Length - 10)
            return null;
        bodyAt = 10 + length;
        try { return Encoding.UTF8.GetString(data, 10, length); }
        catch { return null; }
    }

    /// `n` bytes from `at`, in this machine's order (the file's is little-endian).
    private static byte[] Little(byte[] data, int at, int n)
    {
        var b = new byte[n];
        Array.Copy(data, at, b, 0, n);
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(b);
        return b;
    }

    /// The body of `data` under `header` (already read). Any thread; null on a bad body.
    public static SavedClip Unpack(byte[] data, ClipHeader header)
    {
        if (header == null || HeaderText(data, out int bodyAt) == null)
            return null;
        try
        {
            using var r = new BinaryReader(new MemoryStream(data, bodyAt, data.Length - bodyAt, false), Encoding.UTF8);
            var c = new SavedClip { header = header, interval = r.ReadSingle() };
            int samples = Count(r, SampleFloats * 4);
            if (!(c.interval > 0f) || float.IsInfinity(c.interval) || samples < 2)
                return null;
            c.samples = new ShotRecording.Sample[samples];
            for (int i = 0; i < samples; i++)
                c.samples[i] = ReadSample(r);
            c.rotKeys = ReadKeys(r);
            c.posKeys = ReadKeys(r);
            int poses = Count(r, 4 + c.rotKeys.Length * 16 + c.posKeys.Length * 12);
            c.poseTimes = new float[poses];
            for (int i = 0; i < poses; i++)
                c.poseTimes[i] = r.ReadSingle();
            c.rotations = new Quaternion[poses * c.rotKeys.Length];
            for (int i = 0; i < c.rotations.Length; i++)
                c.rotations[i] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            c.positions = new Vector3[poses * c.posKeys.Length];
            for (int i = 0; i < c.positions.Length; i++)
                c.positions[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            return c;
        }
        catch { return null; }
    }

    /// A count of things `each` bytes long that must fit in what is left (a corrupt count would
    /// otherwise ask for gigabytes before the read fails).
    private static int Count(BinaryReader r, long each)
    {
        int n = r.ReadInt32();
        long left = r.BaseStream.Length - r.BaseStream.Position;
        if (n < 0 || n * Math.Max(1L, each) > left)
            throw new InvalidDataException("count past the end");
        return n;
    }

    private static ShotRecording.Sample ReadSample(BinaryReader r) => new ShotRecording.Sample
    {
        ball = ReadVector(r),
        batPosition = ReadVector(r),
        batRotation = ReadQuaternion(r),
        headPosition = ReadVector(r),
        headRotation = ReadQuaternion(r),
    };

    private static Vector3 ReadVector(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    private static Quaternion ReadQuaternion(BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    private static string[] ReadKeys(BinaryReader r)
    {
        int n = Count(r, 1);
        var keys = new string[n];
        for (int i = 0; i < n; i++)
            keys[i] = r.ReadString();
        return keys;
    }
}
