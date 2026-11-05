using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// The Records screen's words, which need no scene (CareerRecordsScreen): the result screen's best
/// line, each row's value ("--" while unset) and where it came from, the milestones row, the career
/// header, Watch only with the clip on disk, and every line Latin-1 and short enough for the card.
/// </summary>
public class CareerRecordsScreenTests
{
    private string dir;

    [SetUp]
    public void UseTempDirectory()
    {
        dir = Path.Combine(Path.GetTempPath(), "cvr_records_screen_" + Guid.NewGuid().ToString("N"));
        ReplayVault.UseDirectoryForTests(dir);
    }

    [TearDown]
    public void RestoreDirectory()
    {
        ReplayVault.UseDirectoryForTests(null);
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        catch (IOException) { }
    }

    private static RecordEntry Entry(float value, int runs = 64, int balls = 41, bool notOut = true, string opponent = "Harbour CC",
                                     int sixes = 3, string date = "2026-11-12")
    {
        return new RecordEntry
        {
            set = true, value = value, runs = runs, balls = balls, notOut = notOut, sixes = sixes,
            team = "Rivertown", opponent = opponent, date = date, rung = eRung.ProLeague, season = 2,
        };
    }

    private static CareerRecordBook Book()
    {
        return new CareerRecordBook
        {
            highScore = Entry(112, runs: 112, balls: 70),
            fastestFifty = Entry(23),
            bestStrikeRate = Entry(245.46f),
            mostSixes = Entry(9, sixes: 9),
            longestSix = Entry(103.6f),
        };
    }

    private static string Plain(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "");

    private static void AssertLatin1(string s)
    {
        foreach (char c in s ?? "")
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
        StringAssert.DoesNotContain("—", s ?? "");
    }

    [Test]
    public void EachRowsValue_AndDashesWhileUnset()
    {
        CareerRecordBook b = Book();
        Assert.AreEqual("112*", CareerRecordsScreen.RowValue(eRecord.HighScore, b.highScore));
        Assert.AreEqual("23 balls", CareerRecordsScreen.RowValue(eRecord.FastestFifty, b.fastestFifty));
        Assert.AreEqual("245.5", CareerRecordsScreen.RowValue(eRecord.BestStrikeRate, b.bestStrikeRate));
        Assert.AreEqual("9", CareerRecordsScreen.RowValue(eRecord.MostSixes, b.mostSixes));
        Assert.AreEqual("104 m", CareerRecordsScreen.RowValue(eRecord.LongestSix, b.longestSix));
        b.highScore.notOut = false;
        Assert.AreEqual("112", CareerRecordsScreen.RowValue(eRecord.HighScore, b.highScore));
        foreach (eRecord r in CareerRecordsScreen.RecordRows)
        {
            Assert.AreEqual("--", CareerRecordsScreen.RowValue(r, new RecordEntry()));
            Assert.AreEqual("--", CareerRecordsScreen.RowValue(r, null));
        }
    }

    [Test]
    public void TheDetailSaysWhereTheRecordCameFrom()
    {
        Assert.AreEqual("64* off 41  ·  vs Harbour CC  ·  Pro League  ·  2026-11-12", CareerRecordsScreen.RowDetail(Entry(64)));
        Assert.AreEqual("", CareerRecordsScreen.RowDetail(new RecordEntry()), "unset: nothing");
        Assert.AreEqual("", CareerRecordsScreen.RowDetail(null));
        var old = new RecordEntry { set = true, value = 81f };
        Assert.AreEqual("From your earlier matches", CareerRecordsScreen.RowDetail(old), "a backfilled six knows no match");
        Assert.AreEqual("vs Hörby", CareerRecordsScreen.RowDetail(new RecordEntry { set = true, opponent = "Hörby" }));
    }

    [Test]
    public void TheRowsAreNamedInOrder_WithTheMilestonesLast()
    {
        Assert.AreEqual(5, CareerRecordsScreen.RecordRows.Length);
        Assert.AreEqual(CareerRecordsScreen.RecordRows.Length + 1, CareerRecordsScreen.Rows);
        string[] names = { "Highest score", "Fastest fifty", "Best strike rate", "Most sixes", "Longest six", "Milestones" };
        for (int i = 0; i < names.Length; i++)
            Assert.AreEqual(names[i], CareerRecordsScreen.RowName(i));
        Assert.AreEqual("Highest score   112*", Plain(CareerRecordsScreen.NameLine(0, Book())));
        Assert.AreEqual("Longest six   --", Plain(CareerRecordsScreen.NameLine(4, null)));
        Assert.AreEqual("Milestones   0", Plain(CareerRecordsScreen.NameLine(5, null)));
    }

    [Test]
    public void TheMilestonesRowCountsAndNamesTheLatest()
    {
        CareerRecordBook b = Book();
        Assert.AreEqual("Fifties, hundreds, career runs and caps.", CareerRecordsScreen.MilestoneDetail(b));
        b.reached.AddRange(new[] { "fifty", "runs100", "caps1" });
        b.lastMilestone = "100 career runs";
        b.lastMilestoneDate = "2026-11-12";
        Assert.AreEqual("3", CareerRecordsScreen.MilestoneValue(b));
        Assert.AreEqual("Latest: 100 career runs  ·  2026-11-12", CareerRecordsScreen.MilestoneDetail(b));
        b.lastMilestoneDate = "";
        Assert.AreEqual("Latest: 100 career runs", CareerRecordsScreen.MilestoneDetail(b));
        Assert.AreEqual("0", CareerRecordsScreen.MilestoneValue(null));
    }

