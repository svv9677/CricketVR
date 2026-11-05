using NUnit.Framework;
using UnityEngine;

/// <summary>
/// RoomFacing: the room's saved down-the-pitch, and the rig yaw that puts it on each scene's and
/// each end's facing. The arithmetic tests are pure; the rig tests build a throwaway rig with a
/// head under it; the save tests use their own PlayerPrefs key (never the player's) and delete it
/// after. Run: Window > General > Test Runner > EditMode > RoomFacingTests.
/// </summary>
public class RoomFacingTests
{
    private const string TestKey = "room_down_pitch_yaw_test";
    private const float TowardsBowler = 270f;   // -X, the crease's EndFacing
    private const float TowardsStriker = 90f;   // +X, the non-striker's EndFacing

    private GameObject rig;

    [TearDown]
    public void CleanUp()
    {
        if (rig != null)
            Object.DestroyImmediate(rig);
        rig = null;
        PlayerPrefs.DeleteKey(TestKey);
    }

    /// Where a rig at `rigYaw` points a direction that is `trackingYaw` in its own frame.
    private static float WorldYaw(float rigYaw, float trackingYaw) => RoomFacing.Normalize(rigYaw + trackingYaw);

    /// The rig's yaw after a placement from `startRigYaw`, as RoomFacing.Place turns it.
    private static float Placed(float startRigYaw, float saved, float scene) =>
        RoomFacing.Normalize(startRigYaw + RoomFacing.Turn(startRigYaw, saved, scene));

    private static Vector3 Dir(float yaw) =>
        new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

    // ---- Arithmetic --------------------------------------------------------------------------------

    [Test]
    public void YawIsUnitysEulerY()
    {
        Assert.IsTrue(RoomFacing.TryYawOf(Vector3.forward, out float f));
        Assert.IsTrue(RoomFacing.TryYawOf(Vector3.right, out float r));
        Assert.IsTrue(RoomFacing.TryYawOf(Vector3.left, out float l));
        Assert.AreEqual(0f, f, 1e-3f);
        Assert.AreEqual(TowardsStriker, r, 1e-3f);
        Assert.AreEqual(TowardsBowler, l, 1e-3f);
        Assert.IsTrue(RoomFacing.TryYawOf(new Vector3(-2f, 5f, 0f), out float tilted), "only the level part counts");
        Assert.AreEqual(TowardsBowler, tilted, 1e-3f);
    }

    [Test]
    public void LookingStraightDownGivesNoDirection()
    {
        Assert.IsFalse(RoomFacing.TryYawOf(Vector3.down, out _));
        Assert.IsFalse(RoomFacing.TryYawOf(new Vector3(0.001f, 1f, 0.001f), out _));
    }

    [Test]
    public void TheSavedDirectionLandsOnTheSceneFacing()
    {
        foreach (float saved in new[] { 0f, 37f, 180f, 271.5f, 359f })
            foreach (float scene in new[] { TowardsBowler, TowardsStriker, 0f, 12f })
                Assert.AreEqual(0f, Mathf.DeltaAngle(scene, WorldYaw(RoomFacing.RigYaw(saved, scene), saved)), 1e-3f,
                    "saved " + saved + ", scene " + scene);
    }

    /// One placement as XRRigSetup.PlaceAtCrease makes it, on bare yaws: the choice of
    /// down-the-pitch from the saved value and the head, then the turn from the rig's yaw now.
    /// Returns the rig's yaw after, and the room yaw used.
    private static float PlaceAsTheRigDoes(bool hasSaved, float saved, float headTrackingYaw,
                                           float startRigYaw, float scene, out float roomYaw)
    {
        Assert.IsTrue(RoomFacing.Choose(hasSaved, saved, true, headTrackingYaw, out roomYaw, out _));
        return Placed(startRigYaw, roomYaw, scene);
    }

