using UnityEngine;

/// <summary>
/// Lets the player set the bat grip to their own hand instead of the offsets baked into the scene.
///
/// Flow: "Calibrate Bat Grip" in the B menu stands the bat upright in front of the player, face
/// toward the bowler, toe just off the floor - a batting stance. The player puts the batting-hand
/// controller on the handle the way they hold a real bat and presses A. The hand-to-bat pose at
/// that moment becomes the grab offset for that hand, and is saved to PlayerPrefs so it survives
/// restarts. B cancels and puts the previous grip back.
///
/// Main drives this through <see cref="Tick"/> while it is active, and skips its own A/B handling,
/// so the A that locks the grip can never also start a delivery.
/// </summary>
public class BatGripCalibration : MonoBehaviour
{
    /// How far in front of the headset the bat is stood up, measured flat on the floor.
    private const float StandDistance = 0.35f;
    /// Gap between the toe of the bat and the floor while it stands.
    private const float ToeClearance = 0.02f;

    private Bat bat;
    private bool leftHand;
    private Vector3 previousPosition;
    private Vector3 previousEuler;
    private Vector3 defaultLeftPosition, defaultLeftEuler, defaultRightPosition, defaultRightEuler;
    private TMPro.TextMeshPro prompt;

    public bool IsActive { get; private set; }

    private void Awake()
    {
        bat = GetComponent<Bat>();

        // The scene values are the defaults a reset goes back to.
        defaultLeftPosition = bat.leftGrabOffsetPosition;
        defaultLeftEuler = bat.leftGrabOffsetEuler;
        defaultRightPosition = bat.rightGrabOffsetPosition;
        defaultRightEuler = bat.rightGrabOffsetEuler;

        LoadSaved(true);
        LoadSaved(false);
    }

    public void Begin()
    {
        if (IsActive || bat.attachParent == null)
            return;

        leftHand = bat.attachParent == bat.leftHandParent;
        previousPosition = leftHand ? bat.leftGrabOffsetPosition : bat.rightGrabOffsetPosition;
        previousEuler = leftHand ? bat.leftGrabOffsetEuler : bat.rightGrabOffsetEuler;

        Transform head = Camera.main.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 1e-4f)
            flatForward = Vector3.left;
        flatForward.Normalize();

        // Handle (local +Z) straight up, face (local +Y, the normal Bat uses for the hit) toward
        // the bowler, who is down -X.
        Quaternion standRotation = Quaternion.LookRotation(Vector3.up, Vector3.left);
        Vector3 standPosition = head.position + flatForward * StandDistance;
        bat.transform.SetPositionAndRotation(standPosition, standRotation);

        // Drop it so the toe sits just above the floor. The tracking space origin is the floor.
        float floorY = head.parent != null ? head.parent.position.y : 0f;
        Renderer batRenderer = bat.GetComponentInChildren<Renderer>();
        if (batRenderer != null)
            standPosition.y += floorY + ToeClearance - batRenderer.bounds.min.y;

        bat.HoldStill(standPosition, standRotation);
        ShowPrompt(standPosition + Vector3.up * 0.65f, flatForward);
        IsActive = true;
    }

    /// Called by Main every frame while active, with this frame's button presses.
    public void Tick(bool lockPressed, bool cancelPressed)
    {
        if (cancelPressed)
        {
            bat.SetGrabOffset(leftHand, previousPosition, previousEuler);
            End();
            return;
        }
        if (!lockPressed)
            return;

        // Same arithmetic as Bat.LateUpdate, solved the other way round:
        //   batPos = hand.position + hand.rotation * offsetPosition
        //   batRot = hand.rotation * Quaternion.Euler(offsetEuler)
        Transform hand = bat.attachParent;
        Quaternion toHand = Quaternion.Inverse(hand.rotation);
        Vector3 offsetPosition = toHand * (bat.transform.position - hand.position);
        Vector3 offsetEuler = (toHand * bat.transform.rotation).eulerAngles;

        bat.SetGrabOffset(leftHand, offsetPosition, offsetEuler);
        Save(leftHand, offsetPosition, offsetEuler);
        XRInput.SendHaptics(leftHand, 0.6f, 0.15f);
        End();
    }

    /// Back to the scene's grip for both hands, and forget anything saved.
    public void ResetToDefault()
    {
        bat.SetGrabOffset(true, defaultLeftPosition, defaultLeftEuler);
        bat.SetGrabOffset(false, defaultRightPosition, defaultRightEuler);
        PlayerPrefs.DeleteKey(Constants.PP_GripLeftPosition);
        PlayerPrefs.DeleteKey(Constants.PP_GripLeftEuler);
        PlayerPrefs.DeleteKey(Constants.PP_GripRightPosition);
        PlayerPrefs.DeleteKey(Constants.PP_GripRightEuler);
        PlayerPrefs.Save();
    }

    private void End()
    {
        bat.ReleaseHold();
        if (prompt != null)
            prompt.gameObject.SetActive(false);
        IsActive = false;
    }

    private void ShowPrompt(Vector3 position, Vector3 flatForward)
    {
        if (prompt == null)
        {
            var go = new GameObject("GripCalibrationPrompt");
            prompt = go.AddComponent<TMPro.TextMeshPro>();
            prompt.text = "Hold the handle the way you bat\nthen press <b>A</b> to lock the grip\n<size=70%>B cancels</size>";
            prompt.fontSize = 1f;
            prompt.alignment = TMPro.TextAlignmentOptions.Center;
            prompt.rectTransform.sizeDelta = new Vector2(1.2f, 0.3f);
        }
        prompt.gameObject.SetActive(true);
        // TextMeshPro reads toward its -Z, so point +Z away from the player.
        prompt.transform.SetPositionAndRotation(position, Quaternion.LookRotation(flatForward, Vector3.up));
    }

    private void LoadSaved(bool left)
    {
        string posKey = left ? Constants.PP_GripLeftPosition : Constants.PP_GripRightPosition;
        string eulerKey = left ? Constants.PP_GripLeftEuler : Constants.PP_GripRightEuler;
        if (!PlayerPrefs.HasKey(posKey) || !PlayerPrefs.HasKey(eulerKey))
            return;

        Vector3 position = JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(posKey));
        Vector3 euler = JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(eulerKey));
        bat.SetGrabOffset(left, position, euler);
    }

    private static void Save(bool left, Vector3 position, Vector3 euler)
    {
        PlayerPrefs.SetString(left ? Constants.PP_GripLeftPosition : Constants.PP_GripRightPosition, JsonUtility.ToJson(position));
        PlayerPrefs.SetString(left ? Constants.PP_GripLeftEuler : Constants.PP_GripRightEuler, JsonUtility.ToJson(euler));
        PlayerPrefs.Save();
    }
}
