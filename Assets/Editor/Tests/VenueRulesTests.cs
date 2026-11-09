using NUnit.Framework;
using UnityEngine;

/// <summary>
/// VenueRules and MatchVenue: a career match goes where its plan says and a Quick Match where the
/// player set; the stadium sounds exactly as it did before the club ground existed, whatever the
/// crowd; the club claps quieter the emptier it is, never says "ooh", knocks on a fence only when
/// the fence clips exist; and the UI's ground order maps both ways. Standalone.
/// </summary>
public class VenueRulesTests
{
    private static readonly eCrowd[] Crowds = { eCrowd.Packed, eCrowd.Half, eCrowd.Empty };

    [TearDown]
    public void Restore() => MatchVenue.Set(eVenue.Stadium);

    [Test]
    public void CareerUsesThePlanQuickMatchUsesTheSetting()
    {
        Assert.AreEqual(eVenue.ClubGround, VenueRules.Resolve(true, eVenue.ClubGround, eVenue.Stadium));
        Assert.AreEqual(eVenue.Stadium, VenueRules.Resolve(true, eVenue.Stadium, eVenue.ClubGround));
        Assert.AreEqual(eVenue.Stadium, VenueRules.Resolve(false, eVenue.ClubGround, eVenue.Stadium));
        Assert.AreEqual(eVenue.ClubGround, VenueRules.Resolve(false, eVenue.Stadium, eVenue.ClubGround));
    }

    [Test]
    public void TheStadiumSoundsAsItAlwaysDidWithACrowd()
    {
        foreach (eCrowd c in Crowds)
        {
            if (c == eCrowd.Empty) continue;
            AssertReaction(VenueRules.ForSignal(eVenue.Stadium, c, UmpireSignal.Four), "crowd_four", 0.75f);
            AssertReaction(VenueRules.ForSignal(eVenue.Stadium, c, UmpireSignal.Six), "crowd_six", 0.95f);
            AssertReaction(VenueRules.ForSignal(eVenue.Stadium, c, UmpireSignal.Out), "crowd_wicket", 0.95f);
        }
        Assert.AreEqual(0.6f, VenueRules.OohVolume(eVenue.Stadium), 1e-6f);
        Assert.AreEqual("boards", VenueRules.BoardsGroup(eVenue.Stadium, g => true));
    }

    [Test]
    public void TheClubClapsScaledByTheCrowd()
    {
        AssertReaction(VenueRules.ForSignal(eVenue.ClubGround, eCrowd.Packed, UmpireSignal.Four), "club_clap", 0.5f);
        AssertReaction(VenueRules.ForSignal(eVenue.ClubGround, eCrowd.Packed, UmpireSignal.Six), "club_cheer", 0.7f);
        AssertReaction(VenueRules.ForSignal(eVenue.ClubGround, eCrowd.Packed, UmpireSignal.Out), "club_clap", 0.6f);
        AssertReaction(VenueRules.ForSignal(eVenue.ClubGround, eCrowd.Half, UmpireSignal.Six), "club_cheer", 0.7f * 0.85f);
        Assert.AreEqual(0.6f * 0.85f, VenueRules.ForSignal(eVenue.ClubGround, eCrowd.Half, UmpireSignal.Out).volume, 1e-6f);
        Assert.AreEqual(0f, VenueRules.OohVolume(eVenue.ClubGround), "no ooh from a few people on benches");
    }

    [Test]
    public void NoCrowdNoNoise()
    {
        foreach (eVenue v in new[] { eVenue.Stadium, eVenue.ClubGround })
            foreach (UmpireSignal s in new[] { UmpireSignal.Four, UmpireSignal.Six, UmpireSignal.Out })
                Assert.IsNull(VenueRules.ForSignal(v, eCrowd.Empty, s).group, $"{v} {s}");
        Assert.AreEqual(0f, VenueRules.OohVolume(eVenue.Stadium, eCrowd.Empty));
    }

    [Test]
    public void OtherSignalsAreSilentAtBothGrounds()
    {
        UmpireSignal[] quiet = { UmpireSignal.None, UmpireSignal.Wide, UmpireSignal.NoBall, UmpireSignal.Bye,
                                 UmpireSignal.LegBye, UmpireSignal.FreeHit };
        foreach (eVenue v in new[] { eVenue.Stadium, eVenue.ClubGround })
            foreach (eCrowd c in Crowds)
                foreach (UmpireSignal s in quiet)
                    Assert.IsNull(VenueRules.ForSignal(v, c, s).group, $"{v} {c} {s}");
    }

