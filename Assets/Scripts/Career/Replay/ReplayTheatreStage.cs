using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The replay theatre in the match scene (ReplayTheatre): built as the scene loads, only for the
/// theatre. It holds every ball back for as long as it is there (MatchGate, set again each frame,
/// as every scene load clears it), so the match never starts, never ends and never reaches
/// MatchHistory; then it plays the record clip on a loop:
///   * a caption first (CareerMatchCard): the record, where and when, and how to leave;
///   * the clip decoded and put into this scene's layout off the main thread (ClipCodec.Unpack,
///     ClipRemap) once the replay actors are up and the big screen has warmed;
///   * the hologram at the crease (ShotReplay.PlayRecording) and, when the poses matched, the big
///     screen (CameraReplay.ShowRecording), each looping on its own, with no controls.
/// The player's own screen-in-front setting is left as it is. Known gaps: no partner at the other
/// end, the Quick Match scoreboard, the club's default pavilion name.
/// </summary>
public class ReplayTheatreStage : MonoBehaviour
{
    private const float CaptionSeconds = 4.5f;
    /// Seconds to wait for the replay actors and the big screen before playing the hologram alone.
    private const float ActorsTimeout = 10f;

    private ClipHeader header;
    private byte[] data;
    private CareerMatchCard card;
    private Task<ShotRecording> making;
    private float startedAt, captionUntil = -1f;
    private bool playing, teamSet, historySet;
    private string oldTeam;

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
        if (scene.name != CareerMatchSession.MatchScene || !ReplayTheatre.Active || FindFirstObjectByType<ReplayTheatreStage>() != null)
            return;
        SceneManager.MoveGameObjectToScene(new GameObject("Replay Theatre", typeof(ReplayTheatreStage)), scene);
    }

    private void Awake()
    {
        header = ReplayTheatre.Header;
        data = ReplayTheatre.Clip;
        startedAt = Time.time;
        if (header == null || data == null)
            return;     // nothing to play: the balls are still held back (Update)
        card = new CareerMatchCard(transform);
        // Nothing is bowled here, so MatchFlow never ends a match; and if it ever did, not into the history.
        MatchFlow.KeepHistory = false;
        historySet = true;
        oldTeam = ReplayOverlay.Team;
        teamSet = true;
        if (!string.IsNullOrEmpty(header.teamShort))
            ReplayOverlay.Team = header.teamShort;
    }

    private void OnDestroy()
    {
        if (teamSet)
            ReplayOverlay.Team = oldTeam;
        if (historySet)
            MatchFlow.KeepHistory = true;
        MatchGate.Blocking = false;
        ReplayTheatre.Left();
    }

    private void Update()
    {
        MatchGate.Blocking = true;   // every frame: no ball is ever bowled in the theatre
        if (card == null)
            return;
        if (captionUntil >= 0f && Time.time >= captionUntil)
        {
            card.Hide();
            captionUntil = -1f;
        }
        if (playing)
            return;
        if (making == null)
            TryStart();
        else if (making.IsCompleted && captionUntil < 0f)
            Play(making.Status == TaskStatus.RanToCompletion ? making.Result : null);
    }

    /// Once the replay actors are there and the big screen has warmed (or they never come): the
    /// caption up, and the clip decoded and laid out on a worker thread meanwhile.
    private void TryStart()
    {
        ReplayActors actors = ShotRecorder.Instance != null ? ShotRecorder.Instance.Actors : null;
        bool screen = CameraReplay.Instance != null && CameraReplay.Instance.Ready;
        if ((actors == null || !screen) && Time.time - startedAt < ActorsTimeout)
            return;
        card.Show(ReplayTheatre.Heading(header), ReplayTheatre.Body(header));
        captionUntil = Time.time + CaptionSeconds;
        ClipHeader h = header;
        byte[] bytes = data;
        if (actors == null || !screen || actors.Rotations == 0 || !actors.StillLive())
        {
            making = Task.Run(() => ClipCodec.Unpack(bytes, h)?.ToRecording());
            return;
        }
        // The live keys and pose now are read here, on the main thread; the rest is plain data.
        var rot = new List<string>();
        var pos = new List<string>();
        actors.ChannelKeys(rot, pos);
        var now = new PoseTrack(1, actors.Rotations, actors.Positions);
        actors.Capture(now, now.Add(0f));
        making = Task.Run(() => ClipRemap.ToLayout(ClipCodec.Unpack(bytes, h), rot, pos, now.rotations, now.positions));
    }

    private void Play(ShotRecording clip)
    {
        playing = true;
        data = null;    // decoded: the bytes can go
        if (clip == null)
        {
            Debug.LogWarning("[ReplayTheatre] the record clip could not be read");
            card.Show("Replay unavailable", "This clip could not be played.\n<size=80%><color=#98A2B3>Press B, then Main Menu, to leave.</color></size>");
            return;
        }
        if (ShotReplay.Instance != null)
            ShotReplay.Instance.PlayRecording(clip);
        if (clip.Poses != null && CameraReplay.Instance != null && CameraReplay.Instance.Ready)
            CameraReplay.Instance.ShowRecording(clip);
        Debug.Log($"[ReplayTheatre] playing {header.stamp}: {clip.Count} samples, " +
                  $"{(clip.Poses != null ? clip.Poses.Count + " poses" : "hologram only")}");
    }
}
