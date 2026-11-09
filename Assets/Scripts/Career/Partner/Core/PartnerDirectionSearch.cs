using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The direction search behind PartnerShotPlanner.PlanDirectionSteps: which way, how hard and how
/// high to hit so the field model (PartnerField) gives the intent, with the most margin. Runs
/// before the ball is bowled, so it plans from a nominal contact point; Plan re-checks it from the
/// real one.
///
/// He bats like an orthodox, mostly-straight batter (2026-11-05, "the nonstriker shots look weird,
/// make him play more straight shots"): searched across the whole field he played a third of his
/// runs as glances and a fifth of them within 40 degrees of straight. So the field is searched in
/// three arcs, and a wider one only when nothing in the narrower ones lands the intent:
///   0. Straight: the V, mid-off to mid-on (within StraightArc of straight) - drives, pushes and
///      defence.
///   1. Square: the off side out to SquareOff (cover, point: the cut) and the leg side out to
///      SquareLeg (midwicket: the on-drive and the pull).
///   2. Fine: what is left, out to FinestAngle - where the glances and the sweeps go.
/// In each arc, cheapest first, never more than MaxCandidates in all:
///   1. Targets: for runs the gaps between the fielders inside the arc (widest first, the order
///      shuffled so he does not always pick the same one); for a dot the ring fielders in it (a
///      firm push straight at one); for a catch each fielder in it, at the elevation that brings
///      the ball to him at catching height.
///   2. Unless a target already did it comfortably: a sweep across the arc from a random start,
///      closer together in the straight arc, where he means to play.
///   3. If nothing in the arc has landed the intent: a fine refinement around the arc's own
///      nearest miss (RefineMiss), before the search widens.
/// Then, once something lands, a refinement around the best: 2 degrees either way, a little
/// harder or softer, higher or flatter. A shot that lands the intent always beats one that does
/// not, and among those that do the straighter arc wins before the margin does.
///
/// The career player stands at the non-striker's end, just off straight: a shot passing within
/// PartnerShotPlanner.HumanClearance of his head is never taken. Rather than just dropping it, the
/// search tries it again turned away from him (HumanNudge, twice at most), so the straight shots
/// near him stay straight but go past him.
/// Angles are FieldSets': 0 straight back past the bowler, off side positive.
/// </summary>
internal sealed class PartnerDirectionSearch
{
    public const int MaxCandidates = 400;
    /// A margin (metres or seconds, by intent) worth stopping the search for.
    public const float Comfortable = 0.6f;
    /// Most of the field is in front of square: no shot is aimed finer than this.
    public const float FinestAngle = 160f;
    /// The V he plays in: this far either side of straight (mid-off to mid-on).
    public const float StraightArc = 40f;
    /// Where the square arc ends: on the off side past point (a cut), on the leg side at
    /// midwicket (finer than that is sweep and glance country).
    public const float SquareOff = 120f, SquareLeg = 60f;
    /// How far a shot that passes too near the player's head is turned away from him to try again.
    public const float HumanNudge = 5f;
    /// Retries turned away from the player, per shot and in all.
    private const int NudgesPerShot = 2, NudgeBudget = 40;
    /// Among shots that miss the intent (a three, which most fields do not give anywhere), how
    /// much an arc further out costs, in Score's units (a run off the intent is 2): half a run, so
    /// a straight two or four beats a glanced two.
    private const float ArcPenalty = 1f;

    internal struct Candidate { public float angle, elevation, speed; public int nudges; }

    private readonly int intent;
    private readonly FieldSnapshot f;
    private readonly Vector3 contact;
    private readonly System.Random rng;
    private readonly List<BallFlight.Sample> scratch = new List<BallFlight.Sample>(1024);
    private readonly Queue<Candidate> nudged = new Queue<Candidate>();
    private ShotDirection best;
    private Candidate bestCandidate;
    private float bestScore = float.NegativeInfinity, bestMargin;
    private int bestArc, nudgesLeft = NudgeBudget;
    private bool any;
    /// The best shot tried in each arc, by Score alone: what an arc that missed is refined around.
    private readonly Candidate[] arcBest = new Candidate[3];
    private readonly float[] arcScore = new float[3];
    private readonly bool[] arcAny = new bool[3];

    public int Tried { get; private set; }
    public ShotDirection Best => best;

    public PartnerDirectionSearch(int intent, FieldSnapshot f, Vector3 contact, System.Random rng)
    {
        this.intent = intent;
        this.f = f;
        this.contact = contact;
        this.rng = rng ?? new System.Random(0);
        best = new ShotDirection { intent = intent, wicket = PartnerWicket.None, valid = false };
    }

