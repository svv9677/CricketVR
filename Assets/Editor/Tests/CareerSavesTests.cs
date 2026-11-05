using System;
using System.IO;
using NUnit.Framework;

/// <summary>
/// The career save slots (CareerSaves) and the profile they hold (CareerProfile): a career round
/// trips through its file; an empty slot will not activate; every save keeps the previous file as
/// a backup, and a main file that will not read falls back to it; deleting a slot removes both and
/// lets go of it if it was active; a profile keeps only its last MaxBalls balls but totals them all;
/// and slot numbers out of range are harmless. Every test saves into its own temp directory, never
/// the real saves.
/// </summary>
public class CareerSavesTests
{
    private string dir;

    [SetUp]
    public void UseTempDirectory()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_career_tests_" + Guid.NewGuid().ToString("N"));
        CareerSaves.UseDirectoryForTests(dir);
    }

    [TearDown]
    public void RestoreDirectory()
    {
        CareerSaves.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    private string MainFile(int slot) => Path.Combine(dir, $"slot{slot + 1}.json");
    private string BackupFile(int slot) => Path.Combine(dir, $"slot{slot + 1}.bak");

    private static CareerProfile Profile(string name)
    {
        var p = new CareerProfile { name = name, nation = "AUS", hand = eBattingStyle.LeftHanded };
        p.look.number = 7;
        p.look.skin = 4;
        return p;
    }

    private static BallRecord Ball(int runs, eBallEnd end)
    {
        return new BallRecord
        {
            bowlerKind = eBowlerKind.LegSpin, lengthBand = eLengthBand.Full, action = eBallAction.Played,
            realContact = eRealContact.Clean, realQuality = 0.8f, exitSpeed = 27.5f, runs = runs, end = end,
        };
    }

    [Test]
    public void ACareerRoundTripsThroughItsSlot()
    {
        CareerProfile p = Profile("Asha");
        p.Add(Ball(4, eBallEnd.Four));
        p.ratings.timing = 61f;
        CareerSaves.Create(0, p);

        Assert.AreEqual(0, CareerSaves.Active);
        Assert.AreSame(p, CareerSaves.Current);
        Assert.IsTrue(CareerSaves.Exists(0));
        Assert.IsFalse(string.IsNullOrEmpty(p.created), "Create stamps the date");

        CareerProfile loaded = CareerSaves.Load(0);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("Asha", loaded.name);
        Assert.AreEqual("AUS", loaded.nation);
        Assert.AreEqual(eBattingStyle.LeftHanded, loaded.hand);
        Assert.AreEqual(7, loaded.look.number);
        Assert.AreEqual(4, loaded.look.skin);
        Assert.AreEqual(p.created, loaded.created);
        Assert.AreEqual(61f, loaded.ratings.timing);
        Assert.AreEqual(RatingSet.Unknown, loaded.ratings.power);
        Assert.AreEqual(1, loaded.balls.Count);
        Assert.AreEqual(eBowlerKind.LegSpin, loaded.balls[0].bowlerKind);
        Assert.AreEqual(27.5f, loaded.balls[0].exitSpeed);
        Assert.AreEqual(1, loaded.totals.fours);
        Assert.AreEqual(CareerProfile.CurrentVersion, loaded.version);

        CareerSaves.Deactivate();
        Assert.IsTrue(CareerSaves.Activate(0));
        Assert.AreEqual(0, CareerSaves.Active);
        Assert.AreEqual("Asha", CareerSaves.Current.name);
    }

    [Test]
    public void AnEmptySlotWillNotActivate()
    {
        Assert.IsFalse(CareerSaves.Exists(1));
        Assert.IsNull(CareerSaves.Load(1));
        Assert.IsFalse(CareerSaves.Activate(1));
        Assert.AreEqual(-1, CareerSaves.Active);
        Assert.IsNull(CareerSaves.Current);
    }

    [Test]
    public void SavingKeepsThePreviousFileAsTheBackup()
    {
        int saved = 0;
        CareerSaves.Create(0, Profile("First"));
        CareerSaves.Saved += () => saved++;
        Assert.IsFalse(File.Exists(BackupFile(0)), "nothing to back up on the first write");

        CareerSaves.Current.name = "Second";
        CareerSaves.SaveCurrent();

        Assert.AreEqual(1, saved);
        Assert.IsTrue(File.Exists(BackupFile(0)));
        StringAssert.Contains("First", File.ReadAllText(BackupFile(0)));
        StringAssert.Contains("Second", File.ReadAllText(MainFile(0)));
        Assert.IsFalse(File.Exists(MainFile(0) + ".tmp"), "the temp file is moved into place");
        Assert.AreEqual("Second", CareerSaves.Load(0).name);
    }

    [Test]
    public void ACorruptedMainFileFallsBackToTheBackup()
    {
        CareerSaves.Create(0, Profile("First"));
        CareerSaves.Current.name = "Second";
        CareerSaves.SaveCurrent();
        File.WriteAllText(MainFile(0), "{\"version\":1,\"name\":\"Sec");   // cut off mid-write

        CareerProfile p = CareerSaves.Load(0);
        Assert.IsNotNull(p);
        Assert.AreEqual("First", p.name);

        CareerSaves.Deactivate();
        Assert.IsTrue(CareerSaves.Activate(0));
        Assert.AreEqual("First", CareerSaves.Current.name);
    }

    [Test]
    public void ACorruptedFileWithNoBackupReadsAsEmpty()
    {
        CareerSaves.Create(1, Profile("Only"));
        CareerSaves.Deactivate();
        File.WriteAllText(MainFile(1), "");

        Assert.IsNull(CareerSaves.Load(1));
        Assert.IsFalse(CareerSaves.Activate(1));
    }

    [Test]
    public void DeleteRemovesBothFilesAndLetsGoOfTheSlot()
    {
        CareerSaves.Create(2, Profile("Gone"));
        CareerSaves.SaveCurrent();
        Assert.IsTrue(File.Exists(MainFile(2)));
        Assert.IsTrue(File.Exists(BackupFile(2)));

        CareerSaves.Delete(2);

        Assert.IsFalse(File.Exists(MainFile(2)));
        Assert.IsFalse(File.Exists(BackupFile(2)));
        Assert.IsFalse(CareerSaves.Exists(2));
        Assert.AreEqual(-1, CareerSaves.Active);
        Assert.IsNull(CareerSaves.Current);
    }

    [Test]
    public void DeletingAnotherSlotKeepsTheActiveOne()
    {
        CareerSaves.Create(0, Profile("Zero"));
        CareerSaves.Create(1, Profile("One"));
        CareerSaves.Delete(0);

        Assert.IsFalse(CareerSaves.Exists(0));
        Assert.AreEqual(1, CareerSaves.Active);
        Assert.AreEqual("One", CareerSaves.Current.name);
        Assert.AreEqual("One", CareerSaves.Load(1).name);
    }

    [Test]
    public void AProfileKeepsItsLastBallsButTotalsThemAll()
    {
        CareerProfile p = Profile("Totals");
        int counting = CareerProfile.MaxBalls + 20;
        BallRecord last = null;
        for (int i = 0; i < counting; i++)
        {
            eBallEnd end = i % 10 == 0 ? eBallEnd.Four : i % 25 == 0 ? eBallEnd.Six : i % 40 == 1 ? eBallEnd.Out : eBallEnd.Runs;
            int runs = end == eBallEnd.Four ? 4 : end == eBallEnd.Six ? 6 : end == eBallEnd.Out ? 0 : 1;
            last = Ball(runs, end);
            p.Add(last);
        }
        var wide = new BallRecord { wide = true, end = eBallEnd.Wide, runs = 1 };
        p.Add(wide);

        int fours = 0, sixes = 0, outs = 0, runsTotal = 0;
        for (int i = 0; i < counting; i++)
        {
            if (i % 10 == 0) { fours++; runsTotal += 4; }
            else if (i % 25 == 0) { sixes++; runsTotal += 6; }
            else if (i % 40 == 1) outs++;
            else runsTotal += 1;
        }

        Assert.AreEqual(CareerProfile.MaxBalls, p.balls.Count);
        Assert.AreSame(wide, p.balls[p.balls.Count - 1], "the newest is kept");
        Assert.AreSame(last, p.balls[p.balls.Count - 2]);
        Assert.AreEqual(counting, p.totals.balls, "a wide is kept but not counted");
        Assert.AreEqual(runsTotal, p.totals.runs);
        Assert.AreEqual(fours, p.totals.fours);
        Assert.AreEqual(sixes, p.totals.sixes);
        Assert.AreEqual(outs, p.totals.outs);
    }

    [Test]
    public void SlotsOutOfRangeAreHarmless()
    {
        foreach (int slot in new[] { -1, CareerSaves.Slots, 99 })
        {
            Assert.IsFalse(CareerSaves.Exists(slot));
            Assert.IsNull(CareerSaves.Load(slot));
            Assert.IsFalse(CareerSaves.Activate(slot));
            Assert.DoesNotThrow(() => CareerSaves.Delete(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => CareerSaves.Create(slot, Profile("Nope")));
        }
        Assert.AreEqual(-1, CareerSaves.Active);
        Assert.IsNull(CareerSaves.Current);
        Assert.IsFalse(Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0, "nothing written");
    }
}