    [Test]
    public void TheHeaderIsTheCareerFigures()
    {
        Assert.AreEqual("No innings yet. Your bests start with your first match.", CareerRecordsScreen.HeaderLine(null));
        Assert.AreEqual("No innings yet. Your bests start with your first match.", CareerRecordsScreen.HeaderLine(new CareerStats()));
        var st = new CareerStats { innings = 31, notOuts = 1, runs = 1234, balls = 935, fifties = 7, hundreds = 2, sixes = 41 };
        Assert.AreEqual("1,234 runs · avg 41.1 · SR 132 · 50s 7 · 100s 2 · 6s 41", CareerRecordsScreen.HeaderLine(st));
        // A big career still fits the 20 px header line (about 62 characters inside the card).
        var big = new CareerStats { innings = 260, notOuts = 11, runs = 10234, balls = 7750, fifties = 17, hundreds = 12, sixes = 141 };
        Assert.LessOrEqual(CareerRecordsScreen.HeaderLine(big).Length, 62, CareerRecordsScreen.HeaderLine(big));
    }

    [Test]
    public void TheBestLineNamesTwoOrOneAndCountsTheRest()
    {
        CareerRecordBook b = Book();
        Assert.AreEqual("", CareerRecordsScreen.BestLine(null, b));
        Assert.AreEqual("", CareerRecordsScreen.BestLine(new List<eRecord>(), b));
        Assert.AreEqual("", CareerRecordsScreen.BestLine(new List<eRecord> { eRecord.HighScore }, null));
        Assert.AreEqual("New career best: highest score 112*",
                        CareerRecordsScreen.BestLine(new List<eRecord> { eRecord.HighScore }, b));
        Assert.AreEqual("New career bests: highest score 112*, longest six 104 m",
                        CareerRecordsScreen.BestLine(new List<eRecord> { eRecord.HighScore, eRecord.LongestSix }, b));
        Assert.AreEqual("New career bests: fastest fifty 23 balls and 3 more",
                        CareerRecordsScreen.BestLine(new List<eRecord> { eRecord.FastestFifty, eRecord.BestStrikeRate,
                                                                         eRecord.MostSixes, eRecord.LongestSix }, b));
        Assert.AreEqual("New career bests: strike rate 245.5 and 2 more",
                        CareerRecordsScreen.BestLine(new List<eRecord> { eRecord.BestStrikeRate, eRecord.MostSixes,
                                                                         eRecord.HighScore }, b));
    }

    [Test]
    public void EveryLineIsLatin1AndFitsTheCard()
    {
        CareerRecordBook b = Book();
        b.highScore.opponent = "Łódź — Rangers";
        b.lastMilestone = "10,000 career runs";
        var all = new List<eRecord>(CareerRecordsScreen.RecordRows);
        // Every pair named in full, and every first record with 2-4 more: about 60 characters fit one
        // 22 px line of the 696 px card.
        for (int i = 0; i < all.Count; i++)
            for (int j = 0; j < all.Count; j++)
            {
                if (i == j) continue;
                var bests = new List<eRecord> { all[i], all[j] };
                for (int k = 0; k <= all.Count; k++)
                {
                    string line = CareerRecordsScreen.BestLine(bests, b);
                    AssertLatin1(line);
                    Assert.LessOrEqual(line.Length, 60, line);
                    if (k < all.Count && k != i && k != j) bests.Add(all[k]);
                }
            }
        for (int i = 0; i < CareerRecordsScreen.Rows; i++)
        {
            AssertLatin1(CareerRecordsScreen.NameLine(i, b));
            AssertLatin1(CareerRecordsScreen.RowName(i));
        }
        foreach (eRecord r in all)
            AssertLatin1(CareerRecordsScreen.RowDetail(b.Get(r)));
        AssertLatin1(CareerRecordsScreen.MilestoneDetail(b));
        AssertLatin1(CareerRecordsScreen.HeaderLine(new CareerStats { innings = 1, runs = 99999, balls = 1 }));
        AssertLatin1(CareerRecordsScreen.NoteText);
        StringAssert.Contains("carry", CareerRecordsScreen.NoteText);
        StringAssert.Contains("real blade", CareerRecordsScreen.NoteText);
    }

    [Test]
    public void WatchIsOnOnlyWithTheClipOnDisk()
    {
        CareerRecordBook b = Book();
        Assert.IsFalse(CareerRecordsScreen.CanWatch(b, 0), "no stamp: no clip");
        b.longestSix.clipStamp = "0badf00d-b17";
        Assert.IsFalse(CareerRecordsScreen.CanWatch(b, 0), "a stamp but no file");
        Directory.CreateDirectory(ReplayVault.SlotDir(0));
        File.WriteAllText(ReplayVault.PathFor(0, "0badf00d-b17"), "clip");
        Assert.IsTrue(CareerRecordsScreen.CanWatch(b, 0));
        Assert.IsFalse(CareerRecordsScreen.CanWatch(b, 1), "another slot's clips are not this one's");
        Assert.IsFalse(CareerRecordsScreen.CanWatch(b, -1), "no career active");
        b.longestSix.set = false;
        Assert.IsFalse(CareerRecordsScreen.CanWatch(b, 0));
        Assert.IsFalse(CareerRecordsScreen.CanWatch(null, 0));
    }
}
