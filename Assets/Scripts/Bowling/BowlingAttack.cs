using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The bowling side's choices, ball by ball and over by over:
///   * Next - this ball: the over plan (OverPlanner) says stock ball or variation, then MakeDelivery
///     turns that into the line, length, pace and movement BallDelivery bowls.
///   * PickNextBowler - the captain's bowling change at the end of an over: never the same bowler
///     twice running, nobody over his quota (a fifth of the overs, as in a real match), seamers in
///     the powerplay and at the death, spinners through the middle, and a bowler who is on keeps
///     his spell going from his end for a few overs. It replaces bowling the eight in a fixed
///     rotation, which made every innings the same order of types.
/// </summary>
public static class BowlingAttack
{
    private static OverPlanner plan;
    private static Bowler planBowler;
    private static int planOver = -1;
    private static bool lastWasBoundary;
    private static bool subscribed;
    /// Who bowled each over so far, for spells (alternate overs from the same end).
    private static readonly List<Bowler> overBowlers = new List<Bowler>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        plan = null; planBowler = null; planOver = -1; lastWasBoundary = false;
        ForceShape = null;
        overBowlers.Clear();
        if (subscribed) HUD.BallScored -= OnBallScored;
        subscribed = false;
    }

    private static void OnBallScored(BallOutcome o) =>
        lastWasBoundary = o.Signal == UmpireSignal.Four || o.Signal == UmpireSignal.Six;

    /// A new innings: nobody has bowled.
    public static void NewInnings()
    {
        plan = null; planBowler = null; planOver = -1; lastWasBoundary = false;
        overBowlers.Clear();
    }

    // ---- This ball ---------------------------------------------------------------------------

    /// Debug and editor checks (FieldingFilm, grip captures): bowl this kind and delivery next,
    /// once, whoever is on.
    public static eBowlerKind? ForceKind;
    public static eDelivery? ForceDelivery;
    /// The career's drills: the length band and line window for the next ball, once (DeliveryShape).
    public static DeliveryShape? ForceShape;

    public static BowlingParams Next(HUD hud, BowlingProfileManager profiles)
    {
        DeliveryShape? shape = ForceShape;
        ForceShape = null;
        if (ForceDelivery.HasValue)
        {
            eDelivery forced = ForceDelivery.Value;
            eBowlerKind fk = ForceKind ?? hud.CurrentBowler.Kind;
            ForceDelivery = null;
            ForceKind = null;
            return Shape(MakeDelivery(fk, forced, profiles), shape, () => Random.value);
        }
        if (!subscribed) { HUD.BallScored += OnBallScored; subscribed = true; }
        Bowler bowler = hud.CurrentBowler;
        if (plan == null || planBowler != bowler || planOver != hud.Overs)
        {
            plan = new OverPlanner(bowler.Kind, BowlerRepertoire.VariationsFor(MatchSetup.Bowling), () => Random.value);
            planBowler = bowler;
            planOver = hud.Overs;
        }
        BowlingPlan active = BowlingPlans.Fits(BowlingPlans.Active, bowler.Type) ? BowlingPlans.Active : null;
        float slowerBoost = active != null ? active.slowerChance * 2f : 0f;
        eDelivery d = plan.Next(hud.Balls, lastWasBoundary, slowerBoost, () => Random.value);
        lastWasBoundary = false;
        return Shape(MakeDelivery(bowler.Kind, d, profiles), shape, () => Random.value);
    }

    /// A drill's length and line over the bowler's own ball: the same family's bands
    /// (p.swingType, the profile's), the same clamps; never the over-the-head wide (a drill ball
    /// must be playable). The pace and the movement stay the bowler's. No shape: p as it was.
    /// Pure but for rand01.
    public static BowlingParams Shape(BowlingParams p, DeliveryShape? s, System.Func<float> rand01)
    {
        if (p == null || !s.HasValue)
            return p;
        DeliveryPicker.Pick pick = DeliveryShape.Pick(s.Value, p.swingType, rand01);
        p.length = pick.pitchX;
        p.lengthBand = pick.band;
        p.speedZ = pick.line;
        p.overHead = false;
        return p;
    }

    /// Release speed range by kind, in the configs' speed units (m/s x ConfigImpulseMass): a quick
    /// 140-155 km/h, a medium-pacer 124-140, the spinners 68-85.
    public static Vector2 SpeedRange(eBowlerKind k)
    {
        switch (k)
        {
            case eBowlerKind.Fast: return new Vector2(Constants.paceCfg[0], Constants.paceCfg[1]);
            case eBowlerKind.Medium: return new Vector2(Constants.inSwingCfg[0], Constants.inSwingCfg[1]);
            case eBowlerKind.LegSpin: return new Vector2(Constants.legSpinCfg[0], Constants.legSpinCfg[1]);
            default: return new Vector2(Constants.offSpinCfg[0], Constants.offSpinCfg[1]);
        }
    }

    /// How a delivery is bowled. The profile for its physics family picks the line and length, the
    /// plan and the level as for any ball (BowlingProfile.GetRandomDelivery); the delivery then
    /// sets its own pace, swing, cut, turn, dip and bounce.
    public static BowlingParams MakeDelivery(eBowlerKind kind, eDelivery d, BowlingProfileManager profiles)
    {
        eSwingType family = Family(kind, d);
        BowlingProfile profile = profiles.GetProfile(family);
        BowlingParams p = profile.GetRandomDelivery(false);
        p.kind = kind;
        p.delivery = d;
        // Pace: this kind of bowler's, not the profile's (a medium-pacer's inswinger is a medium
        // pacer's speed; a quick's is a quick's).
        Vector2 range = SpeedRange(kind);
        float profileMid = 0.5f * (profile.minX + profile.maxX);
        if (profileMid > 0.01f)
            p.speedX *= Random.Range(range.x, range.y) / profileMid;
        float cut = kind == eBowlerKind.Fast ? Random.Range(0.3f, 0.55f) : Random.Range(0.25f, 0.45f);
        switch (d)
        {
            case eDelivery.InSwinger:
            case eDelivery.OutSwinger:
                // Harder to swing it at 145 than at 130.
                p.swing *= kind == eBowlerKind.Fast ? 0.8f : 1f;
                p.speedX *= 0.98f;
                break;
            case eDelivery.OffCutter:     // into the right-hander off the seam
                p.pitchTurn = -cut;
                p.speedX *= Random.Range(0.88f, 0.94f);
                p.bounceScale = 0.96f;
                break;
            case eDelivery.LegCutter:     // away from him
                p.pitchTurn = cut;
                p.speedX *= Random.Range(0.88f, 0.94f);
                p.bounceScale = 0.96f;
                break;
            case eDelivery.SlowerBall:
                p.speedX *= Random.Range(0.72f, 0.8f);
                p.pitchTurn *= 0.5f;
                p.bounceScale = 0.95f;
                break;
            case eDelivery.TopSpinner:    // over the top: dips, kicks, barely turns
                p.pitchTurn = Random.Range(-0.06f, 0.06f);
                p.swing *= 0.5f;
                p.dip = 1.4f;
                p.bounceScale = 1.15f;
                break;
            case eDelivery.Slider:        // quicker, straight on, skids through low
                p.speedX *= Random.Range(1.10f, 1.16f);
                p.pitchTurn = Random.Range(-0.05f, 0.05f);
                p.bounceScale = kind == eBowlerKind.LegSpin ? 0.78f : 0.82f;
                break;
            case eDelivery.Flighted:      // slower, loops, turns more
                p.speedX *= Random.Range(0.82f, 0.88f);
                p.pitchTurn = Mathf.Min(1f, Mathf.Abs(p.pitchTurn) * 1.35f) * Mathf.Sign(p.pitchTurn == 0f ? 1f : p.pitchTurn);
                p.dip = 0.5f;
                break;
            case eDelivery.Googly:        // the leg-spinner's that turns in
                p.pitchTurn = -Mathf.Abs(p.pitchTurn) * 0.85f;
                break;
            case eDelivery.Carrom:        // flicked off the middle finger: turns away like a leg-break
                p.pitchTurn = -Mathf.Abs(p.pitchTurn) * 0.9f;
                p.swing *= 0.5f;
                break;
        }
        // The mystery spinner's pace is part of the mystery.
        if (kind == eBowlerKind.Mystery)
            p.speedX *= Random.Range(0.85f, 1.18f);
        return p;
    }

    /// The physics family a delivery is bowled with: what BallFlight, the length bands and the plans
    /// expect (seam: Pace / InSwing / OutSwing; spin: OffSpin / LegSpin).
    public static eSwingType Family(eBowlerKind kind, eDelivery d)
    {
        switch (d)
        {
            case eDelivery.InSwinger: return eSwingType.InSwing;
            case eDelivery.OutSwinger: return eSwingType.OutSwing;
            case eDelivery.Stock:
            case eDelivery.OffCutter:
            case eDelivery.LegCutter:
            case eDelivery.SlowerBall: return eSwingType.Pace;
            case eDelivery.OffBreak:
            case eDelivery.Carrom: return eSwingType.OffSpin;
            case eDelivery.LegBreak:
            case eDelivery.Googly: return eSwingType.LegSpin;
            default:   // top-spinner, slider, flighted: the bowler's own family
                return kind == eBowlerKind.LegSpin ? eSwingType.LegSpin : eSwingType.OffSpin;
        }
    }

    // ---- Next over ---------------------------------------------------------------------------

    /// Most overs one bowler may bowl: a fifth of the innings (4 of 20, 10 of 50), at least one.
    public static int Quota(int totalOvers) => Mathf.Max(1, Mathf.CeilToInt(totalOvers / 5f));

    public static int OversBowled(Bowler b) => (b.Balls + 5) / 6;

    /// Record who bowled the over just finished (HUD calls it before picking the next).
    public static void OverDone(Bowler b) => overBowlers.Add(b);

    /// The bowler for over `over` (0-based) of `totalOvers`; -1 if nobody can bowl it.
    public static int PickNextBowler(IList<Bowler> bowlers, Bowler last, int over, int totalOvers, System.Func<float> rand01)
    {
        int quota = Quota(totalOvers);
        int powerplay = FieldCaptain.PowerplayOvers(totalOvers);
        int death = totalOvers >= 5 ? Mathf.Max(1, Mathf.RoundToInt(totalOvers * 0.2f)) : 0;
        bool seamPhase = over < Mathf.Max(1, powerplay) || over >= totalOvers - death;
        Bowler twoAgo = overBowlers.Count >= 2 ? overBowlers[overBowlers.Count - 2] : null;

        float total = 0f;
        var weights = new float[bowlers.Count];
        for (int i = 0; i < bowlers.Count; i++)
        {
            Bowler b = bowlers[i];
            if (b == last && bowlers.Count > 1) continue;      // nobody bowls two in a row
            if (OversBowled(b) >= quota) continue;
            bool spin = BowlerRepertoire.IsSpin(b.Kind);
            float w = seamPhase ? (spin ? 0.35f : 3f) : (spin ? 2.5f : 1f);
            // Keep a bowler on for his spell, from his end (every other over), up to 4 overs.
            if (b == twoAgo && SpellLength(b) < 4) w *= 2.5f;
            // A bowler taking wickets stays on; one going for plenty comes off.
            if (b.Balls > 0)
            {
                float economy = 6f * b.Conceded / b.Balls;
                w *= (1f + 0.25f * b.Wickets) * Mathf.Clamp(1.6f - economy / 12f, 0.5f, 1.4f);
            }
            weights[i] = w;
            total += w;
        }
        if (total <= 0f)
        {
            // Everybody is on his quota (or only `last` is left): anyone but the last bowler.
            for (int i = 0; i < bowlers.Count; i++)
                if (bowlers[i] != last) return i;
            return bowlers.Count > 0 ? 0 : -1;
        }
        float r = Mathf.Clamp01(rand01()) * total;
        for (int i = 0; i < bowlers.Count; i++)
        {
            if (weights[i] <= 0f) continue;
            if (r < weights[i]) return i;
            r -= weights[i];
        }
        for (int i = bowlers.Count - 1; i >= 0; i--)
            if (weights[i] > 0f) return i;
        return 0;
    }

    /// Consecutive overs this bowler has bowled from his end, most recent first.
    private static int SpellLength(Bowler b)
    {
        int n = 0;
        for (int i = overBowlers.Count - 2; i >= 0; i -= 2)
        {
            if (overBowlers[i] != b) break;
            n++;
        }
        return n;
    }
}
