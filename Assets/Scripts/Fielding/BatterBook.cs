using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the captain knows about the batter, kept apart for pace and for spin (a batter who
/// murders the quicks can be all at sea against spin):
///
///   * by delivery - line zone x length band: balls, runs, false shots (an edge, a mistimed
///     hit, played and missed at one on the stumps or just outside) and wickets;
///   * by direction - a wagon wheel of eight sectors (runs, and how many of them came as
///     boundaries), fading by Fade a ball so it follows how they bat now, not twenty balls ago;
///   * how the innings is going - balls since a wicket, dots in a row, the last dozen balls.
///
/// Pure data: FieldCaptain fills it from the score each ball and asks it questions.
/// </summary>
public class BatterBook
{
    public const int LineZones = 4, Bands = 5, Sectors = 8;
    /// Line zones at the stumps, off side positive: wide outside off, the corridor, the stumps,
    /// leg side.
    public static void ZoneRange(int zone, out float lo, out float hi)
    {
        switch (zone)
        {
            case 0: lo = 0.5f; hi = 2f; break;
            case 1: lo = 0.1f; hi = 0.5f; break;
            case 2: lo = -0.1f; hi = 0.1f; break;
            default: lo = -2f; hi = -0.1f; break;
        }
    }
    public static int LineZone(float line) => line >= 0.5f ? 0 : line >= 0.1f ? 1 : line >= -0.1f ? 2 : 3;

    public class Record
    {
        public readonly float[,] balls = new float[LineZones, Bands], runs = new float[LineZones, Bands];
        public readonly float[,] falseShots = new float[LineZones, Bands], wickets = new float[LineZones, Bands];
        public readonly float[] wheel = new float[Sectors], wheelBoundaries = new float[Sectors];
        public float totalBalls, totalRuns, totalFalse, totalWickets;
    }

    /// Each ball, the wagon wheel keeps this much of what it had.
    public const float Fade = 0.9f;
    /// Prior balls each zone is assumed to have gone at the batter's overall rate: a zone seen
    /// once does not decide a plan.
    private const float Prior = 3f;

    private readonly Record pace = new Record(), spin = new Record();
    private readonly Queue<int> recent = new Queue<int>();
    public const int RecentBalls = 12;

    public int BallsSinceWicket { get; private set; }
    public int DotsInARow { get; private set; }

    public Record For(eSwingType bowler) => FieldSets.IsSpin(bowler) ? spin : pace;

    public void NewBatter()
    {
        BallsSinceWicket = 0;
        DotsInARow = 0;
        recent.Clear();
    }

    /// One ball bowled to the batter.
    /// <param name="line">Line at the stumps as bowled, off side positive (batter's frame).</param>
    /// <param name="shotAngle">Direction off the bat (FieldSets angle), NaN if not hit.</param>
    public void Add(eSwingType bowler, float line, eLengthBand band, int runs, bool legal, bool falseShot,
                    bool wicket, float shotAngle)
    {
        Record r = For(bowler);
        for (int s = 0; s < Sectors; s++)
        {
            r.wheel[s] *= Fade;
            r.wheelBoundaries[s] *= Fade;
        }
        int z = LineZone(line), b = Mathf.Clamp((int)band, 0, Bands - 1);
        r.balls[z, b]++; r.runs[z, b] += runs; r.totalBalls++; r.totalRuns += runs;
        if (falseShot) { r.falseShots[z, b]++; r.totalFalse++; }
        if (wicket) { r.wickets[z, b]++; r.totalWickets++; }
        if (runs > 0 && !float.IsNaN(shotAngle))
        {
            int s = Sector(shotAngle);
            r.wheel[s] += runs;
            if (runs >= 4) r.wheelBoundaries[s] += runs;
        }
        if (wicket) { NewBatter(); return; }
        if (!legal) return;
        BallsSinceWicket++;
        DotsInARow = runs == 0 ? DotsInARow + 1 : 0;
        recent.Enqueue(runs);
        while (recent.Count > RecentBalls) recent.Dequeue();
    }

    /// Over the last RecentBalls: strike rate and boundaries.
    public void Recent(out float strikeRate, out int boundaries, out int balls)
    {
        int runs = 0;
        boundaries = 0;
        foreach (int x in recent) { runs += x; if (x >= 4) boundaries++; }
        balls = recent.Count;
        strikeRate = balls > 0 ? 100f * runs / balls : 0f;
    }

    /// Over the last RecentBalls: how many were ones and twos - a batter working the ring.
    public int RecentSingles()
    {
        int n = 0;
        foreach (int x in recent) if (x == 1 || x == 2) n++;
        return n;
    }

    /// How a plan should go against this batter: expected runs a ball, less what its false shots
    /// and wickets are worth. Lower is better for the bowler.
    public float Cost(float[,] share, eSwingType bowler)
    {
        Record r = For(bowler);
        float rate = (r.totalRuns + Prior * 1f) / (r.totalBalls + Prior);
        float falseRate = (r.totalFalse + Prior * 0.1f) / (r.totalBalls + Prior);
        float wicketRate = (r.totalWickets + Prior * 0.02f) / (r.totalBalls + Prior);
        float cost = 0f;
        for (int z = 0; z < LineZones; z++)
            for (int b = 0; b < Bands; b++)
            {
                if (share[z, b] <= 0f) continue;
                float n = r.balls[z, b] + Prior;
                float runs = (r.runs[z, b] + Prior * rate) / n;
                float f = (r.falseShots[z, b] + Prior * falseRate) / n;
                float w = (r.wickets[z, b] + Prior * wicketRate) / n;
                cost += share[z, b] * (runs - 1.5f * f - 8f * w);
            }
        return cost;
    }

    public static int Sector(float angle) => Mathf.FloorToInt(Mathf.Repeat(angle + 180f, 360f) / (360f / Sectors)) % Sectors;
    public static float SectorCentre(int s) => -180f + (s + 0.5f) * (360f / Sectors);
}
