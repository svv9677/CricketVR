using UnityEngine;

/// <summary>
/// Lets the player set the bat grip to their own hand instead of the offsets baked into the scene.
///
/// Flow: "Calibrate Bat Grip" (B menu or the between-balls panel) switches to passthrough - the
/// stadium goes and the player sees their own room, hands and controller - and shows two see-through
/// shells of the bat at once: one standing in front of them, face toward the bowler, toe just off
/// the floor (a batting stance), and one lying level at waist height beside the batting hand,
/// handle toward them. There is no switching between them: the player puts the batting-hand
/// controller into whichever shell is easier, holding it the way they hold a real bat, and presses
/// A (or Lock on the panel). The shell the hand is nearest is the one used; the hand-to-bat pose at
/// that moment becomes the grab offset for that hand and is saved to PlayerPrefs so it survives
/// restarts. B (or Cancel) puts the previous grip back.
///
/// Main drives this through <see cref="Tick"/> while it is active, and skips its own A/B handling,
/// so the A that locks the grip can never also start a delivery.
/// </summary>
public class BatGripCalibration : MonoBehaviour
{
    /// How far in front of the headset the upright shell stands, measured flat on the floor.
    private const float StandDistance = 0.35f;
    /// Gap between the toe of the upright shell and the floor.
    private const float ToeClearance = 0.02f;
    /// The flat shell: how far in front of the headset its grip end sits, how high off the floor,
    /// and how far out to the side of the batting hand, clear of the upright one.
    private const float FlatDistance = 0.25f;
    private const float FlatHeight = 1.0f;
    private const float FlatSide = 0.4f;

    private const string ShellLayer = "GripShell";

    private Material ghostMaterial;
    private Transform upright, flat;

    private Bat bat;
    private bool leftHand;
    private Vector3 previousPosition;
    private Vector3 previousEuler;
    private Vector3 defaultLeftPosition, defaultLeftEuler, defaultRightPosition, defaultRightEuler;
    /// Objects moved onto the shell layer to stay visible in passthrough, and their own layers.
    private readonly System.Collections.Generic.List<(GameObject go, int layer)> relayered =
        new System.Collections.Generic.List<(GameObject, int)>();

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

    /// <param name="flatFirst">Unused: both shells are shown together. Kept for Main's two menu entries.</param>
    public void Begin(bool flatFirst)
    {
        if (IsActive || bat.attachParent == null)
            return;

        leftHand = bat.attachParent == bat.leftHandParent;
        previousPosition = leftHand ? bat.leftGrabOffsetPosition : bat.rightGrabOffsetPosition;
        previousEuler = leftHand ? bat.leftGrabOffsetEuler : bat.rightGrabOffsetEuler;
        IsActive = true;

        Transform head = Camera.main.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
            forward = Vector3.left;
        forward.Normalize();
        float floorY = head.parent != null ? head.parent.position.y : 0f;

        if (upright == null)
            upright = MakeShell("Grip shell (upright)");
        if (flat == null)
            flat = MakeShell("Grip shell (flat)");
        PlaceUpright(head, forward, floorY);
        PlaceFlat(head, forward, floorY);
        upright.gameObject.SetActive(true);
        flat.gameObject.SetActive(true);

        // The real bat stays put and unseen (its layer is not drawn); no contact checks meanwhile.
        bat.HoldStill(upright.position, upright.rotation);

        // The panel sits off to the side of the free hand, so it never hides either shell.
        Vector3 side = Vector3.Cross(Vector3.up, forward) * (leftHand ? 0.45f : -0.45f);
        if (GripCalibrationPanel.Instance != null)
            GripCalibrationPanel.Instance.Show(head.position + forward * 0.7f + side + Vector3.down * 0.2f, forward);

        EnterPassthrough();
    }

