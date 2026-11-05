using System;
using System.IO;
using NUnit.Framework;

/// <summary>
/// The record book in the save (phase 7, version 3): a hand-written version-2 save loads as version 3
/// with its records worked out from what it kept (CareerRecords.Backfill, silently), the book and the
/// innings' moment fields survive a save and a load, and a version-3 save is never backfilled again.
/// Every test saves into its own temp directory, never the real saves. JsonUtility is Unity's, so
/// these run in the Unity Test Runner (EditMode).
/// </summary>
public class CareerRecordsSaveTests
{
    private string dir;

    [SetUp]
    public void UseTempDirectory()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_records_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        ReplayVault.UseDirectoryForTests(null);
    }

    [TearDown]
    public void RestoreDirectory()
    {
        ReplayVault.UseDirectoryForTests(null);
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    private string MainFile(int slot) => Path.Combine(dir, $"slot{slot + 1}.json");

    /// A phase-6 save: three innings (a 64 not out among them), the stats with 164 runs, two caps,
    /// two match sixes in the balls (one only the bat shield found), one inbox message, no record book.
    private static string Save(int version) =>
        "{\"version\":" + version + ",\"name\":\"Old Save\",\"nation\":\"AUS\",\"created\":\"2026-10-01\",\"trialDone\":true," +
        "\"rung\":3,\"caps\":2," +
        "\"innings\":[" +
        "{\"season\":1,\"rung\":0,\"team\":\"Rivertown\",\"opponent\":\"Harbour CC\",\"runs\":40,\"balls\":30,\"batted\":true,\"isOut\":true,\"date\":\"2026-10-02\"}," +
        "{\"season\":1,\"rung\":0,\"team\":\"Rivertown\",\"opponent\":\"Hillside\",\"runs\":64,\"balls\":32,\"sixes\":3,\"batted\":true,\"isOut\":false,\"date\":\"2026-10-03\"}," +
        "{\"season\":2,\"rung\":3,\"team\":\"Australia\",\"opponent\":\"India\",\"runs\":60,\"balls\":50,\"batted\":true,\"isOut\":true,\"date\":\"2026-10-04\"}]," +
        "\"stats\":{\"matches\":3,\"innings\":3,\"notOuts\":1,\"runs\":164,\"balls\":112,\"sixes\":3,\"fifties\":2,\"highScore\":64,\"highScoreNotOut\":true}," +
        "\"balls\":[" +
        "{\"source\":3,\"end\":3,\"realContact\":1,\"carry\":79.5}," +
        "{\"source\":3,\"end\":3,\"realContact\":3,\"carry\":115.0}]," +
        "\"inbox\":[{\"kind\":5,\"from\":\"Coach\",\"title\":\"Your first fifty\",\"body\":\"64*\",\"date\":\"2026-10-03\"}]}";

    [Test]
    public void AVersion2SaveLoadsAsVersion3WithItsRecords()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(MainFile(0), Save(2));
        CareerProfile p = CareerSaves.Load(0);
        Assert.IsNotNull(p);
        Assert.AreEqual(3, CareerProfile.CurrentVersion);
        Assert.AreEqual(3, p.version);
        Assert.AreEqual(1, p.inbox.Count, "backfilled silently");
        CareerRecordBook b = p.recordBook;
        Assert.AreEqual(64, b.highScore.runs);
        Assert.IsTrue(b.highScore.notOut);
        Assert.AreEqual("Hillside", b.highScore.opponent);
        Assert.AreEqual(200f, b.bestStrikeRate.value, 0.001f);
        Assert.AreEqual(3, b.mostSixes.sixes);
        Assert.AreEqual(79.5f, b.longestSix.value, 0.001f, "the shield's 115 m is not a record");
        Assert.IsFalse(b.fastestFifty.set);
        Assert.Contains(CareerRecords.FiftyId, b.reached);
        Assert.Contains(CareerRecords.RunsId(100), b.reached);
        Assert.Contains(CareerRecords.CapsId(1), b.reached);
        Assert.IsFalse(b.reached.Contains(CareerRecords.CapsId(10)));
    }

    [Test]
    public void TheRecordBookRoundTrips()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(MainFile(0), Save(2));
        CareerProfile p = CareerSaves.Load(0);
        p.recordBook.longestSix.clipStamp = "0badf00d-b17";
        p.recordBook.fastestFifty.set = true;
        p.recordBook.fastestFifty.value = 24f;
        p.recordBook.lastMilestone = "100 career runs";
        p.recordBook.lastMilestoneDate = "2026-10-05";
        p.innings[0].ballsToFifty = 41;
        p.innings[0].longestSix = 77.25f;
        p.innings[0].sixStamp = "0badf00d-b9";
        CareerSaves.Create(0, p);

        CareerProfile back = CareerSaves.Load(0);
        Assert.AreEqual(3, back.version);
        Assert.AreEqual("0badf00d-b17", back.recordBook.longestSix.clipStamp);
        Assert.AreEqual(79.5f, back.recordBook.longestSix.value, 0.001f);
        Assert.AreEqual(24f, back.recordBook.fastestFifty.value);
        Assert.AreEqual(64, back.recordBook.highScore.runs);
        Assert.AreEqual("100 career runs", back.recordBook.lastMilestone);
        Assert.AreEqual("2026-10-05", back.recordBook.lastMilestoneDate);
        CollectionAssert.AreEqual(p.recordBook.reached, back.recordBook.reached);
        Assert.AreEqual(41, back.innings[0].ballsToFifty);
        Assert.AreEqual(77.25f, back.innings[0].longestSix, 0.001f);
        Assert.AreEqual("0badf00d-b9", back.innings[0].sixStamp);
    }

    [Test]
    public void AVersion3SaveIsNotBackfilledAgain()
    {
        Directory.CreateDirectory(dir);
        string v3 = Save(3).Replace("\"caps\":2,", "\"caps\":2,\"recordBook\":{\"highScore\":{\"set\":true,\"value\":12,\"runs\":12}},");
        File.WriteAllText(MainFile(0), v3);
        CareerProfile p = CareerSaves.Load(0);
        Assert.AreEqual(12, p.recordBook.highScore.runs, "the book is the save's own once it is version 3");
        Assert.IsFalse(p.recordBook.longestSix.set);
        Assert.IsEmpty(p.recordBook.reached);
    }
}
