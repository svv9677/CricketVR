using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// One field being set: a plan's nine positions placed on the ground, then changed for the
/// batter (FieldCaptain) - catchers in, men back, men moved to where the runs go. Every change is
/// kept only if the field still keeps the fielding limits (FieldSets.Valid). Pure, so it can be
/// tested against a made-up batter.
/// </summary>
public class FieldPlan
{
    public readonly List<Vector3> places = new List<Vector3>();
    public readonly List<FieldSets.Kind> kinds = new List<FieldSets.Kind>();
    public readonly List<string> names = new List<string>();
    private readonly FieldSets.Ground g;
    private readonly BatterBook.Record record;


    public FieldPlan(FieldSets.Position[] positions, FieldSets.Ground ground, BatterBook.Record batter)
    {
        g = ground;
        record = batter;
        foreach (FieldSets.Position p in positions)
            Add(p);
        Enforce();
    }

    /// The plan's field brought within the limits it is being set under: men up from the rope in
    /// the powerplay (the quietest first, into the ring at their own angle), a man behind square on
    /// the leg side brought square, a sixth leg-side man moved to the off side.
    private void Enforce()
    {
        for (int guard = 0; guard < 12; guard++)
        {
            FieldSets.Count(places, g, out int outside, out int behindLeg, out int leg);
            if (outside > g.MaxOutsideNow)
            {
                int i = QuietestWhere(j => !FieldSets.InsideRing(places[j], g));
                if (i < 0) break;
                Move(i, new FieldSets.Position(UpName(names[i]), FieldSets.Kind.Ring, FieldSets.AngleOf(places[i], g)));
            }
            else if (behindLeg > FieldSets.MaxBehindSquareLeg)
            {
                int i = QuietestWhere(j => FieldSets.BehindSquareLeg(places[j], g));
                if (i < 0) break;
                Move(i, new FieldSets.Position("Square leg", FieldSets.Kind.Ring, -85f));
            }
            else if (leg > FieldSets.MaxLegSide)
            {
                int i = QuietestWhere(j => FieldSets.LegSide(places[j], g) && kinds[j] == FieldSets.Kind.Ring);
                if (i < 0) break;
                Move(i, new FieldSets.Position("Cover point", FieldSets.Kind.Ring, 72f));
            }
            else
                break;
        }
    }

    private static string UpName(string deep) =>
        deep.StartsWith("Deep ") ? char.ToUpper(deep[5]) + deep.Substring(6) : deep == "Long-off" ? "Mid-off" :
        deep == "Long-on" ? "Mid-on" : deep == "Third man" ? "Short third man" : deep == "Fine leg" ? "Short fine leg" : deep + " (up)";

    /// Put fielder `i` at `p` - or, if someone already stands there, at the nearest angle either
    /// side of it with room (long-off brought up for the powerplay found mid-off 1 m away).
    private void Move(int i, FieldSets.Position p)
    {
        foreach (float shift in Shifts)
        {
            var q = new FieldSets.Position(p.name, p.kind, p.angle + shift, p.distance);
            places[i] = FieldSets.Place(q, g);
            if (!Crowded(i, 4f))
            {
                kinds[i] = p.kind;
                names[i] = p.name;
                return;
            }
        }
        places[i] = FieldSets.Place(p, g);
        kinds[i] = p.kind;
        names[i] = p.name;
    }

    private static readonly float[] Shifts = { 0f, 12f, -12f, 24f, -24f, 36f, -36f };

    /// Of the fielders matching `which` (never a catcher), the one in the quietest sector.
    private int QuietestWhere(System.Func<int, bool> which)
    {
        int best = -1;
        float least = float.MaxValue;
        for (int i = 0; i < places.Count; i++)
            if (!Catcher(i) && which(i) && RunsIn(i) < least) { least = RunsIn(i); best = i; }
        return best;
    }

    private void Add(FieldSets.Position p)
    {
        places.Add(FieldSets.Place(p, g));
        kinds.Add(p.kind);
        names.Add(p.name);
    }