    private Transform MakeShell(string shellName)
    {
        if (ghostMaterial == null)
            ghostMaterial = Resources.Load<Material>("Materials/BatGhost");
        var shell = new GameObject(shellName);
        shell.layer = LayerMask.NameToLayer(ShellLayer);
        shell.transform.localScale = bat.transform.lossyScale;
        shell.AddComponent<MeshFilter>().sharedMesh = bat.GetComponent<MeshFilter>().sharedMesh;
        var renderer = shell.AddComponent<MeshRenderer>();
        var mats = new Material[bat.GetComponent<MeshRenderer>().sharedMaterials.Length];
        for (int i = 0; i < mats.Length; i++)
            mats[i] = ghostMaterial;
        renderer.sharedMaterials = mats;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shell.SetActive(false);
        return shell.transform;
    }

    /// Handle (local +Z) straight up, face (local +Y, the normal Bat uses for the hit) toward the
    /// bowler, who is down -X; toe just above the floor. The tracking space origin is the floor.
    private void PlaceUpright(Transform head, Vector3 forward, float floorY)
    {
        Quaternion rotation = Quaternion.LookRotation(Vector3.up, Vector3.left);
        Vector3 position = head.position + forward * StandDistance;
        upright.SetPositionAndRotation(position, rotation);
        position.y += floorY + ToeClearance - upright.GetComponent<Renderer>().bounds.min.y;
        upright.position = position;
    }

