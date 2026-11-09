using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RoomSpot: the room's saved crease spot lands on each end's spot, so walking between ends and
/// scenes never moves the room under the pitch. Saves use their own PlayerPrefs keys.
/// </summary>
public class RoomSpotTests
{
    private const string KeyX = "room_crease_spot_x_test", KeyZ = "room_crease_spot_z_test";
    private GameObject rig;

    [TearDown]
    public void CleanUp()
    {
        if (rig != null)
            Object.DestroyImmediate(rig);
        rig = null;
        RoomSpot.Clear(KeyX, KeyZ);
    }

    private Transform BuildRig(float rigYaw, Vector3 headLocal, out Transform head)
    {
        rig = new GameObject("Test Rig");
        rig.transform.SetPositionAndRotation(new Vector3(3f, 0f, -2f), Quaternion.Euler(0f, rigYaw, 0f));
        head = new GameObject("Head").transform;
        head.SetParent(rig.transform, false);
        head.localPosition = headLocal;
        return rig.transform;
    }

    [Test]
    public void TheSavedSpotIsUsedWhileTheHeadIsNearIt()
    {
        Vector2 saved = new Vector2(0.2f, -0.3f);
        Assert.AreEqual(saved, RoomSpot.Choose(true, saved, saved + new Vector2(0.8f, 0.6f), out bool fromHead));
        Assert.IsFalse(fromHead);
    }

    [Test]
    public void FarFromTheSavedSpotOrNothingSavedTheHeadIsTaken()
    {
        Vector2 head = new Vector2(2.5f, 0f);
        Assert.AreEqual(head, RoomSpot.Choose(true, Vector2.zero, head, out bool far));
        Assert.IsTrue(far);
        Assert.AreEqual(head, RoomSpot.Choose(false, Vector2.zero, head, out bool fresh));
        Assert.IsTrue(fresh);
    }

    /// The bug (2026-11-06): the head, wherever it stood, went onto the spot. Now a head half a
    /// metre off the room's spot is half a metre off the crease, at any rig turn.
    [Test]
    public void TheRoomSpotNotTheHeadLandsOnTheCrease()
    {
        Vector2 crease = EndPose.CreaseHeadXZ;
        foreach (float yaw in new[] { 0f, 90f, 217f })
        {
            Transform r = BuildRig(yaw, new Vector3(0.5f, 1.7f, 0f), out Transform head);
            Vector2 roomSpot = Vector2.zero;
            RoomSpot.Slide(r, roomSpot, crease);
            Vector3 spotWorld = r.TransformPoint(Vector3.zero);
            Assert.AreEqual(crease.x, spotWorld.x, 1e-3f, "yaw " + yaw);
            Assert.AreEqual(crease.y, spotWorld.z, 1e-3f, "yaw " + yaw);
            Vector2 headXZ = new Vector2(head.position.x, head.position.z);
            Assert.AreEqual(0.5f, (headXZ - crease).magnitude, 1e-3f, "the step aside is kept");
            Object.DestroyImmediate(rig);
        }
    }

    [Test]
    public void SameRoomSpotSameCreaseAfterGoingToTheOtherEndAndBack()
    {
        Transform r = BuildRig(40f, new Vector3(0.3f, 1.7f, 0.4f), out Transform head);
        Vector2 roomSpot = RoomSpot.OfHead(r, head);
        RoomSpot.Slide(r, roomSpot, EndPose.CreaseHeadXZ);
        Vector3 before = head.position;
        head.localPosition += new Vector3(0.7f, 0f, -0.4f);          // walks about at the other end
        r.rotation = Quaternion.Euler(0f, 220f, 0f);
        RoomSpot.Slide(r, roomSpot, EndPose.NonStrikerHeadXZ);
        head.localPosition -= new Vector3(0.7f, 0f, -0.4f);          // back where he stood
        r.rotation = Quaternion.Euler(0f, 40f, 0f);
        RoomSpot.Slide(r, roomSpot, EndPose.CreaseHeadXZ);
        Assert.Less((head.position - before).magnitude, 1e-3f);
    }

    [Test]
    public void CaptureRoundTrips()
    {
        Transform r = BuildRig(-60f, new Vector3(0.25f, 1.6f, -0.5f), out Transform head);
        Vector2 spot = RoomSpot.Capture(r, head, KeyX, KeyZ);
        Assert.AreEqual(new Vector2(0.25f, -0.5f).x, spot.x, 1e-4f);
        Assert.IsTrue(RoomSpot.TryLoad(out Vector2 loaded, KeyX, KeyZ));
        Assert.AreEqual(-0.5f, loaded.y, 1e-4f);
    }
}