    /// Two scenes, the player looking a different way at each load (head tracking yaw 10, then
    /// 200, and the rig loaded at a different yaw too): the same rig yaw both times, so the same
    /// wall of his room is the bowler. And his head is not snapped onto the bowler: it sees as far
    /// off the bowler as it was off the saved direction. (The old placement turned the head onto
    /// the bowler, so the two loads would differ by 190 degrees.)
    [Test]
    public void WhereTheHeadLooksDoesNotMoveThePitch()
    {
        const float saved = 40f;
        float first = PlaceAsTheRigDoes(true, saved, headTrackingYaw: 10f, 0f, TowardsBowler, out _);
        float second = PlaceAsTheRigDoes(true, saved, headTrackingYaw: 200f, 123f, TowardsBowler, out _);
        Assert.AreEqual(0f, Mathf.DeltaAngle(first, second), 1e-3f);
        Assert.AreEqual(0f, Mathf.DeltaAngle(TowardsBowler, WorldYaw(first, saved)), 1e-3f);
        Assert.AreEqual(0f, Mathf.DeltaAngle(TowardsBowler + (200f - saved), WorldYaw(second, 200f)), 1e-3f,
            "the head looks 160 degrees off the bowler, as it was 160 off down-the-pitch");
    }

    /// The very first placement (nothing saved) takes down-the-pitch from the head, and says to
    /// save it; once saved the head no longer decides.
    [Test]
    public void TheFirstPlacementEverTakesTheHeadAndLaterOnesDoNot()
    {
        Assert.IsTrue(RoomFacing.Choose(false, 0f, true, -45f, out float first, out bool fromHead));
        Assert.IsTrue(fromHead, "first ever: save the head's direction");
        Assert.AreEqual(315f, first, 1e-3f);
        Assert.IsTrue(RoomFacing.Choose(true, first, true, 90f, out float later, out fromHead));
        Assert.IsFalse(fromHead, "saved: nothing new to save");
        Assert.AreEqual(315f, later, 1e-3f, "the saved value, not the head's 90");
    }

    /// Nothing saved and the head looking straight up or down: no direction at all, so the rig
    /// only slides and nothing is saved.
    [Test]
    public void NoSavedValueAndNoHeadDirectionChoosesNothing()
    {
        Assert.IsFalse(RoomFacing.Choose(false, 0f, false, 0f, out _, out bool fromHead));
        Assert.IsFalse(fromHead);
        Assert.IsTrue(RoomFacing.Choose(true, 12f, false, 0f, out float yaw, out _), "a saved value needs no head");
        Assert.AreEqual(12f, yaw, 1e-3f);
    }

