using NUnit.Framework;

/// <summary>
/// ClubScoreboardFigures: the board keeps its batter rows in batting order and moves only the star
/// with the strike; numbers are index + 1; wickets stay 0..10; overs read "6" or "6.3"; no target
/// means a blank row; equal states compare equal (the board only re-meshes on change). Standalone.
/// </summary>
public class ClubScoreboardFiguresTests
{
    private static string[] Rows(BoardState s) => ClubScoreboardFigures.Compose(s).Split('\n');

    /// A row's figure, without the position tag (and, on the first row, the line-height tag).
    private static string Figure(string row)
    {
        string tag = "<pos=" + ClubScoreboardFigures.FiguresAt + ">";
        int at = row.IndexOf(tag, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(at, 0, row);
        return row.Substring(at + tag.Length);
    }

    [Test]
    public void TheLowerIndexIsRowAAndTheStarFollowsTheStrike()
    {
        BoardState s = ClubScoreboardFigures.From(52, 2, 6, 3, 0, 4, 12, 3, 34);
        Assert.AreEqual(4, s.batANo);
        Assert.AreEqual(34, s.batARuns);
        Assert.AreEqual(5, s.batBNo);
        Assert.AreEqual(12, s.batBRuns);
        Assert.IsFalse(s.aOnStrike);
        string[] rows = Rows(s);
        Assert.AreEqual("4  34", Figure(rows[4]));
        Assert.AreEqual("5  12*", Figure(rows[5]));

        BoardState swapped = ClubScoreboardFigures.From(53, 2, 6, 4, 0, 3, 34, 4, 13);
        Assert.IsTrue(swapped.aOnStrike);
        Assert.AreEqual("4  34*", Figure(Rows(swapped)[4]));
        Assert.AreEqual("5  13", Figure(Rows(swapped)[5]));
    }

    [Test]
    public void TotalsWicketsOversAndTarget()
    {
        string[] rows = Rows(ClubScoreboardFigures.From(123, 4, 15, 2, 150, 0, 60, 5, 7));
        Assert.AreEqual(6, rows.Length);
        Assert.AreEqual("123", Figure(rows[0]));
        Assert.AreEqual("4", Figure(rows[1]));
        Assert.AreEqual("15.2", Figure(rows[2]));
        Assert.AreEqual("150", Figure(rows[3]));
        Assert.IsTrue(rows[0].StartsWith("<line-height=" + ClubScoreboardFigures.LineHeight + ">"));

        string[] noTarget = Rows(ClubScoreboardFigures.From(40, 1, 6, 0, 0, 0, 20, 2, 10));
        Assert.AreEqual("6", Figure(noTarget[2]), "end of an over: no .0");
        Assert.AreEqual("", Figure(noTarget[3]), "no target, blank row");
        Assert.AreEqual("0", ClubScoreboardFigures.Overs(0, 0));
    }

    [Test]
    public void WicketsAreClampedToTen()
    {
        Assert.AreEqual(10, ClubScoreboardFigures.From(90, 13, 5, 1, 0, -1, 0, -1, 0).wkts);
        Assert.AreEqual(0, ClubScoreboardFigures.From(0, -2, 0, 0, 0, -1, 0, -1, 0).wkts);
    }

    [Test]
    public void MissingBattersLeaveBlankRows()
    {
        BoardState none = ClubScoreboardFigures.From(0, 0, 0, 0, 0, -1, 0, -1, 0);
        Assert.AreEqual(0, none.batANo);
        Assert.AreEqual("", Figure(Rows(none)[4]));
        Assert.AreEqual("", Figure(Rows(none)[5]));

        BoardState strikerOnly = ClubScoreboardFigures.From(10, 9, 3, 1, 0, 10, 4, -1, 0);
        Assert.AreEqual("11  4*", Figure(Rows(strikerOnly)[4]));
        Assert.AreEqual("", Figure(Rows(strikerOnly)[5]));

        BoardState otherOnly = ClubScoreboardFigures.From(10, 9, 3, 1, 0, -1, 0, 6, 8);
        Assert.AreEqual("7  8", Figure(Rows(otherOnly)[4]), "nobody on strike: no star");
        Assert.AreEqual("", Figure(Rows(otherOnly)[5]));

        BoardState same = ClubScoreboardFigures.From(10, 0, 1, 0, 0, 2, 5, 2, 5);
        Assert.AreEqual(0, same.batBNo, "one batter listed twice is one row");
    }

    [Test]
    public void EqualStatesCompareEqual()
    {
        BoardState a = ClubScoreboardFigures.From(52, 2, 6, 3, 80, 4, 12, 3, 34);
        BoardState b = ClubScoreboardFigures.From(52, 2, 6, 3, 80, 4, 12, 3, 34);
        Assert.IsTrue(a == b);
        Assert.IsTrue(a.Equals((object)b));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.IsTrue(a != ClubScoreboardFigures.From(52, 2, 6, 3, 80, 3, 34, 4, 12), "strike changed");
        Assert.IsTrue(a != ClubScoreboardFigures.From(53, 2, 6, 3, 80, 4, 12, 3, 34), "a run");
        Assert.IsTrue(a != ClubScoreboardFigures.From(52, 2, 6, 4, 80, 4, 12, 3, 34), "a ball");
        Assert.IsFalse(a.Equals(null));
    }

    [Test]
    public void LabelsMatchTheRowsAndTextIsLatin1()
    {
        string[] labels = ClubScoreboardFigures.Labels.Split('\n');
        Assert.AreEqual(Rows(default).Length, labels.Length);
        string all = ClubScoreboardFigures.Labels + ClubScoreboardFigures.Compose(
            ClubScoreboardFigures.From(123, 4, 15, 2, 150, 0, 60, 5, 7));
        foreach (char ch in all) Assert.LessOrEqual((int)ch, 255, "board text must be Latin-1");
    }
}
