using System;
using System.Collections.Generic;

/// One drill's history in a career: attempts begun and finished, best and last score (0..100).
[Serializable]
public class DrillRecord
{
    public eDrill drill;
    public int started, attempts, best, last;
}

/// <summary>The career's drills: a record per drill, and the training slot between fixtures (the
/// season and round it was last used in, sessions this season).</summary>
[Serializable]
public class DrillBook
{
    public List<DrillRecord> records = new List<DrillRecord>();
    public int slotSeason = -1, slotRound = -1, sessionsSeason = -1, sessions;

    /// The record for `d`, made if missing.
    public DrillRecord For(eDrill d)
    {
        records ??= new List<DrillRecord>();
        DrillRecord r = records.Find(x => x.drill == d);
        if (r == null)
        {
            r = new DrillRecord { drill = d };
            records.Add(r);
        }
        return r;
    }
}
