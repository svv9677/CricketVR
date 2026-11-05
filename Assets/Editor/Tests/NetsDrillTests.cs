using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A drill in the nets (NetsTrial's drill side, NetsDrillRun, NetsSession's drill statics, the field
/// markers' geometry): a drill ball is stamped with the drill and its attempt and moves the drill on
/// like a trial ball, and is measured exactly as any other ball; the trial and practice are as they
/// were; the run's plan is the drill core's for this attempt, the same again after a restart;
/// finishing twice records once and returns the first result; the session's drill state starts,
/// carries over a restart and ends as it should. Standalone (no scene, no save files).
/// </summary>
public class NetsDrillTests
{
    private static readonly eDrill[] Drills = { eDrill.Yorkers, eDrill.LeaveOutsideOff, eDrill.ShortBall, eDrill.Spin, eDrill.Gaps };

    [SetUp]
    public void SetUp() => NetsSession.ResetForTests();

    [TearDown]
    public void TearDown() => NetsSession.ResetForTests();

    private static BallRecord Ball(eBallEnd end, bool wide = false) =>
        new BallRecord { end = end, wide = wide, bowlerKind = eBowlerKind.Fast, action = eBallAction.Played, contact = 0, quality = 0.7f, realQuality = 0.7f, realContact = eRealContact.Clean };

    private static CareerProfile Profile() => new CareerProfile { name = "Asha", created = "2026-11-05" };

    /// BeginDrill sets the session up and then loads the Nets scene, last; an EditMode test cannot
    /// load a scene (it throws outside play mode), so the load is skipped and the rest is checked.
    private static void BeginDrill(eDrill d, int attempt, RatingSet before)
    {
        try
        {
            NetsSession.BeginDrill(d, attempt, before);
        }
        catch (Exception)
        {
            // The scene load.
        }
    }

    // ---- Measuring -------------------------------------------------------------------------------

    [Test]
    public void ADrillBallIsStampedWithTheDrillAndAttemptAndMovesTheDrillOn()
    {
        var p = Profile();
        BallRecord wide = Ball(eBallEnd.Wide, wide: true);
        Assert.IsFalse(NetsTrial.Measure(p, wide, eNetsMode.Drill, eDrill.Spin, 4), "a wide is bowled again");
        BallRecord dead = Ball(eBallEnd.Dead);
        Assert.IsFalse(NetsTrial.Measure(p, dead, eNetsMode.Drill, eDrill.Spin, 4), "so is a dead ball");
        BallRecord dot = Ball(eBallEnd.Dot);
        Assert.IsTrue(NetsTrial.Measure(p, dot, eNetsMode.Drill, eDrill.Spin, 4));
        Assert.AreEqual(3, p.balls.Count, "every faced ball is kept");
        foreach (BallRecord r in p.balls)
        {
            Assert.AreEqual(eBallSource.Drill, r.source);
            Assert.AreEqual(eDrill.Spin, r.drill);
            Assert.AreEqual(4, r.drillAttempt);
        }
        Assert.AreEqual(1, p.totals.balls);
        Assert.IsNotNull(p.ratings);
    }

    [Test]
    public void TheGamesMeasureTakesTheDrillAndAttemptFromTheSession()
    {
        BeginDrill(eDrill.ShortBall, 2, null);
        Assert.AreEqual(eNetsMode.Drill, NetsSession.Mode);
        var p = Profile();
        BallRecord r = Ball(eBallEnd.Four);
        Assert.IsTrue(NetsTrial.Measure(p, r, eNetsMode.Drill));
        Assert.AreEqual(eBallSource.Drill, r.source);
        Assert.AreEqual(eDrill.ShortBall, r.drill);
        Assert.AreEqual(2, r.drillAttempt);
        Assert.IsFalse(NetsTrial.Measure(null, Ball(eBallEnd.Dot), eNetsMode.Drill));
    }

    [Test]
    public void TheTrialAndPracticeAreMeasuredAsBefore()
    {
        var p = Profile();
        BallRecord trial = Ball(eBallEnd.Dot);
        Assert.IsTrue(NetsTrial.Measure(p, trial, eNetsMode.Trial, eDrill.Spin, 4));
        Assert.AreEqual(eBallSource.Trial, trial.source);
        Assert.AreEqual(eDrill.None, trial.drill, "only a drill ball carries a drill");
        Assert.AreEqual(0, trial.drillAttempt);
        Assert.IsFalse(NetsTrial.Measure(p, Ball(eBallEnd.Wide, wide: true), eNetsMode.Trial));

        BallRecord practice = Ball(eBallEnd.Dot);
        Assert.IsFalse(NetsTrial.Measure(p, practice, eNetsMode.Practice, eDrill.Spin, 4), "practice has nothing to move on");
        Assert.AreEqual(eBallSource.Nets, practice.source);
        Assert.AreEqual(eDrill.None, practice.drill);
        Assert.AreEqual(0, practice.drillAttempt);
    }

    [Test]
    public void ADrillBallIsRatedTheSameAsAnyOther()
    {
        // The ratings only measure: the same balls give the same ratings from a drill as from practice.
        var drilled = Profile();
        var practised = Profile();
        for (int i = 0; i < 40; i++)
        {
            eBallEnd end = i % 5 == 0 ? eBallEnd.Out : i % 3 == 0 ? eBallEnd.Four : eBallEnd.Dot;
            BallRecord a = Ball(end), b = Ball(end);
            a.lineAtStumps = b.lineAtStumps = (i % 7) * 0.1f;
            a.wouldHitStumps = b.wouldHitStumps = i % 4 == 0;
            a.lengthBand = b.lengthBand = (eLengthBand)(i % 5);
            a.exitSpeed = b.exitSpeed = 10f + i;
            NetsTrial.Measure(drilled, a, eNetsMode.Drill, eDrill.Yorkers, 1);
            NetsTrial.Measure(practised, b, eNetsMode.Practice);
        }
        Assert.AreEqual(practised.ratings.sample, drilled.ratings.sample);
        for (int i = 0; i < RatingSet.Count; i++)
            Assert.AreEqual(practised.ratings[i], drilled.ratings[i], RatingSet.Names[i]);
    }

    // ---- The session -----------------------------------------------------------------------------

    [Test]
    public void BeginningADrillSetsTheSessionUp()
    {
        var start = new RatingSet { sample = 12 };
        NetsSession.TrialIndex = 7;
        NetsSession.DrillRecorded = true;
        NetsSession.LastDrill = new DrillResult();
        BeginDrill(eDrill.Gaps, 5, start);
        Assert.AreEqual(eNetsMode.Drill, NetsSession.Mode);
        Assert.AreEqual(eDrill.Gaps, NetsSession.Drill);
        Assert.AreEqual(5, NetsSession.DrillAttempt);
        Assert.AreEqual(0, NetsSession.TrialIndex, "a drill starts at its first ball");
        Assert.IsNotNull(NetsSession.DrillStartRatings);
        Assert.AreNotSame(start, NetsSession.DrillStartRatings, "a copy: the live ratings go on changing");
        Assert.AreEqual(12, NetsSession.DrillStartRatings.sample);
        Assert.IsNull(NetsSession.LastDrill);
        Assert.IsFalse(NetsSession.DrillRecorded);

        BeginDrill(eDrill.Spin, 1, null);
        Assert.IsNull(NetsSession.DrillStartRatings);
    }

    [Test]
    public void LeavingMidDrillCarriesOnAndEndingItKeepsTheResult()
    {
        BeginDrill(eDrill.Yorkers, 3, new RatingSet());
        NetsSession.TrialIndex = 6;
        NetsSession.Left();
        Assert.IsTrue(NetsSession.ReturnToHub);
        Assert.AreEqual(eNetsMode.Drill, NetsSession.Mode, "a restart of the nets picks the drill up again");
        Assert.AreEqual(eDrill.Yorkers, NetsSession.Drill);
        Assert.AreEqual(3, NetsSession.DrillAttempt);
        Assert.AreEqual(6, NetsSession.TrialIndex);

        var result = new DrillResult { drill = eDrill.Yorkers, score = 70 };
        NetsSession.LastDrill = result;
        NetsSession.DrillRecorded = true;
        NetsSession.End();
        Assert.AreEqual(eNetsMode.Free, NetsSession.Mode);
        Assert.AreEqual(eDrill.None, NetsSession.Drill);
        Assert.AreEqual(0, NetsSession.TrialIndex);
        Assert.IsTrue(NetsSession.ReturnToHub);
        Assert.AreSame(result, NetsSession.LastDrill, "the hub may still want the drill just done");
    }

    [Test]
    public void TheTrialAndPracticeAreNotDrills()
    {
        BeginDrill(eDrill.Spin, 2, null);
        try { NetsSession.BeginTrial(); } catch (Exception) { }
        Assert.AreEqual(eNetsMode.Trial, NetsSession.Mode);
        Assert.AreEqual(eDrill.None, NetsSession.Drill);

        BeginDrill(eDrill.Spin, 2, null);
        try { NetsSession.BeginPractice(); } catch (Exception) { }
        Assert.AreEqual(eNetsMode.Practice, NetsSession.Mode);
        Assert.AreEqual(eDrill.None, NetsSession.Drill);
    }

    [Test]
    public void ResetForgetsTheDrill()
    {
        BeginDrill(eDrill.Gaps, 9, new RatingSet());
        NetsSession.LastDrill = new DrillResult();
        NetsSession.DrillRecorded = true;
        NetsSession.ResetForTests();
        Assert.AreEqual(eNetsMode.Free, NetsSession.Mode);
        Assert.AreEqual(eDrill.None, NetsSession.Drill);
        Assert.AreEqual(0, NetsSession.DrillAttempt);
        Assert.IsNull(NetsSession.DrillStartRatings);
        Assert.IsNull(NetsSession.LastDrill);
        Assert.IsFalse(NetsSession.DrillRecorded);
    }

    // ---- The run ---------------------------------------------------------------------------------

    [Test]
    public void TheRunBowlsTheDrillCoresPlanForThisAttemptAndTheSameAgainAfterARestart()
    {
        var p = Profile();
        foreach (eDrill d in Drills)
        {
            var run = new NetsDrillRun(p, 1, d, 3, null);
            var again = new NetsDrillRun(p, 1, d, 3, null);
            Assert.AreSame(DrillDefs.For(d), run.Def, $"{d}");
            DrillBall[] expected = run.Def != null ? DrillDefs.Plan(d, DrillDefs.SeedFor(p, 1, d, 3)) : new DrillBall[0];
            Assert.AreEqual(expected.Length, run.Plan.Length, $"{d}");
            Assert.AreEqual(run.Plan.Length, again.Plan.Length, $"{d}");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].kind, run.Plan[i].kind, $"{d} ball {i + 1}");
                Assert.AreEqual(expected[i].delivery, run.Plan[i].delivery, $"{d} ball {i + 1}");
                Assert.AreEqual(expected[i].shape.band, run.ShapeAt(i).band, $"{d} ball {i + 1}");
                Assert.AreEqual(expected[i].shape.minLine, run.ShapeAt(i).minLine, $"{d} ball {i + 1}");
                Assert.AreEqual(expected[i].shape.maxLine, run.ShapeAt(i).maxLine, $"{d} ball {i + 1}");
                Assert.AreEqual(run.Plan[i], again.Plan[i], $"{d} ball {i + 1}: a restart bowls the same order");
            }
            if (expected.Length > 0)
                Assert.AreEqual(expected[expected.Length - 1].shape.band, run.ShapeAt(expected.Length + 5).band, "past the end: the last ball");
        }
    }

    [Test]
    public void TheProgressLineIsLatin1ForEveryBall()
    {
        var p = Profile();
        foreach (eDrill d in Drills)
        {
            var run = new NetsDrillRun(p, 0, d, 1, null);
            for (int i = 0; i <= run.Plan.Length; i++)
            {
                AssertLatin1(run.ProgressText(i, p));
                AssertLatin1(run.ProgressText(i, null));
                if (i < run.Plan.Length)
                {
                    BallRecord r = Ball(eBallEnd.Dot);
                    r.bowlerKind = run.Plan[i].kind;
                    r.delivery = run.Plan[i].delivery;
                    NetsTrial.Measure(p, r, eNetsMode.Drill, d, 1);
                    run.OnBall(r);   // no markers made (OnReady was not called): nothing in the scene
                }
            }
        }
    }

    [Test]
    public void FinishingTwiceRecordsOnceAndGivesTheFirstResult()
    {
        var p = Profile();
        var start = new RatingSet { sample = 30 };
        BeginDrill(eDrill.Yorkers, 3, start);
        for (int i = 0; i < DrillDefs.BallsPerDrill; i++)
            Assert.IsTrue(NetsTrial.Measure(p, Ball(eBallEnd.Dot), eNetsMode.Drill));

        var run = new NetsDrillRun(p, 0, eDrill.Yorkers, 3, null);
        DrillResult first = run.Finish(p, "2026-11-05");
        Assert.IsNotNull(first);
        Assert.AreEqual(eDrill.Yorkers, first.drill);
        Assert.AreEqual(3, first.attempt);
        Assert.IsTrue(NetsSession.DrillRecorded);
        Assert.AreSame(first, NetsSession.LastDrill);
        Assert.IsNotNull(first.before);
        Assert.AreNotSame(NetsSession.DrillStartRatings, first.before, "the result keeps its own copies");
        Assert.AreEqual(30, first.before.sample);
        Assert.IsNotNull(first.after);
        Assert.AreNotSame(p.ratings, first.after);
        Assert.AreEqual(p.ratings.sample, first.after.sample);

        DrillRecord book = p.drills.For(eDrill.Yorkers);
        int attempts = book.attempts, best = book.best, last = book.last;
        Assert.AreSame(first, run.Finish(p, "2026-11-05"), "the result card shown again");
        // A restart of the nets while the card was up: a new run, the same attempt, finished again.
        Assert.AreSame(first, new NetsDrillRun(p, 0, eDrill.Yorkers, 3, null).Finish(p, "2026-11-05"));
        Assert.AreEqual(attempts, book.attempts, "recorded once");
        Assert.AreEqual(best, book.best);
        Assert.AreEqual(last, book.last);

        // The next attempt is a new result.
        NetsSession.End();
        BeginDrill(eDrill.Yorkers, 4, p.ratings);
        DrillResult next = new NetsDrillRun(p, 0, eDrill.Yorkers, 4, null).Finish(p, "2026-11-05");
        Assert.AreNotSame(first, next);
        Assert.AreEqual(4, next.attempt);
    }

    [Test]
    public void FinishingWithNoCareerStillGivesAResult()
    {
        BeginDrill(eDrill.Spin, 1, null);
        DrillResult r = new NetsDrillRun(null, 0, eDrill.Spin, 1, null).Finish(null, "2026-11-05");
        Assert.IsNotNull(r);
        Assert.AreEqual(eDrill.Spin, r.drill);
        Assert.IsNull(r.before);
        Assert.IsNull(r.after);
    }

    // ---- The field markers -----------------------------------------------------------------------

    [Test]
    public void TheFieldIsSetOnTheNetsGroundForThePlayersHand()
    {
        foreach (eBowlerKind k in new[] { eBowlerKind.Fast, eBowlerKind.Medium, eBowlerKind.OffSpin, eBowlerKind.LegSpin, eBowlerKind.Mystery })
        {
            FieldSets.Ground g = DrillField.NetsGround(k);
            FieldSets.Ground right = NetsDrillRun.WorldGround(k, false), left = NetsDrillRun.WorldGround(k, true);
            Assert.AreEqual(1f, right.offSign, $"{k}");
            Assert.AreEqual(-1f, left.offSign, $"{k}: a left-hander's off side is -z");
            Assert.AreEqual(g.strikerX, left.strikerX);
            Assert.AreEqual(g.nonStrikerX, left.nonStrikerX);
            Assert.AreEqual(g.boundary, left.boundary);
            Assert.AreEqual(g.keeperX, left.keeperX);
        }
    }

    [Test]
    public void TheMarkersStandWhereTheFieldersWould()
    {
        var g = new FieldSets.Ground { strikerX = BallDelivery.BatsmanStumpsX, nonStrikerX = BallDelivery.BowlerStumpsX, boundary = 56f, keeperX = 14f, offSign = 1f };
        FieldSets.Position[] field =
        {
            new FieldSets.Position("Cover", FieldSets.Kind.Ring, 55f),
            new FieldSets.Position("Deep square leg", FieldSets.Kind.Deep, -88f),
            new FieldSets.Position("Slip", FieldSets.Kind.Slip, 0f, 1f),
            new FieldSets.Position("Short leg", FieldSets.Kind.Close, -100f, 5f),
        };
        Vector3[] at = DrillFieldMarkers.Places(field, g);
        Assert.AreEqual(field.Length, at.Length);
        for (int i = 0; i < field.Length; i++)
            Assert.AreEqual(FieldSets.Place(field[i], g), at[i], field[i].name);
        Assert.Greater(at[0].z, 0f, "cover on the off side");
        g.offSign = -1f;
        Assert.Less(DrillFieldMarkers.Places(field, g)[0].z, 0f, "mirrored for a left-hander");
        Assert.AreEqual(0, DrillFieldMarkers.Places(null, g).Length);
    }

    [Test]
    public void ADeepFieldersNameIsBiggerButNeverHuge()
    {
        Assert.Greater(DrillFieldMarkers.LabelSize(50f), DrillFieldMarkers.LabelSize(20f));
        Assert.AreEqual(DrillFieldMarkers.LabelSize(0f), DrillFieldMarkers.LabelSize(2f), "a floor up close");
        Assert.AreEqual(DrillFieldMarkers.LabelSize(80f), DrillFieldMarkers.LabelSize(200f), "a cap far out");
    }

    private static void AssertLatin1(string s)
    {
        Assert.IsNotNull(s);
        foreach (char c in s)
            Assert.LessOrEqual((int)c, 0xFF, $"'{c}' (U+{(int)c:X4}) is not in the TMP default font: {s}");
    }
}
