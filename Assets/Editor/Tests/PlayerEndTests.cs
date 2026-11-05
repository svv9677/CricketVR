using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The player's end while he watches his partner bat (phase 3), without a scene: PlayerEnd's
/// change event and its reset on every scene load, a moved panel's saved pose carried between the
/// ends (GrabbablePanel), the blink's timing and when the partner's ball is bowled (NonStrikerView),
/// and the next-ball card's heading. Quaternions are written out by hand, so the pure parts also run
/// outside Unity.
/// Run: Window > General > Test Runner > EditMode > PlayerEndTests.
/// </summary>
public class PlayerEndTests
{
    private int changes;
    private void Count() => changes++;

    [SetUp]
    public void SetUp()
    {
        PlayerEnd.Reset();
        changes = 0;
        PlayerEnd.Changed += Count;
    }

    [TearDown]
    public void TearDown()
    {
        PlayerEnd.Changed -= Count;
        PlayerEnd.Reset();
    }

    // ---- PlayerEnd ----------------------------------------------------------------------------------

    [Test]
    public void ChangedIsRaisedOncePerRealChange()
    {
        Assert.IsFalse(PlayerEnd.NonStriker, "every scene starts at the crease");
        PlayerEnd.Set(true);
        PlayerEnd.Set(true);
        Assert.IsTrue(PlayerEnd.NonStriker);
        Assert.AreEqual(1, changes, "setting the same end again is not a change");
        PlayerEnd.Set(false);
        Assert.AreEqual(2, changes);
    }

