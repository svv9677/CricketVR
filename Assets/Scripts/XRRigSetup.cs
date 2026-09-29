using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Restores the two things OVRCameraRig used to do before the OVR -> Unity XR migration
/// deleted it:
///
///   (a) put the rig into a floor-relative tracking space, so the HMD reports the player's
///       real eye height above the physical floor, and
///   (b) drive the hand anchors (that half is restored by XRControllerTracker, one on each
///       of LeftHandAnchor / RightHandAnchor).
///
/// Without (a) the camera read a raw, unreferenced HMD pose, and the rig root had been
/// lifted to y = 2.65 to compensate - which left the player floating roughly a metre above
/// the 1.86 m fielders. With a floor origin the rig root belongs at y = 0 and every player
/// gets their own correct height for free.
/// </summary>
[DefaultExecutionOrder(-100)]
public class XRRigSetup : MonoBehaviour
{
    [Tooltip("Eye height used when there is no headset (plain Editor play mode) or when the " +
             "runtime cannot give us a floor-relative origin, so the Game view roughly matches " +
             "what the player sees on device.")]
    [SerializeField] private float fallbackEyeHeight = 1.7f;

    [Tooltip("The head/eye transform. Defaults to Camera.main's transform.")]
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Frames to keep retrying for an XR input subsystem before giving up and using " +
             "the fallback height. Subsystems are not always running on the first Start().")]
    [SerializeField] private int subsystemRetryFrames = 120;

    private static readonly List<XRInputSubsystem> Subsystems = new List<XRInputSubsystem>();

    /// True once a floor-relative tracking origin is in effect.
    public bool HasFloorOrigin { get; private set; }

    private bool _applyFallbackEachFrame;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        // Start in the fallback so the very first frame is never rendered from the floor.
        _applyFallbackEachFrame = true;
    }

    private void Update()
    {
        if (!HasFloorOrigin && subsystemRetryFrames > 0)
        {
            subsystemRetryFrames--;
            if (TryApplyFloorTrackingOrigin())
            {
                HasFloorOrigin = true;
                _applyFallbackEachFrame = false;
                Debug.Log("[XRRigSetup] Floor tracking origin active; eye height is the player's own.");
            }
        }
    }

    private void LateUpdate()
    {
        // Runs after TrackedPoseDriver. With no device the driver leaves the transform alone,
        // so holding the camera at a sensible eye height here keeps Editor play usable.
        if (_applyFallbackEachFrame && cameraTransform != null)
        {
            Vector3 p = cameraTransform.localPosition;
            p.y = fallbackEyeHeight;
            cameraTransform.localPosition = p;
        }
        PlaceAtCreaseWhenReady();
        // The session's height adjustment (Settings > View & Comfort), on top of the floor.
        Vector3 root = transform.position;
        root.y = _floorY + GameSettings.CameraHeight;
        transform.position = root;
    }

    // ---- Standing at the crease ---------------------------------------------------------------

    [Tooltip("Where the player's head is put: at the batting crease, on the middle-stump line.")]
    [SerializeField] private Vector2 creaseHeadXZ = new Vector2(BallDelivery.BatsmanStumpsX - 0.95f, 0f);

    private float _floorY;
    private bool _placed;
    private int _placeDelayFrames = 3;
    private bool _subscribed;

    private void Start()
    {
        _floorY = transform.position.y;
    }

    private void OnDisable()
    {
        if (!_subscribed)
            return;
        SubsystemManager.GetSubsystems(Subsystems);
        foreach (XRInputSubsystem s in Subsystems)
            s.trackingOriginUpdated -= OnTrackingOriginUpdated;
        _subscribed = false;
    }

    /// The player stands still at the crease: wherever they are in their room, the rig is moved
    /// and turned so their head is on the crease, facing the bowler. Done once the headset is
    /// tracking, and again whenever it is recentred (the Oculus button held).
    private void PlaceAtCreaseWhenReady()
    {
        if (!_subscribed)
        {
            SubsystemManager.GetSubsystems(Subsystems);
            foreach (XRInputSubsystem s in Subsystems)
            {
                s.trackingOriginUpdated += OnTrackingOriginUpdated;
                _subscribed = true;
            }
        }
        // Only in a match (CricketVR, Nets): Splash has no crease, just the menu.
        if (_placed || cameraTransform == null || Main.Instance == null)
            return;
        // A few frames in, so the first tracked head pose is in.
        if (_placeDelayFrames-- > 0)
            return;
        PlaceAtCrease();
    }

    private void OnTrackingOriginUpdated(XRInputSubsystem subsystem) => _placed = false;

    /// Turn the rig about the head so the head faces the bowler (down -X), then slide it so the
    /// head is over the crease.
    public void PlaceAtCrease()
    {
        if (cameraTransform == null)
            return;
        Vector3 facing = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (facing.sqrMagnitude > 1e-4f)
        {
            float turn = Vector3.SignedAngle(facing, Vector3.left, Vector3.up);
            transform.RotateAround(cameraTransform.position, Vector3.up, turn);
        }
        Vector3 head = cameraTransform.position;
        transform.position += new Vector3(creaseHeadXZ.x - head.x, 0f, creaseHeadXZ.y - head.z);
        _placed = true;
    }

    /// <summary>
    /// Ask every running XR input subsystem for a floor-relative origin. Returns false when
    /// no subsystem is running yet, or when none of them accepted the request.
    /// </summary>
    private bool TryApplyFloorTrackingOrigin()
    {
        SubsystemManager.GetSubsystems(Subsystems);
        if (Subsystems.Count == 0)
            return false;

        bool applied = false;
        for (int i = 0; i < Subsystems.Count; i++)
        {
            XRInputSubsystem subsystem = Subsystems[i];
            if (subsystem == null || !subsystem.running)
                continue;

            TrackingOriginModeFlags supported = subsystem.GetSupportedTrackingOriginModes();

            if ((supported & TrackingOriginModeFlags.Floor) != 0)
            {
                if (subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    applied = true;
                else
                    Debug.LogWarning("[XRRigSetup] Floor origin is supported but TrySetTrackingOriginMode failed.");
            }
            else if (supported != TrackingOriginModeFlags.Unknown)
            {
                // Device-relative runtime: the HMD reports ~0 at eye level, so the fallback
                // height is the correct behaviour rather than a degraded one.
                Debug.LogWarning("[XRRigSetup] Runtime has no floor origin; holding a fixed eye height of "
                                 + fallbackEyeHeight + " m.");
            }
        }
        return applied;
    }

    /// <summary>Re-centre the player's forward direction and position on the rig origin.</summary>
    public void Recenter()
    {
        SubsystemManager.GetSubsystems(Subsystems);
        for (int i = 0; i < Subsystems.Count; i++)
            if (Subsystems[i] != null && Subsystems[i].running)
                Subsystems[i].TryRecenter();
    }
}
