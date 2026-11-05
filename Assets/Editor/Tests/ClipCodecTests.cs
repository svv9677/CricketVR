using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Record clips to bytes and back (ClipCodec, format 1): every sample, key and pose comes back
/// exactly, the clip is the shot's window (1.2 s before the release, at most 8 s) with its times
/// re-based to 0, the header reads on its own, and anything short, corrupt or of another format
/// reads as null without throwing.
/// </summary>
public class ClipCodecTests
{
    internal const float Interval = 1f / 60f;
    internal static readonly string[] RotKeys = { "W:Bowler", "B:Bowler/Hips", "B:Bowler/Hips/Spine" };
    internal static readonly string[] PosKeys = { "W:Bowler", "P:Bowler/Hips" };

    /// A recording as ShotRecorder makes one: `seconds` of 60 Hz samples after `preRoll` seconds that
    /// the pre-roll then dropped, released `release` s and struck `contact` s into what is kept (no
    /// contact when negative), with 30 Hz poses stamped from Begin. Sample i's ball is at x = i (from
    /// Begin); pose channel c of a pose stamped t is (t, c, 0.5, 1) or (t, c, -1).
    internal static ShotRecording Recording(float seconds, float release, float contact, float preRoll = 0.5f, bool poses = true)
    {
        int kept = Mathf.RoundToInt(seconds / Interval) + 1, dropped = Mathf.RoundToInt(preRoll / Interval);
        var track = poses ? new PoseTrack(Mathf.CeilToInt((seconds + preRoll) * 30f) + 4, RotKeys.Length, PosKeys.Length) : null;
        var s = new ShotRecording(kept + dropped + 2, Interval, track);
        s.Clear(100f);
        float nextPose = 0f;
        for (int i = 0; i < kept + dropped; i++)
        {
            float t = i * Interval;
            s.Add(new ShotRecording.Sample
            {
                ball = new Vector3(i, 1f, 2f), batPosition = new Vector3(0.1f * i, 3f, 4f),
                batRotation = new Quaternion(0f, 0.6f, 0f, 0.8f), headPosition = new Vector3(5f, 1.7f, i),
                headRotation = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
            });
            if (track != null && t >= nextPose - 1e-5f)
            {
                int slot = track.Add(t);
                for (int c = 0; c < RotKeys.Length; c++) track.rotations[slot * RotKeys.Length + c] = new Quaternion(t, c, 0.5f, 1f);
                for (int c = 0; c < PosKeys.Length; c++) track.positions[slot * PosKeys.Length + c] = new Vector3(t, c, -1f);
                nextPose += 1f / 30f;
            }
        }
        if (dropped > 0)
            s.DropOlderThan((kept - 1) * Interval);
        s.Released = true;
        s.ReleaseTime = release;
        s.HasContact = contact >= 0f;
        s.ContactTime = Mathf.Max(0f, contact);
        s.ContactPoint = new Vector3(0.2f, 0.9f, -9.5f);
        return s;
    }

    internal static ClipHeader Header(string stamp = "5eed0001-b14") => new ClipHeader
    {
        stamp = stamp, value = 104.5f, team = "Harbour CC", teamShort = "HAR", opponent = "Northgate",
        date = "2026-11-05", rung = eRung.Club, season = 2, venue = eVenue.ClubGround, time = eTimeOfDay.Night,
        crowd = eCrowd.Half, pitch = ePitch.Green, bowling = eBowlingLevel.Medium, hand = eBattingStyle.LeftHanded, overs = 10,
    };

    private static void Same(ShotRecording.Sample a, ShotRecording.Sample b, string what)
    {
        Assert.AreEqual(a.ball, b.ball, what);
        Assert.AreEqual(a.batPosition, b.batPosition, what);
        Assert.AreEqual(a.headPosition, b.headPosition, what);
        Assert.IsTrue(a.batRotation.x == b.batRotation.x && a.batRotation.y == b.batRotation.y &&
                      a.batRotation.z == b.batRotation.z && a.batRotation.w == b.batRotation.w, what);
        Assert.IsTrue(a.headRotation.x == b.headRotation.x && a.headRotation.w == b.headRotation.w, what);
    }

