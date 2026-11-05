using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The phase 4 screens' wording, which needs no scene: the inbox's pages, rows, tags and dates, a
/// message's header and choices, the hub's inbox, offer, scout and left-out lines, and the result
/// screen's scout line - and that every string stays in Latin-1, the only characters the TMP
/// default font has. Standalone; nothing here reads or writes a save.
/// Run: Window > General > Test Runner > EditMode > InboxScreensTests.
/// </summary>
public class InboxScreensTests
{
    /// The text a player sees: rich-text tags taken out.
    private static string Plain(string rich) => Regex.Replace(rich ?? "", "<[^>]*>", "");

    private static void AssertLatin1(string s)
    {
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
    }

    private static CareerMessage Offer(params string[] actions)
    {
        var m = new CareerMessage { kind = eMessageKind.Offer, from = "Scout, Pro League", title = "An offer", date = "2026-11-05" };
        foreach (string a in actions)
            m.options.Add(new MessageOption { label = a, action = a });
        return m;
    }

    // ---- Inbox --------------------------------------------------------------------------------------

    [Test]
    public void PagesHoldFiveAndAnEmptyInboxIsOnePage()
    {
        Assert.AreEqual(5, InboxScreen.PerPage);
        Assert.AreEqual(1, InboxScreen.Pages(0));
        Assert.AreEqual(1, InboxScreen.Pages(5));
        Assert.AreEqual(2, InboxScreen.Pages(6));
        Assert.AreEqual(8, InboxScreen.Pages(CareerProfile.MaxMessages));
        Assert.AreEqual("Page 1 of 3", InboxScreen.PageText(0, 3));
        Assert.AreEqual("Page 1 of 1", InboxScreen.PageText(0, 0));
    }

    [Test]
    public void RowsAreNewestFirstAndStopAtTheEnd()
    {
        // 7 messages, newest last in the list: page 1 is 6..2, page 2 is 1, 0, then nothing.
        Assert.AreEqual(6, InboxScreen.IndexAt(7, 0, 0));
        Assert.AreEqual(2, InboxScreen.IndexAt(7, 0, 4));
        Assert.AreEqual(1, InboxScreen.IndexAt(7, 1, 0));
        Assert.AreEqual(0, InboxScreen.IndexAt(7, 1, 1));
        Assert.AreEqual(-1, InboxScreen.IndexAt(7, 1, 2));
        Assert.AreEqual(-1, InboxScreen.IndexAt(0, 0, 0));
        Assert.AreEqual(-1, InboxScreen.IndexAt(7, 0, InboxScreen.PerPage));
        Assert.AreEqual(-1, InboxScreen.IndexAt(7, -1, 0));
    }

    [Test]
    public void EachKindHasItsTag()
    {
        Assert.AreEqual("OFFER", InboxScreen.Tag(eMessageKind.Offer));
        Assert.AreEqual("SELECTION", InboxScreen.Tag(eMessageKind.Selection));
        Assert.AreEqual("SCOUT", InboxScreen.Tag(eMessageKind.Scout));
        Assert.AreEqual("COACH", InboxScreen.Tag(eMessageKind.Coach));
        Assert.AreEqual("MILESTONE", InboxScreen.Tag(eMessageKind.Milestone));
        Assert.AreEqual("NEWS", InboxScreen.Tag(eMessageKind.Info));
    }

    [Test]
    public void ARowIsTheTagThenTheTitleBoldWhileUnread()
    {
        var m = new CareerMessage { kind = eMessageKind.Scout, from = "Scout, State", title = "A scout's note" };
        Assert.AreEqual("SCOUTA scout's note", Plain(InboxScreen.RowText(m)));
        StringAssert.Contains("<b>A scout's note</b>", InboxScreen.RowText(m));
        m.read = true;
        StringAssert.DoesNotContain("<b>A scout's note</b>", InboxScreen.RowText(m));
        m.title = "";
        Assert.AreEqual("SCOUTScout, State", Plain(InboxScreen.RowText(m)), "no title: who it is from");
        Assert.AreEqual("", InboxScreen.RowText(null));
    }

    [Test]
    public void DatesReadAsDayMonthYear()
    {
        Assert.AreEqual("5 Nov 2026", InboxScreen.DateText("2026-11-05"));
        Assert.AreEqual("yesterday", InboxScreen.DateText("yesterday"));
        Assert.AreEqual("", InboxScreen.DateText(""));
        Assert.AreEqual("", InboxScreen.DateText(null));
    }

