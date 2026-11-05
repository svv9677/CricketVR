using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// ClubScoreboardFigures.LastWicket: the LAST WKT figure is the total at the fall of the last
/// wicket, worked out from the scorebook's ball list (HUD.BowledBalls), not the total when the board
/// next polls. The case that matters is the career's batch of simulated balls in one frame: a
/// wicket and then runs before the board looks, which must still show the total at the wicket.
/// BallRuns reads each kind of entry the scorebook writes. Standalone.
/// </summary>
public class ClubScoreboardFallOfWicketTests
{
    static List<string> Book(params string[] balls) => new List<string>(balls);

    [Test]
    public void EachScorebookEntryCountsTheRunsItPutOnTheTotal()
    {
        Assert.AreEqual(0, ClubScoreboardFigures.BallRuns("*"), "a dot");
        Assert.AreEqual(0, ClubScoreboardFigures.BallRuns("W"), "a wicket ball scores none");
        Assert.AreEqual(4, ClubScoreboardFigures.BallRuns("4"));
        Assert.AreEqual(6, ClubScoreboardFigures.BallRuns("6"));
        Assert.AreEqual(1, ClubScoreboardFigures.BallRuns("wd"), "a wide is one");
        Assert.AreEqual(3, ClubScoreboardFigures.BallRuns("3wd"), "a wide and two more");
        Assert.AreEqual(1, ClubScoreboardFigures.BallRuns("nb"), "a no-ball is one");
        Assert.AreEqual(5, ClubScoreboardFigures.BallRuns("5nb"), "a no-ball and a four");
        Assert.AreEqual(2, ClubScoreboardFigures.BallRuns("2b"), "two byes");
        Assert.AreEqual(0, ClubScoreboardFigures.BallRuns(""));
        Assert.AreEqual(0, ClubScoreboardFigures.BallRuns(null));
    }

    [Test]
    public void RunsScoredAfterTheWicketInTheSameBatchAreNotChalkedToIt()
    {
        // 40/3 when the partner is out, then the new batter takes 6 (a 4 and a 2) before the
        // board's next poll: the board must read 40, not 46.
        List<string> book = Book("6", "6", "4", "W", "6", "6", "4", "W", "4", "4", "W", "4", "2");
        Assert.AreEqual(40, ClubScoreboardFigures.LastWicket(book, 46, 3));
    }

    [Test]
    public void TheRestOfTheInningsInOneFrameStillShowsTheLastWicketNotTheFinalTotal()
    {
        // The player is out, then FinishInnings plays the rest in one go: wides, no-balls and byes
        // after the last wicket all come off: 10 at the wicket, 1 + 4 + 1 + 1 + 3 + 5 + 2 = 17 after.
        List<string> book = Book("6", "W", "4", "W", "1", "*", "4", "wd", "1", "3wd", "5nb", "2b");
        int total = 10 + 17;
        Assert.AreEqual(10, ClubScoreboardFigures.LastWicket(book, total, 2));
    }

    [Test]
    public void AWicketWithNothingAfterItIsTheTotalNow()
    {
        Assert.AreEqual(17, ClubScoreboardFigures.LastWicket(Book("4", "6", "1", "6", "W"), 17, 1));
        Assert.AreEqual(11, ClubScoreboardFigures.LastWicket(Book("4", "6", "1", "W", "W"), 11, 2), "two in a row");
    }

    [Test]
    public void NothingDownShowsNothing()
    {
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(Book("4", "6"), 10, 0), "no wicket yet");
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(new List<string>(), 0, 0), "a new innings");
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(null, 0, 0), "no book yet");
    }

    [Test]
    public void ABookWithNoWicketInItFallsBackToTheTotalNeverBelowZero()
    {
        Assert.AreEqual(30, ClubScoreboardFigures.LastWicket(Book("6", "4"), 30, 1), "no W to count back to");
        Assert.AreEqual(30, ClubScoreboardFigures.LastWicket(null, 30, 1), "no book");
        Assert.AreEqual(0, ClubScoreboardFigures.LastWicket(Book("W", "6"), 2, 1), "a total behind its book");
    }
}
