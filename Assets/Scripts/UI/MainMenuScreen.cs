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
        int overs = GameSettings.Overs;
        oversLabel.text = overs == 1 ? "1 over" : $"{overs} overs";
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
        GameSettings.Overs++;
        RefreshOvers();
    }

    public void OnFewerOvers()
    {
        GameSettings.Overs--;
        RefreshOvers();
    }
}
