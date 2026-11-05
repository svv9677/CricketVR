using System;
using System.IO;
using NUnit.Framework;

/// <summary>
/// Phase 7's shared contract before any of it does anything: a version-2 save with none of the new
/// fields loads with every new field usable, the new record-book, goal and innings fields survive a
/// save, the inbox tags the two new kinds, the upkeep changes nothing yet, a slot's record clips
/// go with its career, and the season review reads as it did with no goals. Every test saves into
/// its own temp directory, never the real saves.
/// </summary>
public class Phase7ContractTests
{
    private string dir;

    [SetUp]
    public void UseTempDirectory()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_phase7_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
        ReplayVault.UseDirectoryForTests(null);   // beside the saves: clips/slot{n}
    }

    [TearDown]
    public void RestoreDirectory()
    {
        ReplayVault.UseDirectoryForTests(null);
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    /// A version-2 save as phase 6 wrote it: a season with a side, a match begun, no phase 7 field.
    private const string V2Save =
        "{\"version\":2,\"name\":\"Old Save\",\"nation\":\"AUS\",\"created\":\"2026-10-01\",\"trialDone\":true," +
        "\"rung\":0,\"batFinish\":\"\",\"kitTrim\":\"\",\"unlocked\":[]," +
        "\"season\":{\"number\":1,\"rung\":0,\"yourTeam\":\"t0\",\"teams\":[{\"id\":\"t0\",\"name\":\"Harbour CC\"}]," +
        "\"fixtures\":[{\"round\":1,\"home\":\"t0\",\"away\":\"t1\"}],\"finished\":false}," +
        "\"pending\":{\"active\":true,\"plan\":{\"fixture\":0,\"overs\":6,\"batFirst\":true},\"runs\":4,\"balls\":3}}";

    private string MainFile(int slot) => Path.Combine(dir, $"slot{slot + 1}.json");

    private string ClipDir(int slot) => Path.Combine(dir, "clips", $"slot{slot + 1}");

    private void PutClip(int slot, string stamp)
    {
        Directory.CreateDirectory(ClipDir(slot));
        File.WriteAllText(Path.Combine(ClipDir(slot), stamp + ReplayVault.Extension), "clip");
    }

    private static void AssertLatin1(string s)
    {
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    [Test]
    public void AVersion2Save_LoadsWithEveryNewFieldUsable()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(MainFile(0), V2Save);
        CareerProfile p = CareerSaves.Load(0);
        Assert.IsNotNull(p);
        Assert.AreEqual("Old Save", p.name);
        Assert.AreEqual("", p.gloves);
        Assert.AreEqual("", p.batFinish);
        Assert.AreEqual(0, p.goalsDone);
        Assert.IsNotNull(p.recordBook);
        Assert.IsNotNull(p.recordBook.reached);
        Assert.IsNotNull(p.recordBook.longestSix);
        Assert.IsNotNull(p.season);
        Assert.IsNotNull(p.season.goals);
        Assert.IsNotNull(p.pending.plan);
        Assert.IsNotNull(p.pending.plan.collapse);
        Assert.AreEqual(0, p.pending.plan.revisedTarget);
        Assert.AreEqual("", p.pending.sixStamp);
        Assert.AreEqual(CareerProfile.CurrentVersion, p.version);
    }

    [Test]
    public void TheNewFields_SurviveASave()
    {
        CareerProfile p = CareerTestPlay.Career(3);
        p.gloves = "gloves.red";
        p.goalsDone = 4;
        p.innings.Add(new InningsRecord
        {
            batted = true, runs = 61, balls = 40, situation = eSituation.RainShortened, overs = 14, target = 124,
            ballsToFifty = 33, longestSix = 82.5f, sixStamp = "0badf00d-b17",
        });
        p.season.goals.Add(new SeasonGoal { id = "runs", kind = eGoalKind.Boundaries, stat = 2, minBalls = 30, baseline = 12.5f, done = true, doneDate = "2026-11-05" });
        p.season.situationsDealt = true;
        p.recordBook.longestSix = new RecordEntry { set = true, value = 82.5f, clipStamp = "0badf00d-b17", rung = eRung.State, season = 2 };
        p.recordBook.reached.Add("fifty");
        p.recordBook.lastMilestone = "Your first fifty";
        CareerSaves.Create(0, p);

        CareerProfile back = CareerSaves.Load(0);
        InningsRecord r = back.innings[back.innings.Count - 1];
        Assert.AreEqual(eSituation.RainShortened, r.situation);
        Assert.AreEqual(14, r.overs);
        Assert.AreEqual(124, r.target);
        Assert.AreEqual(33, r.ballsToFifty);
        Assert.AreEqual(82.5f, r.longestSix);
        Assert.AreEqual("0badf00d-b17", r.sixStamp);
        SeasonGoal g = back.season.goals[0];
        Assert.AreEqual(eGoalKind.Boundaries, g.kind);
        Assert.AreEqual(2, g.stat);
        Assert.AreEqual(30, g.minBalls);
        Assert.AreEqual(12.5f, g.baseline);
        Assert.AreEqual("2026-11-05", g.doneDate);
        Assert.IsTrue(back.season.situationsDealt);
        Assert.AreEqual("gloves.red", back.gloves);
        Assert.AreEqual(4, back.goalsDone);
        Assert.IsTrue(back.recordBook.longestSix.set);
        Assert.AreEqual("0badf00d-b17", back.recordBook.Get(eRecord.LongestSix).clipStamp);
        Assert.AreEqual(eRung.State, back.recordBook.longestSix.rung);
        CollectionAssert.AreEqual(new[] { "fifty" }, back.recordBook.reached);
        Assert.AreEqual("Your first fifty", back.recordBook.lastMilestone);
    }

    [Test]
    public void TheInbox_TagsGoalsAndTheLocker()
    {
        Assert.AreEqual("GOAL", InboxScreen.Tag(eMessageKind.Goal));
        Assert.AreEqual("LOCKER", InboxScreen.Tag(eMessageKind.Locker));
        Assert.AreEqual("MILESTONE", InboxScreen.Tag(eMessageKind.Milestone), "the old tags are as they were");
        Assert.AreEqual("NEWS", InboxScreen.Tag(eMessageKind.Info));
        AssertLatin1(InboxScreen.Tag(eMessageKind.Goal));
        AssertLatin1(InboxScreen.Tag(eMessageKind.Locker));
        Assert.AreEqual(6, (int)eMessageKind.Goal, "appended: saved kinds keep their numbers");
        Assert.AreEqual(7, (int)eMessageKind.Locker);
    }

    [Test]
    public void Upkeep_ChangesNothingYet()
    {
        Assert.IsFalse(CareerUpkeep.Run(null));
        CareerProfile p = CareerTestPlay.Career(5);
        Assert.IsFalse(CareerUpkeep.Run(p));
        Assert.AreEqual(0, p.season.goals.Count);
        Assert.AreEqual(0, p.unlocked.Count);
    }

    [Test]
    public void TheVault_FindsOnlyClipsThatAreThere_AndDeletesASlot()
    {
        Assert.IsFalse(ReplayVault.Has(0, "missing"));
        Assert.IsFalse(ReplayVault.Has(0, ""));
        Assert.IsFalse(ReplayVault.Has(-1, "x"));
        PutClip(0, "x");
        PutClip(1, "y");
        Assert.IsTrue(ReplayVault.Has(0, "x"));
        Assert.IsFalse(ReplayVault.Has(1, "x"));
        ReplayVault.DeleteSlot(0);
        Assert.IsFalse(Directory.Exists(ClipDir(0)));
        Assert.IsTrue(ReplayVault.Has(1, "y"), "the other slot's clips stay");
        Assert.DoesNotThrow(() => ReplayVault.DeleteSlot(2), "no folder: nothing to do");
        Assert.DoesNotThrow(() => ReplayVault.DeleteSlot(-1));
    }

    [Test]
    public void ACareerDeletedOrStartedAgain_TakesItsClipsWithIt()
    {
        CareerSaves.Create(0, CareerTestPlay.Career(7));
        PutClip(0, "a");
        PutClip(1, "b");
        CareerSaves.Delete(0);
        Assert.IsFalse(Directory.Exists(ClipDir(0)), "deleted with its career");
        Assert.IsTrue(Directory.Exists(ClipDir(1)));

        PutClip(0, "c");
        CareerSaves.Create(0, CareerTestPlay.Career(8));
        Assert.IsFalse(Directory.Exists(ClipDir(0)), "a new career in the slot starts with none");
        Assert.IsTrue(ReplayVault.Has(1, "b"));
    }

    [Test]
    public void SeasonReview_ReadsAsBefore_WithNoGoals()
    {
        CareerProfile p = CareerTestPlay.Career(9);
        string review = CareerProgressMath.SeasonReview(p, p.season);
        StringAssert.StartsWith("Club cricket, season 1: You did not bat.", review);
        StringAssert.DoesNotContain("goal", review);
        Assert.IsFalse(review.EndsWith(" "), review);
        Assert.AreEqual("", CoachGoals.ReviewLine(p.season));
    }
}
