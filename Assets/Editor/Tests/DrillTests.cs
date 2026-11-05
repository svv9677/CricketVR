using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The drill core (DrillDefs, DrillScoring, DrillField): five drills of twelve balls, every one a
/// ball that bowler bowls and never a wide by line; each attempt in its own seeded order (the gaps
/// drill's seamer half first); every scoring table row; the virtual field (gaps, fielders, ring,
/// slip and deep catches, nothing caught beyond the rope); and the drills really feed the ratings
/// they say they feed (through CareerRatings itself). The coach and the words are DrillCoachTests,
/// which share the ball records here. Standalone (no scene, no save files).
/// </summary>
public class DrillTests
{
    // ---- Records ----------------------------------------------------------------------------------

    internal static BallRecord R(eBallAction a = eBallAction.Played, eRealContact c = eRealContact.Clean, float line = 0f,
                                bool hitting = false, float angle = 0f, int runs = 0, eBallEnd end = eBallEnd.Dot,
                                float height = 0.5f, float clearance = 99f, float step = 0f, float contactY = 0.8f,
                                eBowlerKind k = eBowlerKind.Fast, eDelivery d = eDelivery.Stock,
                                eLengthBand band = eLengthBand.GoodLength, float exit = 25f, float launch = 5f, float carry = 5f)
    {
        if (end == eBallEnd.Dot && runs > 0)
            end = runs >= 6 ? eBallEnd.Six : runs == 4 ? eBallEnd.Four : eBallEnd.Runs;
        return new BallRecord
        {
            source = eBallSource.Drill, action = a, contact = a == eBallAction.Played ? 0 : -1,
            realContact = a == eBallAction.Played ? c : eRealContact.None, realQuality = 0.7f, quality = 0.7f,
            lineAtStumps = line, wouldHitStumps = hitting, shotAngle = angle, runs = runs, end = end,
            heightAtStumps = height, headClearance = clearance, headStep = step, contactHeight = contactY,
            bowlerKind = k, delivery = d, lengthBand = band, exitSpeed = exit, launchDeg = launch, carry = carry,
        };
    }

    internal static BallRecord Left(float line = 0.5f, bool hitting = false, float height = 0.5f, float clearance = 99f, float step = 0f) =>
        R(eBallAction.Left, line: line, hitting: hitting, height: height, clearance: clearance, step: step);

    internal static BallRecord Missed(float line = 0f, float step = 0f) => R(eBallAction.Missed, line: line, step: step);

    internal static BallRecord Out() => R(eBallAction.Missed, hitting: true, end: eBallEnd.Out);

    private static BallRecord Spun(float angle, int runs = 0, float step = 0f, float contactY = 0.8f,
                                   eBowlerKind k = eBowlerKind.OffSpin, eDelivery d = eDelivery.OffBreak) =>
        R(angle: angle, runs: runs, step: step, contactY: contactY, k: k, d: d);

    internal static BallRecord Shot(float angle, float exit, float launch, float carry, eBowlerKind k = eBowlerKind.Medium,
                                   eBallEnd end = eBallEnd.Dot) =>
        R(angle: angle, exit: exit, launch: launch, carry: carry, k: k, d: BowlerRepertoire.StockBall(k), end: end);

    internal struct Case
    {
        public string name;
        public eDrill drill;
        public BallRecord r;
        public float value;
        public eDrillTag tag;
    }

    private static Case C(string name, eDrill d, BallRecord r, float v, eDrillTag t) =>
        new Case { name = name, drill = d, r = r, value = v, tag = t };

    // ---- The drills -------------------------------------------------------------------------------