    /// Level at waist height beside the batting hand, handle (local +Z) toward the player, face
    /// toward the batter's front-foot side - how the face sits with the arms out. The grip end, not
    /// the middle of the bat, sits FlatDistance in front of the player.
    private void PlaceFlat(Transform head, Vector3 forward, float floorY)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Quaternion rotation = Quaternion.LookRotation(-forward, leftHand ? right : -right);
        Vector3 position = head.position + forward * FlatDistance + right * (leftHand ? -FlatSide : FlatSide);
        position.y = floorY + FlatHeight;
        flat.SetPositionAndRotation(position, rotation);
        position += forward * (FlatDistance - NearestAlong(flat.GetComponent<Renderer>().bounds, head.position, forward));
        flat.position = position;
    }

    /// Distance along `direction` from `from` to the nearest corner of the bounds.
    private static float NearestAlong(Bounds b, Vector3 from, Vector3 direction)
    {
        float nearest = float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z);
            nearest = Mathf.Min(nearest, Vector3.Dot(corner - from, direction));
        }
        return nearest;
    }

    /// Only the shells, the panel and the pointer are drawn; everything else is the real room.
    private void EnterPassthrough()
    {
        int shell = LayerMask.NameToLayer(ShellLayer);
        relayered.Clear();
        if (GripCalibrationPanel.Instance != null)
            Relayer(GripCalibrationPanel.Instance.gameObject, shell);
        if (OpenXRMenuInputModule.Instance != null && OpenXRMenuInputModule.Instance.Beam != null)
            Relayer(OpenXRMenuInputModule.Instance.Beam.gameObject, shell);
        XRHandVisual.HideAll = true;
        Passthrough.Begin(Camera.main, 1 << shell);
    }

    private void ExitPassthrough()
    {
        Passthrough.End();
        XRHandVisual.HideAll = false;
        foreach (var (go, layer) in relayered)
            if (go != null)
                go.layer = layer;
        relayered.Clear();
    }

    private void Relayer(GameObject root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            relayered.Add((t.gameObject, t.gameObject.layer));
            t.gameObject.layer = layer;
        }
    }

    /// Called by Main every frame while active, with this frame's button presses. The switch button
    /// used to flip between upright and flat; both are shown now, so it does nothing.
    public void Tick(bool lockPressed, bool cancelPressed, bool switchPressed)
    {
        if (cancelPressed)
        {
            Cancel();
            return;
        }
        if (lockPressed)
            Lock();
    }

    public void Cancel()
    {
        if (!IsActive)
            return;
        bat.SetGrabOffset(leftHand, previousPosition, previousEuler);
        End();
    }

    public void Lock()
    {
        if (!IsActive)
            return;
        Transform hand = bat.attachParent;
        Transform shell = HandleDistance(upright, hand.position) <= HandleDistance(flat, hand.position) ? upright : flat;
        // Same arithmetic as Bat.LateUpdate, solved the other way round:
        //   batPos = hand.position + hand.rotation * offsetPosition
        //   batRot = hand.rotation * Quaternion.Euler(offsetEuler)
        Quaternion toHand = Quaternion.Inverse(hand.rotation);
        Vector3 offsetPosition = toHand * (shell.position - hand.position);
        Vector3 offsetEuler = (toHand * shell.rotation).eulerAngles;

        bat.SetGrabOffset(leftHand, offsetPosition, offsetEuler);
        Save(leftHand, offsetPosition, offsetEuler);
        XRInput.SendHaptics(leftHand, 0.6f, 0.15f);
        End();
    }

    /// How far the hand is from the shell's long axis (a bat is held on its handle, and a hand
    /// halfway down the blade still clearly means that bat).
    private static float HandleDistance(Transform shell, Vector3 point)
    {
        Bounds local = shell.GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 a = shell.TransformPoint(new Vector3(0f, 0f, local.min.z));
        Vector3 b = shell.TransformPoint(new Vector3(0f, 0f, local.max.z));
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector3.Distance(point, a + ab * t);
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
        PlayerPrefs.DeleteKey(PP_GripLeftScale);
        PlayerPrefs.DeleteKey(PP_GripRightScale);
        PlayerPrefs.Save();
    }

    private void End()
    {
        ExitPassthrough();
        bat.ReleaseHold();
        if (upright != null) upright.gameObject.SetActive(false);
        if (flat != null) flat.gameObject.SetActive(false);
        if (GripCalibrationPanel.Instance != null)
            GripCalibrationPanel.Instance.Hide();
        IsActive = false;
    }

    private void OnDisable()
    {
        if (IsActive)
            Cancel();
    }

    private void LoadSaved(bool left)
    {
        string posKey = left ? Constants.PP_GripLeftPosition : Constants.PP_GripRightPosition;
        string eulerKey = left ? Constants.PP_GripLeftEuler : Constants.PP_GripRightEuler;
        if (!PlayerPrefs.HasKey(posKey) || !PlayerPrefs.HasKey(eulerKey))
            return;

        Vector3 position = JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(posKey));
        Vector3 euler = JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(eulerKey));
        // A grip saved on a bat of another size: keep the hand on the same point of the handle.
        float savedScale = PlayerPrefs.GetFloat(left ? PP_GripLeftScale : PP_GripRightScale, ScaleBeforeRecording);
        position = RescaleGrip(position, euler, savedScale, bat.transform.lossyScale.x);
        bat.SetGrabOffset(left, position, euler);
    }

    private void Save(bool left, Vector3 position, Vector3 euler)
    {
        PlayerPrefs.SetString(left ? Constants.PP_GripLeftPosition : Constants.PP_GripRightPosition, JsonUtility.ToJson(position));
        PlayerPrefs.SetString(left ? Constants.PP_GripLeftEuler : Constants.PP_GripRightEuler, JsonUtility.ToJson(euler));
        PlayerPrefs.SetFloat(left ? PP_GripLeftScale : PP_GripRightScale, bat.transform.lossyScale.x);
        PlayerPrefs.Save();
    }

    private const string PP_GripLeftScale = "GripLeftBatScale", PP_GripRightScale = "GripRightBatScale";
    /// Grips saved before the scale was recorded were all made on the 0.51 bat.
    private const float ScaleBeforeRecording = 0.51f;
    /// Where the hands hold the bat, along its local Z (the grip section of the handle, see BatGripPreview).
    public const float GripPointLocalZ = 0.83f;

    /// <summary>
    /// The grab offset that holds a bat of scale `to` at the same point of its handle as `offset`
    /// held one of scale `from`. The bat is placed at hand + offset (hand space) with rotation
    /// Euler(euler), and the grip point lies GripPointLocalZ * scale along its +Z from there; move
    /// the bat along that axis by the difference so the grip point stays in the hand.
    /// </summary>
    public static Vector3 RescaleGrip(Vector3 offset, Vector3 euler, float from, float to)
    {
        return offset + Quaternion.Euler(euler) * new Vector3(0f, 0f, GripPointLocalZ * (from - to));
    }
}
