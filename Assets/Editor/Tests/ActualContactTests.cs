using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// The career reads the contact on the player's own bat width, one verdict, not the real-width
/// blade's beside it (2026-11-08: "I don't need to know 2 stats... only based on my bat width").
public class ActualContactTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    /// A shot the widened bat middled, which the real-width blade would have missed altogether.
    private static ShotInfo WideBatMiddle() => new ShotInfo
    {
        quality = 0.92f,
        edge = false,
        kind = BatContact.ContactKind.Middled,
        exitVelocity = new Vector3(-30f, 6f, 2f),
        contactPoint = new Vector3(9.5f, 0.6f, 0f),
        realContact = eRealContact.WouldHaveMissed,
        realQuality = 0f,
        realKind = BatContact.ContactKind.ThinEdge,
        realExitVelocity = Vector3.zero,
    };

    [Test]
    public void TheContactIsTheOneOnYourOwnBat()
    {
        ShotInfo middled = WideBatMiddle();
        Assert.AreEqual(eRealContact.Clean, middled.ActualContact);
        Assert.AreEqual("Middled", ShotReading.ContactWord(middled.ActualContact, (int)middled.kind, middled.quality));
        ShotInfo edged = middled;
        edged.edge = true;
        edged.kind = BatContact.ContactKind.ThickEdge;
        Assert.AreEqual(eRealContact.Edge, edged.ActualContact);
        Assert.AreEqual("Thick edge", ShotReading.ContactWord(edged.ActualContact, (int)edged.kind, edged.quality));
    }

    [Test]
    public void TheNetsRecordWhatYouActuallyDid()
    {
        var go = new GameObject("Ledger");
        try
        {
            var ledger = go.AddComponent<BallLedger>();
            var rec = new BallRecord();
            typeof(BallLedger).GetField("rec", Private).SetValue(ledger, rec);
            typeof(BallLedger).GetField("released", Private).SetValue(ledger, true);
            typeof(BallLedger).GetMethod("OnShot", Private).Invoke(ledger, new object[] { WideBatMiddle() });

            Assert.AreEqual(eRealContact.Clean, rec.realContact, "not the real blade's miss");
            Assert.AreEqual((int)BatContact.ContactKind.Middled, rec.realKind);
            Assert.AreEqual(0.92f, rec.realQuality, 1e-5f);
            Assert.AreEqual(new Vector3(-30f, 6f, 2f).magnitude, rec.exitSpeed, 1e-3f, "the shot as it went off your bat");
            Assert.AreEqual(eBallAction.Played, BallLedgerMath.Action(true, rec.realContact, 0f, 10f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
