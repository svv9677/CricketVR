using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Controller-driven hand visual. Needs no optical hand tracking and no OVR runtime.
///
/// The pose comes from the Oculus CustomHands sample's animator (restored under Assets/Hands),
/// which blends authored hand poses on a 2D freeform tree - X is <c>Flex</c>, the grip squeeze
/// from flat to fist, and Y is <c>Pinch</c>, the index curl. Two extra layers hold the gesture
/// overlays, "Point Layer" and "Thumb Layer", driven by their layer weight.
///
/// That is why this drives an Animator rather than rotating bones directly: the earlier version
/// rotated every b_* bone around its local Z by a guessed angle, which cannot reproduce a real
/// grip - each joint has a different axis - and had to guess bone names besides. The authored
/// clips already contain the correct per-joint rotations, so the only job left is mapping OpenXR
/// input onto the two blend parameters.
/// </summary>
public class XRHandVisual : MonoBehaviour
{
    public bool isLeftHand;

    [Tooltip("Animator on the hand model. Found on a child if left empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("How far a full grip squeeze closes the hand. The sample ran at 0.5 so the fingers " +
             "keep a relaxed curl instead of clenching, which reads better on an empty hand.")]
    [SerializeField, Range(0f, 1f)] private float maxFlex = 0.5f;

    [Tooltip("Flex held while the bat is in this hand, regardless of the grip axis.")]
    [SerializeField, Range(0f, 1f)] private float batGripFlex = 1f;

    // HandPoseId from the sample's HandPose.cs, which the animator's Pose parameter selects.
    private const int PoseDefault = 0;
    private const int PoseGeneric = 1;

    private const string LayerPoint = "Point Layer";
    private const string LayerThumb = "Thumb Layer";

    /// Gesture blend speed, in units per second. Matches the sample's INPUT_RATE_CHANGE, which
    /// exists because the capacitive sensors flicker and snapping the pose looks like a twitch.
    private const float GestureRate = 20f;

    private Renderer[] renderers;
    private int paramFlex, paramPinch, paramPose;
    private int layerPoint = -1, layerThumb = -1;
    private float pointBlend, thumbBlend;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        paramFlex = Animator.StringToHash("Flex");
        paramPinch = Animator.StringToHash("Pinch");
        paramPose = Animator.StringToHash("Pose");
        if (animator != null)
        {
            layerPoint = animator.GetLayerIndex(LayerPoint);
            layerThumb = animator.GetLayerIndex(LayerThumb);
        }

        SetTracked(false);
    }

    /// Hide the hand when its controller is not reporting a pose, so it does not hang in mid-air.
    public void SetTracked(bool tracked)
    {
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = tracked;
    }

    private void Update()
    {
        if (animator == null) return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(isLeftHand ? XRNode.LeftHand : XRNode.RightHand);
        device.TryGetFeatureValue(CommonUsages.grip, out float grip);
        device.TryGetFeatureValue(CommonUsages.trigger, out float trigger);

        Bat bat = Main.Instance != null ? Main.Instance.theBatScript : null;
        bool holdingBat = bat != null && bat.gameObject.activeInHierarchy
            && bat.attachParent == (isLeftHand ? bat.leftHandParent : bat.rightHandParent);

        // The gestures come off the capacitive near-touch sensors: lifting the index finger clear
        // of the trigger points, lifting the thumb off the buttons gives a thumbs up. Never
        // gesture while the bat is held - the hand is wrapped around the handle.
        bool pointing = false, thumbsUp = false;
        if (!holdingBat)
        {
            if (TryReadTouch(device, "IndexTouch", out bool indexTouching)) pointing = !indexTouching;
            if (TryReadTouch(device, "ThumbTouch", out bool thumbTouching)) thumbsUp = !thumbTouching;
        }
        pointBlend = Approach(pointing, pointBlend);
        thumbBlend = Approach(thumbsUp, thumbBlend);

        animator.SetInteger(paramPose, holdingBat ? PoseGeneric : PoseDefault);
        animator.SetFloat(paramFlex, holdingBat ? batGripFlex : grip * maxFlex);
        animator.SetFloat(paramPinch, trigger);
        if (layerPoint >= 0) animator.SetLayerWeight(layerPoint, pointBlend);
        if (layerThumb >= 0) animator.SetLayerWeight(layerThumb, thumbBlend);
    }

    /// <summary>
    /// Read a capacitive near-touch sensor by feature name.
    ///
    /// The usage is not fetched through a constant because the providers disagree on its type -
    /// Unity's CommonUsages declares IndexTouch/ThumbTouch as float, the Oculus provider declares
    /// them as bool - and TryGetFeatureValue matches on name *and* type, so a constant from the
    /// wrong one silently never reads. Asking by name for both types sidesteps that, and keeps this
    /// off the Oculus package, which the project only still has transitively via com.unity.feature.vr.
    ///
    /// Returns false when the active interaction profile does not expose the sensor at all. The
    /// caller must not treat that as "finger lifted", or the hand sticks in a permanent point.
    /// </summary>
    private static bool TryReadTouch(InputDevice device, string feature, out bool touching)
    {
        if (device.TryGetFeatureValue(new InputFeatureUsage<bool>(feature), out bool asBool))
        {
            touching = asBool;
            return true;
        }
        if (device.TryGetFeatureValue(new InputFeatureUsage<float>(feature), out float asFloat))
        {
            touching = asFloat > 0.5f;
            return true;
        }
        touching = false;
        return false;
    }

    private static float Approach(bool isOn, float value)
    {
        return Mathf.Clamp01(value + Time.deltaTime * GestureRate * (isOn ? 1f : -1f));
    }
}
