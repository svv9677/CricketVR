using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The batting card's feed (BattingCardFeed): how each batter got out, from HUD's dismissal lines
/// off real and simulated balls, kept per scorebook. The HUD events themselves need the scene;
/// this is what the feed does with them. Standalone.
/// Run: Window > General > Test Runner > EditMode > BattingCardFeedTests.
/// </summary>
public class BattingCardFeedTests
{
    [Test]
    public void ARealBallsDismissalLosesTheNameAndTheScore()
    {
        var feed = new BattingCardFeed();
        var ravi = new Batsman("Ravi");
        feed.Record(ravi, "Ravi c Point b Khan  34 (22)");
        Assert.AreEqual("c Point b Khan", feed.Outs[ravi]);
    }

    [Test]
    public void ASimulatedBallsDismissalIsKept()
    {
        // HUD.ScoreSimulatedBall: "<name> <how>", no score behind it.
        var feed = new BattingCardFeed();
        var dev = new Batsman("Dev Mehta");
        feed.Record(dev, "Dev Mehta lbw b Om Iyer");
        Assert.AreEqual("lbw b Om Iyer", feed.Outs[dev]);
        var card = BattingCard.Build("X", new List<Batsman> { dev, new Batsman("B"), new Batsman("C") }, null, null,
                                     feed.Outs, 0, 1, 0, 1);
        Assert.AreEqual("lbw b Iyer", card.Rows[0].HowOut);
    }

    [Test]
    public void NoLineIsPlainOut()
    {
        var feed = new BattingCardFeed();
        var b = new Batsman("A");
        feed.Record(b, null);
        Assert.AreEqual("out", feed.Outs[b]);
        feed.Record(null, "A b Khan");   // ignored
        Assert.AreEqual(1, feed.Outs.Count);
    }

    [Test]
    public void EveryWicketSaysTheCardChanged()
    {
        var feed = new BattingCardFeed();
        int changed = 0;
        feed.Changed += () => changed++;
        feed.Record(new Batsman("A"), "A b Khan  0 (1)");
        feed.Record(new Batsman("B"), "B run out");
        Assert.AreEqual(2, changed);
    }

    [Test]
    public void ANewScorebookForgetsTheOldOutsButNotItsOwn()
    {
        var feed = new BattingCardFeed();
        var oldBook = new List<Batsman> { new Batsman("A"), new Batsman("B") };
        feed.UseBook(oldBook);
        feed.Record(oldBook[0], "A b Khan  4 (6)");

        // HUD.Reset: a new list of new batters, and a wicket in it before the card is next built.
        var newBook = new List<Batsman> { new Batsman("A"), new Batsman("B") };
        feed.Record(newBook[1], "B c & b Khan  0 (1)");
        feed.UseBook(newBook);
        Assert.IsFalse(feed.Outs.ContainsKey(oldBook[0]));
        Assert.AreEqual("c & b Khan", feed.Outs[newBook[1]]);

        feed.UseBook(newBook);   // the same book again changes nothing
        Assert.AreEqual(1, feed.Outs.Count);
    }

    [Test]
    public void DetachingTwiceIsSafe()
    {
        var feed = new BattingCardFeed();
        Assert.DoesNotThrow(() =>
        {
            feed.Detach();
            feed.Detach();
        });
    }
}
