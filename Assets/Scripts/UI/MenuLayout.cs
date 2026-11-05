using UnityEngine;

/// <summary>
/// Where every screen comes up: fixed spots in front of the player, not wherever they happen to
/// be looking. The MenuLayout prefab (Assets/Resources/Prefabs/UI/MenuLayout.prefab, built by
/// Tools > CricketVR > Build Menu Layout) is one root at standing eye height facing the way the
/// player faces - the bowler, in a match - with a child per screen at its pose relative to that
/// eye point. Each scene places one instance: at the crease in CricketVR, at the origin in Splash.
///
/// Spots rise and fall with the session's camera-height setting, as the player's view does.
/// A scene without a layout falls back to each screen's old in-front-of-the-gaze placement.
///
/// In a career match the player may watch his partner from the non-striker's end (PlayerEnd): the
/// whole layout is then turned end for end with him (EndPose), so every screen that asks it -
/// the next-ball card, the over break, the match and career cards - comes up in front of him there.
/// </summary>
public class MenuLayout : MonoBehaviour
{
    public const string Menu = "Menu", Settings = "Settings", NextBall = "NextBall",
                        GripPanelLeftHanded = "GripPanelLeftHanded", GripPanelRightHanded = "GripPanelRightHanded";

    public static MenuLayout Instance { get; private set; }

    /// The pose the scene placed it at: the striker's crease.
    private Vector3 creasePosition;
    private Quaternion creaseRotation;

    private void Awake()
    {
        Instance = this;
        creasePosition = transform.position;
        creaseRotation = transform.rotation;
    }

    private void OnEnable()
    {
        PlayerEnd.Changed += FollowEnd;
        FollowEnd();
    }

    private void OnDisable() => PlayerEnd.Changed -= FollowEnd;

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// At the player's end: the crease pose as placed, or that pose carried to the non-striker's end.
    private void FollowEnd()
    {
        bool far = PlayerEnd.NonStriker;
        transform.SetPositionAndRotation(EndPose.FromCrease(creasePosition, far), EndPose.FromCrease(creaseRotation, far));
    }

    /// The fixed eye point and the level direction the player faces.
    public static bool TryGetEye(out Vector3 eye, out Vector3 forward)
    {
        eye = Vector3.zero;
        forward = Vector3.forward;
        if (Instance == null)
            return false;
        Transform t = Instance.transform;
        eye = t.position + Vector3.up * GameSettings.CameraHeight;
        forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        return true;
    }

    /// The pose of the named spot, or false when this scene has no layout (or no such spot).
    public static bool TryGet(string spot, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Transform t = Instance != null ? Instance.transform.Find(spot) : null;
        if (t == null)
            return false;
        position = t.position + Vector3.up * GameSettings.CameraHeight;
        rotation = t.rotation;
        return true;
    }
}
