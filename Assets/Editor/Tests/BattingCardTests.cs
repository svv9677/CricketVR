using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The match's batting card (BattingCard): who is in, out and yet to bat, how each went, the
/// striker's star, "You", the extras, and names and dismissals cut to fit. Standalone.
/// Run: Window > General > Test Runner > EditMode > BattingCardTests.
/// </summary>
public class BattingCardTests
{
    private static List<Batsman> Side(params string[] names)
    {
        var side = new List<Batsman>();
        foreach (string n in names)
            side.Add(new Batsman(n));
        return side;
    }

    private static void Bat(Batsman b, int runs, int balls)
    {
        b.Runs = runs;
        b.Balls = balls;
    }

    /// The card in the brief: 42/2 after 6.3, two out, the career player and a partner in.
    private static BattingCard WestfieldCard()
    {
        List<Batsman> side = Side("Arjun Shah", "Dev Mehta", "Me Myself", "Ravi Patel", "Ben Lee");
        Bat(side[0], 4, 6);
        Bat(side[1], 0, 2);
        Bat(side[2], 27, 19);
        Bat(side[3], 9, 11);
        side[2].IsHuman = true;
        var outs = new Dictionary<Batsman, string> { { side[0], "c Jones b Khan" }, { side[1], "b Khan" } };
        return BattingCard.Build("Westfield CC", side, side[2], side[3], outs, 42, 2, 6, 3);
    }

    [Test]
    public void TheHeadingAndTheFoot()
    {
        BattingCard card = WestfieldCard();
        Assert.AreEqual("Westfield CC", card.Team);
        Assert.AreEqual("42/2", card.Score);
        Assert.AreEqual("6.3 ov", card.Overs);
        Assert.AreEqual("Total 42/2", card.Total);
        Assert.AreEqual("Extras 2", card.Extras, "42 on the board, 40 off the bat");
    }

    [Test]
    public void OutNotOutAndYetToBat()
    {
        BattingCard card = WestfieldCard();
        Assert.AreEqual(5, card.Rows.Count);
        BattingCardRow shah = card.Rows[0], you = card.Rows[2], patel = card.Rows[3], lee = card.Rows[4];
        Assert.AreEqual("A. Shah", shah.Name);
        Assert.AreEqual("c Jones b Khan", shah.HowOut);
        Assert.AreEqual(eBatterState.Out, shah.State);
        Assert.AreEqual("4", shah.Runs);
        Assert.AreEqual("(6)", shah.Balls);
        Assert.AreEqual("b Khan", card.Rows[1].HowOut);
        Assert.AreEqual("0", card.Rows[1].Runs, "a duck is a 0");

        Assert.AreEqual("You *", you.Name);
        Assert.IsTrue(you.You && you.Striker);
        Assert.AreEqual(BattingCard.NotOut, you.HowOut);
        Assert.AreEqual("27", you.Runs);
        Assert.AreEqual("(19)", you.Balls);

        Assert.AreEqual("R. Patel", patel.Name, "the non-striker has no star");
        Assert.AreEqual(BattingCard.NotOut, patel.HowOut);
        Assert.IsFalse(patel.Striker);

        Assert.AreEqual("B. Lee", lee.Name);
        Assert.AreEqual(eBatterState.YetToBat, lee.State);
        Assert.AreEqual(BattingCard.YetToBat, lee.HowOut);
        Assert.AreEqual("", lee.Runs, "no nought for a man who has not batted");
        Assert.AreEqual("", lee.Balls);
    }

    [Test]
    public void AFallenBatterTheFeedMissedIsStillOut()
    {
        // Two wickets down and nothing recorded for either (the card opened late): the first two
        // have plainly been and gone, the third and fourth are in, the fifth is to come.
        List<Batsman> side = Side("A", "B", "C", "D", "E");
        BattingCard card = BattingCard.Build("X", side, side[2], side[3], null, 10, 2, 2, 0);
        Assert.AreEqual(eBatterState.Out, card.Rows[0].State);
        Assert.AreEqual(BattingCard.Out, card.Rows[0].HowOut);
        Assert.AreEqual(eBatterState.Out, card.Rows[1].State);
        Assert.AreEqual(eBatterState.NotOut, card.Rows[2].State);
        Assert.AreEqual(eBatterState.NotOut, card.Rows[3].State);
        Assert.AreEqual(eBatterState.YetToBat, card.Rows[4].State);
    }

