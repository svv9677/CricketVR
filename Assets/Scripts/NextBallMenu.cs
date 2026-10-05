using TMPro;
using UnityEngine;

/// <summary>
/// The dock that comes up in front of the player between balls: who is bowling (with Change),
/// a big Bowl button, Settings and Calibrate Grip - and, beside it, the replay card
/// (ReplayControls). It appears where the player is looking whenever the game is ready for the
/// next delivery and goes away the moment the ball is on its way. The laser pointer clicks it;
/// A still bowls the next ball.
///
/// The panel is a prefab (Assets/Resources/Prefabs/UI/NextBallMenu.prefab, built by
/// Tools > CricketVR > Build Player UI) with its buttons wired to the On* methods below - nothing
/// is created at runtime. This component sits on the prefab's root, which stays active; the
/// `panel` child is what shows and hides.
/// </summary>
public class NextBallMenu : MonoBehaviour
{
    private const float Distance = 1.1f;
    /// Below eye level, so the pitch stays in view over the top of it.
    private const float BelowEyes = 0.38f;

    [SerializeField] private GameObject panel;
    [Tooltip("Shows the current bowler, e.g. \"Bumrah  Inswing\".")]
    [SerializeField] private TextMeshProUGUI bowlerLabel;

    private bool shown;
    private bool registered;
    private Bowler shownBowler;
    private bool shownNewOver;
    private TMP_Text header;

    private void Start()
    {
        panel.SetActive(false);
        FindHeaderAndBowlButton();
    }

    /// The card's small heading ("Bowler") and the big button, from the prefab's layout: the
    /// heading becomes "Next over" between overs, the button "Next ball".
    private void FindHeaderAndBowlButton()
    {
        Transform card = bowlerLabel != null ? bowlerLabel.transform.parent.parent : null;
        if (card == null)
            return;
        foreach (Transform child in card)
        {
            if (header == null && child.TryGetComponent(out TMP_Text text))
                header = text;
            if (child.name.StartsWith("Bowl"))
            {
                TMP_Text label = child.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = "Next ball  <size=75%>(A)</size>";
            }
        }
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
        if (shown)
            UpdateBowler(inst.theHUD);
    }

    /// Only when the bowler or the over changes: no string building per frame. Before the first
    /// ball of an over the card says so - "Next over" - with who is coming on and how he bowls.
    private void UpdateBowler(HUD hud)
    {
        if (bowlerLabel == null || hud == null || hud.CurrentBowler == null)
            return;
        bool newOver = hud.Balls == 0;
        if (hud.CurrentBowler == shownBowler && newOver == shownNewOver)
            return;
        shownBowler = hud.CurrentBowler;
        shownNewOver = newOver;
        string type = UIFormat.BowlerType(shownBowler.Type);
        // Every bowling action in the game is right-arm, over the wicket (the run-ups come in on
        // the stumps' left as he sees them, AnimatedBowler).
        bowlerLabel.text = newOver
            ? $"{shownBowler.Name}  <color=#98A2B3>{type}</color>\n<size=55%><color=#98A2B3>Right-arm, over the wicket</color></size>"
            : $"{shownBowler.Name}  <color=#98A2B3>{type}</color>";
        if (header != null)
            header.text = !newOver ? "BOWLER" : hud.Overs == 0 ? "<color=#3B82F6>FIRST OVER</color>" : "<color=#3B82F6>NEXT OVER</color>";
    }

    private void Show()
    {
        if (!registered && OpenXRMenuInputModule.Instance != null)
        {
            OpenXRMenuInputModule.RegisterPanel(panel);
            registered = true;
        }
        // Its fixed spot in front of the player (MenuLayout); in a scene without one, ahead of the gaze.
        if (MenuLayout.TryGet(MenuLayout.NextBall, out Vector3 spot, out Quaternion facing))
        {
            panel.transform.SetPositionAndRotation(spot, facing);
        }
        else
        {
            Transform head = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.left;
            forward.Normalize();
            // Canvas text faces -Z, so point +Z away from the player; tilt it up toward the eyes.
            panel.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * BelowEyes,
                                                   Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(20f, 0f, 0f));
        }
        // Moved by hand before (grip): open where it was left instead.
        GrabbablePanel grab = GetComponent<GrabbablePanel>();
        if (grab != null)
            grab.Restore();
        shownBowler = null;
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
