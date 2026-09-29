using UnityEngine;

/// <summary>
/// The pause card in CricketVR and Nets, opened with B: Resume, Settings, Main Menu.
///
/// Pausing sets Time.timeScale to 0, which deliberately freezes a ball in mid-air rather than
/// letting it complete - that is the correct behaviour for a paused match, and resuming picks the
/// delivery back up where it was.
///
/// Leaving for the main menu throws away the match, so in CricketVR the button asks first. In
/// Nets there is no score, so the same prefab is used with `confirmOnLeave` off.
/// </summary>
public class PauseScreen : MenuScreen
{
    public const string SplashScene = "Splash";

    [Tooltip("Ask before leaving, because a match in progress is lost. Off in Nets.")]
    [SerializeField] private bool confirmOnLeave = true;

    [Tooltip("The \"Leave match?\" row, hidden until Main Menu is pressed.")]
    [SerializeField] private GameObject confirmRow;

    [Tooltip("The normal Resume / Settings / Main Menu row.")]
    [SerializeField] private GameObject actionRow;

    public override void OnShow()
    {
        Time.timeScale = 0f;
        ShowConfirm(false);
    }

    public override void OnHide()
    {
        // Settings is pushed over Pause, so do NOT resume time here - only Resume and the scene
        // loads do that, or opening Settings would silently un-pause the match.
    }

    // ---- Button handlers (wired in the prefab) ----------------------------------------------------

    public void OnResume()
    {
        Time.timeScale = 1f;
        if (Root != null)
            Root.Close();
    }

    public void OnSettings()
    {
        if (Root != null)
            Root.Push("settings");
    }

    public void OnMainMenu()
    {
        if (confirmOnLeave)
            ShowConfirm(true);
        else
            LeaveNow();
    }

    public void OnConfirmLeave() => LeaveNow();

    public void OnCancelLeave() => ShowConfirm(false);

    private void LeaveNow()
    {
        Time.timeScale = 1f;      // SceneFader does this too; explicit here for the no-fader path
        SceneFader.Load(SplashScene);
    }

    private void ShowConfirm(bool on)
    {
        if (confirmRow != null) confirmRow.SetActive(on);
        if (actionRow != null) actionRow.SetActive(!on);
    }
}
