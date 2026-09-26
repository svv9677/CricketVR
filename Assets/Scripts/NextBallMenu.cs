using TMPro;
using UnityEngine;

/// <summary>
/// The panel that comes up in front of the player between balls: Next Ball, Change Bowler,
/// Settings, Calibrate Grip. It appears where the player is looking whenever the game is ready for
/// the next delivery and goes away the moment the ball is on its way. The laser pointer clicks it;
/// A still bowls the next ball.
///
/// The panel is a prefab (Assets/Resources/Prefabs/UI/NextBallMenu.prefab, built by
/// Tools > CricketVR > Build UI Prefabs) with its buttons wired to the On* methods below - nothing
/// is created at runtime. This component sits on the prefab's root, which stays active; the
/// `panel` child is what shows and hides.
/// </summary>
public class NextBallMenu : MonoBehaviour
{
    private const float Distance = 1.0f;
    /// Below eye level, so the pitch stays in view over the top of it.
    private const float BelowEyes = 0.35f;

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI bowlerLabel;

    private bool shown;
    private bool registered;

    private void Start()
    {
        panel.SetActive(false);
    }

    private void Update()
    {
        Main inst = Main.Instance;
        bool want = inst != null && inst.gameState == eGameState.InGame_Ready &&
                    !inst.SettingsOpen && !inst.CalibratingGrip;
        if (want && !shown)
            Show();
        else if (!want && shown)
            Hide();
        if (shown && bowlerLabel != null && inst.theHUD != null && inst.theHUD.CurrentBowler != null)
            bowlerLabel.text = $"Change Bowler  <size=70%>({inst.theHUD.CurrentBowler.Name}, {inst.theHUD.CurrentBowler.Type})</size>";
    }

    private void Show()
    {
        if (!registered && OpenXRMenuInputModule.Instance != null)
        {
            OpenXRMenuInputModule.RegisterPanel(panel);
            registered = true;
        }
        Transform head = Camera.main.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.left;
        forward.Normalize();
        // Canvas text faces -Z, so point +Z away from the player; tilt it up toward the eyes.
        panel.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * BelowEyes,
                                               Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(20f, 0f, 0f));
        panel.SetActive(true);
        shown = true;
    }

    private void Hide()
    {
        panel.SetActive(false);
        shown = false;
    }

    // ---- Button handlers (wired in the prefab) -------------------------------------------------
    public void OnNextBall() => Main.Instance.StartNextBall();
    public void OnChangeBowler() => Main.Instance.theHUD.ChangeBowler();
    public void OnSettings() => Main.Instance.OpenSettings();
    public void OnCalibrateGrip() => Main.Instance.onCalibrateGrip();
}