    [Test]
    public void TheFenceKnocksOnlyAtTheClubAndOnlyIfItsClipsExist()
    {
        bool asked = false;
        Assert.AreEqual("boards", VenueRules.BoardsGroup(eVenue.Stadium, g => { asked = true; return true; }));
        Assert.IsFalse(asked, "the stadium never asks GameSfx (it would load every clip)");
        Assert.AreEqual("fence", VenueRules.BoardsGroup(eVenue.ClubGround, g => g == "fence"));
        Assert.AreEqual("boards", VenueRules.BoardsGroup(eVenue.ClubGround, g => false));
        Assert.AreEqual("boards", VenueRules.BoardsGroup(eVenue.ClubGround, null));
    }

    [Test]
    public void ClubCrowdNeverSwellsAndFillsWithTheSetting()
    {
        AssertCrowd(VenueRules.ClubCrowd(eCrowd.Empty), 0.30f);
        AssertCrowd(VenueRules.ClubCrowd(eCrowd.Half), 0.32f);
        AssertCrowd(VenueRules.ClubCrowd(eCrowd.Packed), 0.35f);
        Assert.AreEqual(1, VenueRules.SpectatorGroups(eCrowd.Empty));
        Assert.AreEqual(2, VenueRules.SpectatorGroups(eCrowd.Half));
        Assert.AreEqual(3, VenueRules.SpectatorGroups(eCrowd.Packed));
    }

    [Test]
    public void GlowsAndNames()
    {
        Assert.AreEqual(new Vector2(13f, 5.5f), VenueRules.GlowSize(eVenue.Stadium));
        Assert.AreEqual(new Vector2(4f, 2.5f), VenueRules.GlowSize(eVenue.ClubGround));
        Assert.AreEqual("Stadium", VenueRules.Name(eVenue.Stadium));
        Assert.AreEqual("Club ground", VenueRules.Name(eVenue.ClubGround));
        Assert.AreEqual("Ashbourne", VenueRules.ClubName(true, "Ashbourne"));
        Assert.AreEqual("Ashbourne", VenueRules.ClubName(true, "  Ashbourne "));
        Assert.AreEqual("Cricket Club", VenueRules.ClubName(true, ""));
        Assert.AreEqual("Cricket Club", VenueRules.ClubName(true, null));
        Assert.AreEqual("Cricket Club", VenueRules.ClubName(false, "Ashbourne"), "a Quick Match is nobody's club");
    }

    [Test]
    public void QuickIndexListsStadiumFirstAndRoundTrips()
    {
        Assert.AreEqual(0, VenueRules.QuickIndex(eVenue.Stadium));
        Assert.AreEqual(1, VenueRules.QuickIndex(eVenue.ClubGround));
        foreach (eVenue v in new[] { eVenue.Stadium, eVenue.ClubGround })
            Assert.AreEqual(v, VenueRules.FromQuickIndex(VenueRules.QuickIndex(v)));
        Assert.AreEqual(eVenue.Stadium, VenueRules.FromQuickIndex(-1));
        Assert.AreEqual(eVenue.Stadium, VenueRules.FromQuickIndex(7));
    }

    [Test]
    public void MatchVenueIsTheStadiumUntilSet()
    {
        MatchVenue.Set(eVenue.ClubGround);
        Assert.IsTrue(MatchVenue.IsClub);
        Assert.AreEqual(eVenue.ClubGround, MatchVenue.Current);
        MatchVenue.Set(eVenue.Stadium);
        Assert.IsFalse(MatchVenue.IsClub);
    }

    private static void AssertReaction(VenueRules.Reaction r, string group, float volume)
    {
        Assert.AreEqual(group, r.group);
        Assert.AreEqual(volume, r.volume, 1e-5f, group);
    }

    private static void AssertCrowd(VenueRules.CrowdAudio a, float v)
    {
        Assert.AreEqual(v, a.ambience, 1e-6f);
        Assert.AreEqual(a.ambience, a.cheer, 1e-6f, "cheer == ambience: the club never surges");
    }
}
