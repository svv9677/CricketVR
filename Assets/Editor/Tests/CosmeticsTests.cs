using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// The locker (phase 7): the catalogue's shape, earning from saved facts only (so relegation keeps
/// kit and an old save gets its kit back), one message a batch, equipping only what is his, and
/// every line Latin-1. Cosmetic only: none of this reaches the batting.
/// </summary>
public class CosmeticsTests
{
    private static void AssertLatin1(string s)
    {
        foreach (char c in s ?? "")
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not Latin-1 in \"{s}\"");
        StringAssert.DoesNotContain("—", s ?? "", "no em dash");
    }

    private static List<string> Ids(List<CosmeticDef> l) => l.ConvertAll(d => d.id);

    private static int LockerMessages(CareerProfile p) => p.inbox.FindAll(m => m.kind == eMessageKind.Locker).Count;

    [Test]
    public void TheCatalogue_NineteenItems_UniqueIds_OneStartPerSlot()
    {
        Assert.AreEqual(19, CosmeticCatalog.All.Length);
        var ids = new HashSet<string>();
        foreach (CosmeticDef d in CosmeticCatalog.All)
        {
            Assert.IsTrue(ids.Add(d.id), $"{d.id} twice");
            Assert.IsNotEmpty(d.name);
            if (d.slot == eCosmeticSlot.Bat && !d.IsDefault)
                Assert.AreEqual(BatSubmesh.Count, d.bat.Length, d.id);
            if (d.slot != eCosmeticSlot.Bat)
                Assert.IsNull(d.bat, d.id);
        }
        Assert.AreEqual(7, Cosmetics.Items(eCosmeticSlot.Bat).Count);
        Assert.AreEqual(6, Cosmetics.Items(eCosmeticSlot.Gloves).Count);
        Assert.AreEqual(6, Cosmetics.Items(eCosmeticSlot.Trim).Count);
        foreach (eCosmeticSlot s in new[] { eCosmeticSlot.Bat, eCosmeticSlot.Gloves, eCosmeticSlot.Trim })
        {
            Assert.AreEqual(1, Cosmetics.Items(s).FindAll(d => d.rule == eUnlock.Start).Count, s.ToString());
            Assert.AreEqual(s, CosmeticCatalog.Default(s).slot);
            Assert.AreEqual(0, Cosmetics.IndexOf(CosmeticCatalog.Default(s)), "the default comes first");
        }
        Assert.AreEqual("bat.classic", CosmeticCatalog.Default(eCosmeticSlot.Bat).id);
        Assert.AreEqual("gloves.standard", CosmeticCatalog.Default(eCosmeticSlot.Gloves).id);
        Assert.AreEqual("trim.silver", CosmeticCatalog.Default(eCosmeticSlot.Trim).id);
        Assert.IsNull(CosmeticCatalog.Default(eCosmeticSlot.Bat).bat, "Classic leaves the bat alone");
        Assert.IsNull(CosmeticCatalog.For("bat.nope"));
        Assert.IsNull(CosmeticCatalog.For(null));
    }

    [Test]
    public void EveryNameAndHowToUnlockIsLatin1()
    {
        foreach (CosmeticDef d in CosmeticCatalog.All)
        {
            AssertLatin1(d.name);
            AssertLatin1(CosmeticCatalog.FullName(d));
            Assert.IsNotEmpty(CosmeticCatalog.HowToUnlock(d), d.id);
            AssertLatin1(CosmeticCatalog.HowToUnlock(d));
            AssertLatin1(CareerLockerScreen.StatusLine(new CareerProfile(), d));
            AssertLatin1(CareerLockerScreen.NameLine(d, false));
        }
        Assert.AreEqual("Reach the Pro League", CosmeticCatalog.HowToUnlock(CosmeticCatalog.For("bat.pro")));
        Assert.AreEqual("Score your first fifty", CosmeticCatalog.HowToUnlock(CosmeticCatalog.For("gloves.white")));
        Assert.AreEqual("Score 1,000 career runs", CosmeticCatalog.HowToUnlock(CosmeticCatalog.For("gloves.gold")));
    }

    [Test]
    public void ANewCareerHasOnlyTheDefaults()
    {
        var p = new CareerProfile();
        Assert.IsEmpty(Cosmetics.Grant(p));
        Assert.AreEqual(0, p.unlocked.Count, "the defaults are never listed");
        Assert.AreEqual(0, p.inbox.Count);
        Assert.AreEqual(3, Cosmetics.UnlockedCount(p));
        foreach (CosmeticDef d in CosmeticCatalog.All)
            Assert.AreEqual(d.IsDefault, Cosmetics.Unlocked(p, d), d.id);
    }

    [Test]
    public void TheFirstFiftyEarnsWhiteGloves_AHundredCountsToo()
    {
        var p = new CareerProfile();
        p.stats.fifties = 1;
        CollectionAssert.AreEquivalent(new[] { "gloves.white" }, Ids(Cosmetics.Grant(p)));
        Assert.Contains("gloves.white", p.unlocked);
        Assert.AreEqual(1, LockerMessages(p));
        StringAssert.StartsWith("White gloves is in your Locker now", p.inbox[0].body);

        var ton = new CareerProfile();
        ton.stats.hundreds = 1;
        CollectionAssert.AreEquivalent(new[] { "gloves.white", "bat.ton" }, Ids(Cosmetics.Grant(ton)));
    }

    [Test]
    public void ARungPlayedOnlyInTheRecordBookStillCounts()
    {
        var p = new CareerProfile { rung = eRung.Club };   // relegated since
        p.innings.Add(new InningsRecord { rung = eRung.ProLeague });
        Assert.AreEqual(eRung.ProLeague, Cosmetics.HighestRung(p));
        List<string> got = Ids(Cosmetics.Grant(p));
        CollectionAssert.AreEquivalent(new[] { "bat.state", "bat.pro", "trim.side" }, got);
        Assert.IsFalse(got.Contains("trim.gold"), "International not reached");
    }

    [Test]
    public void GrantIsIdempotent_OneMessageABatch()
    {
        var p = new CareerProfile();
        p.stats.matches = 10;
        p.stats.fifties = 2;
        p.stats.sixes = 25;
        List<CosmeticDef> first = Cosmetics.Grant(p);
        CollectionAssert.AreEquivalent(new[] { "bat.ember", "gloves.black", "gloves.white", "trim.red" }, Ids(first));
        Assert.AreEqual(1, LockerMessages(p), "one message for the batch");
        Assert.IsEmpty(Cosmetics.Grant(p));
        Assert.AreEqual(1, LockerMessages(p));
        Assert.AreEqual(4, p.unlocked.Count);
    }

    [Test]
    public void EachRuleReadsItsFact()
    {
        var p = new CareerProfile();
        void Has(string id, bool want) => Assert.AreEqual(want, Cosmetics.Qualifies(p, CosmeticCatalog.For(id)), id);
        Has("bat.cap", false); p.caps = 1; Has("bat.cap", true);
        Has("bat.coach", false); p.goalsDone = 3; Has("bat.coach", true); Has("gloves.side", true); Has("trim.sky", false);
        p.goalsDone = 6; Has("trim.sky", true);
        Has("trim.white", false); p.seasonsPlayed = 1; Has("trim.white", true);
        Has("gloves.gold", false); p.stats.runs = 999; Has("gloves.gold", false); p.stats.runs = 1000; Has("gloves.gold", true);
        Has("gloves.lime", false); p.drills.For(eDrill.Spin).best = 79; Has("gloves.lime", false);
        p.drills.For(eDrill.Gaps).best = 80; Has("gloves.lime", true);
        Has("trim.gold", false); p.rung = eRung.International; Has("trim.gold", true);
        Assert.IsFalse(Cosmetics.Qualifies(null, CosmeticCatalog.For("bat.ember")));
        Assert.IsFalse(Cosmetics.Qualifies(p, null));
    }

    [Test]
    public void EquipOnlyWhatIsHis_ABadIdIsTheDefault()
    {
        var p = new CareerProfile();
        Assert.IsFalse(Cosmetics.Equip(p, "bat.pro"), "locked");
        Assert.AreEqual("", p.batFinish);
        Assert.IsFalse(Cosmetics.Equip(p, "bat.nope"));
        p.unlocked.Add("bat.pro");
        Assert.IsTrue(Cosmetics.Equip(p, "bat.pro"));
        Assert.AreEqual("bat.pro", p.batFinish);
        Assert.AreEqual("bat.pro", Cosmetics.Equipped(p, eCosmeticSlot.Bat).id);
        Assert.IsTrue(Cosmetics.Equip(p, "bat.classic"));
        Assert.AreEqual("", p.batFinish, "the default is saved as nothing");

        p.batFinish = "bat.nope";
        Assert.AreEqual("bat.classic", Cosmetics.Equipped(p, eCosmeticSlot.Bat).id);
        p.batFinish = "gloves.white";
        Assert.AreEqual("bat.classic", Cosmetics.Equipped(p, eCosmeticSlot.Bat).id, "another slot's id");
        p.gloves = "gloves.gold";
        Assert.AreEqual("gloves.standard", Cosmetics.Equipped(p, eCosmeticSlot.Gloves).id, "not his");
        p.kitTrim = null;
        Assert.AreEqual("trim.silver", Cosmetics.Equipped(p, eCosmeticSlot.Trim).id);
        Assert.AreEqual("trim.silver", Cosmetics.Equipped(null, eCosmeticSlot.Trim).id);
    }

    [Test]
    public void SteppingWraps()
    {
        Assert.AreEqual(6, Cosmetics.Step(eCosmeticSlot.Bat, 0, -1));
        Assert.AreEqual(0, Cosmetics.Step(eCosmeticSlot.Bat, 6, 1));
        Assert.AreEqual(3, Cosmetics.Step(eCosmeticSlot.Gloves, 2, 1));
        Assert.AreEqual(5, Cosmetics.Step(eCosmeticSlot.Trim, 0, -1));
    }

    [Test]
    public void TheNewsLineNamesTheKit()
    {
        Assert.AreEqual("", Cosmetics.NewsLine(null));
        Assert.AreEqual("", Cosmetics.NewsLine(new List<CosmeticDef>()));
        var l = new List<CosmeticDef> { CosmeticCatalog.For("bat.pro"), CosmeticCatalog.For("gloves.white") };
        StringAssert.StartsWith("Pro gold bat and White gloves are in your Locker now", Cosmetics.NewsLine(l));
        l.Add(CosmeticCatalog.For("trim.gold"));
        string three = Cosmetics.NewsLine(l);
        StringAssert.StartsWith("Pro gold bat, White gloves and Gold helmet trim are", three);
        AssertLatin1(three);
    }

    [Test]
    public void TheLockerScreensLines()
    {
        var p = new CareerProfile();
        CosmeticDef pro = CosmeticCatalog.For("bat.pro");
        Assert.AreEqual("Locked: Pro gold bat - reach the Pro League. 3 of 19 unlocked.", CareerLockerScreen.StatusLine(p, pro));
        StringAssert.StartsWith("3 of 19 unlocked.", CareerLockerScreen.StatusLine(p, null));
        Assert.AreEqual("Pro gold", CareerLockerScreen.NameLine(pro, true));
        Assert.AreEqual("Pro gold  · locked", Regex.Replace(CareerLockerScreen.NameLine(pro, false), "<[^>]*>", ""));
        Assert.AreEqual(CareerLockerScreen.LockedAlpha, CareerLockerScreen.Swatch(pro, null, false).a);
        Assert.AreEqual(1f, CareerLockerScreen.Swatch(pro, null, true).a);
    }

    [Test]
    public void UpkeepGrantsKitEarnedByGoals()
    {
        CareerProfile p = CareerTestPlay.Career(30);
        p.goalsDone = 1;
        Assert.IsTrue(CareerUpkeep.Run(p));
        Assert.Contains("gloves.side", p.unlocked);
        Assert.IsFalse(CareerUpkeep.Run(p), "nothing new the second time");
    }
}
