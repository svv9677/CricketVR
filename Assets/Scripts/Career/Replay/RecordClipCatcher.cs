using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the clip of a career match's record six, for the Records screen's Watch (ReplayTheatre).
/// Built as the match scene loads, only for a career match. As it starts, the slot's clips are
/// pruned to the one its record book still points at (ReplayVault.Prune): a clip kept last match
/// for a six that did not end up the record goes then, with no race against the write.
///
/// Each six of the match that is the longest so far comes in through RecordMoments.YourSix
/// (CareerMatch, off the real blade only, so the bat shield can never make one). One that does not
/// beat the career's longest six is let go; one that does is saved as {stamp}.cvrclip:
///   * the recording is copied on the main thread (ClipCodec.Capture), once it has finished (the
///     recorder's Recorded) - or at once if it already has, or as the scene goes (OnDestroy: the
///     recording is plain data and outlives the recorder) if the match ends on it;
///   * it is then encoded and written on the vault's one worker chain, atomically, with this
///     match's earlier clip deleted after it (only the longest can be the record).
/// The record book names the clip (RecordEntry.clipStamp, CareerRecords.AfterMatch); nothing here
/// changes a ball or a record.
/// </summary>
public class RecordClipCatcher : MonoBehaviour
{
    /// A six waiting for its recording to finish, with the pose channels' names as they were.
    private sealed class Pending
    {
        public SixMoment six;
        public List<string> rot, pos;
    }

    private MatchPlan plan;
    private int slot = -1;
    private ShotRecorder recorder;
    private Pending waiting;
    private string written;     // this match's clip on disk, if any
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != CareerMatchSession.MatchScene || !CareerMatchSession.Active || FindFirstObjectByType<RecordClipCatcher>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Record Clip Catcher", typeof(RecordClipCatcher)), scene);
    }

    private void Awake()
    {
        plan = CareerMatchSession.Plan;     // kept: the session lets go of it as the match finishes
        slot = CareerSaves.Active;
        CareerProfile p = CareerSaves.Current;
        if (plan == null || p == null || slot < 0)
            return;
        ReplayVault.Prune(slot, new[] { p.recordBook?.longestSix?.clipStamp });
        RecordMoments.YourSix += OnSix;
        subscribed = true;
    }

    private void OnDestroy()
    {
        if (subscribed)
            RecordMoments.YourSix -= OnSix;
        subscribed = false;
        if (recorder != null)
            recorder.Recorded -= OnRecorded;
        if (waiting != null)
            Save(waiting);      // the match ended on it: the recording is whole enough, and still ours
        waiting = null;
    }

    private void Hook()
    {
        if (recorder != null || ShotRecorder.Instance == null)
            return;
        recorder = ShotRecorder.Instance;
        recorder.Recorded += OnRecorded;
    }

    private void OnSix(SixMoment six)
    {
        if (six.recording == null || !ReplayVault.SafeStamp(six.stamp))
            return;
        RecordEntry best = CareerSaves.Current?.recordBook?.longestSix;
        if (best != null && best.set && six.carry <= best.value)
            return;     // not a career best: the book will not point at it
        Hook();
        var pending = new Pending { six = six, rot = new List<string>(), pos = new List<string>() };
        if (recorder != null && recorder.Actors != null)
            recorder.Actors.ChannelKeys(pending.rot, pending.pos);
        waiting = null;
        if (recorder != null && recorder.IsRecording && recorder.Latest == six.recording)
            waiting = pending;              // still recording the ball's flight: on Recorded
        else if (recorder == null || recorder.Last == six.recording)
            Save(pending);
        else
            Debug.LogWarning($"[RecordClip] {six.stamp}: the recording has already gone");
    }

    private void OnRecorded()
    {
        if (waiting == null || recorder == null || recorder.Last != waiting.six.recording)
            return;
        Pending p = waiting;
        waiting = null;
        Save(p);
    }

    /// Copied now (the recorder will reuse it), then encoded and written on the worker chain.
    private void Save(Pending p)
    {
        ClipHeader h = Header(p.six);
        SavedClip clip = ClipCodec.Capture(p.six.recording, p.rot, p.pos, h);
        if (clip == null || slot < 0)
            return;
        string json = ClipCodec.HeaderJson(h);
        string path = ReplayVault.PathFor(slot, p.six.stamp);
        string old = written != null && written != p.six.stamp ? ReplayVault.PathFor(slot, written) : null;
        written = p.six.stamp;
        string stamp = p.six.stamp;
        ReplayVault.Queue(() =>
        {
            byte[] bytes = ClipCodec.Pack(json, clip);
            if (!ReplayVault.Write(path, bytes))
                return;
            if (old != null && File.Exists(old))
                File.Delete(old);
            Debug.Log($"[RecordClip] {stamp}: {bytes.Length / 1024} KB, {clip.samples.Length} samples, " +
                      $"{clip.Poses} poses of {clip.rotKeys.Length} rotations and {clip.posKeys.Length} positions");
        });
    }

    /// The match as the clip remembers it, for the theatre's ground, light, crowd, hand and caption.
    private ClipHeader Header(SixMoment six)
    {
        CareerProfile p = CareerSaves.Current;
        return new ClipHeader
        {
            stamp = six.stamp,
            record = eRecord.LongestSix,
            value = six.carry,
            team = plan.yourTeamName ?? "",
            teamShort = plan.yourShort ?? "",
            opponent = plan.opponentName ?? "",
            date = CareerProgressMath.Today(),
            rung = plan.rung,
            season = p?.season != null ? p.season.number : 0,
            venue = MatchVenue.Current,     // where it was played (the club falls back to the stadium)
            time = plan.time,
            crowd = plan.crowd,
            pitch = plan.pitch,
            bowling = plan.bowling,
            hand = p != null ? p.hand : GameSettings.BattingStyle,
            overs = plan.overs,
        };
    }
}
