using NUnit.Framework;
using Slot = ClubScoreboardLayout.Slot;

/// <summary>
/// ClubScoreboardFigures: the board keeps its batter groups in batting order and moves only the star
/// with the strike; numbers are index + 1; wickets stay 0..10; overs read "6" at the end of an over
/// (no ball, no dot) and "6" "3" during one; no target means blank TARGET and NEED tins; equal states
/// compare equal (the board only re-meshes on change). Standalone. The layout of the text itself is
/// ClubScoreboardLayoutTests'.
/// </summary>
public class ClubScoreboardFiguresTests
{
    private static string Shown(BoardState s, Slot slot) => ClubScoreboardFigures.Figures(s)[(int)slot];

    [Test]
    public void TheLowerIndexIsGroupAAndTheStarFollowsTheStrike()
    {
        BoardState s = ClubScoreboardFigures.From(52, 2, 6, 3, 0, 4, 12, 3, 34);
        Assert.AreEqual(4, s.batANo);
        Assert.AreEqual(34, s.batARuns);
        Assert.AreEqual(5, s.batBNo);
        Assert.AreEqual(12, s.batBRuns);
        Assert.IsFalse(s.aOnStrike);
        Assert.AreEqual("4", Shown(s, Slot.BatANo));
        Assert.AreEqual("34", Shown(s, Slot.BatARuns));
        Assert.AreEqual("", Shown(s, Slot.BatAStar));
        Assert.AreEqual("5", Shown(s, Slot.BatBNo));
        Assert.AreEqual("12", Shown(s, Slot.BatBRuns));
        Assert.AreEqual("*", Shown(s, Slot.BatBStar));

        BoardState swapped = ClubScoreboardFigures.From(53, 2, 6, 4, 0, 3, 34, 4, 13);
        Assert.IsTrue(swapped.aOnStrike);
        Assert.AreEqual("*", Shown(swapped, Slot.BatAStar));
        Assert.AreEqual("", Shown(swapped, Slot.BatBStar));
        Assert.AreEqual("13", Shown(swapped, Slot.BatBRuns));
    }

    [Test]
    public void TotalsWicketsOversAndTarget()
    {
        BoardState s = ClubScoreboardFigures.From(123, 4, 15, 2, 150, 0, 60, 5, 7);
        Assert.AreEqual("123", Shown(s, Slot.Total));
        Assert.AreEqual("4", Shown(s, Slot.Wickets));
        Assert.AreEqual("15", Shown(s, Slot.Overs));
        Assert.AreEqual("2", Shown(s, Slot.Balls));
        Assert.AreEqual("150", Shown(s, Slot.Target));
        Assert.AreEqual("27", Shown(s, Slot.Need));

        BoardState noTarget = ClubScoreboardFigures.From(40, 1, 6, 0, 0, 0, 20, 2, 10);
        Assert.AreEqual("6", Shown(noTarget, Slot.Overs));
        Assert.AreEqual("", Shown(noTarget, Slot.Balls), "end of an over: no ball tin");
        Assert.AreEqual("", Shown(noTarget, Slot.Target), "no target, blank tins");
        Assert.AreEqual("", Shown(noTarget, Slot.Need));
        Assert.AreEqual("0", Shown(default, Slot.Overs));
        Assert.AreEqual("0", Shown(default, Slot.Total));
    }

    [Test]
    public void WicketsAreClampedToTen()
    {
        Assert.AreEqual(10, ClubScoreboardFigures.From(90, 13, 5, 1, 0, -1, 0, -1, 0).wkts);
        Assert.AreEqual(0, ClubScoreboardFigures.From(0, -2, 0, 0, 0, -1, 0, -1, 0).wkts);
    }

    [Test]
    public void MissingBattersLeaveBlankGroups()
    {
        BoardState none = ClubScoreboardFigures.From(0, 0, 0, 0, 0, -1, 0, -1, 0);
        Assert.AreEqual(0, none.batANo);
        foreach (Slot slot in new[] { Slot.BatANo, Slot.BatARuns, Slot.BatAStar, Slot.BatBNo, Slot.BatBRuns, Slot.BatBStar })
            Assert.AreEqual("", Shown(none, slot), slot.ToString());

        BoardState strikerOnly = ClubScoreboardFigures.From(10, 9, 3, 1, 0, 10, 4, -1, 0);
        Assert.AreEqual("11", Shown(strikerOnly, Slot.BatANo));
        Assert.AreEqual("4", Shown(strikerOnly, Slot.BatARuns));
        Assert.AreEqual("*", Shown(strikerOnly, Slot.BatAStar));
        Assert.AreEqual("", Shown(strikerOnly, Slot.BatBNo));

        BoardState otherOnly = ClubScoreboardFigures.From(10, 9, 3, 1, 0, -1, 0, 6, 8);
        Assert.AreEqual("7", Shown(otherOnly, Slot.BatANo));
        Assert.AreEqual("", Shown(otherOnly, Slot.BatAStar), "nobody on strike: no star");
        Assert.AreEqual("", Shown(otherOnly, Slot.BatBStar));

        BoardState same = ClubScoreboardFigures.From(10, 0, 1, 0, 0, 2, 5, 2, 5);
        Assert.AreEqual(0, same.batBNo, "one batter listed twice is one group");
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
        Assert.IsTrue(a != ClubScoreboardFigures.From(52, 2, 6, 3, 80, 4, 12, 3, 34, 41), "the last wicket");
        Assert.IsFalse(a.Equals(null));
    }

    [Test]
    public void BoardTextIsLatin1()
    {
        string all = ClubScoreboardFigures.Compose(ClubScoreboardFigures.From(123, 4, 15, 2, 150, 0, 60, 5, 7, 98));
        foreach (ClubScoreboardLayout.Label l in ClubScoreboardLayout.Labels) all += l.text;
        foreach (char ch in all) Assert.LessOrEqual((int)ch, 255, "board text must be Latin-1");
    }
}
