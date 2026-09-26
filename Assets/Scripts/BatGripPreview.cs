using UnityEngine;

/// <summary>
/// Editor-time preview of how the bat sits in each hand, for finding the grab offsets without a
/// device build.
///
/// Tuning these offsets used to need an APK and an in-headset session with a controller-driven
/// tuner, with the numbers read back afterwards - and a whole session was lost when the values
/// never made it off the device. The offsets are pure geometry, though: the bat is placed from the
/// hand pose and four vectors, with no input and no physics involved. So the same job is done here
/// in the Editor, showing the real hand model in its real grip pose with the real bat placed by the
/// same arithmetic the game uses. That replaced the on-device tuner, which has been removed.
///
/// Runs with [ExecuteAlways] so dragging the fields below moves the bat immediately in the Scene
/// view. Live in Assets/Scenes/BatOffsetDebug.unity, built by
/// Tools/CricketVR/Build Bat Offset Debug Scene.
///
/// When the bat looks right, use the component's context menu (the three dots) to log the values or
/// write them straight into the Bat prefab.
/// </summary>
[ExecuteAlways]
public class BatGripPreview : MonoBehaviour
{
    [Header("Left hand")]
    public Vector3 leftGrabOffsetPosition = new Vector3(0.18f, 0.10f, 0.18f);
    public Vector3 leftGrabOffsetEuler = new Vector3(270f, 180f, 0f);

    [Header("Right hand")]
    public Vector3 rightGrabOffsetPosition = new Vector3(-0.25f, 0.09f, 0.20f);
    public Vector3 rightGrabOffsetEuler = new Vector3(270f, 180f, 0f);

    [Header("Scene wiring (set up by the builder)")]
    [Tooltip("Stands in for Bat.leftHandParent - the transform the bat attaches to.")]
    public Transform leftGrip;
    public Transform rightGrip;
    public Transform leftBat;
    public Transform rightBat;
    public Animator leftHand;
    public Animator rightHand;

    [Header("Grip point on the bat")]
    [Tooltip("Where along the bat's local Z the hand should hold it. The blade is -Z (the wide part, " +
             "X width 0.2265 from z -0.89 up to +0.30) and the handle is +Z, with the grip section " +
             "between z +0.74 and +0.89. Note the bat's 'Tracker' child at z -0.83 is NOT the grip - " +
             "it sits near the blade tip and Bat uses it as the swing-speed reference.")]
    [Range(0.3f, 0.9f)] public float gripPointLocalZ = 0.83f;

    [Header("Hand pose")]
    [Tooltip("Hold the hands in the bat grip pose, which is what they do in game while holding it.")]
    public bool closedFists = true;
    [Range(0f, 1f)] public float flex = 1f;
    [Range(0f, 1f)] public float pinch = 0f;

    /// HandPoseId.Generic from the sample's HandPose.cs - the animator's "Generic Hold".
    private const int PoseGeneric = 1;
    private const int PoseDefault = 0;

    private void OnEnable() { Apply(); }
    private void OnValidate() { Apply(); }
    private void Update() { Apply(); }

    /// <summary>
    /// Place each bat exactly as <c>Bat.LateUpdate</c> does, so a value that looks right here is
    /// the value the game will use:
    ///   finalPos = grip.position + grip.rotation * grabOffsetPosition
    ///   finalRot = grip.rotation * Quaternion.Euler(grabOffsetEuler)
    /// </summary>
    public void Apply()
    {
        Place(leftGrip, leftBat, leftGrabOffsetPosition, leftGrabOffsetEuler);
        Place(rightGrip, rightBat, rightGrabOffsetPosition, rightGrabOffsetEuler);
        Pose(leftHand);
        Pose(rightHand);
    }

    private static void Place(Transform grip, Transform bat, Vector3 offsetPosition, Vector3 offsetEuler)
    {
        if (grip == null || bat == null) return;
        bat.position = grip.position + grip.rotation * offsetPosition;
        bat.rotation = grip.rotation * Quaternion.Euler(offsetEuler);
    }

    private void Pose(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetInteger("Pose", closedFists ? PoseGeneric : PoseDefault);
        animator.SetFloat("Flex", closedFists ? flex : 0f);
        animator.SetFloat("Pinch", pinch);
        // Animators do not tick in edit mode, so drive it by hand. A few steps let the state
        // machine settle rather than showing a half-finished transition.
        if (!Application.isPlaying)
            for (int i = 0; i < 4; i++) animator.Update(0.25f);
    }

    /// The grip bone inside the hand model - roughly where the handle should end up.
    public Transform GripBone(bool left)
    {
        Animator hand = left ? leftHand : rightHand;
        if (hand == null) return null;
        string suffix = left ? "b_l_grip" : "b_r_grip";
        foreach (var t in hand.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }

    /// World position of the chosen grip point on the bat.
    public Vector3 GripPoint(bool left)
    {
        Transform bat = left ? leftBat : rightBat;
        if (bat == null) return Vector3.zero;
        return bat.position + bat.rotation * new Vector3(0f, 0f, gripPointLocalZ * bat.localScale.z);
    }

    /// <summary>Distance from the hand's grip bone to the bat's grip point - the number to minimise.</summary>
    public float HandleDistance(bool left)
    {
        Transform bone = GripBone(left);
        Transform bat = left ? leftBat : rightBat;
        if (bone == null || bat == null) return -1f;
        return Vector3.Distance(bone.position, GripPoint(left));
    }

    /// <summary>
    /// The offset that puts <see cref="gripPointLocalZ"/> exactly on the hand's grip bone, leaving
    /// the euler alone. Inverts the placement in <c>Apply</c>:
    ///   bat.position = grip.position + grip.rotation * offset
    ///   gripPoint    = bat.position  + batRotation  * (0,0,gripPointLocalZ * scale)
    /// </summary>
    public bool SolvePosition(bool left, out Vector3 offset)
    {
        offset = Vector3.zero;
        Transform grip = left ? leftGrip : rightGrip;
        Transform bat = left ? leftBat : rightBat;
        Transform bone = GripBone(left);
        if (grip == null || bat == null || bone == null) return false;

        Vector3 euler = left ? leftGrabOffsetEuler : rightGrabOffsetEuler;
        Quaternion batRotation = grip.rotation * Quaternion.Euler(euler);
        Vector3 localGrip = new Vector3(0f, 0f, gripPointLocalZ * bat.localScale.z);
        offset = Quaternion.Inverse(grip.rotation) * (bone.position - grip.position - batRotation * localGrip);
        return true;
    }

    private void OnDrawGizmos()
    {
        DrawSide(true);
        DrawSide(false);
    }

    private void DrawSide(bool left)
    {
        Transform grip = left ? leftGrip : rightGrip;
        Transform bat = left ? leftBat : rightBat;
        if (grip == null || bat == null) return;

        // The attach point.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(grip.position, 0.012f);

        // The hand's grip bone - aim the handle at this.
        Transform bone = GripBone(left);
        if (bone != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bone.position, 0.018f);
            Gizmos.color = new Color(0f, 1f, 0f, 0.35f);
            Gizmos.DrawLine(bone.position, bat.position);
        }

        // The bat's long axis. Blade is -Z, handle is +Z.
        float half = 0.67f;
        Gizmos.color = new Color(0.2f, 0.7f, 1f);
        Gizmos.DrawLine(bat.position, bat.position - bat.forward * half);   // blade
        Gizmos.color = Color.white;
        Gizmos.DrawLine(bat.position, bat.position + bat.forward * half);   // handle

        // The chosen grip point - drag the offsets until this sits inside the green bone sphere.
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(GripPoint(left), 0.02f);
    }
}
