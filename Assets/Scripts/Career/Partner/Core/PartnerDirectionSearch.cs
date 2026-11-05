using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The direction search behind PartnerShotPlanner.PlanDirectionSteps: which way, how hard and how
/// high to hit so the field model (PartnerField) gives the intent, with the most margin. Runs
/// before the ball is bowled, so it plans from a nominal contact point; Plan re-checks it from the
/// real one.
///
/// Candidates, cheapest first, never more than MaxCandidates in all:
///   1. Targets: for runs the gaps between the fielders (widest first, the order shuffled so he
///      does not always pick the same one); for a dot the ring fielders themselves (a firm push
///      straight at one); for a catch each fielder in front of square, at the elevation that brings
///      the ball to him at catching height.
///   2. Unless a target already did it comfortably: a sweep every 10 degrees from a random start.
///   3. A refinement around the best so far: 2 degrees either way, a little harder or softer,
///      higher or flatter.
/// Angles are FieldSets': 0 straight back past the bowler, off side positive.
/// </summary>
internal sealed class PartnerDirectionSearch
{
    public const int MaxCandidates = 400;
    /// A margin (metres or seconds, by intent) worth stopping the search for.
    public const float Comfortable = 0.6f;
    /// Most of the field is in front of square: no shot is aimed finer than this.
    public const float FinestAngle = 160f;

    internal struct Candidate { public float angle, elevation, speed; }

    private readonly int intent;
    private readonly FieldSnapshot f;
    private readonly Vector3 contact;
    private readonly System.Random rng;
    private readonly List<BallFlight.Sample> scratch = new List<BallFlight.Sample>(1024);
    private ShotDirection best;
    private Candidate bestCandidate;
    private float bestScore = float.NegativeInfinity, bestMargin;
    private bool any;

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

    /// Exit speeds (m/s) and elevations (degrees) the search tries for an intent.
    private static void Grid(int intent, out float[] speeds, out float[] elevations)
    {
        switch (intent)
        {
            case 0: speeds = new[] { 10f, 13f, 16f, 19f }; elevations = new[] { -6f, -1f }; break;
            case 1: speeds = new[] { 9f, 12f, 15f, 18f, 21f }; elevations = new[] { -6f, 2f }; break;
            case 2: speeds = new[] { 11f, 13f, 15f, 17f }; elevations = new[] { -4f, 3f, 10f }; break;
            case 3: speeds = new[] { 14f, 17f, 20f, 24f }; elevations = new[] { -3f, 5f }; break;
            case 4: speeds = new[] { 24f, 28f, 32f, 36f, 38f }; elevations = new[] { -3f, 4f, 10f }; break;
            case 6: speeds = new[] { 34f, 38f, 42f, 45f }; elevations = new[] { 26f, 32f, 38f }; break;
            default: speeds = new[] { 18f, 24f, 30f }; elevations = new[] { 8f, 16f, 28f }; break;
        }
    }

    /// Every candidate in order; lazy, because the refinement reads the best so far.
    public IEnumerable<Candidate> Candidates()
    {
        if (intent == PartnerIntent.LeaveWide) yield break;
        foreach (Candidate c in Targets()) yield return c;
        if (!(any && best.valid && bestMargin >= Comfortable))
            foreach (Candidate c in Sweep()) yield return c;
        if (any)
            foreach (Candidate c in Refine(bestCandidate)) yield return c;
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
            return true;
        bool match = PartnerShotPlanner.Realises(intent, a, out float margin);
        float score = PartnerShotPlanner.Score(intent, a, match, margin);
        if (score > bestScore)
        {
            any = true;
            bestScore = score;
            bestMargin = margin;
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

    private IEnumerable<Candidate> Targets()
    {
        Grid(intent, out float[] speeds, out float[] elevations);
        if (intent == CareerSim.Wicket)
        {
            foreach (int j in Shuffled(Fielders(true)))
                foreach (Candidate c in CatchesAt(f.fielders[j])) yield return c;
            yield break;
        }
        List<float> aims = intent == 0 ? RingAngles() : Gaps();
        foreach (float aim in aims)
            foreach (float s in speeds)
                foreach (float e in elevations)
                    yield return new Candidate
                    {
                        angle = aim + (intent == 0 ? 0f : Jitter(2f)), elevation = e, speed = s + Jitter(1f),
                    };
    }

    private IEnumerable<Candidate> Sweep()
    {
        Grid(intent, out float[] speeds, out float[] elevations);
        float start = -FinestAngle + (float)rng.NextDouble() * 10f;
        for (float angle = start; angle <= FinestAngle; angle += 10f)
            for (int k = 0; k < 3; k++)
                yield return new Candidate
                {
                    angle = angle, speed = speeds[rng.Next(speeds.Length)], elevation = elevations[rng.Next(elevations.Length)],
                };
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

    /// Straight at the fielders saving the single (inside the circle, not the close catchers).
    private List<float> RingAngles()
    {
        var aims = new List<float>();
        foreach (int j in Shuffled(Fielders(false)))
        {
            float d = new Vector2(f.fielders[j].x - contact.x, f.fielders[j].z - contact.z).magnitude;
            if (d > 8f && d < FieldSets.RingFurthest + 4f) aims.Add(AngleTo(f.fielders[j]));
        }
        return aims;
    }

    /// The middle of every gap of 15 degrees or more between fielders, widest first, shuffled
    /// among the wide ones so the same gap is not always the one.
    private List<float> Gaps()
    {
        var angles = new List<float>();
        foreach (int j in Fielders(false)) angles.Add(AngleTo(f.fielders[j]));
        angles.Add(-FinestAngle - 10f);
        angles.Add(FinestAngle + 10f);
        angles.Sort();
        var gaps = new List<Vector2>();   // (middle, width)
        for (int i = 1; i < angles.Count; i++)
        {
            float w = angles[i] - angles[i - 1];
            if (w >= 15f) gaps.Add(new Vector2(0.5f * (angles[i] + angles[i - 1]), w + 20f * (float)rng.NextDouble()));
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
