using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings panel opened with B (or "Settings" between balls). A prefab
/// (Assets/Resources/Prefabs/UI/SettingsPanel.prefab, built by Tools > CricketVR > Build Player UI)
/// with its controls wired to the On* methods below, which pass each change to Main. Main calls
/// Refresh whenever a setting changes, and the panel mirrors Main without re-firing its handlers.
/// This component sits on the always-active root; the `panel` child shows and hides.
///
/// Only what a player changes between balls is up front: batting hand, difficulty (bat width),
/// bat power and grip; bowler type, speed, swing and turn ranges; fielder speed. The delivery line
/// and the debug overlay sit in a collapsed "Advanced" section.
/// </summary>
public partial class SettingsPanel : MonoBehaviour
{
    [System.Serializable]
    public class SliderRow
    {
        public Slider slider;
        public TMP_Text value;
    }

    [System.Serializable]
    public class RangeRow
    {
        public RangeSlider slider;
        public TMP_Text label;
        public TMP_Text value;
    }

    [SerializeField] private GameObject panel;

    [Header("Batting")]
    public SegmentedControl hand;              // Left, Right
    public SegmentedControl difficultyLevel;   // Easy, Medium, Hard
    public TMP_Text difficultyHint;
    public SliderRow batPowerSlider;

    [Header("Bowling")]
    public SegmentedControl bowler;            // UIFormat.BowlerTypes
    public RangeRow speedRange;                // km/h
    public RangeRow swingRange;                // 0..1 of the maximum swing
    public RangeRow turnRange;                 // 0..1 of BallFlight.MaxTurnDegrees

    [Header("Fielding")]
    public SliderRow fielderSpeedSlider;

    [Header("Advanced (collapsed)")]
    public GameObject advanced;
    public TMP_Text advancedLabel;
    public RangeRow lineRange;                 // metres from middle stump, off side positive
    public Toggle overlayToggle;

    private const float Distance = 1.45f;
    /// Bat power snaps to "Realistic" within this much of it.
    private const float SnapToRealistic = 3f;
    private bool registered;

    public GameObject Panel => panel;

    private void Awake()
    {
        panel.SetActive(false);
        if (advanced != null)
            SetAdvanced(false);
    }

