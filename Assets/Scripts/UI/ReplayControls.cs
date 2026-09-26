using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The replay card beside the between-balls menu: Replay / Pause / Stop for the holographic
/// replay of the last shot (ShotReplay), slower / faster with the current speed, and whether the
/// replay screen is brought in front of the batter (CameraReplay).
///
/// Every call into the replay system is in this file, against this API only:
///   ShotReplay.Instance, .Play(), .Stop(), .TogglePause(), .StepSpeed(int), .Speed, .HasShot,
///   .IsPlaying, .IsPaused, event .StateChanged (no arguments);
///   CameraReplay.Instance, .SetScreenInFront(bool), .ScreenInFront.
/// Both instances may be missing in a scene (Nets), so every use is null-checked.
/// Built into the NextBallMenu prefab by Tools > CricketVR > Build Player UI.
/// </summary>
public class ReplayControls : MonoBehaviour
{
    [SerializeField] private Button replayButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private Button slowerButton;
    [SerializeField] private Button fasterButton;
    [SerializeField] private TMP_Text speedLabel;
    [SerializeField] private Toggle screenInFront;

    private ShotReplay subscribed;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        ShotReplay replay = ShotReplay.Instance;
        if (replay == null || replay == subscribed)
            return;
        Unsubscribe();
        replay.StateChanged += Refresh;
        subscribed = replay;
    }

    private void Unsubscribe()
    {
        if (subscribed != null)
            subscribed.StateChanged -= Refresh;
        subscribed = null;
    }

    /// Mirror the replay's state: nothing to replay greys the controls out.
    public void Refresh()
    {
        ShotReplay replay = ShotReplay.Instance;
        bool hasShot = replay != null && replay.HasShot;
        bool playing = hasShot && replay.IsPlaying;
        SetInteractable(replayButton, hasShot);
        SetInteractable(pauseButton, playing);
        SetInteractable(stopButton, playing);
        TMP_Text pauseLabel = pauseButton != null ? pauseButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (pauseLabel != null)
            pauseLabel.text = playing && replay.IsPaused ? "Resume" : "Pause";
        SetInteractable(slowerButton, hasShot);
        SetInteractable(fasterButton, hasShot);
        if (speedLabel != null)
            speedLabel.text = replay != null ? $"{replay.Speed:0.##}×" : "1×";

        CameraReplay screen = CameraReplay.Instance;
        if (screenInFront != null)
        {
            screenInFront.interactable = screen != null;
            if (screen != null)
                screenInFront.SetIsOnWithoutNotify(screen.ScreenInFront);
        }
    }

    // ---- Handlers (wired in the prefab) ----------------------------------------------------------
    public void OnReplay() => WithReplay(r => r.Play());
    public void OnPause() => WithReplay(r => r.TogglePause());
    public void OnStop() => WithReplay(r => r.Stop());
    public void OnSlower() => WithReplay(r => r.StepSpeed(-1));
    public void OnFaster() => WithReplay(r => r.StepSpeed(+1));

    public void OnScreenInFront(bool on)
    {
        if (CameraReplay.Instance != null)
            CameraReplay.Instance.SetScreenInFront(on);
        Refresh();
    }

    private void WithReplay(System.Action<ShotReplay> action)
    {
        Subscribe();   // the replay may have started after this card was first shown
        ShotReplay replay = ShotReplay.Instance;
        if (replay != null)
            action(replay);
        Refresh();
    }

    private static void SetInteractable(Button button, bool on)
    {
        if (button != null)
            button.interactable = on;
    }
}
