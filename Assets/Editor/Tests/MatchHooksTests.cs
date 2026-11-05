using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The hooks a career match runs on top of the normal match, the parts that need no scene: the
/// gate on the next ball, MatchFlow's overrides and their clearing, the career fields on a batter,
/// a simulated ball's no-ball state, and HUD's view of the player and of the innings being over.
/// </summary>
public class MatchHooksTests
{
    private GameObject go;

    [SetUp]
    public void Fresh()
    {
        NoBalls.NewInnings();
        NoBalls.ForceNext = false;
        MatchGate.Reset();
        MatchFlow.ClearOverrides();
    }

    [TearDown]
    public void CleanUp()
    {
        NoBalls.NewInnings();
        NoBalls.ForceNext = false;
        MatchGate.Reset();
        MatchFlow.ClearOverrides();
        if (go != null)
            Object.DestroyImmediate(go);
    }

    [Test]
    public void TheGateOpensOnReset()
    {
        MatchGate.Blocking = true;
        MatchGate.Reset();
        Assert.IsFalse(MatchGate.Blocking);
    }

    [Test]
    public void ClearOverridesPutsANormalMatchBack()
    {
        MatchFlow.AIInningsOverride = (overs, target, level, rng) => new AIInnings.Result();
        MatchFlow.KeepHistory = false;
        MatchFlow.CardNote = "Career: Club League";
        MatchFlow.Ended += OnEnded;
        Assert.IsNotNull(EndedListeners(), "the listener is on");

        MatchFlow.ClearOverrides();

        Assert.IsNull(MatchFlow.AIInningsOverride);
        Assert.IsTrue(MatchFlow.KeepHistory);
        Assert.IsNull(MatchFlow.CardNote);
        Assert.IsNull(EndedListeners(), "no listener left on Ended");
    }

    private static void OnEnded(MatchHistory.Record r, AIInnings.Result ai, string result) { }

    /// The compiler's backing field of the Ended event (same name, private).
    private static object EndedListeners() =>
        typeof(MatchFlow).GetField("Ended", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

    [Test]
    public void ANormalMatchKeepsItsHistory()
    {
        // ReplayOverlay.Team is not checked: a career run in an unreloaded domain may have left it set.
        Assert.IsTrue(MatchFlow.KeepHistory);
    }

    [Test]
    public void ABatterIsNotThePlayerUnlessTheCareerSaysSo()
    {
        var b = new Batsman("Opener");
        Assert.IsFalse(b.IsHuman);
        Assert.AreEqual(40f, b.Rating);
        Assert.AreEqual(0, b.Runs);
    }

    [Test]
    public void ASimulatedBallIsNeverANoBallOrAFreeHit()
    {
        // A no-ball called: the next ball would be a free hit.
        NoBalls.NewDelivery(() => 0f);
        NoBalls.Released();
        NoBalls.Scored(true);
        Assert.IsTrue(NoBalls.NextIsFreeHit);

        HUD.ReadyNoBallsForSimulatedBall();
        Assert.IsFalse(NoBalls.Called);
        Assert.IsFalse(NoBalls.Protected, "a wicket off a simulated ball is a wicket");
        Assert.IsFalse(NoBalls.NextIsFreeHit);
        NoBalls.Scored(false);

        // The next real delivery is an ordinary one.
        NoBalls.NewDelivery(() => 1f);
        Assert.IsFalse(NoBalls.FreeHit);
        Assert.IsFalse(NoBalls.Overstep);
    }

    [Test]
    public void ASimulatedBallLeavesAForcedNoBallForTheNextRealOne()
    {
        NoBalls.ForceNext = true;
        HUD.ReadyNoBallsForSimulatedBall();
        Assert.IsFalse(NoBalls.Overstep, "not this ball");
        Assert.IsTrue(NoBalls.ForceNext);
        NoBalls.Scored(false);
        NoBalls.NewDelivery(() => 1f);
        Assert.IsTrue(NoBalls.Overstep, "the real one");
    }

    [Test]
    public void TheHumanIsTheFlaggedBatter()
    {
        HUD hud = NewHud();
        Assert.IsNull(hud.Human, "nobody in a normal match");
        hud.Batsmen[4].IsHuman = true;
        Assert.AreSame(hud.Batsmen[4], hud.Human);
    }

    [Test]
    public void TheInningsIsOverAllOutOrWhenTheOversAreBowled()
    {
        HUD hud = NewHud();
        hud.TotalOvers = 6;
        hud.Overs = 5; hud.Balls = 5;
        Assert.IsFalse(hud.InningsOver);
        hud.Overs = 6; hud.Balls = 0;
        Assert.IsTrue(hud.InningsOver, "overs bowled");
        hud.Overs = 2; hud.Wickets = 10;
        Assert.IsTrue(hud.InningsOver, "all out");
        hud.Wickets = 9;
        Assert.IsFalse(hud.InningsOver);
    }

    /// A HUD with a scorebook and no scene (no Start, so no Reset and no UI).
    private HUD NewHud()
    {
        go = new GameObject("HUD (test)");
        HUD hud = go.AddComponent<HUD>();
        hud.Batsmen = new List<Batsman>();
        for (int i = 0; i < 11; i++)
            hud.Batsmen.Add(new Batsman("B" + i));
        hud.CurrentBatsman = hud.Batsmen[0];
        hud.CurrentRunner = hud.Batsmen[1];
        hud.NextBatsmanIndex = 2;
        return hud;
    }
}