    [Test]
    public void TheSubtitleCountsUnreadAndAll()
    {
        Assert.AreEqual("2 unread  ·  14 messages", InboxScreen.Subtitle(2, 14));
        Assert.AreEqual("1 message", InboxScreen.Subtitle(0, 1));
        StringAssert.Contains("write to you here", InboxScreen.Subtitle(0, 0));
    }

    // ---- Message ------------------------------------------------------------------------------------

    [Test]
    public void TheMessageHeaderSaysAnsweredOnlyForAChoiceMade()
    {
        CareerMessage m = Offer("join:2:Harbour Kings", "stay");
        Assert.AreEqual("OFFER  ·  5 Nov 2026", Plain(MessageScreen.MetaLine(m)));
        m.answered = true;
        Assert.AreEqual("OFFER  ·  5 Nov 2026  ·  Answered", Plain(MessageScreen.MetaLine(m)));
        var note = new CareerMessage { kind = eMessageKind.Coach, answered = true };
        Assert.IsFalse(MessageScreen.Answered(note), "a note with no choices is only read");
        Assert.AreEqual("COACH", Plain(MessageScreen.MetaLine(note)));
    }

    [Test]
    public void AnOptionWithNoLabelIsOk()
    {
        Assert.AreEqual("OK", MessageScreen.OptionLabel(new MessageOption { action = "ok" }));
        Assert.AreEqual("OK", MessageScreen.OptionLabel(null));
        Assert.AreEqual("Join Harbour Kings", MessageScreen.OptionLabel(new MessageOption { label = "Join Harbour Kings" }));
    }

    [Test]
    public void AnOfferOfThreeSidesAndStayAllHaveAButton()
    {
        Assert.GreaterOrEqual(MessageScreen.MaxOptions, 4);
    }

    // ---- Hub ----------------------------------------------------------------------------------------

    [Test]
    public void TheInboxButtonCountsUnread()
    {
        Assert.AreEqual("Inbox (2)", InboxScreen.InboxLabel(2));
        Assert.AreEqual("Inbox", InboxScreen.InboxLabel(0));
    }

    [Test]
    public void TheOfferLineNamesTheSide()
    {
        Assert.AreEqual("An offer from Harbour Kings - open your inbox", Plain(InboxScreen.OfferLine(Offer("join:2:Harbour Kings", "stay"))));
        Assert.AreEqual("Offers from 2 sides - open your inbox",
                        Plain(InboxScreen.OfferLine(Offer("join:2:Harbour Kings", "join:2:Old Mill", "stay"))));
        Assert.AreEqual("An International call-up - open your inbox", Plain(InboxScreen.OfferLine(Offer("accept-callup", "stay"))));
        Assert.AreEqual("An offer - open your inbox", Plain(InboxScreen.OfferLine(Offer("ok"))));
        Assert.AreEqual("", InboxScreen.OfferLine(null));
    }

    [Test]
    public void AJoinActionGivesItsSide()
    {
        Assert.AreEqual("Harbour Kings", InboxScreen.JoinTeam("join:2:Harbour Kings"));
        Assert.AreEqual("Kings: North", InboxScreen.JoinTeam("join:1:Kings: North"), "a colon in the name stays in it");
        Assert.AreEqual("Harbour Kings", InboxScreen.JoinTeam("join:Harbour Kings:2"), "the name first is read too");
        Assert.IsNull(InboxScreen.JoinTeam("stay"));
        Assert.IsNull(InboxScreen.JoinTeam("join:2"));
        Assert.IsNull(InboxScreen.JoinTeam(null));
    }

    [Test]
    public void TheScoutIsFromTheRungAboveAndSelectorsWatchForTheNationalSide()
    {
        Assert.AreEqual("A State scout is watching", CareerResultScreen.ScoutWatching(eRung.Club));
        Assert.AreEqual("A Pro League scout is watching", CareerResultScreen.ScoutWatching(eRung.State));
        Assert.AreEqual("Selectors watching", CareerResultScreen.ScoutWatching(eRung.ProLeague));
        Assert.AreEqual("Selectors watching", CareerResultScreen.ScoutWatching(eRung.International));
        Assert.AreEqual("Pro League scout", CareerResultScreen.ScoutName(eRung.ProLeague));
        Assert.AreEqual("Selectors", CareerResultScreen.ScoutName(eRung.International));
    }

    [Test]
    public void LeftOutSaysSoAndHowTheMatchWent()
    {
        Assert.AreEqual("Left out of the side for this one", Plain(CareerHubScreen.LeftOutLine(1)));
        Assert.AreEqual("Left out of the side  ·  2 matches to sit out", Plain(CareerHubScreen.LeftOutLine(2)));
        var r = new InningsRecord { leftOut = true, opponent = "Old Mill CC", result = "Harbour CC won by 12 runs" };
        Assert.AreEqual("Left out: Harbour CC won by 12 runs", Plain(CareerHubScreen.LeftOutNote(r)));
        r.result = "";
        Assert.AreEqual("Left out: vs Old Mill CC", Plain(CareerHubScreen.LeftOutNote(r)));
        StringAssert.StartsWith("Left out:", Plain(CareerHubScreen.LeftOutNote(null)));
    }

