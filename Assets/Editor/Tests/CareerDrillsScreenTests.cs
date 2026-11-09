using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The career nets screen's pure side (CareerDrillsScreen), which needs no scene: when the drills
/// open, the coach's pick, which drill each row is, reading a drill's record without adding one to
/// the save, the row name and its COACH'S PICK, and that every string stays in Latin-1, the only
/// characters the TMP default font has. Standalone; nothing here reads or writes a save.
/// Run: Window > General > Test Runner > EditMode > CareerDrillsScreenTests.
/// </summary>
public class CareerDrillsScreenTests
{
    /// The text a player sees: rich-text tags taken out.
    private static string Plain(string rich) => Regex.Replace(rich ?? "", "<[^>]*>", "");

    private static void AssertLatin1(string s)
    {
        foreach (char c in s ?? "")
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    private static CareerProfile Profile(bool trialDone) => new CareerProfile { name = "Test", trialDone = trialDone };

    [Test]
    public void OneRowPerDrill()
    {
        Assert.AreEqual(10, CareerDrillsScreen.Rows);
        Assert.AreEqual(DrillDefs.Count, CareerDrillsScreen.Rows);
    }

    [Test]
    public void LockedUntilTheTrialIsDone()
    {
        Assert.IsFalse(CareerDrillsScreen.CanStart(null));
        Assert.IsFalse(CareerDrillsScreen.CanStart(Profile(false)));
        Assert.IsTrue(CareerDrillsScreen.CanStart(Profile(true)));
    }

    [Test]
    public void TheCoachPicksOnlyOnceThereAreRatings()
    {
        Assert.AreEqual(eDrill.None, CareerDrillsScreen.CoachPick(null));
        Assert.AreEqual(eDrill.None, CareerDrillsScreen.CoachPick(Profile(false)));
        CareerProfile p = Profile(true);
        Assert.AreEqual(DrillCoach.Recommend(p.ratings), CareerDrillsScreen.CoachPick(p));
        p.ratings = null;
        Assert.AreEqual(eDrill.None, CareerDrillsScreen.CoachPick(p));
    }

    [Test]
    public void RowsAreTheDrillsInOrderAndNothingPastTheEnd()
    {
        DrillDef[] all = DrillDefs.All;
        Assert.LessOrEqual(all.Length, CareerDrillsScreen.Rows, "more drills than the card has rows");
        for (int i = 0; i < all.Length; i++)
            Assert.AreSame(all[i], CareerDrillsScreen.DrillAt(i));
        Assert.IsNull(CareerDrillsScreen.DrillAt(all.Length));
        Assert.IsNull(CareerDrillsScreen.DrillAt(-1));
        Assert.IsNull(CareerDrillsScreen.DrillAt(CareerDrillsScreen.Rows));
    }

    [Test]
    public void ReadingARecordNeverAddsOneToTheSave()
    {
        var book = new DrillBook();
        DrillRecord blank = CareerDrillsScreen.RecordOf(book, eDrill.Spin);
        Assert.AreEqual(eDrill.Spin, blank.drill);
        Assert.AreEqual(0, blank.started + blank.attempts + blank.best + blank.last);
        Assert.AreEqual(0, book.records.Count, "showing the screen added a record");

        DrillRecord spin = book.For(eDrill.Spin);
        spin.best = 72;
        spin.last = 64;
        book.For(eDrill.Yorkers).best = 40;
        Assert.AreSame(spin, CareerDrillsScreen.RecordOf(book, eDrill.Spin));
        Assert.AreEqual(2, book.records.Count);

        Assert.AreEqual(eDrill.Gaps, CareerDrillsScreen.RecordOf(null, eDrill.Gaps).drill);
        Assert.AreEqual(eDrill.Gaps, CareerDrillsScreen.RecordOf(new DrillBook { records = null }, eDrill.Gaps).drill);
    }

    [Test]
    public void TheCoachsPickIsMarkedOnItsRowOnly()
    {
        var d = new DrillDef { id = eDrill.ShortBall, name = "Short ball" };
        Assert.AreEqual("Short ball", Plain(CareerDrillsScreen.RowName(d, false)));
        string picked = Plain(CareerDrillsScreen.RowName(d, true));
        StringAssert.StartsWith("Short ball", picked);
        StringAssert.Contains("COACH'S PICK", picked);
        StringAssert.DoesNotContain("COACH", Plain(CareerDrillsScreen.RowName(d, false)));
        Assert.AreEqual("", Plain(CareerDrillsScreen.RowName(null, false)));
    }

    [Test]
    public void EveryLineIsLatin1()
    {
        AssertLatin1(CareerDrillsScreen.LockedText);
        AssertLatin1(CareerDrillsScreen.PracticeName);
        AssertLatin1(CareerDrillsScreen.PracticeDetail);
        AssertLatin1(CareerDrillsScreen.RowName(new DrillDef { name = "Leave – outside off" }, true));
        foreach (DrillDef d in DrillDefs.All)
            AssertLatin1(CareerDrillsScreen.RowName(d, true));
    }
}
