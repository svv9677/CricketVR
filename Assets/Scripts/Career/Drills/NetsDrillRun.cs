using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One attempt at a drill in the nets, for NetsTrial - which stays the one thing driving the nets
/// (the settings guard, the planned bowler, holding the game, the result card, back to the hub).
/// This is the drill's side of it: the attempt's balls, the shape each must be, the running score
/// on the progress line, the field markers for Hitting the gaps, and the result, recorded once.
///
/// The plan is the drill's balls shuffled for this attempt (DrillDefs.Plan with the seed for this
/// career, slot, drill and attempt): not a fixed order the player can learn, but the same order
/// again when the scene is restarted part way, because the attempt number (NetsSession.DrillAttempt)
/// is kept across the restart - so is the ball reached (NetsSession.TrialIndex).
///
/// Finish is safe to run twice: a restart while the result card is up finishes again, and gets the
/// result recorded the first time (NetsSession.LastDrill, with its new best) rather than a second
/// record of the same balls.
///
/// A plain class so the bookkeeping is testable without a scene (NetsDrillTests); the markers are
/// the only scene objects, made on the first OnReady that wants them.
/// </summary>
public sealed class NetsDrillRun
{
    private readonly eDrill id;
    private readonly int attempt;
    private readonly bool leftHanded;
    private readonly DrillBall[] balls;
    private readonly Transform parent;
    private DrillFieldMarkers markers;
    private FieldSets.Position[] shownField;
    private eDrillTag lastTag;

    /// The drill (null for one DrillDefs does not have: the plan is then empty).
    public DrillDef Def { get; }
    /// This attempt's balls in the order they are bowled, as NetsTrial bowls a plan.
    public NetsTrialBall[] Plan { get; }
    /// What the last counting ball was worth (None before the first, and after a restart).
    public eDrillTag LastTag => lastTag;

    public NetsDrillRun(CareerProfile p, int slot, eDrill d, int attempt, Transform parent)
    {
        id = d;
        this.attempt = attempt;
        this.parent = parent;
        leftHanded = p != null && p.hand == eBattingStyle.LeftHanded;
        Def = DrillDefs.For(d);
        balls = Def != null ? DrillDefs.Plan(d, DrillDefs.SeedFor(p, slot, d, attempt)) ?? new DrillBall[0] : new DrillBall[0];
        Plan = new NetsTrialBall[balls.Length];
        for (int i = 0; i < balls.Length; i++)
            Plan[i] = balls[i].Ball;
    }

    private int Clamp(int i) => Mathf.Clamp(i, 0, Mathf.Max(0, balls.Length - 1));

    /// The length band and line window ball `i` (0-based) must be (BowlingAttack.ForceShape).
    public DeliveryShape ShapeAt(int i) => balls.Length > 0 ? balls[Clamp(i)].shape : default;

    /// Ball `i` is next, the game at InGame_Ready: for Hitting the gaps, the field it is bowled to
    /// (the pace half and the spin half each have their own) goes up on the grass.
    public void OnReady(int i)
    {
        if (Def == null || balls.Length == 0)
            return;
        eBowlerKind k = balls[Clamp(i)].kind;
        FieldSets.Position[] field = DrillDefs.FieldFor(Def, k);
        if (field == shownField && (field == null || markers != null))
            return;
        shownField = field;
        if (field == null || field.Length == 0)
        {
            markers?.Hide();
            return;
        }
        markers ??= new DrillFieldMarkers(parent);
        markers.Show(field, WorldGround(k, leftHanded));
    }

    /// A counting ball of this attempt, measured: what it was worth goes on the progress line, and
    /// for Hitting the gaps the fielder who would have had it is marked until the next ball's call.
    public void OnBall(BallRecord r)
    {
        if (Def == null || r == null)
            return;
        lastTag = DrillScoring.Score(Def, r).tag;
        FieldSets.Position[] field = DrillDefs.FieldFor(Def, r.bowlerKind);
        if (field == null || markers == null)
            return;
        GapJudgement j = DrillField.Judge(r, field, DrillField.NetsGround(r.bowlerKind));
        markers.Highlight(j.call == eGapCall.Fielder || j.call == eGapCall.Caught ? j.fielder : null);
    }

    /// The progress line before ball `i` (0-based): which ball of how many, who bowls it, the
    /// score so far (from this attempt's balls in the career) and how the last one went.
    public string ProgressText(int i, CareerProfile p)
    {
        if (Def == null || balls.Length == 0)
            return "";
        IList<BallRecord> soFar = p != null ? DrillScoring.AttemptBalls(p.balls, id, attempt) : new List<BallRecord>();
        float running = DrillScoring.Running(Def, soFar);
        int ballNumber = Mathf.Clamp(i + 1, 1, balls.Length);
        return DrillText.ProgressText(Def, ballNumber, balls[Clamp(i)].kind, running, lastTag);
    }

    /// The attempt is over: scored from its balls in the career, with the ratings when it began and
    /// now, and recorded in the career once (DrillCoach.Record: attempts, last, best and the
    /// training slot). Run again - a restart while the result card was up - it returns that result.
    public DrillResult Finish(CareerProfile p, string date)
    {
        if (NetsSession.DrillRecorded && NetsSession.LastDrill != null)
            return NetsSession.LastDrill;
        IList<BallRecord> attemptBalls = p != null ? DrillScoring.AttemptBalls(p.balls, id, attempt) : new List<BallRecord>();
        DrillResult res = (Def != null ? DrillScoring.Tally(Def, attemptBalls) : null) ?? new DrillResult();
        res.drill = id;
        res.attempt = attempt;
        res.before = NetsSession.DrillStartRatings?.Clone();
        res.after = p?.ratings?.Clone();
        if (p != null && !NetsSession.DrillRecorded)
            DrillCoach.Record(p, res, date);
        NetsSession.DrillRecorded = true;
        NetsSession.LastDrill = res;
        return res;
    }

    /// The markers go (the drill is over, or the nets are).
    public void Dispose()
    {
        markers?.Dispose();
        markers = null;
        shownField = null;
    }

    /// The nets ground the field is set on, in the world: DrillField's (batter's frame, off side +)
    /// turned to the player's hand, so a left-hander's cover stands on his off side.
    public static FieldSets.Ground WorldGround(eBowlerKind k, bool leftHanded)
    {
        FieldSets.Ground g = DrillField.NetsGround(k);
        g.offSign = leftHanded ? -1f : 1f;
        return g;
    }
}