    /// Down the pitch is the far end at both ends: the bowler from the crease, the striker from the
    /// non-striker's spot - so the rig turns end for end and the player does not turn at all.
    [Test]
    public void TheSameRoomDirectionFacesTheFarEndAtBothEnds()
    {
        const float saved = 300f;
        float crease = Placed(10f, saved, TowardsBowler);
        float nonStriker = Placed(crease, saved, TowardsStriker);
        Assert.AreEqual(0f, Mathf.DeltaAngle(TowardsBowler, WorldYaw(crease, saved)), 1e-3f);
        Assert.AreEqual(0f, Mathf.DeltaAngle(TowardsStriker, WorldYaw(nonStriker, saved)), 1e-3f);
        Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(crease, nonStriker)), 1e-3f);
        float back = Placed(nonStriker, saved, TowardsBowler);
        Assert.AreEqual(0f, Mathf.DeltaAngle(crease, back), 1e-3f, "and back again to the same yaw");
    }

    /// A recentre takes the head's direction then as the new down-the-pitch: placed with it, the
    /// head faces the bowler exactly, as the very first placement always did.
    [Test]
    public void ARecentreTakesWhereTheHeadPointsAsDownThePitch()
    {
        const float oldSaved = 20f, headNow = 205f;
        float before = Placed(0f, oldSaved, TowardsBowler);
        Assert.AreNotEqual(0f, Mathf.DeltaAngle(TowardsBowler, WorldYaw(before, headNow)), "old value: not at the bowler");
        float after = Placed(before, headNow, TowardsBowler);
        Assert.AreEqual(0f, Mathf.DeltaAngle(TowardsBowler, WorldYaw(after, headNow)), 1e-3f);
    }

    [Test]
    public void TheTurnIsTheShortWayRound()
    {
        for (float start = -720f; start <= 720f; start += 33f)
            Assert.LessOrEqual(Mathf.Abs(RoomFacing.Turn(start, 75f, TowardsBowler)), 180f + 1e-3f);
    }

    // ---- On a rig (needs the editor) -------------------------------------------------------------

    private Transform BuildRig(float rigYaw, float headYaw, Vector3 headLocal, out Transform head)
    {
        rig = new GameObject("Test Rig");
        rig.transform.SetPositionAndRotation(new Vector3(3f, 0f, -2f), Quaternion.Euler(0f, rigYaw, 0f));
        head = new GameObject("Head").transform;
        head.SetParent(rig.transform, false);
        head.localPosition = headLocal;
        head.localRotation = Quaternion.Euler(15f, headYaw, 0f);
        return rig.transform;
    }

    [Test]
    public void PlacedTheHeadIsOnItsSpotWithTheRoomDirectionDownThePitch()
    {
        const float saved = 50f;
        Vector2 spot = EndPose.CreaseHeadXZ;
        foreach (float headYaw in new[] { 50f, 140f, -95f })
        {
            Transform r = BuildRig(rigYaw: 33f, headYaw, new Vector3(0.4f, 1.7f, -0.6f), out Transform head);
            RoomFacing.Place(r, head, saved, Vector3.left, spot);
            Vector3 roomDownPitch = r.TransformDirection(Dir(saved));
            Assert.Less((roomDownPitch - Vector3.left).magnitude, 1e-3f, "head yaw " + headYaw);
            Assert.AreEqual(spot.x, head.position.x, 1e-3f);
            Assert.AreEqual(spot.y, head.position.z, 1e-3f);
            Assert.AreEqual(1.7f, head.position.y, 1e-3f, "height left alone");
            Object.DestroyImmediate(rig);
        }
    }

    [Test]
    public void TheHeadsTrackingYawIgnoresTheRigsTurn()
    {
        Transform r = BuildRig(rigYaw: 110f, headYaw: 25f, Vector3.up * 1.6f, out Transform head);
        Assert.IsTrue(RoomFacing.TryHeadTrackingYaw(r, head, out float yaw));
        Assert.AreEqual(25f, yaw, 1e-2f);
    }

    // ---- Saved -------------------------------------------------------------------------------------

    [Test]
    public void TheSavedYawRoundTripsThroughPlayerPrefs()
    {
        RoomFacing.Clear(TestKey);
        Assert.IsFalse(RoomFacing.TryLoad(out _, TestKey), "nothing saved on a fresh install");
        RoomFacing.Save(-30f, TestKey);
        Assert.IsTrue(RoomFacing.TryLoad(out float yaw, TestKey));
        Assert.AreEqual(330f, yaw, 1e-3f);
        RoomFacing.Clear(TestKey);
        Assert.IsFalse(RoomFacing.TryLoad(out _, TestKey));
    }

    [Test]
    public void CaptureSavesWhereTheHeadPoints()
    {
        Transform r = BuildRig(rigYaw: -60f, headYaw: 200f, Vector3.up * 1.6f, out Transform head);
        Assert.IsTrue(RoomFacing.Capture(r, head, out float yaw, TestKey));
        Assert.AreEqual(200f, yaw, 1e-2f);
        Assert.IsTrue(RoomFacing.TryLoad(out float loaded, TestKey));
        Assert.AreEqual(200f, loaded, 1e-2f);
    }
}
