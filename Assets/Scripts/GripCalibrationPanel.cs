using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The panel shown beside the bat during grip calibration: Upright | Flat, Lock Grip, Cancel.
/// A prefab (Assets/Resources/Prefabs/UI/GripCalibrationPanel.prefab, built by Tools > CricketVR >
/// Build Player UI) with its controls wired to the On* methods below; BatGripCalibration shows and
/// places it. This component sits on the always-active root; the `panel` child shows and hides.
/// </summary>
public class GripCalibrationPanel : MonoBehaviour
{
    public static GripCalibrationPanel Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [Tooltip("Upright | Flat.")]
    [SerializeField] private SegmentedControl orientation;

    [Header("Legacy layout (Tools > CricketVR > Build UI Prefabs)")]
    [SerializeField] private Image uprightButton;
    [SerializeField] private Image flatButton;
    [SerializeField] private Color selected = new Color(0.2f, 0.36f, 0.62f);
    [SerializeField] private Color unselected = new Color(0.24f, 0.26f, 0.32f);

    private bool registered;

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    /// Both bat shells are shown at once now, so the Upright | Flat control is hidden: only Lock
    /// Grip and Cancel remain.
    public void Show(Vector3 position, Vector3 flatForward)
    {
        if (!registered && OpenXRMenuInputModule.Instance != null)
        {
            OpenXRMenuInputModule.RegisterPanel(panel);
            registered = true;
        }
        panel.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(15f, 0f, 0f));
        if (orientation != null)
            orientation.gameObject.SetActive(false);
        if (uprightButton != null)
            uprightButton.gameObject.SetActive(false);
        if (flatButton != null)
            flatButton.gameObject.SetActive(false);
        panel.SetActive(true);
    }

    public void Hide() => panel.SetActive(false);

    public void ShowSelection(bool flat)
    {
        if (orientation != null)
            orientation.SetValueWithoutNotify(flat ? 1 : 0);
        if (uprightButton != null)
            uprightButton.color = flat ? unselected : selected;
        if (flatButton != null)
            flatButton.color = flat ? selected : unselected;
    }

    // ---- Button handlers (wired in the prefab) -------------------------------------------------
    private static BatGripCalibration Calibration => Main.Instance != null ? Main.Instance.GripCalibration : null;
    // Upright | Flat: both shells show at once now and these controls are hidden. The handlers stay
    // because the prefab's buttons are wired to them.
    public void OnOrientation(int index) { }
    public void OnUpright() { }
    public void OnFlat() { }
    public void OnLock() => Calibration?.Lock();
    public void OnCancel() => Calibration?.Cancel();
}