    [Test]
    public void ARoundTrip_KeepsEverySampleKeyAndPoseExactly_ReBasedToTheShot()
    {
        ShotRecording s = Recording(5f, 2f, 2.6f);
        byte[] bytes = ClipCodec.Encode(s, RotKeys, PosKeys, Header());
        SavedClip c = ClipCodec.Decode(bytes);
        Assert.IsNotNull(c);

        ClipHeader h = c.header;
        Assert.AreEqual("5eed0001-b14", h.stamp);
        Assert.AreEqual(104.5f, h.value);
        Assert.AreEqual(eVenue.ClubGround, h.venue);
        Assert.AreEqual(eTimeOfDay.Night, h.time);
        Assert.AreEqual(eBattingStyle.LeftHanded, h.hand);
        Assert.AreEqual(10, h.overs);
        Assert.AreEqual("HAR", h.teamShort);
        Assert.IsTrue(h.released && h.hasContact);
        Assert.AreEqual(ClipCodec.LeadIn, h.releaseTime, Interval + 1e-4f, "the clip starts 1.2 s before the release");
        Assert.AreEqual(h.releaseTime + 0.6f, h.contactTime, 1e-4f);
        Assert.AreEqual(s.ContactPoint, h.contactPoint);

        int first = Mathf.RoundToInt((s.ReleaseTime - h.releaseTime) / Interval);
        Assert.AreEqual(s.Count - first, c.samples.Length);
        for (int i = 0; i < c.samples.Length; i++)
            Same(s[first + i], c.samples[i], "sample " + i);
        Assert.AreEqual(Interval, c.interval);
        CollectionAssert.AreEqual(RotKeys, c.rotKeys);
        CollectionAssert.AreEqual(PosKeys, c.posKeys);

        float zero = s.SinceBegin(first * Interval);
        Assert.Greater(c.Poses, 2);
        for (int j = 0; j < c.Poses; j++)
        {
            float t = c.poseTimes[j] + zero;    // the time it was stamped with, from Begin
            Assert.AreEqual(t, c.rotations[j * RotKeys.Length + 2].x, 1e-4f, "pose " + j + " came back with its own time");
            Assert.AreEqual(2f, c.rotations[j * RotKeys.Length + 2].y);
            Assert.AreEqual(new Vector3(c.rotations[j * RotKeys.Length].x, 1f, -1f), c.positions[j * PosKeys.Length + 1]);
        }
        Assert.LessOrEqual(c.poseTimes[0], 0f, "the pose at or before the start is kept, to blend from");
        Assert.Greater(c.poseTimes[1], 0f);
        Assert.GreaterOrEqual(c.poseTimes[c.Poses - 1], (c.samples.Length - 1) * Interval - 1f / 30f, "poses to the end");
    }

    [Test]
    public void TheClip_IsAtMostEightSeconds_AndStartsAtZeroWhenTheReleaseWasEarly()
    {
        ShotRecording s = Recording(12f, 0.5f, 1.1f);
        SavedClip c = ClipCodec.Decode(ClipCodec.Encode(s, RotKeys, PosKeys, Header()));
        Assert.IsNotNull(c);
        Assert.AreEqual(Mathf.FloorToInt(ClipCodec.MaxSeconds / Interval) + 1, c.samples.Length);
        Assert.LessOrEqual((c.samples.Length - 1) * c.interval, ClipCodec.MaxSeconds + 1e-4f);
        Assert.AreEqual(0.5f, c.header.releaseTime, 1e-5f, "nothing before the release to cut");
        Same(s[0], c.samples[0], "from the first sample kept");
    }

    [Test]
    public void KeysThatDoNotFitThePoses_SaveTheHologramOnly()
    {
        ShotRecording s = Recording(3f, 1.5f, 1.9f);
        SavedClip c = ClipCodec.Decode(ClipCodec.Encode(s, new[] { "W:Bowler" }, PosKeys, Header()));
        Assert.IsNotNull(c);
        Assert.AreEqual(0, c.Poses);
        Assert.AreEqual(0, c.rotKeys.Length);
        Assert.Greater(c.samples.Length, 1);
        Assert.IsNull(c.ToRecording().Poses);

        SavedClip nets = ClipCodec.Decode(ClipCodec.Encode(Recording(3f, 1.5f, -1f, 0f, false), RotKeys, PosKeys, Header()));
        Assert.IsNotNull(nets, "no pose track at all (the nets) still saves the shot");
        Assert.AreEqual(0, nets.Poses);
        Assert.IsFalse(nets.header.hasContact);
    }

