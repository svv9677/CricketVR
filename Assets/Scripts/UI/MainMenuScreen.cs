using TMPro;
using UnityEngine;

/// <summary>
/// The Splash main menu: Quick Match with an overs stepper on the row itself, Practice in Nets,
/// and Settings. Built as a prefab by Tools > CricketVR > Build Menu Prefabs, with its buttons
/// wired to the On* methods below - nothing is created at runtime.
///
/// The overs value lives in GameSettings, so it is what HUD.Reset picks up once CricketVR loads,
/// and it survives between sessions.
/// </summary>
public class MainMenuScreen : MenuScreen
{
    public const string CricketScene = "CricketVR";
    public const string NetsScene = "Nets";

    [Tooltip("Reads e.g. \"5 overs\" between the stepper arrows.")]
    [SerializeField] private TextMeshProUGUI oversLabel;

    public override void OnShow() => RefreshOvers();

    private void RefreshOvers()
    {
        if (oversLabel == null)
            return;
        // The listed lengths: 5, 10, 20, 50 overs or a Test (MatchFormat).
        MatchFormat.Sync();
        oversLabel.text = MatchFormat.Label();
    }

    // ---- Button handlers (wired in the prefab) ----------------------------------------------------

    public void OnQuickMatch() => SceneFader.Load(CricketScene);

    public void OnPracticeInNets() => SceneFader.Load(NetsScene);

    public void OnSettings()
    {
        if (Root != null)
            Root.Push("settings");
    }

    public void OnMoreOvers()
    {
        MatchFormat.Next();
        RefreshOvers();
    }

    public void OnFewerOvers()
    {
        MatchFormat.Previous();
        RefreshOvers();
    }
}