    [Test]
    public void TheLastLeftOutIsOnlyTheNewestInnings()
    {
        var p = new CareerProfile();
        Assert.IsNull(CareerHubScreen.LastLeftOut(p));
        p.AddInnings(new InningsRecord { leftOut = true, opponent = "A" });
        Assert.AreEqual("A", CareerHubScreen.LastLeftOut(p).opponent);
        p.AddInnings(new InningsRecord { batted = true, opponent = "B" });
        Assert.IsNull(CareerHubScreen.LastLeftOut(p), "an innings he played since");
    }

    // ---- Result -------------------------------------------------------------------------------------

    [Test]
    public void TheResultsInningsIsTheMatchJustPlayed()
    {
        var p = new CareerProfile();
        var plan = new MatchPlan { opponentName = "Old Mill CC" };
        Assert.IsNull(CareerResultScreen.LastInnings(p, plan));
        p.AddInnings(new InningsRecord { opponent = "Old Mill CC", runs = 34 });
        Assert.AreEqual(34, CareerResultScreen.LastInnings(p, plan).runs);
        Assert.AreEqual(34, CareerResultScreen.LastInnings(p, null).runs);
        Assert.IsNull(CareerResultScreen.LastInnings(p, new MatchPlan { opponentName = "Riverside" }), "another match's");
        p.AddInnings(new InningsRecord { opponent = "Old Mill CC", leftOut = true });
        Assert.IsNull(CareerResultScreen.LastInnings(p, plan), "a match he sat out");
    }

    [Test]
    public void AScoutedInningsGetsTheScoutsReportSigned()
    {
        var r = new InningsRecord { rung = eRung.State, batted = true, runs = 40, balls = 30, scouted = true, impression = 0.6f };
        string line = Plain(CareerResultScreen.ScoutLine(new MatchPlan { scout = true, scoutFrom = eRung.ProLeague }, r));
        StringAssert.StartsWith("Pro League scout: ", line);
        Assert.Greater(line.Length, "Pro League scout: ".Length, "the report itself follows");
        r.rung = eRung.ProLeague;
        string selectors = Plain(CareerResultScreen.ScoutLine(null, r));
        Assert.IsNotEmpty(selectors);
        StringAssert.DoesNotStartWith("Selectors:", selectors, "the selectors' report names them itself");
        AssertLatin1(line);
        AssertLatin1(selectors);
    }

    [Test]
    public void NoScoutNoLine()
    {
        Assert.AreEqual("", CareerResultScreen.ScoutLine(null, null));
        Assert.AreEqual("", CareerResultScreen.ScoutLine(new MatchPlan(), new InningsRecord { batted = true, runs = 40 }));
        Assert.AreEqual("", CareerResultScreen.ScoutLine(null, new InningsRecord { scouted = true, leftOut = true }));
    }

    // ---- Every string -------------------------------------------------------------------------------

    [Test]
    public void EveryLineIsLatin1()
    {
        CareerMessage offer = Offer("join:2:Harbour Kings", "stay");
        var lines = new List<string>
        {
            InboxScreen.PageText(1, 3), InboxScreen.RowText(offer), InboxScreen.DateText("2026-11-05"),
            InboxScreen.Subtitle(2, 14), InboxScreen.Subtitle(0, 0), InboxScreen.InboxLabel(3),
            InboxScreen.OfferLine(offer), InboxScreen.OfferLine(Offer("accept-callup")),
            MessageScreen.MetaLine(offer), MessageScreen.OptionLabel(null),
            CareerHubScreen.LeftOutLine(2), CareerHubScreen.LeftOutLine(1), CareerHubScreen.LeftOutNote(null),
            CareerHubScreen.LeftOutNote(new InningsRecord { result = "Harbour CC won by 12 runs" }),
        };
        foreach (eMessageKind k in System.Enum.GetValues(typeof(eMessageKind)))
            lines.Add(InboxScreen.RowText(new CareerMessage { kind = k, title = "Title" }));
        foreach (eRung r in System.Enum.GetValues(typeof(eRung)))
        {
            lines.Add(CareerResultScreen.ScoutWatching(r));
            lines.Add(CareerResultScreen.ScoutName(r));
        }
        foreach (string line in lines)
            AssertLatin1(line);
    }
}