    private int SectorOf(int i) => BatterBook.Sector(FieldSets.AngleOf(places[i], g));
    private float RunsIn(int i) => record.wheel[SectorOf(i)];
    private bool Catcher(int i) => kinds[i] == FieldSets.Kind.Slip || kinds[i] == FieldSets.Kind.Close;

    /// Put `p` in place of fielder `i` if the field stays legal; true if it did.
    private bool TryReplace(int i, FieldSets.Position p)
    {
        Vector3 was = places[i];
        places[i] = FieldSets.Place(p, g);
        if (!FieldSets.Valid(places, g) || Crowded(i))
        {
            places[i] = was;
            return false;
        }
        kinds[i] = p.kind;
        names[i] = p.name;
        return true;
    }

    /// Two men within `apart` metres of each other (slips stand 1.8 m apart).
    private bool Crowded(int i, float apart = 1.5f)
    {
        for (int j = 0; j < places.Count; j++)
            if (j != i && Vector3.Distance(places[i], places[j]) < apart)
                return true;
        return false;
    }

    /// Up to `max` catchers in, from `catchers` in order, each in place of the fielder in the
    /// quietest part of the batter's wagon wheel (a man on the rope first).
    public void AddCatchers(FieldSets.Position[] catchers, int max)
    {
        int added = 0;
        foreach (FieldSets.Position c in catchers)
        {
            if (added >= max) break;
            if (names.Contains(c.name)) continue;
            int i = Quietest(preferDeep: true, notSector: -1);
            if (i >= 0 && TryReplace(i, c)) added++;
        }
    }

    /// Up to `max` men in the ring sent back to the rope at their own angle, where the batter
    /// scores most.
    public void PushBack(int max)
    {
        int moved = 0;
        foreach (int hot in HotSectors(0.5f))
        {
            if (moved >= max) break;
            for (int i = 0; i < places.Count; i++)
                if (kinds[i] == FieldSets.Kind.Ring && SectorOf(i) == hot &&
                    TryReplace(i, new FieldSets.Position("Deep " + names[i].ToLower(), FieldSets.Kind.Deep, FieldSets.AngleOf(places[i], g))))
                {
                    moved++;
                    break;
                }
        }
    }

    /// Up to `max` men moved into the batter's scoring areas the field leaves open.
    public void Adapt(int max, float hotRuns)
    {
        int moves = 0;
        foreach (int hot in HotSectors(hotRuns))
        {
            if (moves >= max) break;
            bool deep = record.wheelBoundaries[hot] >= 0.5f * record.wheel[hot];
            if (Covered(hot, deep)) continue;
            int from = Quietest(preferDeep: false, notSector: hot);
            if (from < 0) continue;
            var p = new FieldSets.Position(deep ? "Sweeper" : "Saving one", deep ? FieldSets.Kind.Deep : FieldSets.Kind.Ring,
                                           BatterBook.SectorCentre(hot), 0f);
            if (TryReplace(from, p)) moves++;
        }
    }

    /// Sectors with at least `minRuns` (faded) runs, most first.
    private List<int> HotSectors(float minRuns)
    {
        var order = new List<int>();
        for (int s = 0; s < BatterBook.Sectors; s++)
            if (record.wheel[s] >= minRuns) order.Add(s);
        order.Sort((a, b) => record.wheel[b].CompareTo(record.wheel[a]));
        return order;
    }

    private bool Covered(int sector, bool deep)
    {
        for (int i = 0; i < places.Count; i++)
            if (SectorOf(i) == sector && (kinds[i] == FieldSets.Kind.Deep) == deep)
                return true;
        return false;
    }

    /// The fielder doing least - in the sector the batter scores least in - never a catcher.
    private int Quietest(bool preferDeep, int notSector)
    {
        int best = -1;
        float least = float.MaxValue;
        for (int i = 0; i < places.Count; i++)
        {
            if (Catcher(i) || SectorOf(i) == notSector) continue;
            float score = RunsIn(i) - (preferDeep && kinds[i] == FieldSets.Kind.Deep ? 1000f : 0f);
            if (score < least) { least = score; best = i; }
        }
        return best;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < names.Count; i++)
            sb.Append(i == 0 ? "" : ", ").Append(names[i]);
        return sb.ToString();
    }
}
