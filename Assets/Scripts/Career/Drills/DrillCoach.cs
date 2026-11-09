using System.Collections.Generic;

/// <summary>
/// The coach's side of the drills: which one to do (the drill that feeds the weakest known
/// rating), the attempt counter (Start: bumped as an attempt begins, so quitting after three balls
/// and starting again gets a new order, DrillDefs.SeedFor), and recording a finished drill in the
/// career's DrillBook.
///
/// The training slot: one session between fixtures counts as the side's training before the next
/// match - the first drill finished while the season's next fixture has not had one. Using it
/// posts the coach's verdict to the inbox and counts a session for the season. It gives nothing in
/// the game: the ratings only ever measure the balls (CareerRatings), drill or not. Pure over the
/// profile (DrillTests).
/// </summary>
public static class DrillCoach
{
    private const string Coach = "Coach";

    /// The drill for each rating (RatingSet order); Temperament is matches only, so none. Power and
    /// Defence have their own drills since 2026-11-06 (power hitting, soft hands).
    private static readonly eDrill[] ForRating =
    {
        eDrill.Gaps, eDrill.PowerHitting, eDrill.Yorkers, eDrill.Spin, eDrill.SoftHands, eDrill.LeaveOutsideOff, eDrill.None,
    };

    /// The drill that works on the weakest known rating (Temperament skipped: no drill measures
    /// it); Yorkers when nothing is known yet. On a tie, the first in RatingSet order.
    public static eDrill Recommend(RatingSet r)
    {
        int weak = -1;
        if (r != null)
            for (int i = 0; i < RatingSet.Count; i++)
            {
                if (ForRating[i] == eDrill.None || !RatingSet.Known(r[i])) continue;
                if (weak < 0 || r[i] < r[weak])
                    weak = i;
            }
        return weak < 0 ? eDrill.Yorkers : ForRating[weak];
    }

    /// A new attempt begins: ++started, returned (the attempt number the balls are stamped with).
    public static int Start(CareerProfile p, eDrill d)
    {
        if (p == null || d == eDrill.None)
            return 0;
        p.drills ??= new DrillBook();
        return ++p.drills.For(d).started;
    }

    /// The training slot before the next fixture is still open: there is one, and no drill has
    /// been recorded against it yet.
    public static bool SlotOpen(CareerProfile p)
    {
        Fixture next = CareerProgress.NextFixture(p);
        if (next == null)
            return false;
        DrillBook b = p.drills;
        return b == null || b.slotSeason != p.season.number || b.slotRound != next.round;
    }

    /// The drill is done: attempts, last and best in its record (and res.best, res.newBest); if
    /// the training slot is open, it is used - the session counted for the season and the coach's
    /// verdict posted to the inbox, dated `date`.
    public static void Record(CareerProfile p, DrillResult res, string date)
    {
        if (p == null || res == null || res.drill == eDrill.None)
            return;
        p.drills ??= new DrillBook();
        DrillRecord rec = p.drills.For(res.drill);
        rec.attempts++;
        rec.started = System.Math.Max(rec.started, rec.attempts);
        rec.last = res.score;
        res.newBest = res.score > rec.best;
        if (res.newBest)
            rec.best = res.score;
        res.best = rec.best;

        if (!SlotOpen(p))
            return;
        UseSlot(p);
        DrillDef def = DrillDefs.For(res.drill);
        p.inbox ??= new List<CareerMessage>();
        p.AddMessage(new CareerMessage
        {
            kind = eMessageKind.Coach,
            from = Coach,
            title = DrillText.MessageTitle(res),
            body = DrillText.Verdict(def, res),
            date = date ?? "",
        });
    }

    /// The slot before the next fixture is taken, and one more session this season.
    private static void UseSlot(CareerProfile p)
    {
        DrillBook b = p.drills;
        Fixture next = CareerProgress.NextFixture(p);
        int season = p.season.number;
        b.slotSeason = season;
        b.slotRound = next.round;
        if (b.sessionsSeason != season)
        {
            b.sessionsSeason = season;
            b.sessions = 0;
        }
        b.sessions++;
    }
}