    [Test]
    public void ASceneLoadPutsThePlayerBackAtTheCrease()
    {
        PlayerEnd.Set(true);
        MethodInfo onLoaded = typeof(PlayerEnd).GetMethod("OnLoaded", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(onLoaded, "PlayerEnd resets from SceneManager.sceneLoaded");
        onLoaded.Invoke(null, new object[] { default(Scene), LoadSceneMode.Single });
        Assert.IsFalse(PlayerEnd.NonStriker, "a Quick Match after a career match starts at the crease");
        Assert.AreEqual(2, changes, "the new scene's listeners hear the move back");
    }

    [Test]
    public void APlaySessionStartsAtTheCreaseWithNoStaleListeners()
    {
        PlayerEnd.Set(true);
        MethodInfo init = typeof(PlayerEnd).GetMethod("Init", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(init, "PlayerEnd resets at SubsystemRegistration");
        init.Invoke(null, null);
        Assert.IsFalse(PlayerEnd.NonStriker);
        int before = changes;
        PlayerEnd.Set(true);
        Assert.AreEqual(before, changes, "listeners from before the session are dropped");
    }

    // ---- GrabbablePanel's saved pose ---------------------------------------------------------------

    private static readonly Vector3 Pose = new Vector3(8.6f, 1.25f, 0.7f);
    private static readonly Quaternion Turned = new Quaternion(0f, 0.3826834f, 0f, 0.9238795f);   // 45 deg about up

    [Test]
    public void APanelPoseRoundTripsAtBothEnds()
    {
        foreach (bool far in new[] { false, true })
        {
            Assert.IsTrue(GrabbablePanel.Decode(GrabbablePanel.Encode(Pose, Turned, far), far, out Vector3 p, out Quaternion q));
            AssertNear(Pose, p, $"position, nonStriker={far}");
            AssertSame(Turned, q, $"rotation, nonStriker={far}");
        }
    }

    [Test]
    public void APoseIsSavedAsAtTheCreaseAndOpensAtThePlayersEnd()
    {
        string saved = GrabbablePanel.Encode(Pose, Turned, true);
        Assert.IsTrue(GrabbablePanel.Decode(saved, false, out Vector3 atCrease, out Quaternion atCreaseQ));
        AssertNear(EndPose.ToCrease(Pose, true), atCrease, "saved in crease terms");
        AssertSame(EndPose.ToCrease(Turned, true), atCreaseQ, "saved in crease terms");

        string fromCrease = GrabbablePanel.Encode(Pose, Turned, false);
        Assert.IsTrue(GrabbablePanel.Decode(fromCrease, true, out Vector3 there, out Quaternion thereQ));
        AssertNear(EndPose.FromCrease(Pose, true), there, "carried to the non-striker's end");
        AssertSame(EndPose.FromCrease(Turned, true), thereQ, "carried to the non-striker's end");
    }

    [Test]
    public void APoseSavedBeforeTheEndsExistedReadsTheSameAtTheCrease()
    {
        // The format GrabbablePanel always wrote: world x,y,z then the quaternion, invariant "R".
        string old = string.Join(",", new[] { Pose.x, Pose.y, Pose.z, Turned.x, Turned.y, Turned.z, Turned.w }
                                     .Select(f => f.ToString("R", CultureInfo.InvariantCulture)));
        Assert.AreEqual(old, GrabbablePanel.Encode(Pose, Turned, false));
        Assert.IsTrue(GrabbablePanel.Decode(old, false, out Vector3 p, out Quaternion q));
        AssertNear(Pose, p, "position");
        AssertSame(Turned, q, "rotation");
    }

    [Test]
    public void NothingSavedIsNotAPose()
    {
        Assert.IsFalse(GrabbablePanel.Decode("", false, out _, out _));
        Assert.IsFalse(GrabbablePanel.Decode(null, true, out _, out _));
        Assert.IsFalse(GrabbablePanel.Decode("1,2,3", false, out _, out _));
        Assert.IsFalse(GrabbablePanel.Decode("1,2,3,a,0,0,1", false, out _, out _));
    }

    // ---- NonStrikerView ----------------------------------------------------------------------------

    [Test]
    public void TheBlinkGoesToBlackHoldsAndComesBack()
    {
        Assert.AreEqual(0.25f, NonStrikerView.FadeSeconds);
        Assert.AreEqual(0f, NonStrikerView.BlinkAlpha(0f));
        Assert.AreEqual(0.5f, NonStrikerView.BlinkAlpha(0.125f), 1e-4f);
        Assert.AreEqual(1f, NonStrikerView.BlinkAlpha(NonStrikerView.FadeSeconds));
        Assert.AreEqual(1f, NonStrikerView.BlinkAlpha(NonStrikerView.FadeSeconds + NonStrikerView.HoldSeconds));
        Assert.AreEqual(0.5f, NonStrikerView.BlinkAlpha(NonStrikerView.BlinkSeconds - 0.125f), 1e-4f);
        Assert.AreEqual(0f, NonStrikerView.BlinkAlpha(NonStrikerView.BlinkSeconds));
        Assert.AreEqual(0f, NonStrikerView.BlinkAlpha(5f), "clear after the blink");
        Assert.Less(NonStrikerView.BlinkSeconds, 0.8f, "a blink, not a scene change");
    }

    [Test]
    public void TheEndsChangeOnlyWhileNoBallIsBowled()
    {
        Assert.IsTrue(NonStrikerView.BetweenBalls(eGameState.InGame_Ready));
        Assert.IsTrue(NonStrikerView.BetweenBalls(eGameState.InGame_ResetToReady));
        Assert.IsTrue(NonStrikerView.BetweenBalls(eGameState.InGame_ResetToReadyLoop));
        Assert.IsFalse(NonStrikerView.BetweenBalls(eGameState.InGame_SelectDelivery), "a blink does not start once the ball is asked for");
        Assert.IsTrue(NonStrikerView.CanChangeEnds(eGameState.InGame_SelectDeliveryLoop), "the bowler still running in");
        foreach (eGameState s in new[] { eGameState.InGame_DeliverBall, eGameState.InGame_DeliverBallLoop, eGameState.InGame_BallHit,
                                         eGameState.InGame_BallHitLoop, eGameState.InGame_BallFielded, eGameState.InGame_Bowled })
            Assert.IsFalse(NonStrikerView.CanChangeEnds(s), s.ToString());
    }

    [Test]
    public void ThePartnersBallComesAfterTheDelayAndOnlyWhenNothingIsInTheWay()
    {
        const float readyAt = 10f, bowlAt = readyAt + NonStrikerView.AutoBowlDelay;
        Assert.AreEqual(1.5f, NonStrikerView.AutoBowlDelay);
        Assert.IsTrue(NonStrikerView.MayAutoBowl(eGameState.InGame_Ready, true, true, false, bowlAt, bowlAt));
        Assert.IsFalse(NonStrikerView.MayAutoBowl(eGameState.InGame_Ready, true, true, false, bowlAt - 0.01f, bowlAt), "too soon");
        Assert.IsFalse(NonStrikerView.MayAutoBowl(eGameState.InGame_Ready, true, true, true, bowlAt + 5f, bowlAt), "held");
        Assert.IsFalse(NonStrikerView.MayAutoBowl(eGameState.InGame_Ready, false, true, false, bowlAt + 5f, bowlAt), "the player on strike bowls his own");
        Assert.IsFalse(NonStrikerView.MayAutoBowl(eGameState.InGame_Ready, true, false, false, bowlAt + 5f, bowlAt), "not before he is at the other end");
        Assert.IsFalse(NonStrikerView.MayAutoBowl(eGameState.InGame_ResetToReadyLoop, true, true, false, bowlAt + 5f, bowlAt), "only at Ready");
    }

    [Test]
    public void OnlyAPlayerWhoWatchesGetsTheView()
    {
        Assert.IsTrue(NonStrikerView.Watching(new CareerProfile()), "Watch is the default");
        Assert.IsFalse(NonStrikerView.Watching(new CareerProfile { watchPartner = false }), "Skip: phase 2's cut to your next ball");
        Assert.IsFalse(NonStrikerView.Watching(null));
    }

    // ---- The next-ball card ------------------------------------------------------------------------

    [Test]
    public void TheNextBallCardSaysWhoIsOnStrike()
    {
        Assert.AreEqual("BOWLER", NextBallMenu.Header(false, false, false));
        Assert.AreEqual("FIRST OVER", Plain(NextBallMenu.Header(false, false, true)));
        Assert.AreEqual("PARTNER ON STRIKE", Plain(NextBallMenu.Header(false, true, false)));
        Assert.AreEqual("PARTNER ON STRIKE", Plain(NextBallMenu.Header(false, true, true)), "the partner before the first over");
        Assert.AreEqual("FREE HIT  you can't be bowled or caught", Plain(NextBallMenu.Header(true, false, false)));
        Assert.AreEqual("FREE HIT  he can't be bowled or caught", Plain(NextBallMenu.Header(true, true, false)));
        foreach (bool a in new[] { false, true })
            foreach (bool b in new[] { false, true })
                foreach (char c in NextBallMenu.Header(a, b, true))
                    Assert.LessOrEqual((int)c, 0xFF, "Latin-1 only");
    }

    // ---- Helpers -------------------------------------------------------------------------------------

    private static string Plain(string rich) => System.Text.RegularExpressions.Regex.Replace(rich ?? "", "<[^>]*>", "");

    private static void AssertNear(Vector3 expected, Vector3 actual, string what)
    {
        Assert.AreEqual(expected.x, actual.x, 1e-4f, what + " x");
        Assert.AreEqual(expected.y, actual.y, 1e-4f, what + " y");
        Assert.AreEqual(expected.z, actual.z, 1e-4f, what + " z");
    }

    /// The same rotation (q and -q are).
    private static void AssertSame(Quaternion expected, Quaternion actual, string what) =>
        Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(expected, actual)), 1e-4f, what);
}
