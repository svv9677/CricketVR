using System;
using System.Collections.Generic;

/// <summary>
/// The seven measured ratings, 0..99 (CareerRatings). A rating not yet measured (too few balls of
/// its kind) is Unknown (-1) and shown as "--" rather than guessed.
/// </summary>
[Serializable]
public class RatingSet
{
    public const float Unknown = -1f;

    public float timing = Unknown, power = Unknown, pace = Unknown, spin = Unknown,
                 defence = Unknown, judgement = Unknown, temperament = Unknown;
    /// Balls the set was computed from.
    public int sample;

    public static readonly string[] Names = { "Timing", "Power", "vs Pace", "vs Spin", "Defence", "Judgement", "Temperament" };

    public float this[int i]
    {
        get => i switch { 0 => timing, 1 => power, 2 => pace, 3 => spin, 4 => defence, 5 => judgement, _ => temperament };
        set
        {
            switch (i)
            {
                case 0: timing = value; break;
                case 1: power = value; break;
                case 2: pace = value; break;
                case 3: spin = value; break;
                case 4: defence = value; break;
                case 5: judgement = value; break;
                default: temperament = value; break;
            }
        }
    }

    public const int Count = 7;

    public static bool Known(float r) => r >= 0f;

    /// The mean of the known ratings (Unknown when none is known).
    public float Overall
    {
        get
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < Count; i++)
                if (Known(this[i])) { sum += this[i]; n++; }
            return n > 0 ? sum / n : Unknown;
        }
    }

    public RatingSet Clone() => (RatingSet)MemberwiseClone();
}

/// How the player looks: on the player card, the scoreboard and (later phases) the kit.
[Serializable]
public class CareerAppearance
{
    /// Index into Appearance.SkinTones / KitColours.
    public int skin = 2, kit = 0;
    /// Shirt number, 1..99.
    public int number = 18;
}

/// Running totals over the whole career (the balls list only keeps the recent window).
[Serializable]
public class CareerTotals
{
    public int balls, runs, fours, sixes, outs, trials;
}

/// <summary>
/// One career, as saved in its slot (CareerSaves): who the player is, the balls measured (the most
/// recent CareerProfile.MaxBalls), the ratings, and the totals. Later phases add the ladder, season,
/// teams and records; bump `version` and migrate in CareerSaves.Load when the shape changes.
/// </summary>
[Serializable]
public class CareerProfile
{
    public const int CurrentVersion = 1;
    /// Balls kept for recomputing the ratings: the rating window plus some history.
    public const int MaxBalls = 400;

    public int version = CurrentVersion;
    public string name = "";
    /// Nations.All code ("IND", "AUS", ...).
    public string nation = "IND";
    public eBattingStyle hand = eBattingStyle.RightHanded;
    public CareerAppearance look = new CareerAppearance();
    /// Local date the career was started ("2026-11-05").
    public string created = "";

    /// The 30-ball trial in the nets has been completed (its ratings are the starting ratings).
    public bool trialDone;
    /// The ratings now (from `balls`), and as the trial set them.
    public RatingSet ratings = new RatingSet();
    public RatingSet trialRatings = new RatingSet();

    public List<BallRecord> balls = new List<BallRecord>();
    public CareerTotals totals = new CareerTotals();

    /// Add a measured ball: trim to MaxBalls (oldest first) and add it to the totals.
    public void Add(BallRecord r)
    {
        balls.Add(r);
        while (balls.Count > MaxBalls)
            balls.RemoveAt(0);
        if (!r.Counts)
            return;
        totals.balls++;
        totals.runs += r.runs;
        if (r.end == eBallEnd.Four) totals.fours++;
        if (r.end == eBallEnd.Six) totals.sixes++;
        if (r.Out) totals.outs++;
    }

    /// "Right-hand bat" / "Left-hand bat".
    public string HandName => hand == eBattingStyle.LeftHanded ? "Left-hand bat" : "Right-hand bat";
}
