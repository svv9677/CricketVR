using UnityEngine;

/// <summary>
/// Which way in the player's real room is "down the pitch", kept across scenes, ends and app
/// restarts, so he never has to turn round to find the bowler again.
///
/// The direction is stored as a yaw in the rig's own frame (tracking space): the way the head
/// pointed, relative to the rig root, at the moment it was taken. That frame is the headset's,
/// so it stays put in the room while the rig is moved about the world; a world yaw would not.
/// It is taken the first time the player is ever placed (wherever he looks then, as before) and
/// again after every recentre (the Oculus button held, which redraws tracking space so an old
/// value would point somewhere arbitrary). XRRigSetup decides when; this class holds the value
/// and the arithmetic.
///
/// A placement then turns the rig so that stored direction lands on the scene's facing for the
/// player's end - the bowler (-X) at the crease, the batter (+X) from the non-striker's spot - and
/// slides the head onto its spot. Where the head happens to be looking at that moment no longer
/// matters: the far end is always the same wall of his room.
///
/// Yaws are degrees clockwise from +Z seen from above, the same as Transform.eulerAngles.y.
/// </summary>
public static class RoomFacing
{
    /// PlayerPrefs key for the saved tracking-space yaw.
    public const string PrefKey = "room_down_pitch_yaw";

    /// Below this a level projection is too short to give a direction (looking straight up/down).
    private const float MinLevelSqr = 1e-4f;

    // ---- Saved value ---------------------------------------------------------------------------

    /// The saved down-the-pitch yaw, or false on a fresh install (or after Clear).
    public static bool TryLoad(out float trackingYaw, string key = PrefKey)
    {
        trackingYaw = 0f;
        if (!PlayerPrefs.HasKey(key))
            return false;
        trackingYaw = Normalize(PlayerPrefs.GetFloat(key));
        return true;
    }

    /// Written through to disk now: the headset may be taken off and the app killed at any time.
    public static void Save(float trackingYaw, string key = PrefKey)
    {
        PlayerPrefs.SetFloat(key, Normalize(trackingYaw));
        PlayerPrefs.Save();
    }

    public static void Clear(string key = PrefKey)
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    // ---- Arithmetic (pure; RoomFacingTests) ----------------------------------------------------

    /// 0 to 360.
    public static float Normalize(float yaw) => Mathf.Repeat(yaw, 360f);

    /// The yaw of a direction's level part; false when it is (nearly) vertical.
    public static bool TryYawOf(Vector3 direction, out float yaw)
    {
        yaw = 0f;
        var level = new Vector2(direction.x, direction.z);
        if (level.sqrMagnitude < MinLevelSqr)
            return false;
        yaw = Normalize(Mathf.Atan2(level.x, level.y) * Mathf.Rad2Deg);
        return true;
    }

    /// Which down-the-pitch a placement uses. The saved one whenever there is one, so where the
    /// head happens to look at this load cannot move the pitch. Only on the first placement ever
    /// (nothing saved) is it the head's own direction, and `fromHead` tells the caller to save it.
    /// False when there is neither (nothing saved, head looking straight up or down): just slide.
    public static bool Choose(bool hasSaved, float saved, bool headHasYaw, float headYaw,
                              out float roomYaw, out bool fromHead)
    {
        fromHead = !hasSaved && headHasYaw;
        roomYaw = hasSaved ? Normalize(saved) : fromHead ? Normalize(headYaw) : 0f;
        return hasSaved || fromHead;
    }

    /// The rig yaw that puts the saved room direction on the scene's facing: rig yaw plus a
    /// tracking-space yaw is that direction's world yaw, so the rig takes the difference.
    public static float RigYaw(float savedTrackingYaw, float sceneFacingYaw) =>
        Normalize(sceneFacingYaw - savedTrackingYaw);

    /// The shortest turn (degrees, about world up) from the rig's yaw now to RigYaw.
    public static float Turn(float currentRigYaw, float savedTrackingYaw, float sceneFacingYaw) =>
        Mathf.DeltaAngle(currentRigYaw, RigYaw(savedTrackingYaw, sceneFacingYaw));

    // ---- On the rig ----------------------------------------------------------------------------

    /// Where the head points in the rig's frame (tracking space), level; false when he is looking
    /// straight up or down and there is no direction to take.
    public static bool TryHeadTrackingYaw(Transform rig, Transform head, out float trackingYaw) =>
        TryYawOf(rig.InverseTransformDirection(head.forward), out trackingYaw);

    /// Take where the head points now as the room's down-the-pitch, and save it. False (nothing
    /// saved) when the head is looking straight up or down.
    public static bool Capture(Transform rig, Transform head, out float trackingYaw, string key = PrefKey)
    {
        if (!TryHeadTrackingYaw(rig, head, out trackingYaw))
            return false;
        Save(trackingYaw, key);
        trackingYaw = Normalize(trackingYaw);
        return true;
    }

    /// Turn the rig about the head so the saved room direction faces `sceneFacing`, then slide it
    /// level so the head is over `headXZ`. Turning about the head first keeps the slide exact.
    public static void Place(Transform rig, Transform head, float savedTrackingYaw, Vector3 sceneFacing, Vector2 headXZ)
    {
        if (TryYawOf(sceneFacing, out float sceneYaw))
            rig.RotateAround(head.position, Vector3.up, Turn(rig.eulerAngles.y, savedTrackingYaw, sceneYaw));
        Slide(rig, head, headXZ);
    }

    /// Move the rig level so the head is over `headXZ`; its height is left alone.
    public static void Slide(Transform rig, Transform head, Vector2 headXZ)
    {
        Vector3 at = head.position;
        rig.position += new Vector3(headXZ.x - at.x, 0f, headXZ.y - at.z);
    }
}
