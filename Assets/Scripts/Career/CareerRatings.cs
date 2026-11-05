using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The career ratings, computed from the measured balls (BallRecord) - never the other way round:
/// a rating describes how the player actually bats, and nothing in the game reads it to help them.
///
/// How a rating is made:
///   * Only the most recent Window balls that count (BallRecord.Counts: no wides, no dead balls) are
///     used, so a rating is slow, earned skill that still moves as the player improves.
///   * Each ball the rating cares about gets a value 0..1 (how well that ball was handled), the mean
///     is mapped onto 0..99, and the result is shrunk toward a modest prior by how much evidence
///     there is: r = prior + (obs - prior) * n / (n + k). A handful of lucky balls cannot make a
///     player Elite, and a handful of bad ones cannot bury him.
///   * A rating is Unknown ("--") until it has its minimum sample, rather than being guessed.
///   * Contact is always read off the REAL blade (realContact, realQuality), never `quality`: the bat
///     shield widens the bat to help find the ball, and a wider shield must never inflate a rating.
/// </summary>
public static class CareerRatings
{
    /// The ratings are over the most recent Window counting balls (slow, earned skill).
    public const int Window = 300;

    // ---- Shrinkage. The prior is where a rating starts when there is barely any evidence: a little
    // below Solid (45), because an unproven batter should not be told he is solid. k is the number
    // of balls at which the evidence and the prior weigh the same; ratings measured on fewer balls
    // per innings (power: only clean strikes; defence, judgement: only some deliveries) get a
    // smaller k so they reach their true level in about the same number of sessions.
    private const float Prior = 35f;
    private const float KTiming = 12f, KPower = 10f, KBatting = 12f, KDefence = 10f, KJudgement = 10f, KTemperament = 12f;

    // ---- Minimum samples before a rating is shown at all.
    private const int MinTiming = 8, MinPower = 6, MinBatting = 6, MinDefence = 6, MinJudgement = 6, MinTemperament = 12;

    /// An out weighs as this many balls in the vs-pace, vs-spin and defence means: a dismissal costs
    /// the whole innings, so it must hurt far more than one ball played badly.
    private const float OutWeight = 4f;

    /// Metres from middle stump (batter's frame, + = off side). A ball passing further than OffSide
    /// outside off that is not hitting is a "decision" ball (leave it or play it); the defensive
    /// corridor is the channel a good ball is bowled in (on the stumps or just outside off).
    private const float OffSide = 0.3f, CorridorLeg = -0.15f, CorridorOff = 0.35f;

    /// What an edge is worth as contact: something, but hardly timing.
    private const float EdgeValue = 0.15f;

    private const float MaxRating = 99f;

    /// A batter averaging this value on a measure maps to 95 (well into Elite); the scale is linear
    /// from 0. Real clean contact of ~0.75 quality on every attempt is exceptional timing.
    private const float TimingFull = 0.75f, BattingFull = 0.75f, DefenceFull = 0.85f, JudgementFull = 0.85f;
    private const float FullMarks = 95f;

    /// Exit speed (m/s) of the hardest-hit shots: ~12 m/s is a push, ~38 m/s a big hit.
    private const float PowerLow = 12f, PowerHigh = 38f, PowerLowRating = 10f, PowerHighRating = 95f;
    /// Power is the mean of the hardest share of the clean strikes, so defensive blocks (which are
    /// meant to be soft) do not drag it down.
    private const float PowerTopShare = 0.4f;

    /// Running mean with weights (an out weighs more than one ball) and a count of balls.
    private struct Mean
    {
        public float sum, weight;
        public int n;

        public void Add(float v, float w = 1f)
        {
            sum += v * w;
            weight += w;
            n++;
        }

        public float Value => weight > 0f ? sum / weight : 0f;
    }

    /// Everything Compute collects in one pass over the window.
    private sealed class Tally
    {
        public int sample;
        public Mean timing, pace, spin, defence, judgement;
        public Mean pressure, calm;
        public int pressureOuts;
        public readonly List<float> exits = new List<float>(64);
    }

    public static RatingSet Compute(IList<BallRecord> balls)
    {
        var set = new RatingSet();
        if (balls == null) return set;

        var t = new Tally();
        for (int i = WindowStart(balls); i < balls.Count; i++)
        {
            BallRecord r = balls[i];
            if (r != null && r.Counts)
                Collect(t, r);
        }

        set.sample = t.sample;
        set.timing = Shrink(Scale(t.timing.Value, TimingFull), t.timing.n, MinTiming, KTiming);
        set.power = PowerRating(t.exits);
        set.pace = Shrink(Scale(t.pace.Value, BattingFull), t.pace.n, MinBatting, KBatting);
        set.spin = Shrink(Scale(t.spin.Value, BattingFull), t.spin.n, MinBatting, KBatting);
        set.defence = Shrink(Scale(t.defence.Value, DefenceFull), t.defence.n, MinDefence, KDefence);
        set.judgement = Shrink(Scale(t.judgement.Value, JudgementFull), t.judgement.n, MinJudgement, KJudgement);
        set.temperament = TemperamentRating(t);
        return set;
    }

