using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The partner's grip and his two standing poses (PartnerStance), standalone - no scene. The grip
/// is read from the game's own bat (Assets/Resources/Prefabs/Bat.prefab, as BatContactTests does):
/// both hands on the handle beyond the shoulder, the top one higher; at guard the bat is grounded
/// on middle stump with both hands on it and within his reach; backing up he is out of the
/// bowler's way. Run: Window > General > Test Runner > EditMode > PartnerBatterTests.
/// </summary>
public class PartnerBatterTests
{
    private const string BatPrefab = "Assets/Resources/Prefabs/Bat.prefab";
    /// David standing (hips, measured on the model in the editor: ~0.95 m).
    private const float StandingHips = 0.95f;
    /// Shoulders above the hips, and an arm with its fist (ReachMath.Body.Default's arm is 0.62).
    private const float HipsToShoulders = 0.5f, ArmAndFist = 0.75f;

    private BatGrip grip;
    private float shoulderZ, handleEndZ;

    [OneTimeSetUp]
    public void LoadBat()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BatPrefab);
        var geo = prefab.GetComponent<BatGeometry>();
        Mesh mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
        grip = PartnerStance.GripFrom(geo, mesh, prefab.transform.localScale.x);
        shoulderZ = PartnerStance.LocalTo(geo.shoulder, geo.transform).z;
        handleEndZ = mesh.bounds.max.z;
    }

    [Test]
    public void Grip_FromTheBatPrefab_HandsUpTheHandleBeyondTheShoulder()
    {
        Assert.Greater(grip.topHandLocal.z, shoulderZ, "top hand beyond the shoulder (+Z is the handle)");
        Assert.Greater(grip.bottomHandLocal.z, shoulderZ, "bottom hand on the handle too");
        Assert.Greater(grip.topHandLocal.z, grip.bottomHandLocal.z, "top hand above the bottom hand");
        Assert.LessOrEqual(grip.topHandLocal.z, handleEndZ, "top hand still on the handle");
        Assert.Less(grip.toeLocal.z, grip.sweetLocal.z, "toe below the sweet spot");
        Assert.Less(grip.sweetLocal.z, shoulderZ, "sweet spot on the blade");
        float apart = (grip.topHandLocal.z - grip.bottomHandLocal.z) * grip.scale;
        Assert.That(apart, Is.InRange(0.06f, 0.14f), "the hands about a hand's width apart (m)");
    }

    [Test]
    public void Grip_WithoutMarkersOrMesh_FallsBackToThisBat()
    {
        BatGrip g = PartnerStance.GripFrom(null, null, 0.475f);
        Assert.Greater(g.topHandLocal.z, PartnerStance.DefaultShoulderZ);
        Assert.Greater(g.topHandLocal.z, g.bottomHandLocal.z);
        Assert.AreEqual(0.475f, g.scale, 1e-6f);
    }

    [Test]
    public void Guard_BatGroundedOnMiddleStump_BothHandsOnIt([Values(1f, -1f)] float offSign)
    {
        SwingPose p = PartnerStance.Stance(grip, offSign, StandingHips);
        Vector3 toe = p.batPosition + p.batRotation * (grip.toeLocal * grip.scale);
        Assert.AreEqual(PartnerStance.GuardZ, toe.z, 1e-4f, "guard on middle stump");
        Assert.AreEqual(PartnerStance.BatToeHeight, toe.y, 1e-4f, "toe grounded");
        Assert.Less(toe.x, BallDelivery.BatsmanStumpsX, "in front of his stumps");
        Assert.AreEqual(0f, (p.topHand - (p.batPosition + p.batRotation * (grip.topHandLocal * grip.scale))).magnitude, 1e-4f);
        Assert.Greater(p.topHand.y, p.bottomHand.y, "top hand higher on the upright bat");
        Assert.Greater(Vector3.Dot(p.batRotation * Vector3.up, Vector3.left), 0.9f, "face toward the bowler");
        Assert.True(PartnerStance.Valid(p));
    }

    [Test]
    public void Guard_StandsOnTheLegSide_MirroredForALeftHander()
    {
        SwingPose right = PartnerStance.Stance(grip, 1f, StandingHips);
        SwingPose left = PartnerStance.Stance(grip, -1f, StandingHips);
        Assert.Less(right.hips.z, 0f, "right-hander's hips on the leg side (-z)");
        Assert.Less(right.leftFoot.z, 0f);
        Assert.Less(right.leftFoot.x, right.rightFoot.x, "right-hander's left foot toward the bowler");
        Assert.AreEqual(right.hips.z, -left.hips.z, 1e-5f);
        Assert.AreEqual(right.topHand.z, -left.topHand.z, 1e-4f);
        Assert.AreEqual(right.leftFoot.x, left.rightFoot.x, 1e-5f, "the front foot is the right for a left-hander");
        Assert.Greater(right.topHand.z, right.hips.z, "hands in front of him, toward the off side");
    }

    [Test]
    public void Guard_HandsWithinReachOfHisShoulders([Values(1f, -1f)] float offSign)
    {
        SwingPose p = PartnerStance.Stance(grip, offSign, StandingHips);
        Vector3 shoulders = p.hips + Vector3.up * HipsToShoulders;
        Assert.Less((p.topHand - shoulders).magnitude, ArmAndFist);
        Assert.Less((p.bottomHand - shoulders).magnitude, ArmAndFist);
    }

    [Test]
    public void BackingUp_OutOfTheBowlersWay_BatGrounded()
    {
        SwingPose p = PartnerStance.NonStriker(grip, StandingHips);
        Vector3 toe = p.batPosition + p.batRotation * (grip.toeLocal * grip.scale);
        Assert.AreEqual(PartnerStance.BatToeHeight, toe.y, 1e-4f, "bat grounded");
        Assert.Less(p.hips.z, -1f, "on the -z side: every bowler runs in on +z");
        Assert.Less(toe.z, -1f, "his bat too");
        Assert.Less(p.topHand.z, -1f, "and his hand");
        Assert.Less(Mathf.Abs(p.hips.x - BallDelivery.BowlerStumpsX), BallDelivery.PoppingCreaseDistance, "inside his crease");
        Assert.Greater(p.topHand.y, 0.6f, "hand on the top of the handle");
    }

    [Test]
    public void DefaultPose_IsNotValid_SoTheRestPoseIsUsed()
    {
        Assert.False(PartnerStance.Valid(default));
    }

    [Test]
    public void Blend_EndsOnTheTarget()
    {
        SwingPose a = PartnerStance.Stance(grip, 1f, StandingHips), b = PartnerStance.NonStriker(grip, StandingHips);
        SwingPose m = PartnerStance.Blend(a, b, 1f);
        Assert.AreEqual(0f, (m.batPosition - b.batPosition).magnitude, 1e-5f);
        Assert.AreEqual(0f, (m.topHand - b.topHand).magnitude, 1e-5f);
    }
}
