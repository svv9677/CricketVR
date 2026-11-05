using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The striker as the rules see him (Striker), and the partner's contact (PartnerContact), the parts
/// that need no scene. With no career match - Quick Match, the nets, these tests - the partner is
/// never the striker, so every reader gets its own answer for the player back unchanged.
/// </summary>
public class StrikerTests
{
    [SetUp]
    public void Fresh() => PartnerWatch.Reset();

    [TearDown]
    public void CleanUp() => PartnerWatch.Reset();

    // ---- Striker: the fallbacks ---------------------------------------------------------------

    [Test]
    public void WithNoCareerMatchThePartnerIsNeverTheStriker()
    {
        Assert.IsFalse(CareerMatchSession.Active, "these tests run with no career match");
        Assert.IsFalse(PartnerWatch.Enabled);
        Assert.IsFalse(PartnerWatch.Live);
        Assert.IsFalse(PartnerWatch.ThisBall);
        Assert.IsFalse(Striker.IsPartner);
    }

    [Test]
    public void TheHeadPassesThrough()
    {
        var head = new Vector3(9.3f, 1.71f, -0.12f);
        Assert.AreEqual(head, Striker.HeadPosition(head), "the keeper looks at the player");
    }

    [Test]
    public void TheHeadTopPassesThrough()
    {
        Assert.AreEqual(1.83f, Striker.HeadTopY(1.83f), 0f, "the over-the-head wide is the player's");
    }

    [Test]
    public void ThereIsNoPartnerStance()
    {
        Assert.IsFalse(Striker.TryStanceZ(out float z), "the wide lines follow the player");
        Assert.AreEqual(0f, z);
    }

    [Test]
    public void ThePartnerBallIsLetGoAtTheRelease()
    {
        PartnerWatch.OnState(eGameState.InGame_DeliverBall);
        Assert.IsFalse(PartnerWatch.ThisBall, "not watching: no ball is the partner's");
        PartnerWatch.OnState(eGameState.InGame_ResetToReady);
        Assert.IsFalse(PartnerWatch.ThisBall);
    }

    // ---- PartnerContact: one step ahead --------------------------------------------------------

    private const float Dt = 0.01f;
    private const float ContactX = 9.1f;

    [Test]
    public void TheBallCrossingInTheNextStepIsMetOnThePlane()
    {
        var p = new Vector3(ContactX - 0.2f, 0.5f, 0.1f);
        var v = new Vector3(30f, -2f, 1f);
        Assert.IsTrue(PartnerContact.CrossesNextStep(p, v, Dt, ContactX, out Vector3 at));
        Assert.AreEqual(ContactX, at.x, 1e-5f, "on the contact plane");
        float t = 0.2f / 30f;
        Assert.AreEqual(0.5f - 2f * t, at.y, 1e-5f);
        Assert.AreEqual(0.1f + 1f * t, at.z, 1e-5f);
    }

    [Test]
    public void NotBeforeTheStepThatCrosses()
    {
        var v = new Vector3(30f, 0f, 0f);
        Assert.IsFalse(PartnerContact.CrossesNextStep(new Vector3(ContactX - 0.31f, 0.5f, 0f), v, Dt, ContactX, out _),
                       "0.31 m short at 30 m/s: two steps away");
        Assert.IsTrue(PartnerContact.CrossesNextStep(new Vector3(ContactX - 0.29f, 0.5f, 0f), v, Dt, ContactX, out _));
    }

    [Test]
    public void NotOnceItIsPastOrGoingAway()
    {
        var v = new Vector3(30f, 0f, 0f);
        Assert.IsFalse(PartnerContact.CrossesNextStep(new Vector3(ContactX + 0.01f, 0.5f, 0f), v, Dt, ContactX, out _), "past the bat");
        Assert.IsFalse(PartnerContact.CrossesNextStep(new Vector3(ContactX - 0.1f, 0.5f, 0f), -v, Dt, ContactX, out _), "going back down the pitch");
        Assert.IsFalse(PartnerContact.CrossesNextStep(new Vector3(ContactX - 0.1f, 0.5f, 0f), Vector3.zero, Dt, ContactX, out _), "still");
    }

    [Test]
    public void TheFaceNormalIsThePush()
    {
        var incoming = new Vector3(30f, -3f, 0.5f);
        var exit = new Vector3(-25f, 6f, 8f);
        Vector3 n = PartnerContact.FaceNormal(incoming, exit);
        Assert.AreEqual(1f, n.magnitude, 1e-5f);
        Assert.Greater(Vector3.Dot(n, exit - incoming), 0f, "out from the face, the way the bat pushed it");
        Assert.AreEqual(-incoming.normalized, PartnerContact.FaceNormal(incoming, incoming), "no push: back toward the bowler");
    }

    [Test]
    public void TheShotIsReportedAsTheRealBladeWouldHaveIt()
    {
        var plan = new ShotPlan
        {
            shot = PartnerShot.Drive, makesContact = true, kind = BatContact.ContactKind.Middled, quality = 0.92f,
            exitVelocity = new Vector3(-28f, 4f, 6f), batVelocity = new Vector3(-12f, 2f, 3f),
        };
        var incoming = new Vector3(31f, -2f, 0f);
        var at = new Vector3(9.1f, 0.4f, 0.05f);
        ShotInfo i = PartnerContact.Info(plan, incoming, at);
        Assert.AreEqual(eRealContact.Clean, i.realContact);
        Assert.AreEqual(plan.exitVelocity, i.exitVelocity);
        Assert.AreEqual(plan.exitVelocity, i.realExitVelocity, "the partner has no shield: real is actual");
        Assert.AreEqual(plan.quality, i.realQuality, 0f);
        Assert.AreEqual(incoming.magnitude, i.ballSpeedIn, 1e-5f);
        Assert.AreEqual(plan.batVelocity.magnitude, i.batSpeed, 1e-5f);
        Assert.AreEqual(at, i.contactPoint);
        Assert.AreEqual(BatContact.Label(plan.kind), i.contactLabel);
        Assert.IsFalse(i.edge);

        plan.edge = true;
        plan.kind = BatContact.ContactKind.ThinEdge;
        i = PartnerContact.Info(plan, incoming, at);
        Assert.AreEqual(eRealContact.Edge, i.realContact);
        Assert.IsTrue(i.edge);
    }
}
