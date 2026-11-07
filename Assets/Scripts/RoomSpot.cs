using UnityEngine;

/// <summary>
/// Where in the player's real room the crease is, kept across scenes, ends and app restarts, as
/// RoomFacing keeps which way down the pitch is (2026-11-06: "the direction is working, but im in
/// different positions... everytime im on the nonstriker, and then i go back to the striker side,
/// my position shifts").
///
/// Before this, every placement slid the head itself onto the crease. So a player who had stepped
/// half a metre aside at the non-striker's end was put back on the crease half a metre from where
/// he stood last time: the room moved under the pitch by however far he had walked. Now the spot
/// is a point in tracking space (the room), taken where the head stood the first time he was
/// placed, and every placement puts that point - not the head - on the end's spot. Standing where
/// he stood then, he is on the crease (or on the non-striker's spot); a step aside in the room is
/// a step aside at the crease, at both ends and in every scene.
///
/// The point is taken again with the direction after a recentre (which redraws tracking space),
/// and whenever the head is more than MaxDrift from it - a new guardian, or a player who has moved
/// the play space - so nobody is ever put metres from the crease by an old value.
///
/// Spots are (x, z) metres in the rig's frame, level.
/// </summary>
public static class RoomSpot
{
    public const string PrefKeyX = "room_crease_spot_x", PrefKeyZ = "room_crease_spot_z";

    /// Further than this from the saved spot (m), the head's own position is taken instead.
    public const float MaxDrift = 1.5f;

    public static bool TryLoad(out Vector2 spot, string keyX = PrefKeyX, string keyZ = PrefKeyZ)
    {
        spot = Vector2.zero;
        if (!PlayerPrefs.HasKey(keyX) || !PlayerPrefs.HasKey(keyZ))
            return false;
        spot = new Vector2(PlayerPrefs.GetFloat(keyX), PlayerPrefs.GetFloat(keyZ));
        return true;
    }

    public static void Save(Vector2 spot, string keyX = PrefKeyX, string keyZ = PrefKeyZ)
    {
        PlayerPrefs.SetFloat(keyX, spot.x);
        PlayerPrefs.SetFloat(keyZ, spot.y);
        PlayerPrefs.Save();
    }

    public static void Clear(string keyX = PrefKeyX, string keyZ = PrefKeyZ)
    {
        PlayerPrefs.DeleteKey(keyX);
        PlayerPrefs.DeleteKey(keyZ);
        PlayerPrefs.Save();
    }

    /// The head's level position in the rig's frame (tracking space).
    public static Vector2 OfHead(Transform rig, Transform head)
    {
        Vector3 local = rig.InverseTransformPoint(head.position);
        return new Vector2(local.x, local.z);
    }

    /// Which spot a placement uses: the saved one while the head is within MaxDrift of it, else
    /// the head's own (and `fromHead` tells the caller to save it).
    public static Vector2 Choose(bool hasSaved, Vector2 saved, Vector2 head, out bool fromHead)
    {
        fromHead = !hasSaved || (saved - head).sqrMagnitude > MaxDrift * MaxDrift;
        return fromHead ? head : saved;
    }

    /// Take where the head stands now as the room's crease spot, and save it.
    public static Vector2 Capture(Transform rig, Transform head, string keyX = PrefKeyX, string keyZ = PrefKeyZ)
    {
        Vector2 spot = OfHead(rig, head);
        Save(spot, keyX, keyZ);
        return spot;
    }

    /// Move the rig level so the room's `spot` (rig frame) is over `worldXZ`; height left alone.
    /// Call after the rig has been turned.
    public static void Slide(Transform rig, Vector2 spot, Vector2 worldXZ)
    {
        Vector3 at = rig.TransformPoint(new Vector3(spot.x, 0f, spot.y));
        rig.position += new Vector3(worldXZ.x - at.x, 0f, worldXZ.y - at.z);
    }
}
