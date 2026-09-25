using UnityEngine;

/// <summary>
/// In-headset live tuning for how the bat sits in the hand.
///
/// The bat's grab offset can only really be judged with the headset on and the controller in
/// your hand, which otherwise costs a full APK build per attempt. With this enabled you nudge
/// the offset with the thumbsticks until the bat looks right, then press the dump button and
/// read six numbers off the debug overlay.
///
/// Controls (the hand NOT holding the bat drives rotation; the holding hand drives position):
///   Off-hand stick  X / Y  - rotate around the two axes of the current pair
///   Bat-hand stick  X / Y  - slide along the two axes of the current pair
///   A (right)              - cycle the axis pair: XY -> YZ -> ZX
///   B (right)              - dump current values to the console and the overlay
///   Y (left)               - reset to the values captured at startup
///
/// Leave <see cref="enableTuner"/> off for normal play; it is inert when disabled.
/// </summary>
public class BatOffsetTuner : MonoBehaviour
{
    [Tooltip("Turn on to tune the bat offset in-headset. Leave off for normal play.")]
    [SerializeField] private bool enableTuner;

    [Tooltip("The bat being tuned. Defaults to the Bat on this GameObject.")]
    [SerializeField] private Bat bat;

    [SerializeField] private float degreesPerSecond = 30f;
    [SerializeField] private float metresPerSecond = 0.06f;

    private enum AxisPair { XY, YZ, ZX }
    private AxisPair _pair = AxisPair.XY;

    private Vector3 _initialLeftPos, _initialLeftEuler, _initialRightPos, _initialRightEuler;
    private bool _captured;

    private void Awake()
    {
        if (bat == null) bat = GetComponent<Bat>();
    }

    private void Start()
    {
        if (bat == null) return;
        _initialLeftPos = bat.leftGrabOffsetPosition;
        _initialLeftEuler = bat.leftGrabOffsetEuler;
        _initialRightPos = bat.rightGrabOffsetPosition;
        _initialRightEuler = bat.rightGrabOffsetEuler;
        _captured = true;
    }

    private void Update()
    {
        if (!enableTuner || bat == null || !_captured) return;

        bool batInRightHand = bat.attachParent != null && bat.attachParent == bat.rightHandParent;

        if (XRInput.GetDown(XRButton.A))
            _pair = (AxisPair)(((int)_pair + 1) % 3);

        if (XRInput.GetDown(XRButton.Y))
        {
            bat.leftGrabOffsetPosition = _initialLeftPos;
            bat.leftGrabOffsetEuler = _initialLeftEuler;
            bat.rightGrabOffsetPosition = _initialRightPos;
            bat.rightGrabOffsetEuler = _initialRightEuler;
        }

        Vector2 rotStick = XRInput.GetThumbstick(leftHand: batInRightHand);
        Vector2 posStick = XRInput.GetThumbstick(leftHand: !batInRightHand);

        Vector3 dRot = Spread(rotStick) * (degreesPerSecond * Time.deltaTime);
        Vector3 dPos = Spread(posStick) * (metresPerSecond * Time.deltaTime);

        if (batInRightHand)
        {
            bat.rightGrabOffsetEuler += dRot;
            bat.rightGrabOffsetPosition += dPos;
        }
        else
        {
            bat.leftGrabOffsetEuler += dRot;
            bat.leftGrabOffsetPosition += dPos;
        }

        // B dumps the current values to the log as well, for a permanent record:
        //   adb logcat -v time -s Unity:I
        if (XRInput.GetDown(XRButton.B))
            Dump(batInRightHand);

        ShowOverlay(batInRightHand);
    }

    /// Map a 2D stick onto the two active axes of the current pair.
    private Vector3 Spread(Vector2 stick)
    {
        switch (_pair)
        {
            case AxisPair.XY: return new Vector3(stick.y, stick.x, 0f);
            case AxisPair.YZ: return new Vector3(0f, stick.x, stick.y);
            default: return new Vector3(stick.y, 0f, stick.x);
        }
    }

    private void Dump(bool batInRightHand)
    {
        Vector3 p = batInRightHand ? bat.rightGrabOffsetPosition : bat.leftGrabOffsetPosition;
        Vector3 e = batInRightHand ? bat.rightGrabOffsetEuler : bat.leftGrabOffsetEuler;
        Debug.LogWarning(string.Format(
            "[BatOffsetTuner] {0} hand -> Position ({1:F4}, {2:F4}, {3:F4})  Euler ({4:F2}, {5:F2}, {6:F2})",
            batInRightHand ? "RIGHT" : "LEFT", p.x, p.y, p.z, e.x, e.y, e.z));
    }

    /// <summary>
    /// Live readout on the in-world debug overlay (Controllers/DebugOverlay, the board standing
    /// in the ground behind the batsman). Main owns that Text and rewrites it every frame, so the
    /// lines are handed to it through Main.debugExtra rather than written directly.
    /// </summary>
    private void ShowOverlay(bool batInRightHand)
    {
        Main main = Main.Instance;
        if (main == null) return;

        Vector3 p = batInRightHand ? bat.rightGrabOffsetPosition : bat.leftGrabOffsetPosition;
        Vector3 e = batInRightHand ? bat.rightGrabOffsetEuler : bat.leftGrabOffsetEuler;
        main.debugExtra = string.Format(
            "BAT OFFSET TUNER  [{0} hand]\naxis pair: {1}   (A = cycle, B = dump, Y = reset)\n" +
            "pos   ({2:F4}, {3:F4}, {4:F4})\neuler ({5:F2}, {6:F2}, {7:F2})",
            batInRightHand ? "RIGHT" : "LEFT", _pair, p.x, p.y, p.z, e.x, e.y, e.z);
    }

    /// Leave the overlay as we found it, so the readout does not linger once tuning stops.
    private void OnDisable()
    {
        if (Main.Instance != null)
            Main.Instance.debugExtra = null;
    }
}
