using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A saved record clip's poses put into the layout of the scene playing it. The scene finds its
/// actors in no fixed order (ReplayActors), and its cast can differ from the match the clip came
/// from (no partner at the other end in the theatre), so every channel is matched by name
/// (ReplayActors.ChannelKeys), never by index. A live channel the clip does not have keeps the pose
/// it has now (one live capture); a saved one the scene does not have is dropped.
///
/// If fewer than 80% of the clip's rotation channels are found, the scene is too different to
/// pose (renamed or rebuilt actors) and the clip plays as the hologram alone: the ball, the bat and
/// the helmet, with no poses for the big screen. Pure apart from the overload that reads the live
/// actors, so it can run on a worker thread.
/// </summary>
public static class ClipRemap
{
    public const float MinMatch = 0.8f;

    /// The share of `saved` keys that `live` has (0 with nothing saved).
    public static float Match(IList<string> saved, IList<string> live)
    {
        if (saved == null || saved.Count == 0 || live == null)
            return 0f;
        var have = new HashSet<string>(live);
        int found = 0;
        foreach (string k in saved)
            if (have.Contains(k))
                found++;
        return found / (float)saved.Count;
    }

    /// `clip` as a recording in `actors`' layout (main thread: the actors are read for their keys
    /// and their pose now). Null with no clip.
    public static ShotRecording ToLayout(SavedClip clip, ReplayActors actors)
    {
        if (clip == null)
            return null;
        if (clip.samples == null || clip.samples.Length < 2)
            return null;
        if (actors == null || actors.Rotations == 0 || !actors.StillLive())
            return clip.ToRecording();
        var rot = new List<string>();
        var pos = new List<string>();
        actors.ChannelKeys(rot, pos);
        var now = new PoseTrack(1, actors.Rotations, actors.Positions);
        actors.Capture(now, now.Add(0f));
        return ToLayout(clip, rot, pos, now.rotations, now.positions);
    }

    /// `clip` as a recording whose poses are laid out as `liveRot` / `livePos`, a channel the clip
    /// lacks taken from `baseRot` / `basePos` (one live pose). The hologram alone (no poses) when
    /// fewer than MinMatch of the saved rotation channels are found.
    public static ShotRecording ToLayout(SavedClip clip, IList<string> liveRot, IList<string> livePos,
                                         Quaternion[] baseRot, Vector3[] basePos)
    {
        if (clip?.samples == null || clip.samples.Length < 2)
            return null;
        bool pose = clip.Poses > 0 && liveRot != null && livePos != null && liveRot.Count > 0 &&
                    baseRot != null && basePos != null && baseRot.Length >= liveRot.Count && basePos.Length >= livePos.Count &&
                    Match(clip.rotKeys, liveRot) >= MinMatch;
        if (!pose)
            return clip.ToRecording();
        int[] rotFrom = Sources(clip.rotKeys, liveRot), posFrom = Sources(clip.posKeys, livePos);
        int r = liveRot.Count, v = livePos.Count, sr = clip.rotKeys.Length, sv = clip.posKeys.Length;
        var track = new PoseTrack(clip.Poses, r, v);
        ShotRecording s = clip.ToRecording(track);   // Clear first: it empties the track too
        for (int j = 0; j < clip.Poses; j++)
        {
            int slot = track.Add(clip.poseTimes[j]);
            for (int c = 0; c < r; c++)
                track.rotations[slot * r + c] = rotFrom[c] >= 0 ? clip.rotations[j * sr + rotFrom[c]] : baseRot[c];
            for (int c = 0; c < v; c++)
                track.positions[slot * v + c] = posFrom[c] >= 0 ? clip.positions[j * sv + posFrom[c]] : basePos[c];
        }
        return s;
    }

    /// For each live channel, the index of the saved channel with its key, or -1.
    private static int[] Sources(string[] saved, IList<string> live)
    {
        var at = new Dictionary<string, int>();
        for (int i = 0; i < saved.Length; i++)
            if (saved[i] != null && !at.ContainsKey(saved[i]))
                at.Add(saved[i], i);
        var from = new int[live.Count];
        for (int c = 0; c < live.Count; c++)
            from[c] = live[c] != null && at.TryGetValue(live[c], out int i) ? i : -1;
        return from;
    }
}