    /// Which arc a direction is in: 0 straight (the V), 1 square, 2 fine.
    public static int Arc(float angle)
    {
        if (Mathf.Abs(angle) <= StraightArc) return 0;
        return (angle > 0f ? angle <= SquareOff : -angle <= SquareLeg) ? 1 : 2;
    }

    /// The stretches of FieldSets angle (lo, hi) that make up an arc.
    private static Vector2[] Ranges(int arc)
    {
        switch (arc)
        {
            case 0: return new[] { new Vector2(-StraightArc, StraightArc) };
            case 1: return new[] { new Vector2(StraightArc, SquareOff), new Vector2(-SquareLeg, -StraightArc) };
            default: return new[] { new Vector2(SquareOff, FinestAngle), new Vector2(-FinestAngle, -SquareLeg) };
        }
    }

    /// Exit speeds (m/s) and elevations (degrees) the search tries for an intent.
    private static void Grid(int intent, out float[] speeds, out float[] elevations)
    {
        switch (intent)
        {
            case 0: speeds = new[] { 10f, 13f, 16f, 19f }; elevations = new[] { -6f, -1f }; break;
            // 24 and 27: driven hard to the man in the deep in the V for one, which on most fields
            // is the only straight single (measured 2026-11-05: on 13 of the 18 test fields).
            case 1: speeds = new[] { 9f, 12f, 15f, 18f, 21f, 24f, 27f }; elevations = new[] { -6f, 2f }; break;
            case 2: speeds = new[] { 11f, 13f, 15f, 17f }; elevations = new[] { -4f, 3f, 10f }; break;
            // 11 and 12.5: since the outfield lets a slow ball roll on (BallFlight.RollFloor,
            // 2026-11-09) the three's window moved down from 14.5-15 m/s with it. Measured on the
            // test fields: 11 of 180 planned with these, 9 with 10-13 in whole steps, 2 without.
            case 3: speeds = new[] { 11f, 12.5f, 14f, 17f, 20f, 24f }; elevations = new[] { -3f, 5f }; break;
            case 4: speeds = new[] { 24f, 28f, 32f, 36f, 38f }; elevations = new[] { -3f, 4f, 10f }; break;
            case 6: speeds = new[] { 34f, 38f, 42f, 45f }; elevations = new[] { 26f, 32f, 38f }; break;
            default: speeds = new[] { 18f, 24f, 30f }; elevations = new[] { 8f, 16f, 28f }; break;
        }
    }

    private bool Landed => any && best.valid;

    /// Every candidate in order; lazy, because the widening and the refinement read the best so far.
    /// An arc where nothing landed the intent is refined around its own nearest miss before the
    /// search widens. The intents a field gives only in a narrow window are found by that
    /// refinement - the threes on the test fields, 14.5 to 15 m/s a few degrees either side of
    /// square leg, on two of the 18 - and refining only the overall best, a straight two that the
    /// arc order prefers among misses, never looked there (measured 2026-11-05 over 180 seeded
    /// searches: threes planned 14 searching the whole field, 0 with the arcs refining only the
    /// overall best, 18 now; PartnerIntentReachTests).
    public IEnumerable<Candidate> Candidates()
    {
        if (intent == PartnerIntent.LeaveWide) yield break;
        for (int arc = 0; arc <= 2 && !(arc > 0 && Landed); arc++)
        {
            foreach (Candidate c in WithNudges(Targets(arc))) yield return c;
            if (!(Landed && bestMargin >= Comfortable))
                foreach (Candidate c in WithNudges(Sweep(arc))) yield return c;
            if (!Landed && arcAny[arc])
                foreach (Candidate c in WithNudges(RefineMiss(arcBest[arc]))) yield return c;
        }
        if (Landed)
            foreach (Candidate c in WithNudges(Refine(bestCandidate))) yield return c;
    }

    /// The candidates, each followed by any retries Try queued for it (turned off the player).
    private IEnumerable<Candidate> WithNudges(IEnumerable<Candidate> list)
    {
        foreach (Candidate c in list)
        {
            yield return c;
            while (nudged.Count > 0) yield return nudged.Dequeue();
        }
    }