    [Test]
    public void TheHeader_ReadsOnItsOwn_FromTheFrontOfTheFile()
    {
        byte[] bytes = ClipCodec.Encode(Recording(4f, 2f, 2.5f), RotKeys, PosKeys, Header("abc-1"));
        int length = BitConverter.ToInt32(bytes, 6);
        var front = new byte[10 + length];
        Array.Copy(bytes, front, front.Length);
        ClipHeader h = ClipCodec.ReadHeader(front);
        Assert.IsNotNull(h, "the header alone, without the poses");
        Assert.AreEqual("abc-1", h.stamp);
        Assert.AreEqual(ClipCodec.Format, h.format);
        Assert.IsNull(ClipCodec.Decode(front), "but it is not a whole clip");
    }

    [Test]
    public void ATruncatedClip_ReadsAsNull_WithoutThrowing()
    {
        byte[] bytes = ClipCodec.Encode(Recording(4f, 2f, 2.5f), RotKeys, PosKeys, Header());
        int header = 10 + BitConverter.ToInt32(bytes, 6);
        foreach (int cut in new[] { 0, 3, 5, 9, 10, header - 1, header, header + 3, header + 100, bytes.Length / 2, bytes.Length - 1 })
        {
            var part = new byte[cut];
            Array.Copy(bytes, part, cut);
            SavedClip c = null;
            Assert.DoesNotThrow(() => c = ClipCodec.Decode(part), "cut at " + cut);
            Assert.IsNull(c, "cut at " + cut);
        }
        Assert.IsNotNull(ClipCodec.Decode(bytes), "the whole file still reads");
    }

    [Test]
    public void ACorruptOrForeignFile_ReadsAsNull_WithoutThrowing()
    {
        byte[] good = ClipCodec.Encode(Recording(4f, 2f, 2.5f), RotKeys, PosKeys, Header());
        int header = 10 + BitConverter.ToInt32(good, 6);
        var cases = new List<byte[]> { null, new byte[0], new byte[64], System.Text.Encoding.UTF8.GetBytes("{\"stamp\":\"x\"}") };
        byte[] magic = (byte[])good.Clone(); magic[0] = (byte)'X'; cases.Add(magic);
        byte[] format = (byte[])good.Clone(); format[4] = 2; cases.Add(format);
        byte[] length = (byte[])good.Clone(); length[9] = 0x7F; cases.Add(length);
        byte[] count = (byte[])good.Clone();
        Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, count, header + 4, 4); cases.Add(count);     // the sample count
        byte[] json = (byte[])good.Clone();
        for (int i = 10; i < header; i++) json[i] = (byte)'#';
        cases.Add(json);
        for (int k = 0; k < cases.Count; k++)
        {
            SavedClip c = null;
            byte[] data = cases[k];
            Assert.DoesNotThrow(() => c = ClipCodec.Decode(data), "case " + k);
            Assert.IsNull(c, "case " + k);
        }
        Assert.IsNull(ClipCodec.ReadHeader(json), "a header that is not JSON");
        Assert.IsNull(ClipCodec.Unpack(good, null));
    }

    [Test]
    public void PackAndUnpack_NeedNoJson_AndAreTheSameBodyAsEncode()
    {
        ShotRecording s = Recording(4f, 2f, 2.5f);
        ClipHeader h = Header();
        SavedClip c = ClipCodec.Capture(s, RotKeys, PosKeys, h);
        byte[] packed = ClipCodec.Pack(ClipCodec.HeaderJson(h), c);
        CollectionAssert.AreEqual(ClipCodec.Encode(s, RotKeys, PosKeys, Header()), packed);
        SavedClip back = ClipCodec.Unpack(packed, h);
        Assert.IsNotNull(back);
        Assert.AreSame(h, back.header);
        CollectionAssert.AreEqual(c.poseTimes, back.poseTimes);
        Assert.AreEqual(c.rotations.Length, back.rotations.Length);
        Assert.IsNull(ClipCodec.Capture(null, RotKeys, PosKeys, h));
        Assert.IsNull(ClipCodec.Pack("{}", new SavedClip()));
    }
}