    [Test]
    public void FiveDrillsOfTwelveValidBallsNeverAWideByLine()
    {
        Assert.AreEqual(DrillDefs.Count, DrillDefs.All.Length);
        var ids = new HashSet<eDrill>();
        foreach (DrillDef d in DrillDefs.All)
        {
            Assert.IsTrue(ids.Add(d.id), $"{d.id} twice");
            Assert.AreSame(d, DrillDefs.For(d.id));
            Assert.AreEqual(DrillDefs.BallsPerDrill, d.balls.Length, d.name);
            Assert.Greater(d.feeds.Length, 0, d.name);
            Assert.IsFalse(string.IsNullOrEmpty(d.name) || string.IsNullOrEmpty(d.title) || string.IsNullOrEmpty(d.blurb), d.name);
            Assert.AreEqual(0, DrillDefs.BallsPerDrill % d.blockSize, d.name);
            foreach (DrillBall b in d.balls)
            {
                Assert.IsTrue(NetsTrialPlan.Valid(b.Ball), $"{d.name}: {b.kind} does not bowl {b.delivery}");
                Assert.AreEqual(b.kind, b.Ball.kind);
                Assert.LessOrEqual(b.shape.minLine, b.shape.maxLine, d.name);
                Assert.GreaterOrEqual(b.shape.minLine, DeliveryShape.MinLine, d.name);
                Assert.LessOrEqual(b.shape.maxLine, DeliveryShape.MaxLine, d.name);
                Assert.IsTrue(System.Enum.IsDefined(typeof(eLengthBand), b.shape.band), d.name);
            }
        }
        Assert.AreEqual(System.Enum.GetValues(typeof(eDrill)).Length - 1, ids.Count, "every drill but None");
        Assert.IsNull(DrillDefs.For(eDrill.None));
        Assert.AreEqual(0, DrillDefs.Plan(eDrill.None, 1).Length);
    }

    private static string Key(DrillBall b) => $"{b.kind}/{b.delivery}/{b.shape.band}/{b.shape.minLine}/{b.shape.maxLine}";