    public void Show()
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
        panel.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * 0.1f,
                                               Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(8f, 0f, 0f));
        if (M != null)
            M.ApplyTweaks();   // the bowler may have changed at the end of an over
        Refresh();
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);

    // ---- Mirroring Main --------------------------------------------------------------------------

    /// Show Main's current settings. Never fires the control handlers.
    public void Refresh()
    {
        Main m = Main.Instance;
        if (m == null)
            return;
        if (hand != null)
            hand.SetValueWithoutNotify(m.BattingStyle == eBattingStyle.LeftHanded ? 0 : 1);
        if (difficultyLevel != null)
            difficultyLevel.SetValueWithoutNotify(Mathf.Clamp((int)m.Difficulty - 1, 0, 2));
        SetText(difficultyHint, DifficultyHint(m.Difficulty));
        SetSlider(batPowerSlider, m.BatAmplifier, UIFormat.BatPower(m.BatAmplifier));
        RefreshBowling(m);
        SetSlider(fielderSpeedSlider, m.fielderSpeed, $"{m.fielderSpeed:0.0}×");
        if (overlayToggle != null)
            overlayToggle.SetIsOnWithoutNotify(m.overlayVisible);
        RefreshLegacy(m);
    }

    private void RefreshBowling(Main m)
    {
        bool spin = UIFormat.IsSpin(m.swingType);
        if (bowler != null)
            bowler.SetValueWithoutNotify(UIFormat.BowlerTypeIndex(m.swingType));
        float kmh = Constants.KmhPerSpeedUnit;
        SetRange(speedRange, m.MinX * kmh, m.MaxX * kmh, null,
                 UIFormat.Range(m.MinX * kmh, m.MaxX * kmh, "0", "km/h"));
        SetRange(swingRange, m.MinSwing, m.MaxSwing, spin ? "Drift" : "Swing",
                 UIFormat.Range(m.MinSwing * 100f, m.MaxSwing * 100f, "0", "%"));
        float deg = BallFlight.MaxTurnDegrees;
        SetRange(turnRange, m.MinPitchTurn, m.MaxPitchTurn, spin ? "Turn" : "Seam movement",
                 UIFormat.Range(m.MinPitchTurn * deg, m.MaxPitchTurn * deg, "0.0", UIFormat.Degrees));
        SetRange(lineRange, m.MinZ, m.MaxZ, null, UIFormat.LineRange(m.MinZ, m.MaxZ));
    }

    private static string DifficultyHint(eDifficulty level)
    {
        switch (level)
        {
            case eDifficulty.Easy: return $"Bat {Constants.BatColliderMultiplierEasy:0}× wide";
            case eDifficulty.Medium: return $"Bat {Constants.BatColliderMultiplierMedium:0}× wide";
            case eDifficulty.Hard: return "Real bat width";
            default: return "";
        }
    }

    private static void SetSlider(SliderRow row, float value, string text)
    {
        if (row == null) return;
        if (row.slider != null) row.slider.SetValueWithoutNotify(value);
        SetText(row.value, text);
    }

    private static void SetRange(RangeRow row, float low, float high, string label, string text)
    {
        if (row == null) return;
        if (row.slider != null) row.slider.SetValuesWithoutNotify(low, high);
        if (label != null) SetText(row.label, label);
        SetText(row.value, text);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value)
            text.text = value;
    }

    private void SetAdvanced(bool open)
    {
        advanced.SetActive(open);
        SetText(advancedLabel, open ? "Hide advanced" : "Advanced");
    }

    // ---- Control handlers (wired in the prefab) --------------------------------------------------
    private static Main M => Main.Instance;

    public void OnHand(int index) => M.SetBattingStyle(index == 0 ? eBattingStyle.LeftHanded : eBattingStyle.RightHanded);

    public void OnDifficulty(int index)
    {
        M.SetDifficulty((eDifficulty)(index + 1));
        Refresh();
    }

    public void OnBatPower(float v)
    {
        if (Mathf.Abs(v - Constants.BatPowerRealistic) <= SnapToRealistic)
            v = Constants.BatPowerRealistic;
        M.BatAmplifier = v;
        M.ApplyTweaks();
    }

    public void OnBowler(int index)
    {
        // Not while a ball is being bowled: the bowler's run-up is already under way.
        eGameState state = M.gameState;
        bool delivering = state >= eGameState.InGame_SelectDelivery && state <= eGameState.InGame_DeliverBallLoop;
        if (index < 0 || index >= UIFormat.BowlerTypes.Length || delivering)
        {
            Refresh();
            return;
        }
        eSwingType type = UIFormat.BowlerTypes[index];
        if (M.theHUD != null) M.theHUD.SelectBowlerOfType(type);
        else M.swingType = type;
        M.ApplyTweaks();
    }

    public void OnSpeedRange(float lowKmh, float highKmh)
    {
        M.MinX = lowKmh / Constants.KmhPerSpeedUnit;
        M.MaxX = highKmh / Constants.KmhPerSpeedUnit;
        M.ApplyTweaks();
    }

    public void OnSwingRange(float low, float high) { M.MinSwing = low; M.MaxSwing = high; M.ApplyTweaks(); }
    public void OnTurnRange(float low, float high) { M.MinPitchTurn = low; M.MaxPitchTurn = high; M.ApplyTweaks(); }
    public void OnLineRange(float low, float high) { M.MinZ = low; M.MaxZ = high; M.ApplyTweaks(); }

    /// Back to this bowler type's built-in speed, line, swing and turn.
    public void OnResetBowling()
    {
        M.reloadTweakables();
        M.ApplyTweaks();
    }

    public void OnFielderSpeed(float v) => M.onFielderSpeed(Mathf.Round(v * 10f) / 10f);
    public void OnOverlay(bool on) { M.overlayVisible = on; M.ApplyTweaks(); }
    public void OnToggleAdvanced() { if (advanced != null) SetAdvanced(!advanced.activeSelf); }
    public void OnCalibrateGrip() => M.onCalibrateGrip();
    public void OnResetGrip() => M.onResetGrip();
    public void OnClose() => M.CloseSettings();
}
