using NUnit.Framework;

/// <summary>
/// The floating-panel career UI's pure parts: the tabs (every tab a real career screen, Home the
/// hub), the hub's big tiles and news panel, and the training screen's tiles and chosen-drill lines.
/// </summary>
public class CareerDeckTests
{
    [Test]
    public void EveryTabIsACareerScreenAndHomeIsTheHub()
    {
        Assert.AreEqual(CareerDeckScreen.Hub, CareerDeckScreen.TabOf("careerHub"));
        foreach (string s in new[] { "careerDrills", "careerSeason", "careerRatings", "careerInbox", "careerGoals", "careerLocker" })
            Assert.AreEqual(s, CareerDeckScreen.TabOf(s));
        Assert.IsNull(CareerDeckScreen.TabOf("careerRecords"));
        Assert.IsNull(CareerDeckScreen.TabOf("main"));
    }

    [Test]
    public void TheHubTilesShowANumberOrDashes()
    {
        Assert.AreEqual("54", CareerHubScreen.Number(54.4f));
        Assert.AreEqual("--", CareerHubScreen.Number(RatingSet.Unknown));
        Assert.AreEqual("OVERALL", CareerHubScreen.OverallCaption(RatingSet.Unknown));
        StringAssert.StartsWith("OVERALL  ·  ", CareerHubScreen.OverallCaption(60f));
    }

    [Test]
    public void TheNewsPanelSaysWhatIsUnreadAndTheNewestTitles()
    {
        var p = new CareerProfile { name = "Asha" };
        Assert.That(CareerHubScreen.NewsText(p), Does.Contain("No new messages"));
        p.inbox.Add(new CareerMessage { title = "Old one", read = true });
        p.inbox.Add(new CareerMessage { title = "Middle" });
        p.inbox.Add(new CareerMessage { title = "Newest" });
        string news = CareerHubScreen.NewsText(p);
        StringAssert.Contains("2 unread", news);
        StringAssert.Contains("Newest", news);
        StringAssert.Contains("Middle", news);
        StringAssert.DoesNotContain("Old one", news, "only the newest two");
        Assert.AreEqual("", CareerHubScreen.NewsText(null));
    }

    [Test]
    public void TrainingTilesAndTheChosenDrill()
    {
        DrillDef yorkers = DrillDefs.For(eDrill.Yorkers);
        StringAssert.Contains("Yorkers", CareerDrillsScreen.TileName(yorkers, false));
        StringAssert.Contains("PICK", CareerDrillsScreen.TileName(yorkers, true));
        Assert.AreEqual("New", CareerDrillsScreen.TileDetail(null));
        Assert.AreEqual("Best 72", CareerDrillsScreen.TileDetail(new DrillRecord { attempts = 2, best = 72, last = 60 }));
        string stats = CareerDrillsScreen.ChosenStats(yorkers, new DrillRecord { attempts = 1, best = 50, last = 50 });
        StringAssert.Contains("Feeds", stats);
        StringAssert.Contains("1 attempt", stats);
        StringAssert.Contains("Not tried yet", CareerDrillsScreen.ChosenStats(yorkers, null));
    }
}