    /// Try one candidate; false once the budget is spent.
    public bool Try(Candidate c)
    {
        if (Tried >= MaxCandidates) return false;
        Tried++;
        c.speed = Mathf.Min(c.speed, PartnerShotPlanner.SpeedCap(intent));
        Vector3 exit = PartnerShotPlanner.ExitFor(c.angle, c.elevation, c.speed, f.offSign);
        PartnerField.Assessment a = PartnerField.Assess(contact, exit, f, scratch);
        if (PartnerField.HasHuman(f) && a.result.closestToHuman < PartnerShotPlanner.HumanClearance)
        {
            NudgeOffThePlayer(c);
            return true;
        }
        bool match = PartnerShotPlanner.Realises(intent, a, out float margin);
        float score = PartnerShotPlanner.Score(intent, a, match, margin);
        int arc = Arc(c.angle);
        if (!arcAny[arc] || score > arcScore[arc])
        {
            arcAny[arc] = true;
            arcScore[arc] = score;
            arcBest[arc] = c;
        }
        if (Better(match, arc, score))
        {
            any = true;
            bestScore = score;
            bestMargin = margin;
            bestArc = arc;
            bestCandidate = c;
            best = new ShotDirection
            {
                intent = intent, exitVelocity = exit, predicted = a.result, valid = match,
                wicket = !a.result.caught ? PartnerWicket.None
                    : a.result.byFielder == f.keeperIndex ? PartnerWicket.CaughtBehind : PartnerWicket.Caught,
            };
        }
        return true;
    }

    /// Landing the intent first; then, among those that do, the straighter arc, then the margin;
    /// among those that do not, the nearer miss, an arc further out costing ArcPenalty.
    private bool Better(bool match, int arc, float score)
    {
        if (!any) return true;
        if (match != best.valid) return match;
        if (match && arc != bestArc) return arc < bestArc;
        if (match) return score > bestScore;
        return score - ArcPenalty * arc > bestScore - ArcPenalty * bestArc;
    }

    /// Queue the shot again turned away from the player's head (his bearing from the bat).
    private void NudgeOffThePlayer(Candidate c)
    {
        if (c.nudges >= NudgesPerShot || nudgesLeft <= 0) return;
        nudgesLeft--;
        float away = c.angle >= AngleTo(f.nonStrikerHead) ? 1f : -1f;
        nudged.Enqueue(new Candidate { angle = c.angle + away * HumanNudge, elevation = c.elevation, speed = c.speed, nudges = c.nudges + 1 });
    }

    private IEnumerable<Candidate> Targets(int arc)
    {
        Grid(intent, out float[] speeds, out float[] elevations);
        if (intent == CareerSim.Wicket)
        {
            foreach (int j in Shuffled(Fielders(true)))
                if (Arc(AngleTo(f.fielders[j])) == arc)
                    foreach (Candidate c in CatchesAt(f.fielders[j])) yield return c;
            yield break;
        }
        List<float> aims = intent == 0 ? RingAngles(arc) : Gaps(arc);
        foreach (float aim in aims)
            foreach (float s in speeds)
                foreach (float e in elevations)
                    yield return new Candidate
                    {
                        angle = aim + (intent == 0 ? 0f : Jitter(2f)), elevation = e, speed = s + Jitter(1f),
                    };
    }

    /// Across the arc. In the V, where he means to play, every 4 degrees from its very edge (a
    /// straight single is often only to be had there, past mid-on or mid-off) with four shots at
    /// each, walking through the whole speed x elevation grid from a random place so every pair
    /// is tried at several angles. Further out, every 10 degrees from a random start, three shots
    /// of random speed and elevation at each.
    private IEnumerable<Candidate> Sweep(int arc)
    {
        Grid(intent, out float[] speeds, out float[] elevations);
        bool straight = arc == 0;
        float step = straight ? 4f : 10f;
        int each = straight ? 4 : 3, pairs = speeds.Length * elevations.Length, next = rng.Next(pairs);
        foreach (Vector2 r in Ranges(arc))
        {
            float start = r.x + (float)rng.NextDouble() * (straight ? 1f : step);
            for (float angle = start; angle <= r.y; angle += step)
                for (int k = 0; k < each; k++)
                {
                    int pair = straight ? next++ % pairs : rng.Next(pairs);
                    yield return new Candidate
                    {
                        angle = angle, speed = speeds[pair % speeds.Length], elevation = elevations[pair / speeds.Length],
                    };
                }
        }
    }

