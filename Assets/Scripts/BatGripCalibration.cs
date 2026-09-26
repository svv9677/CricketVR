using UnityEngine;

/// <summary>
/// Lets the player set the bat grip to their own hand instead of the offsets baked into the scene.
///
/// Flow: "Calibrate Grip - Upright" in the B menu stands the bat in front of the player, face
/// toward the bowler, toe just off the floor - a batting stance; "Calibrate Grip - Flat" lays it
/// level at waist height, handle toward the player. The bat is drawn see-through meanwhile so the
/// controller shows inside the handle. The player puts the batting-hand
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
    /// Lying flat: how far in front of the headset the grip end sits, and how high off the floor.
    private const float FlatDistance = 0.25f;
    private const float FlatHeight = 1.0f;

    private Material ghostMaterial;
    private Material[] solidMaterials;

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

    /// <param name="flat">
    /// False: the bat stands upright, toe on the floor, as in a batting stance.
    /// True: it floats level at waist height, handle toward the player, which is easier to line
    /// the controller up along.
    /// </param>
    public void Begin(bool flat)
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
        float floorY = head.parent != null ? head.parent.position.y : 0f;
        Renderer batRenderer = bat.GetComponentInChildren<Renderer>();

        Quaternion standRotation;
        Vector3 standPosition;
        if (flat)
        {
            // Handle (local +Z) toward the player, face (local +Y) toward the batter's front
            // foot side - left for a right-hander - which is how the face sits with the arms out.
            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward);
            standRotation = Quaternion.LookRotation(-flatForward, leftHand ? flatRight : -flatRight);
            standPosition = head.position + flatForward * FlatDistance;
            standPosition.y = floorY + FlatHeight;
            bat.transform.SetPositionAndRotation(standPosition, standRotation);
            // Put the grip end, not the middle of the bat, at FlatDistance in front of the player.
            if (batRenderer != null)
                standPosition += flatForward * (FlatDistance - HandleReach(batRenderer, head.position, flatForward));
        }
        else
        {
            // Handle (local +Z) straight up, face (local +Y, the normal Bat uses for the hit)
            // toward the bowler, who is down -X.
            standRotation = Quaternion.LookRotation(Vector3.up, Vector3.left);
            standPosition = head.position + flatForward * StandDistance;
            bat.transform.SetPositionAndRotation(standPosition, standRotation);
            // Drop it so the toe sits just above the floor. The tracking space origin is the floor.
            if (batRenderer != null)
                standPosition.y += floorY + ToeClearance - batRenderer.bounds.min.y;
        }

        bat.HoldStill(standPosition, standRotation);
        ShowGhost(true);
        ShowPrompt(new Vector3(standPosition.x, floorY + 1.45f, standPosition.z) + flatForward * 0.25f, flatForward);
        IsActive = true;
    }

    /// Distance along the player's forward from the head to the nearest point of the bat.
    private static float HandleReach(Renderer batRenderer, Vector3 headPosition, Vector3 flatForward)
    {
        Bounds b = batRenderer.bounds;
        float nearest = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z);
            nearest = Mathf.Min(nearest, Vector3.Dot(corner - headPosition, flatForward));
        }
        return nearest;
    }

    /// While calibrating the bat is drawn see-through so the controller shows inside the handle.
    private void ShowGhost(bool ghost)
    {
        Renderer batRenderer = bat.GetComponentInChildren<Renderer>();
        if (batRenderer == null)
            return;
        if (ghost)
        {
            if (ghostMaterial == null)
                ghostMaterial = Resources.Load<Material>("Materials/BatGhost");
            if (ghostMaterial == null)
                return;
            solidMaterials = batRenderer.sharedMaterials;
            var ghosts = new Material[solidMaterials.Length];
            for (int i = 0; i < ghosts.Length; i++)
                ghosts[i] = ghostMaterial;
            batRenderer.sharedMaterials = ghosts;
        }
        else if (solidMaterials != null)
        {
            batRenderer.sharedMaterials = solidMaterials;
            solidMaterials = null;
        }
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
        ShowGhost(false);
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
