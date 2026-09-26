using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings panel opened with B. A prefab (Assets/Resources/Prefabs/UI/SettingsPanel.prefab,
/// built by Tools > CricketVR > Build UI Prefabs) - it replaces the Oculus DebugUIBuilder menu,
/// which built every control at runtime. Its controls are wired to the On* methods below, which
/// pass the change to Main; Main reads the controls through the fields to keep them in sync.
/// This component sits on the always-active root; the `panel` child shows and hides.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [System.Serializable]
    public class SliderRow
    {
        public Slider slider;
        public TMP_Text value;
    }

    [SerializeField] private GameObject panel;

    [Header("Settings")]
    public Toggle leftHanded;
    public Toggle rightHanded;
    public Toggle easy;
    public Toggle medium;
    public Toggle hard;

    [Header("Tuning")]
    public Toggle overlay;
    public SliderRow resetDelay;
    public SliderRow fielderSpeed;
    public SliderRow batPower;
    public SliderRow ampMin;
    public SliderRow ampMax;
    public TMP_Text bowlingType;
    public SliderRow minSpeed, maxSpeed, minLine, maxLine, minSwing, maxSwing, minTurn, maxTurn;

    private const float Distance = 1.3f;
    private bool registered;

    public GameObject Panel => panel;

    private void Awake()
    {
        panel.SetActive(false);
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
        panel.transform.SetPositionAndRotation(head.position + forward * Distance + Vector3.down * 0.15f,
                                               Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(8f, 0f, 0f));
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);

    // ---- Control handlers (wired in the prefab) ------------------------------------------------
    private static Main M => Main.Instance;
    public void OnLeftHanded(bool on) { if (on) M.onRadioLeftHanded(leftHanded); }
    public void OnRightHanded(bool on) { if (on) M.onRadioRightHanded(rightHanded); }
    public void OnEasy(bool on) { if (on) M.onRadioEasy(easy); }
    public void OnMedium(bool on) { if (on) M.onRadioMedium(medium); }
    public void OnHard(bool on) { if (on) M.onRadioHard(hard); }
    public void OnOverlay(bool on) => M.onOverlayToggle(overlay);
    public void OnResetDelay(float v) => M.onResetDelay(v);
    public void OnFielderSpeed(float v) => M.onFielderSpeed(v);
    public void OnBatPower(float v) { M.BatAmplifier = v; M.ApplyTweaks(); }
    public void OnAmpMin(float v) => M.onAmpMinChange(v);
    public void OnAmpMax(float v) => M.onAmpMaxChange(v);
    public void OnMinSpeed(float v) { M.MinX = v; M.ApplyTweaks(); }
    public void OnMaxSpeed(float v) { M.MaxX = v; M.ApplyTweaks(); }
    public void OnMinLine(float v) { M.MinZ = v; M.ApplyTweaks(); }
    public void OnMaxLine(float v) { M.MaxZ = v; M.ApplyTweaks(); }
    public void OnMinSwing(float v) { M.MinSwing = v; M.ApplyTweaks(); }
    public void OnMaxSwing(float v) { M.MaxSwing = v; M.ApplyTweaks(); }
    public void OnMinTurn(float v) { M.MinPitchTurn = v; M.ApplyTweaks(); }
    public void OnMaxTurn(float v) { M.MaxPitchTurn = v; M.ApplyTweaks(); }
    public void OnCalibrateGrip() => M.onCalibrateGrip();
    public void OnResetGrip() => M.onResetGrip();
    public void OnClose() => M.CloseSettings();
}