    [Test]
    public void AllOutTheWrappedStrikerIsNotNotOut()
    {
        // After the tenth wicket HUD's striker wraps back to the first opener (IncrementWickets).
        List<Batsman> side = Side("P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8", "P9", "P10", "P11");
        var outs = new Dictionary<Batsman, string>();
        for (int i = 0; i < 11; i++)
            if (i != 6)
                outs[side[i]] = "b Khan";
        BattingCard card = BattingCard.Build("X", side, side[0], side[6], outs, 80, 10, 9, 2);
        Assert.AreEqual(eBatterState.Out, card.Rows[0].State);
        Assert.IsFalse(card.Rows[0].Striker);
        Assert.AreEqual("P1", card.Rows[0].Name, "no star on a man who is out");
        Assert.AreEqual(eBatterState.NotOut, card.Rows[6].State);
        int notOut = 0;
        foreach (BattingCardRow r in card.Rows)
            if (r.State == eBatterState.NotOut) notOut++;
        Assert.AreEqual(1, notOut);
    }

    [Test]
    public void TheOpenersAreNotOutBeforeABall()
    {
        List<Batsman> side = Side("A", "B", "C");
        BattingCard card = BattingCard.Build("X", side, side[0], side[1], null, 0, 0, 0, 0);
        Assert.AreEqual("A *", card.Rows[0].Name);
        Assert.AreEqual(BattingCard.NotOut, card.Rows[0].HowOut);
        Assert.AreEqual("0", card.Rows[0].Runs);
        Assert.AreEqual("(0)", card.Rows[0].Balls);
        Assert.AreEqual(BattingCard.NotOut, card.Rows[1].HowOut);
        Assert.AreEqual(BattingCard.YetToBat, card.Rows[2].HowOut);
        Assert.AreEqual("Extras 0", card.Extras);
        Assert.AreEqual("0.0 ov", card.Overs);
    }

    [Test]
    public void ExtrasAreNeverNegative()
    {
        // Runs the side was given before HUD counted them to batters cannot make extras below 0.
        List<Batsman> side = Side("A", "B");
        Bat(side[0], 10, 4);
        BattingCard card = BattingCard.Build("X", side, side[0], side[1], null, 6, 0, 0, 4);
        Assert.AreEqual("Extras 0", card.Extras);
    }

    [Test]
    public void NamesAreShortenedAndCut()
    {
        Assert.AreEqual("A. Shah", BattingCard.ShortName("Arjun Shah"));
        Assert.AreEqual("A. de Villiers", BattingCard.ShortName("Abraham de Villiers"));
        Assert.AreEqual("Ishan", BattingCard.ShortName("Ishan"), "Quick Match names are one word");
        Assert.AreEqual("", BattingCard.ShortName(null));
        Assert.AreEqual("A. Shah", BattingCard.ShortName("  Arjun Shah "));

        List<Batsman> side = Side("Bartholomew Wolstenholme-Fairweather", "B");
        BattingCard card = BattingCard.Build("X", side, side[0], side[1], null, 0, 0, 0, 0);
        string name = card.Rows[0].Name;
        Assert.IsTrue(name.EndsWith("... *"), $"the star survives the cut: '{name}'");
        Assert.LessOrEqual(name.Length, BattingCard.NameChars + 2);
    }

    [Test]
    public void TruncateKeepsShortTextAndMarksTheCut()
    {
        Assert.AreEqual("short", BattingCard.Truncate("short", 10));
        Assert.AreEqual("exactly10!", BattingCard.Truncate("exactly10!", 10));
        Assert.AreEqual("abcdefg...", BattingCard.Truncate("abcdefghijklmnop", 10));
        Assert.AreEqual("", BattingCard.Truncate(null, 10));
    }