    private IEnumerable<Candidate> Refine(Candidate b)
    {
        float[] turns = { -2f, 2f, -4f, 4f, -6f, 6f };
        foreach (float d in turns)
        {
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation, speed = b.speed };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation, speed = b.speed + 2f };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation, speed = b.speed - 2f };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation + 3f, speed = b.speed };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation - 3f, speed = b.speed };
        }
    }

    /// Around an arc's nearest miss: finer than Refine, because what was missed is usually a
    /// narrow window - the threes on the test fields are only to be had between 14.5 and 15 m/s,
    /// which a 2 m/s step from the grid's 14 or 17 jumps straight over. The miss itself and 2 and
    /// 4 degrees either side, each 1 and 2 m/s harder and softer and 3 degrees higher and flatter.
    private IEnumerable<Candidate> RefineMiss(Candidate b)
    {
        float[] turns = { 0f, -2f, 2f, -4f, 4f };
        float[] speeds = { 1f, -1f, 2f, -2f };
        foreach (float d in turns)
        {
            foreach (float ds in speeds)
                yield return new Candidate { angle = b.angle + d, elevation = b.elevation, speed = b.speed + ds };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation + 3f, speed = b.speed };
            yield return new Candidate { angle = b.angle + d, elevation = b.elevation - 3f, speed = b.speed };
        }
    }

    /// The fielders other than the keeper (and, for catches, only those in front of square-ish).
    private List<int> Fielders(bool forCatch)
    {
        var list = new List<int>();
        if (f.fielders == null) return list;
        for (int j = 0; j < f.fielders.Length; j++)
            if (j != f.keeperIndex && (!forCatch || Mathf.Abs(AngleTo(f.fielders[j])) <= FinestAngle - 20f))
                list.Add(j);
        return list;
    }

    private float AngleTo(Vector3 p) =>
        Mathf.Atan2((p.z - contact.z) * Off, -(p.x - contact.x)) * Mathf.Rad2Deg;

    private float Off => f.offSign < 0f ? -1f : 1f;

    /// Straight at the fielders saving the single (inside the circle, not the close catchers) in
    /// this arc.
    private List<float> RingAngles(int arc)
    {
        var aims = new List<float>();
        foreach (int j in Shuffled(Fielders(false)))
        {
            float d = new Vector2(f.fielders[j].x - contact.x, f.fielders[j].z - contact.z).magnitude;
            float angle = AngleTo(f.fielders[j]);
            if (d > 8f && d < FieldSets.RingFurthest + 4f && Arc(angle) == arc) aims.Add(angle);
        }
        return aims;
    }

    /// Every gap of 15 degrees or more between fielders, as much of it as lies in this arc, widest
    /// first, shuffled among the wide ones so the same gap is not always the one. Aimed at the
    /// middle of the whole gap when that is in the arc - the run is likeliest there, furthest from
    /// both fielders - and only at the middle of the piece in the arc when it is not (a gap that
    /// straddles the edge of the V). Aiming at the piece's middle every time moved the aims off the
    /// gaps' middles and lost threes (measured 2026-11-05: 11 of 180 seeded searches, 18 this way).
    private List<float> Gaps(int arc)
    {
        var angles = new List<float>();
        foreach (int j in Fielders(false)) angles.Add(AngleTo(f.fielders[j]));
        angles.Add(-FinestAngle - 10f);
        angles.Add(FinestAngle + 10f);
        angles.Sort();
        var gaps = new List<Vector2>();   // (middle, width)
        Vector2[] ranges = Ranges(arc);
        for (int i = 1; i < angles.Count; i++)
            foreach (Vector2 r in ranges)
            {
                float lo = Mathf.Max(angles[i - 1], r.x), hi = Mathf.Min(angles[i], r.y), w = hi - lo;
                float whole = 0.5f * (angles[i - 1] + angles[i]);
                float aim = whole >= r.x && whole <= r.y ? whole : 0.5f * (lo + hi);
                if (w >= 15f) gaps.Add(new Vector2(aim, w + 20f * (float)rng.NextDouble()));
            }
        gaps.Sort((x, y) => y.y.CompareTo(x.y));
        var aims = new List<float>();
        for (int i = 0; i < gaps.Count && i < 6; i++) aims.Add(gaps[i].x);
        return aims;
    }

    /// For a catch at `at`: speeds and the flat elevation that brings it to him at chest height
    /// (drag-free, with the speed taken a little low for the drag), and head height.
    private IEnumerable<Candidate> CatchesAt(Vector3 at)
    {
        float d = new Vector2(at.x - contact.x, at.z - contact.z).magnitude;
        float angle = AngleTo(at);
        float g = Mathf.Abs(Physics.gravity.y);
        float[] speeds = { 18f, 24f, 30f };
        float[] heights = { 1.1f, 1.7f };
        foreach (float v in speeds)
            foreach (float h in heights)
            {
                float ve = v * 0.9f;
                float A = g * d * d / (2f * ve * ve);
                float disc = d * d - 4f * A * (A + h - contact.y);
                if (disc < 0f) continue;
                float tan = (d - Mathf.Sqrt(disc)) / (2f * A);
                yield return new Candidate { angle = angle, speed = v, elevation = Mathf.Atan(tan) * Mathf.Rad2Deg };
            }
    }

    private float Jitter(float half) => ((float)rng.NextDouble() * 2f - 1f) * half;

    private List<int> Shuffled(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
        return list;
    }
}