    /// The index of the first ball of the window: walking back from the newest ball until Window
    /// counting balls are behind us (the uncounted ones in between are skipped by Compute anyway).
    private static int WindowStart(IList<BallRecord> balls)
    {
        int counted = 0;
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            BallRecord r = balls[i];
            if (r == null || !r.Counts) continue;
            if (++counted >= Window) return i;
        }
        return 0;
    }

    /// One counting ball into every rating it says something about.
    private static void Collect(Tally t, BallRecord r)
    {
        t.sample++;
        float outWeight = r.Out ? OutWeight : 1f;

        if (Attempted(r))
            t.timing.Add(ContactValue(r));

        if (r.realContact == eRealContact.Clean && r.exitSpeed > 0f)
            t.exits.Add(r.exitSpeed);

        float batting = BattingValue(r);
        if (r.Spin) t.spin.Add(batting, outWeight);
        else t.pace.Add(batting, outWeight);

        float defence = DefenceValue(r);
        if (!float.IsNaN(defence))
            t.defence.Add(defence, outWeight);

        float judgement = JudgementValue(r);
        if (!float.IsNaN(judgement))
            t.judgement.Add(judgement);

        // Temperament compares like with like: the same per-ball value, unweighted, under pressure
        // and not; survival under pressure is counted separately.
        if (r.pressure)
        {
            t.pressure.Add(batting);
            if (r.Out) t.pressureOuts++;
        }
        else
            t.calm.Add(batting);
    }

    /// He played at it: touched it, or swung and was beaten (or found it only with the shield).
    private static bool Attempted(BallRecord r) => r.action == eBallAction.Played || r.action == eBallAction.Missed;

    /// How well it was hit, on the real blade: a clean strike is worth its quality, an edge a little,
    /// a miss (or a "hit" the real blade would have missed) nothing.
    private static float ContactValue(BallRecord r)
    {
        switch (r.realContact)
        {
            case eRealContact.Clean: return Mathf.Clamp01(r.realQuality);
            case eRealContact.Edge: return EdgeValue;
            default: return 0f;
        }
    }

    /// What the runs off a ball are worth, 0..1. Boundaries are worth far more than ones and twos
    /// (that is what a batter is for), but a well-run single is not nothing.
    private static float ScoreValue(BallRecord r)
    {
        int runs = Runs(r);
        if (runs <= 0) return 0f;
        if (runs == 1) return 0.25f;
        if (runs == 2) return 0.4f;
        if (runs == 3) return 0.5f;
        if (runs < 6) return 0.85f;
        return 1f;
    }

    /// <summary>
    /// One ball as batting, for vs Pace and vs Spin (and temperament): did he survive, how well did
    /// he hit it, and what did it bring. Surviving is a base (0.2 for being beaten and still there),
    /// the real-blade contact adds up to 0.35 and the runs up to 0.45. Runs outweigh contact on
    /// purpose: a batter who only survives (leaves what misses, blocks what hits) is Solid, not
    /// Strong, because these ratings are about batting, and batting is scoring. An out is zero, and
    /// weighs as OutWeight balls in the mean. A sensible leave of a ball well outside off is worth a
    /// little less than a sweetly timed block; letting any other ball go (a loose leg-side one, say)
    /// is a chance missed, as Judgement also says; leaving one that was hitting the stumps is as bad
    /// as being out.
    /// </summary>
    private static float BattingValue(BallRecord r)
    {
        if (r.Out) return 0f;
        if (r.action == eBallAction.Left)
        {
            if (r.wouldHitStumps) return 0f;
            return r.lineAtStumps > OffSide ? 0.45f : 0.3f;
        }
        return Mathf.Min(1f, 0.2f + 0.35f * ContactValue(r) + 0.45f * ScoreValue(r));
    }

    /// <summary>
    /// Defence, over "good balls" only: one that would hit the stumps, or a good length / back of a
    /// length ball in the corridor (on the stumps to just outside off). Getting through them not
    /// out and solid (clean on the real blade, better the sweeter) is best; being beaten and
    /// surviving counts for something; an edge is poor (it is how good balls take wickets); out is
    /// zero. A leave is fine when it was missing, but leaving one that was hitting is the worst
    /// thing a defence can do. NaN when the ball was not a good ball.
    /// </summary>
    private static float DefenceValue(BallRecord r)
    {
        bool goodLength = r.lengthBand == eLengthBand.GoodLength || r.lengthBand == eLengthBand.BackOfALength;
        bool inCorridor = r.lineAtStumps >= CorridorLeg && r.lineAtStumps <= CorridorOff;
        if (!r.wouldHitStumps && !(goodLength && inCorridor)) return float.NaN;

        if (r.Out) return 0f;
        if (r.action == eBallAction.Left) return r.wouldHitStumps ? 0f : 0.6f;
        switch (r.realContact)
        {
            case eRealContact.Clean: return 0.6f + 0.4f * Mathf.Clamp01(r.realQuality);
            case eRealContact.Edge: return 0.2f;
            default: return 0.35f;   // beaten (or found only with the shield), and survived
        }
    }

    /// <summary>
    /// Judgement, over the balls that ask a decision of the batter:
    ///   * clearly outside off and missing (line beyond OffSide): leaving it is perfect; playing it
    ///     cleanly is fine; playing and missing is a warning; an edge is how batters get out there.
    ///   * a loose ball (short or full, off the corridor): the right call is to punish it - hit
    ///     cleanly for four or more is full marks.
    ///   * a ball hitting the stumps left alone is the worst call there is (it is bowled).
    /// Other balls hitting the stumps are not a choice (they must be played), so they are left to
    /// Defence. NaN when the ball asked no decision.
    /// </summary>
    private static float JudgementValue(BallRecord r)
    {
        if (r.wouldHitStumps)
            return r.action == eBallAction.Left ? 0f : float.NaN;

        bool outsideOff = r.lineAtStumps > OffSide;
        bool looseLength = r.lengthBand == eLengthBand.Short || r.lengthBand == eLengthBand.Full;
        bool offCorridor = r.lineAtStumps < CorridorLeg || r.lineAtStumps > CorridorOff;
        bool loose = looseLength && offCorridor;
        if (!outsideOff && !loose) return float.NaN;

        if (r.Out) return 0f;
        if (loose && r.realContact == eRealContact.Clean && Runs(r) >= 4) return 1f;
        if (r.action == eBallAction.Left) return outsideOff ? 1f : 0.5f;   // a loose leg-side ball let go: a chance missed
        switch (r.realContact)
        {
            case eRealContact.Clean: return 0.75f;
            case eRealContact.Edge: return 0.1f;
            default: return 0.35f;
        }
    }

    /// Runs off the bat, at least four for a four and six for a six (whatever the ledger wrote).
    private static int Runs(BallRecord r)
    {
        if (r.end == eBallEnd.Six) return Mathf.Max(r.runs, 6);
        if (r.end == eBallEnd.Four) return Mathf.Max(r.runs, 4);
        return r.runs;
    }

    /// <summary>
    /// Power: how hard he hits it when he means to, from the real-blade clean strikes only (an edge
    /// that flies is not power). The mean of the hardest PowerTopShare of them, mapped linearly from
    /// PowerLow m/s -> 10 to PowerHigh m/s -> 95.
    /// </summary>
    private static float PowerRating(List<float> exits)
    {
        int n = exits.Count;
        if (n < MinPower) return RatingSet.Unknown;
        exits.Sort();
        int top = Mathf.Max(1, Mathf.CeilToInt(n * PowerTopShare));
        float sum = 0f;
        for (int i = n - top; i < n; i++)
            sum += exits[i];
        float mean = sum / top;
        float obs = PowerLowRating + (mean - PowerLow) / (PowerHigh - PowerLow) * (PowerHighRating - PowerLowRating);
        return Shrink(obs, n, MinPower, KPower);
    }

    /// <summary>
    /// Temperament: only from balls faced under pressure (matches). Two halves: how he batted under
    /// pressure against how he bats otherwise (holding his level is a pass, raising it is the real
    /// thing), and whether he survived (an out every six pressure balls or more is a zero there).
    /// With no calm balls to compare against, the comparison is taken as level. Unknown until
    /// MinTemperament pressure balls, so in the nets-only phase 1 it is always "--".
    /// </summary>
    private static float TemperamentRating(Tally t)
    {
        int n = t.pressure.n;
        if (n < MinTemperament) return RatingSet.Unknown;
        float baseline = t.calm.n > 0 ? t.calm.Value : t.pressure.Value;
        float relative = Mathf.Clamp01(0.5f + (t.pressure.Value - baseline) * 2f);
        float survival = Mathf.Clamp01(1f - (float)t.pressureOuts / n * 6f);
        float obs = MaxRating * (0.6f * relative + 0.4f * survival);
        return Shrink(obs, n, MinTemperament, KTemperament);
    }

    /// A mean 0..1 onto the rating scale, `full` mapping to FullMarks.
    private static float Scale(float mean, float full) => Mathf.Clamp(mean / full * FullMarks, 0f, MaxRating);

    /// Unknown below the minimum sample, else the observation shrunk toward the prior, as a whole
    /// number 0..99 (ratings are shown and compared as whole numbers).
    private static float Shrink(float obs, int n, int min, float k)
    {
        if (n < min) return RatingSet.Unknown;
        obs = Mathf.Clamp(obs, 0f, MaxRating);
        return Mathf.Round(Mathf.Clamp(Prior + (obs - Prior) * n / (n + k), 0f, MaxRating));
    }

    /// "--" (unknown), "Raw", "Developing", "Solid", "Strong", "Elite".
    public static string Grade(float rating)
    {
        if (!RatingSet.Known(rating)) return "--";
        if (rating < 30f) return "Raw";
        if (rating < 45f) return "Developing";
        if (rating < 60f) return "Solid";
        if (rating < 75f) return "Strong";
        return "Elite";
    }

    /// Index (RatingSet order) of the strongest / weakest known rating, -1 when none is known.
    /// On a tie, the first in RatingSet order.
    public static int Strongest(RatingSet r) => Extreme(r, true);
    public static int Weakest(RatingSet r) => Extreme(r, false);

    private static int Extreme(RatingSet r, bool highest)
    {
        if (r == null) return -1;
        int best = -1;
        for (int i = 0; i < RatingSet.Count; i++)
        {
            float v = r[i];
            if (!RatingSet.Known(v)) continue;
            if (best < 0 || (highest ? v > r[best] : v < r[best]))
                best = i;
        }
        return best;
    }

    /// The weakest known rating other than `skip` (first in RatingSet order on a tie), -1 when no
    /// other rating is known. Same as Weakest unless every known rating is equal.
    private static int WeakestBesides(RatingSet r, int skip)
    {
        int weak = -1;
        for (int i = 0; i < RatingSet.Count; i++)
        {
            if (i == skip || !RatingSet.Known(r[i])) continue;
            if (weak < 0 || r[i] < r[weak])
                weak = i;
        }
        return weak;
    }

    // The coach's words, RatingSet order. Plain ASCII (the TMP default font is Latin-1).
    private static readonly string[] StrengthLines =
    {
        "Your timing is the real deal.",
        "You hit the ball a long way.",
        "You look at home against the quicks.",
        "You read the spinners well.",
        "Your defence takes some getting through.",
        "You pick the right balls to leave and the right ones to hit.",
        "You keep your head when it matters.",
    };

    private static readonly string[] EarlyStrengthLines =
    {
        "Early days, but your timing is the best part of your game.",
        "Early days, but your power is the best part of your game.",
        "Early days, but you look most at ease against pace.",
        "Early days, but you look most at ease against spin.",
        "Early days, but your defence is the best part of your game.",
        "Early days, but your judgement is the best part of your game.",
        "Early days, but your temperament is the best part of your game.",
    };

    private static readonly string[] WorkLines =
    {
        "Middling it more often is where the work is.",
        "Putting more into your shots is where the work is.",
        "Playing the quicks is where the work is.",
        "Playing spin is where the work is.",
        "Keeping out the good balls is where the work is.",
        "Leaving outside off is where the work is.",
        "Staying calm under pressure is where the work is.",
    };

    /// What each rating is about, for "keep sharpening your ..." (RatingSet order).
    private static readonly string[] Nouns =
    {
        "timing", "power", "batting against pace", "batting against spin", "defence", "judgement", "temperament",
    };

    /// Below this, the strongest rating is praised as "early days" rather than as a real strength.
    private const float RealStrength = 45f;
    /// At or above this (Strong), the weakest rating is not "where the work is": it is already good.
    private const float NoRealWeakness = 60f;
    /// Ratings this close together are an even game, not a strength and a weakness.
    private const float EvenSpread = 4f;

    /// One line from the coach on the ratings ("Your timing is the real deal. Leaving outside off is
    /// where the work is."), naming the strongest and the weakest. Never empty.
    public static string Verdict(RatingSet r)
    {
        int strong = Strongest(r);
        if (strong < 0)
            return "Face a few more balls and the coach will have a read on your game.";

        string praise = r[strong] >= RealStrength ? StrengthLines[strong] : EarlyStrengthLines[strong];
        // The weakest is looked for among the others, so equal ratings (pace 50, spin 50) still get
        // a strength and a weakness rather than "only one thing is known".
        int weak = WeakestBesides(r, strong);
        if (weak < 0)
            return praise + " Face a few more balls so the coach can see the rest of your game.";
        // Close together AND good is a balanced game; close together and low (14, 15, 17) is not
        // "no weak spot", so it falls through to the praise plus the work.
        if (r[strong] - r[weak] < EvenSpread && r[weak] >= RealStrength)
            return "A well-balanced game so far, with no real weak spot. Keep it going.";
        if (r[weak] >= NoRealWeakness)
            return praise + " Nothing is holding you back; keep sharpening your " + Nouns[weak] + ".";
        return praise + " " + WorkLines[weak];
    }
}