    [Test]
    public void DismissalsBySurname()
    {
        Assert.AreEqual("c Khan b Lee", BattingCard.CompactHowOut("c Arjun Khan b Ravi Lee"));
        Assert.AreEqual("st Rao b Iyer", BattingCard.CompactHowOut("st Dev Rao b Om Iyer"));
        Assert.AreEqual("lbw b Lee", BattingCard.CompactHowOut("lbw b Ravi Lee"));
        Assert.AreEqual("c & b Lee", BattingCard.CompactHowOut("c & b Ravi Lee"));
        Assert.AreEqual("b Lee", BattingCard.CompactHowOut("b Ravi Lee"));
        Assert.AreEqual("run out", BattingCard.CompactHowOut("run out"));
        Assert.AreEqual("c wk b Bumrah", BattingCard.CompactHowOut("c wk b Bumrah"), "HUD's forms keep their shape");
        Assert.AreEqual("c Point b Bumrah", BattingCard.CompactHowOut("c Point b Bumrah"));
        Assert.AreEqual("c b Bumrah", BattingCard.CompactHowOut("c b Bumrah"), "an unnamed catcher is not the bowler");
        Assert.AreEqual("retired out", BattingCard.CompactHowOut("retired out"));
        Assert.AreEqual(BattingCard.Out, BattingCard.CompactHowOut(null));
        Assert.AreEqual(BattingCard.Out, BattingCard.CompactHowOut("  "));
    }

    [Test]
    public void ALongDismissalIsCut()
    {
        List<Batsman> side = Side("A", "B", "C");
        var outs = new Dictionary<Batsman, string> { { side[0], "c Wolstenholme-Fairweather b Christodoulopoulos" } };
        BattingCard card = BattingCard.Build("X", side, side[1], side[2], outs, 0, 1, 0, 1);
        Assert.LessOrEqual(card.Rows[0].HowOut.Length, BattingCard.HowChars);
        Assert.IsTrue(card.Rows[0].HowOut.EndsWith("..."));
    }

    [Test]
    public void EveryLineIsLatin1()
    {
        List<Batsman> side = Side("Zo\u00EB \u0141ukasz", "B");
        var outs = new Dictionary<Batsman, string> { { side[0], "c \u0160imon b Ng\u0169y\u1EC5n" } };
        BattingCard card = BattingCard.Build("\u201CNorth\u201D \u2013 CC", side, side[1], null, outs, 3, 1, 0, 2);
        foreach (char c in card.ToString())
            Assert.LessOrEqual(c, '\u00FF', $"'{c}' (U+{(int)c:X4}) is not Latin-1:\n{card}");
    }

    [Test]
    public void TheSideIsNamed()
    {
        Assert.AreEqual("Westfield CC", BattingCard.TeamName("Westfield CC", "INDIA"));
        Assert.AreEqual("INDIA", BattingCard.TeamName("", "INDIA"));
        Assert.AreEqual("Batting", BattingCard.TeamName(null, null));
    }

    [Test]
    public void ElevenFitUnderSevenHundredPixels()
    {
        Assert.AreEqual(11, ScorecardPanelBuilder.Rows);
        Assert.Less(ScorecardPanelBuilder.Height(), 700f);
        Assert.GreaterOrEqual(ScorecardPanelBuilder.RowFont, 22f, "as big as the other panels' lines");
    }

    [Test]
    public void NullsAreSafe()
    {
        BattingCard card = BattingCard.Build(null, null, null, null, null, 0, 0, 0, 0);
        Assert.AreEqual(0, card.Rows.Count);
        Assert.AreEqual("", card.Team);
        var side = new List<Batsman> { null, new Batsman("A") };
        Assert.AreEqual(1, BattingCard.Build("X", side, side[1], null, null, 0, 0, 0, 0).Rows.Count);
    }
}