    [Test]
    public void EachAttemptIsShuffledBySeedAndTheSameSeedBowlsTheSamePlan()
    {
        foreach (DrillDef d in DrillDefs.All)
        {
            CollectionAssert.AreEqual(DrillDefs.Plan(d.id, 7), DrillDefs.Plan(d.id, 7), d.name);
            var want = new List<string>();
            foreach (DrillBall b in d.balls) want.Add(Key(b));
            int differs = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                DrillBall[] plan = DrillDefs.Plan(d.id, seed);
                var got = new List<string>();
                foreach (DrillBall b in plan) got.Add(Key(b));
                CollectionAssert.AreEquivalent(want, got, $"{d.name} seed {seed}: the same twelve balls");
                if (!string.Join(",", got).Equals(string.Join(",", Plan(d.id, seed + 1000)))) differs++;
            }
            Assert.Greater(differs, 15, $"{d.name}: different seeds should mostly bowl a different order");
        }
    }

    private static List<string> Plan(eDrill d, int seed)
    {
        var l = new List<string>();
        foreach (DrillBall b in DrillDefs.Plan(d, seed)) l.Add(Key(b));
        return l;
    }

    [Test]
    public void TheGapsDrillKeepsTheSeamerHalfBeforeTheSpinnerHalf()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            DrillBall[] plan = DrillDefs.Plan(eDrill.Gaps, seed);
            for (int i = 0; i < plan.Length; i++)
                Assert.AreEqual(i >= 6, BowlerRepertoire.IsSpin(plan[i].kind), $"seed {seed} ball {i + 1}");
        }
    }

    [Test]
    public void TheSeedIsFixedPerCareerDrillAndAttempt()
    {
        var p = new CareerProfile { name = "Asha", created = "2026-11-05" };
        Assert.AreEqual(DrillDefs.SeedFor(p, 1, eDrill.Spin, 3), DrillDefs.SeedFor(p, 1, eDrill.Spin, 3));
        Assert.AreNotEqual(DrillDefs.SeedFor(p, 1, eDrill.Spin, 3), DrillDefs.SeedFor(p, 1, eDrill.Spin, 4));
        Assert.AreNotEqual(DrillDefs.SeedFor(p, 1, eDrill.Spin, 3), DrillDefs.SeedFor(p, 1, eDrill.Yorkers, 3));
        Assert.AreNotEqual(DrillDefs.SeedFor(p, 1, eDrill.Spin, 3), DrillDefs.SeedFor(p, 2, eDrill.Spin, 3));
        Assert.AreEqual(DrillDefs.SeedFor(null, 0, eDrill.Gaps, 1), DrillDefs.SeedFor(null, 0, eDrill.Gaps, 1));
    }

    [Test]
    public void OnlyTheGapsDrillHasAFieldTheSeamersOrTheSpinners()
    {
        foreach (DrillDef d in DrillDefs.All)
            if (d.id != eDrill.Gaps)
                Assert.IsNull(DrillDefs.FieldFor(d, eBowlerKind.Fast), d.name);
        DrillDef gaps = DrillDefs.For(eDrill.Gaps);
        Assert.AreSame(BowlingPlans.OffStump.field, DrillDefs.FieldFor(gaps, eBowlerKind.Medium));
        Assert.AreSame(BowlingPlans.Darts.field, DrillDefs.FieldFor(gaps, eBowlerKind.OffSpin));
        Assert.IsNull(DrillDefs.FieldFor(null, eBowlerKind.Fast));
    }

    // ---- Scoring ----------------------------------------------------------------------------------

    internal static readonly Case[] Cases =
    {
        C("yorker dug out for runs", eDrill.Yorkers, R(runs: 1), 1f, eDrillTag.DugOut),
        C("yorker dug out", eDrill.Yorkers, R(), 0.8f, eDrillTag.DugOut),
        C("yorker edged", eDrill.Yorkers, R(c: eRealContact.Edge), 0.4f, eDrillTag.Edged),
        C("yorker beaten", eDrill.Yorkers, Missed(), 0.25f, eDrillTag.Beaten),
        C("yorker left, missing", eDrill.Yorkers, Left(0.45f), 0.6f, eDrillTag.GoodLeave),
        C("yorker left, hitting", eDrill.Yorkers, Left(0f, true), 0f, eDrillTag.LeftStraight),
        C("yorker out", eDrill.Yorkers, Out(), 0f, eDrillTag.Bowled),

        C("wide one left", eDrill.LeaveOutsideOff, Left(0.6f), 1f, eDrillTag.GoodLeave),
        C("wide one fished at", eDrill.LeaveOutsideOff, R(line: 0.6f), 0.5f, eDrillTag.Fished),
        C("wide one beaten", eDrill.LeaveOutsideOff, Missed(0.6f), 0.2f, eDrillTag.Beaten),
        C("wide one edged", eDrill.LeaveOutsideOff, R(c: eRealContact.Edge, line: 0.6f), 0f, eDrillTag.Edged),
        C("straight one left", eDrill.LeaveOutsideOff, Left(0.05f, true), 0f, eDrillTag.LeftStraight),
        C("straight one played", eDrill.LeaveOutsideOff, R(line: 0.05f), 1f, eDrillTag.Survived),
        C("straight one edged", eDrill.LeaveOutsideOff, R(c: eRealContact.Edge, line: 0.05f), 0.3f, eDrillTag.Edged),
        C("straight one beaten", eDrill.LeaveOutsideOff, Missed(0.05f), 0.3f, eDrillTag.Beaten),
        C("corridor one left, missing", eDrill.LeaveOutsideOff, Left(0.2f), 0.6f, eDrillTag.GoodLeave),
        C("leave drill out", eDrill.LeaveOutsideOff, Out(), 0f, eDrillTag.Bowled),

        C("pulled for four", eDrill.ShortBall, R(angle: -70f, runs: 4), 1f, eDrillTag.Pulled),
        C("pulled", eDrill.ShortBall, R(angle: -70f), 0.8f, eDrillTag.Pulled),
        C("wide one cut for four", eDrill.ShortBall, R(line: 0.5f, angle: 80f, runs: 4), 1f, eDrillTag.Pulled),
        C("straight one carved for four", eDrill.ShortBall, R(line: 0.05f, angle: 80f, runs: 4), 0.6f, eDrillTag.Survived),
        C("short ball driven", eDrill.ShortBall, R(angle: 10f), 0.6f, eDrillTag.Survived),
        C("bouncer ducked", eDrill.ShortBall, Left(0.05f, height: 1.6f, clearance: 0.5f), 0.8f, eDrillTag.Ducked),
        C("bouncer unmeasured head", eDrill.ShortBall, Left(0.05f, height: 1.6f), 0.8f, eDrillTag.Ducked),
        C("bouncer swayed", eDrill.ShortBall, Left(0.05f, height: 1.6f, clearance: 0.2f), 0.5f, eDrillTag.Evaded),
        C("bouncer would hit him", eDrill.ShortBall, Left(0.05f, height: 1.6f, clearance: 0.1f), 0.1f, eDrillTag.HitHead),
        C("chest-high one left", eDrill.ShortBall, Left(0.05f, height: 1f), 0.6f, eDrillTag.GoodLeave),
        C("short ball edged", eDrill.ShortBall, R(c: eRealContact.Edge), 0.2f, eDrillTag.Edged),
        C("short ball beaten", eDrill.ShortBall, Missed(), 0.3f, eDrillTag.Beaten),
        C("short ball out", eDrill.ShortBall, Out(), 0f, eDrillTag.Bowled),

        C("off-break against the turn", eDrill.Spin, Spun(40f), 0.6f, eDrillTag.Survived),
        C("off-break with the turn", eDrill.Spin, Spun(-40f), 0.8f, eDrillTag.PlayedTurn),
        C("off-break, feet and turn", eDrill.Spin, Spun(-40f, step: 0.5f), 1f, eDrillTag.UsedFeet),
        C("off-break swept", eDrill.Spin, Spun(-90f, contactY: 0.3f), 1f, eDrillTag.Swept),
        C("off-break hit for four", eDrill.Spin, Spun(40f, runs: 4), 0.8f, eDrillTag.PlayedTurn),
        C("leg-break with the turn", eDrill.Spin, Spun(40f, k: eBowlerKind.LegSpin, d: eDelivery.LegBreak), 0.8f, eDrillTag.PlayedTurn),
        C("googly with the turn", eDrill.Spin, Spun(-40f, k: eBowlerKind.LegSpin, d: eDelivery.Googly), 0.8f, eDrillTag.PlayedTurn),
        C("top-spinner straight", eDrill.Spin, Spun(10f, k: eBowlerKind.LegSpin, d: eDelivery.TopSpinner), 0.8f, eDrillTag.PlayedTurn),
        C("stumped, missed", eDrill.Spin, Missed(0.1f, 0.6f), 0f, eDrillTag.Bowled),
        C("stumped, left", eDrill.Spin, Left(0.3f, step: 0.6f), 0f, eDrillTag.Bowled),
        C("spin edged", eDrill.Spin, R(c: eRealContact.Edge), 0.2f, eDrillTag.Edged),
        C("spin beaten", eDrill.Spin, Missed(), 0.25f, eDrillTag.Beaten),
        C("spin left, missing", eDrill.Spin, Left(0.3f), 0.4f, eDrillTag.GoodLeave),
        C("spin left, hitting", eDrill.Spin, Left(0f, true), 0f, eDrillTag.LeftStraight),

        C("cover-point gap, hard", eDrill.Gaps, Shot(75f, 30f, 3f, 5f), 1f, eDrillTag.Gap),
        C("cover-point gap", eDrill.Gaps, Shot(75f, 24f, 3f, 5f), 0.9f, eDrillTag.Gap),
        C("slow push into the gap", eDrill.Gaps, Shot(75f, 12f, 3f, 5f), 0.15f, eDrillTag.Fielder),
        C("straight at cover", eDrill.Gaps, Shot(55f, 25f, 3f, 5f), 0.15f, eDrillTag.Fielder),
        C("caught at cover", eDrill.Gaps, Shot(55f, 22f, 20f, 24f), 0f, eDrillTag.Caught),
        C("over the top", eDrill.Gaps, Shot(75f, 30f, 30f, 45f), 0.8f, eDrillTag.OverTop),
        C("six", eDrill.Gaps, Shot(0f, 40f, 30f, 70f, end: eBallEnd.Six), 1f, eDrillTag.Six),
        C("back to the keeper", eDrill.Gaps, Shot(172f, 25f, 3f, 5f), 0.2f, eDrillTag.Wasted),
        C("gaps edged", eDrill.Gaps, R(c: eRealContact.Edge, k: eBowlerKind.Medium), 0.1f, eDrillTag.Edged),
        C("gaps beaten", eDrill.Gaps, Missed(), 0.1f, eDrillTag.Beaten),
        C("gaps left", eDrill.Gaps, Left(0.2f), 0f, eDrillTag.Wasted),
        C("gaps out", eDrill.Gaps, Out(), 0f, eDrillTag.Bowled),
    };

    [Test]
    public void EveryBallScoresByItsDrillsTable()
    {
        foreach (Case c in Cases)
        {
            c.r.drill = c.drill;
            DrillBallScore s = DrillScoring.Score(DrillDefs.For(c.drill), c.r);
            Assert.AreEqual(c.value, s.value, 1e-4f, c.name);
            Assert.AreEqual(c.tag, s.tag, c.name);
        }
        Assert.AreEqual(eDrillTag.None, DrillScoring.Score(null, R()).tag);
    }

    [Test]
    public void TurnSideFollowsTheWayEachBallTurns()
    {
        Assert.AreEqual(-1, DrillScoring.TurnSide(eBowlerKind.OffSpin, eDelivery.OffBreak));
        Assert.AreEqual(-1, DrillScoring.TurnSide(eBowlerKind.OffSpin, eDelivery.Flighted));
        Assert.AreEqual(-1, DrillScoring.TurnSide(eBowlerKind.LegSpin, eDelivery.Googly));
        Assert.AreEqual(1, DrillScoring.TurnSide(eBowlerKind.LegSpin, eDelivery.LegBreak));
        Assert.AreEqual(1, DrillScoring.TurnSide(eBowlerKind.LegSpin, eDelivery.Flighted));
        Assert.AreEqual(1, DrillScoring.TurnSide(eBowlerKind.Mystery, eDelivery.Carrom));
        Assert.AreEqual(0, DrillScoring.TurnSide(eBowlerKind.OffSpin, eDelivery.Slider));
        Assert.AreEqual(0, DrillScoring.TurnSide(eBowlerKind.LegSpin, eDelivery.TopSpinner));
        Assert.AreEqual(0, DrillScoring.TurnSide(eBowlerKind.Fast, eDelivery.Stock));
    }

    internal static List<BallRecord> Many(int n, System.Func<BallRecord> make)
    {
        var l = new List<BallRecord>();
        for (int i = 0; i < n; i++) l.Add(make());
        return l;
    }

    [Test]
    public void TheScoreIsOverTwelveBallsWithTagsAndAGrade()
    {
        DrillDef y = DrillDefs.For(eDrill.Yorkers);
        DrillResult perfect = DrillScoring.Tally(y, Many(12, () => R(runs: 1)));
        Assert.AreEqual(100, perfect.score);
        Assert.AreEqual(12, perfect.balls);
        Assert.AreEqual(12, perfect.tags[(int)eDrillTag.DugOut]);
        Assert.AreEqual("Excellent", perfect.grade);
        Assert.AreEqual(eDrill.Yorkers, perfect.drill);

        DrillResult outs = DrillScoring.Tally(y, Many(12, Out));
        Assert.AreEqual(0, outs.score);
        Assert.AreEqual(12, outs.tags[(int)eDrillTag.Bowled]);
        Assert.AreEqual("Needs work", outs.grade);

        Assert.AreEqual(50, DrillScoring.Tally(y, Many(6, () => R(runs: 1))).score, "a ball not faced is worth nothing");
        Assert.AreEqual(100, DrillScoring.Tally(y, Many(15, () => R(runs: 1))).score, "only the first twelve count");
        Assert.AreEqual(0, DrillScoring.Tally(y, null).score);
        Assert.AreEqual(100f, DrillScoring.Running(y, Many(3, () => R(runs: 1))), 1e-3f);
        Assert.AreEqual(40f, DrillScoring.Running(y, new List<BallRecord> { R(), Out() }), 1e-3f);
        Assert.AreEqual(0f, DrillScoring.Running(y, new List<BallRecord>()));
        Assert.AreEqual("Good", DrillScoring.Grade(60));
        Assert.AreEqual("Getting there", DrillScoring.Grade(59));
        Assert.AreEqual("Getting there", DrillScoring.Grade(40));
        Assert.AreEqual("Needs work", DrillScoring.Grade(39));
        Assert.AreEqual("Excellent", DrillScoring.Grade(80));
    }

    [Test]
    public void AttemptBallsAreThisAttemptsCountingDrillBallsOnly()
    {
        BallRecord Mk(eBallSource s, eDrill d, int attempt, eBallEnd end = eBallEnd.Dot, bool wide = false)
        {
            BallRecord r = R(end: end);
            r.source = s; r.drill = d; r.drillAttempt = attempt; r.wide = wide;
            return r;
        }
        BallRecord a = Mk(eBallSource.Drill, eDrill.Spin, 2), b = Mk(eBallSource.Drill, eDrill.Spin, 2);
        var balls = new List<BallRecord>
        {
            Mk(eBallSource.Drill, eDrill.Spin, 1), a, Mk(eBallSource.Nets, eDrill.None, 0), Mk(eBallSource.Drill, eDrill.Yorkers, 2),
            Mk(eBallSource.Drill, eDrill.Spin, 2, eBallEnd.Wide, true), Mk(eBallSource.Drill, eDrill.Spin, 2, eBallEnd.Dead), b, null,
        };
        CollectionAssert.AreEqual(new[] { a, b }, DrillScoring.AttemptBalls(balls, eDrill.Spin, 2));
        Assert.AreEqual(0, DrillScoring.AttemptBalls(null, eDrill.Spin, 2).Count);
        Assert.AreEqual(2, DrillScoring.Tally(DrillDefs.For(eDrill.Spin), DrillScoring.AttemptBalls(balls, eDrill.Spin, 2)).attempt);
    }

    /// Two attempts of each drill, every ball met cleanly, at the shapes the drill bowls: every
    /// rating the drill says it feeds must then be measured (Known) by CareerRatings itself.
    [Test]
    public void EachDrillReallyFeedsTheRatingsItNames()
    {
        foreach (DrillDef d in DrillDefs.All)
        {
            var balls = new List<BallRecord>();
            for (int attempt = 1; attempt <= 2; attempt++)
                foreach (DrillBall b in DrillDefs.Plan(d.id, attempt))
                {
                    float line = 0.5f * (b.shape.minLine + b.shape.maxLine);
                    bool bounces = b.shape.band == eLengthBand.Short || b.shape.band == eLengthBand.BackOfALength;
                    BallRecord r = R(line: line, hitting: !bounces && System.Math.Abs(line) <= 0.15f, runs: 1,
                                     k: b.kind, d: b.delivery, band: b.shape.band);
                    r.drill = d.id; r.drillAttempt = attempt;
                    balls.Add(r);
                }
            RatingSet set = CareerRatings.Compute(balls);
            foreach (int i in d.feeds)
                Assert.IsTrue(RatingSet.Known(set[i]), $"{d.name} should feed {RatingSet.Names[i]}");
        }
    }

    // ---- The virtual field ------------------------------------------------------------------------

    private static GapJudgement Judge(BallRecord r) =>
        DrillField.Judge(r, DrillDefs.FieldFor(DrillDefs.For(eDrill.Gaps), r.bowlerKind), DrillField.NetsGround(r.bowlerKind));

    [Test]
    public void TheFieldStopsCatchesAndLeavesGapsWhereARealOneWould()
    {
        Assert.AreEqual(eGapCall.Gap, Judge(Shot(75f, 30f, 3f, 5f)).call, "cover-point gap at 30 m/s");
        GapJudgement cover = Judge(Shot(55f, 25f, 3f, 5f));
        Assert.AreEqual(eGapCall.Fielder, cover.call);
        Assert.AreEqual("Cover", cover.fielder);
        Assert.AreEqual(eGapCall.Fielder, Judge(Shot(75f, 12f, 3f, 5f)).call, "a slow push is cut off");
        GapJudgement ring = Judge(Shot(55f, 22f, 20f, 24f));
        Assert.AreEqual(eGapCall.Caught, ring.call, "lofted into the ring");
        Assert.AreEqual("Cover", ring.fielder);
        GapJudgement slip = Judge(Shot(160f, 20f, 10f, 8.5f));
        Assert.AreEqual(eGapCall.Caught, slip.call, "the late cut in the air to slip");
        Assert.AreEqual("First slip", slip.fielder);
        GapJudgement deep = Judge(Shot(-55f, 26f, 35f, 50f, eBowlerKind.OffSpin));
        Assert.AreEqual(eGapCall.Caught, deep.call, "to deep midwicket off the spinner");
        Assert.AreEqual("Deep midwicket", deep.fielder);
        Assert.AreEqual(eGapCall.OverTop, Judge(Shot(75f, 30f, 30f, 45f)).call);
        Assert.AreEqual(eGapCall.OverTop, Judge(Shot(-88f, 33f, 30f, 62f)).call, "nobody catches it beyond the rope");
        Assert.AreEqual(eGapCall.Six, Judge(Shot(0f, 40f, 30f, 70f, end: eBallEnd.Six)).call);
        Assert.AreEqual(eGapCall.Wasted, Judge(Shot(172f, 25f, 3f, 5f)).call, "the keeper's");
        BallRecord edge = Shot(75f, 30f, 3f, 5f);
        edge.realContact = eRealContact.Edge;
        Assert.AreEqual(eGapCall.Wasted, Judge(edge).call);
        Assert.AreEqual(eGapCall.Wasted, DrillField.Judge(null, null, DrillField.NetsGround(eBowlerKind.Medium)).call);
        Assert.AreEqual(eGapCall.Gap, DrillField.Judge(Shot(55f, 25f, 3f, 5f), null, DrillField.NetsGround(eBowlerKind.Medium)).call,
                        "no field, no fielders");
    }

    [Test]
    public void AFielderCoversLessAngleTheHarderItIsHit()
    {
        Assert.Greater(DrillField.Cover(28f, 12f), DrillField.Cover(28f, 30f));
        Assert.AreEqual(14.7f, DrillField.Cover(28f, 30f), 0.3f);
        Assert.AreEqual(90f, DrillField.Cover(0f, 30f));
        FieldSets.Ground g = DrillField.NetsGround(eBowlerKind.Fast);
        Assert.AreEqual(BallDelivery.BatsmanStumpsX, g.strikerX);
        Assert.AreEqual(1f, g.offSign);
        Assert.AreEqual(DrillField.Boundary, g.boundary);
        Assert.AreEqual(FieldSets.KeeperX(eSwingType.Pace), g.keeperX);
        Assert.AreEqual(FieldSets.KeeperX(eSwingType.OffSpin), DrillField.NetsGround(eBowlerKind.OffSpin).keeperX);
    }
}
