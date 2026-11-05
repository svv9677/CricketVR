using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The captain: picks the bowler's plan and sets the field for it, between balls, against the
/// batter in front of him - the way captains do it: bowl to your field, and move the field to
/// where the batter is scoring.
///
/// What he knows comes from the score, ball by ball (BatterBook, kept apart for pace and spin):
/// where each ball pitched and what line, what came of it - runs, an edge or a play-and-miss, a
/// wicket - and which way the shot went.
///
/// The plan (BowlingPlans). At the start of every over, when a new bowler comes on, and straight
/// away after two boundaries in a row, he weighs every plan this bowler can bowl by what it should
/// cost against this batter: the runs the batter has scored off the lines and lengths that plan
/// bowls, less the edges and wickets they have given (BatterBook.Cost; a line or length not seen
/// yet is assumed to go at the batter's overall rate). The cheapest plan is the likeliest pick,
/// not a certainty (Temperature), so a batter cannot sit on one line. A new batter gets the
/// attacking plans; the last overs of a match, the yorker and wide-of-off plans.
///
/// The field. The plan's own field, then:
///   * attack (new batter, or a run of dot balls): catchers in - slips and gully for pace, short
///     leg and silly point for spin - in place of men saving runs where the batter is not scoring;
///   * defend (scoring fast, or boundaries): men in the ring where the batter is hitting go back
///     to the rope;
///   * either way, up to two fielders moved from where the batter does not score into the areas
///     they score most in that the field leaves open - to the rope if the runs there come in
///     boundaries, the ring if in ones and twos;
///   * a batter milking the ones and twos with no boundaries: the ring comes in a few metres to
///     cut off the single (FieldSets.RingInFor1s), as captains walk the ring in.
/// The limits it is set under (FieldSets.Valid): two outside the circle in the powerplay (the first
/// 30% of the overs, six of twenty, ten of fifty), then five (four through an ODI's middle overs);
/// five on the leg side; two behind square on it.
///
/// When: as the last ball ends (InGame_ResetToReady), so the fielders walk to their new places
/// while the ball comes back; and again at the start of the next ball if the bowler or the batting
/// hand has changed meanwhile (settings), when everyone is put on his mark anyway.
/// </summary>
public class FieldCaptain : MonoBehaviour
{
    private const int NewBatterBalls = 8, DotsToAttack = 6;
    private const float DefendStrikeRate = 150f, AttackStrikeRate = 60f;
    private const int DefendBoundaries = 2;
    /// A sector needs this many (faded) runs before he moves someone for it; at most this many moves.
    private const float HotSector = 4f;
    private const int MaxMoves = 2, MaxCatchers = 2, MaxPushBack = 2;
    /// How much he likes to stay with the plan he is on. How firmly he goes for the cheapest plan
    /// is the bowling level's (BowlingLevels.Level.temperature): plans that much dearer (runs a
    /// ball) are e times less likely. An Easy attack has no plans at all.
    private const float Stick = 0.05f;
    /// A played-and-missed ball this close to off stump, or a hit this badly middled, is a false shot.
    private const float BeatenWithin = 0.35f, Mistimed = 0.3f;

    /// The plan and how the field is set, for the scoreboard: "Short-ball plan, attacking".
    public static string Label { get; private set; }

    private AnimatedFielderManagement field;
    private readonly BatterBook book = new BatterBook();
    private int lastRuns, lastWickets, lastLegal;
    private bool haveScore;
    /// A career match's simulated balls (the partners', HUD.ScoreSimulatedBall) have been scored
    /// since the last read: they are not the batter's, so the next read only catches up with the
    /// score and puts nothing in the book.
    private bool simulatedSince;
    // The ball being bowled: who by, where, and what came of it.
    private eSwingType bowledBy;
    private float bowledLine;
    private eLengthBand bowledBand;
    private bool struck, falseShot, boundaryLast;
    private float shotAngle = float.NaN;
    private int boundariesInARow;
    private eSwingType setFor = eSwingType.None;
    private eBattingStyle setForHand = eBattingStyle.None;
    private BowlingPlan plan;
    private FieldSets.Plan aggression = FieldSets.Plan.Attack;
    private float nonStrikerX = -10.36f;

    /// Set by test tools (FieldingFilm) that launch balls at fielders where they stand: the field
    /// and the plan stay as they are, and nothing is read into the book.
    public static bool Suspended;

    public BowlingPlan CurrentPlan => plan;
    public FieldSets.Plan CurrentAggression => aggression;

    private void Start()
    {
        field = GetComponent<AnimatedFielderManagement>();
        if (Main.Instance == null) { enabled = false; return; }
        GameObject bowlingEnd = GameObject.Find("Main/Stumps_BowlingEnd");
        if (bowlingEnd != null)
            nonStrikerX = bowlingEnd.transform.position.x;
        Main.Instance.onGameStateChanged += HandleGameState;
        Bat.ShotStruck += OnShot;
        HUD.SimulatedBallScored += OnSimulatedBall;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
        Bat.ShotStruck -= OnShot;
        HUD.SimulatedBallScored -= OnSimulatedBall;
        BowlingPlans.Active = null;
        Label = null;
    }

    private void OnShot(ShotInfo info)
    {
        struck = true;
        falseShot |= info.edge || info.quality < Mistimed;
    }

    private void OnSimulatedBall() => simulatedSince = true;

    private void HandleGameState()
    {
        Main inst = Main.Instance;
        if (Suspended)
            return;
        switch (inst.gameState)
        {
            case eGameState.InGame_DeliverBall:
                bowledBy = inst.swingType;
                bowledLine = inst.currentBowlingConfig != null ? inst.currentBowlingConfig.speedZ : 0f;
                bowledBand = inst.currentBowlingConfig != null ? inst.currentBowlingConfig.lengthBand : eLengthBand.GoodLength;
                struck = falseShot = false;
                shotAngle = float.NaN;
                break;
            case eGameState.InGame_BallHit:
                Vector3 v = inst.theBallRigidBody.linearVelocity;
                if (new Vector2(v.x, v.z).sqrMagnitude > 0.25f)
                    shotAngle = FieldSets.AngleOf(new Vector3(BallDelivery.BatsmanStumpsX + v.x, 0f, v.z), GroundNow());
                break;
            case eGameState.InGame_ResetToReady:
                bool overStarts = ReadBall();
                Set(false, overStarts || boundariesInARow >= 2);
                break;
            case eGameState.InGame_SelectDelivery:
                if (!haveScore && HUD.Instance != null)
                {
                    haveScore = true;
                    Remember(HUD.Instance);
                }
                else if (simulatedSince && HUD.Instance != null)
                {
                    // Simulated balls with no read after them: start this real ball from the score now.
                    simulatedSince = false;
                    Remember(HUD.Instance);
                }
                if (inst.swingType != setFor || inst.BattingStyle != setForHand || !BowlingPlans.Fits(plan, inst.swingType))
                    Set(true, true);
                break;
        }
    }

    // ---- Reading the ball ---------------------------------------------------------------------

    /// What the last ball did, from the score, into the book. True if a new over starts now.
    private bool ReadBall()
    {
        HUD hud = HUD.Instance;
        if (hud == null)
            return false;
        int legal = hud.Overs * 6 + hud.Balls;
        if (!haveScore || hud.Runs < lastRuns || legal < lastLegal)
        {
            // First ball seen, or a new match (HUD.Reset).
            haveScore = true;
            simulatedSince = false;
            Remember(hud);
            book.NewBatter();
            return true;
        }
        if (simulatedSince)
            return CatchUp(hud);
        int runs = hud.Runs - lastRuns;
        bool wicket = hud.Wickets > lastWickets;
        bool counted = legal != lastLegal;
        Remember(hud);
        Ball ball = Main.Instance.theBallScript;
        bool beaten = !struck && !wicket && counted && ball != null && !ball.wide && Mathf.Abs(bowledLine - 0.05f) < BeatenWithin;
        book.Add(bowledBy, bowledLine, bowledBand, runs, counted, falseShot || beaten || wicket, wicket, shotAngle);
        boundariesInARow = runs >= 4 ? boundariesInARow + 1 : 0;
        return counted && hud.Balls == 0;
    }

    /// After simulated balls: take the score as it is now, with nothing in the book. True if they
    /// took the match into a new over (the field is set again for it).
    private bool CatchUp(HUD hud)
    {
        simulatedSince = false;
        int legalBefore = lastLegal;
        Remember(hud);
        return lastLegal != legalBefore && hud.Balls == 0;
    }

    private void Remember(HUD hud)
    {
        lastRuns = hud.Runs;
        lastWickets = hud.Wickets;
        lastLegal = hud.Overs * 6 + hud.Balls;
    }

    // ---- The plan -----------------------------------------------------------------------------

    private bool NewBatter => book.BallsSinceWicket < NewBatterBalls;

    private bool DeathOvers
    {
        get
        {
            HUD hud = HUD.Instance;
            return hud != null && hud.TotalOvers >= 3 && hud.TotalOvers - hud.Overs <= 1;
        }
    }

    private BowlingPlan ChoosePlan(eSwingType bowler)
    {
        BowlingPlan[] plans = BowlingPlans.For(bowler);
        var weights = new float[plans.Length];
        float best = float.MaxValue;
        var costs = new float[plans.Length];
        for (int i = 0; i < plans.Length; i++)
        {
            float c = book.Cost(BowlingPlans.Share(plans[i], bowler), bowler);
            if (NewBatter && (plans[i] == BowlingPlans.AttackStumps || plans[i] == BowlingPlans.OffStump ||
                              plans[i] == BowlingPlans.Flight)) c -= 0.15f;
            if (DeathOvers && (plans[i] == BowlingPlans.Yorkers || plans[i] == BowlingPlans.WideOff ||
                               plans[i] == BowlingPlans.Darts)) c -= 0.3f;
            if (plans[i] == plan) c -= Stick;
            costs[i] = c;
            best = Mathf.Min(best, c);
        }
        float total = 0f;
        float temperature = BowlingLevels.Current.temperature;
        for (int i = 0; i < plans.Length; i++) { weights[i] = Mathf.Exp(-(costs[i] - best) / temperature); total += weights[i]; }
        float r = Random.value * total;
        for (int i = 0; i < plans.Length; i++)
        {
            if (r < weights[i]) return plans[i];
            r -= weights[i];
        }
        return plans[plans.Length - 1];
    }

    private FieldSets.Plan ChooseAggression()
    {
        book.Recent(out float strikeRate, out int boundaries, out int balls);
        if (NewBatter || book.DotsInARow >= DotsToAttack || (balls >= 6 && strikeRate < AttackStrikeRate))
            return FieldSets.Plan.Attack;
        if (DeathOvers || (balls >= 6 && (strikeRate >= DefendStrikeRate || boundaries >= DefendBoundaries)))
            return FieldSets.Plan.Defend;
        return FieldSets.Plan.Standard;
    }

    // ---- The field ----------------------------------------------------------------------------

    /// Overs of the powerplay for a match of `total` overs: six of twenty, ten of fifty, 30% of a
    /// shorter one (none in a one-over match).
    public static int PowerplayOvers(int total) =>
        total >= 50 ? 10 : total >= 20 ? 6 : total >= 2 ? Mathf.Max(1, Mathf.RoundToInt(total * 0.3f)) : 0;

    /// Most outside the circle in this over (0-based) of a `total`-over match.
    public static int MaxOutsideIn(int over, int total)
    {
        if (over < PowerplayOvers(total)) return 2;
        if (total > 20 && over < total - 10) return 4;
        return FieldSets.MaxOutside;
    }

    private float RingIn()
    {
        book.Recent(out _, out int boundaries, out int balls);
        return balls >= 6 && boundaries == 0 && book.RecentSingles() >= balls / 2 ? FieldSets.RingInFor1s : 0f;
    }

    private FieldSets.Ground GroundNow()
    {
        Main inst = Main.Instance;
        HUD hud = HUD.Instance;
        return new FieldSets.Ground
        {
            maxOutside = hud != null ? MaxOutsideIn(hud.Overs, hud.TotalOvers) : FieldSets.MaxOutside,
            ringIn = RingIn(),
            strikerX = BallDelivery.BatsmanStumpsX,
            nonStrikerX = nonStrikerX,
            boundary = field != null ? field.BoundaryRadius : 56f,
            keeperX = FieldSets.KeeperX(inst.swingType),
            offSign = inst.BattingStyle == eBattingStyle.LeftHanded ? -1f : 1f,
        };
    }

    /// Pick the plan (if `rethink`) and set its field; `now` puts the fielders on it at once.
    private void Set(bool now, bool rethink)
    {
        Main inst = Main.Instance;
        List<AnimatedFielder> fielders = Fielders();
        if (fielders.Count == 0)
            return;
        eSwingType bowler = inst.swingType;
        bool plans = BowlingLevels.Current.plans;
        if (!plans)
            plan = BowlingPlans.Stock(bowler);   // club bowlers just bowl; the field is still set
        else if (rethink || !BowlingPlans.Fits(plan, bowler))
            plan = ChoosePlan(bowler);
        BowlingPlans.Active = plans ? plan : null;
        setFor = bowler;
        setForHand = inst.BattingStyle;
        aggression = ChooseAggression();
        FieldSets.Ground g = GroundNow();
        BatterBook.Record record = book.For(bowler);
        var layout = new FieldPlan(plan.field, g, record);
        if (aggression == FieldSets.Plan.Attack)
            layout.AddCatchers(BowlingPlans.Catchers(plan.spin), MaxCatchers);
        else if (aggression == FieldSets.Plan.Defend)
            layout.PushBack(MaxPushBack);
        layout.Adapt(MaxMoves, HotSector);
        Assign(fielders, layout.places, now);
        current = layout;
        Label = !plans ? null : $"{plan.name}, {(aggression == FieldSets.Plan.Attack ? "attacking" : aggression == FieldSets.Plan.Defend ? "defensive" : "standard")}" +
                                $"{(g.maxOutside == 2 ? " · powerplay" : "")}{(g.ringIn > 0f ? " · ring in" : "")}";
        if (HUD.Instance != null)
            HUD.Instance.UpdateUI();
        Debug.Log($"[FieldCaptain] {Label} field for a {(g.offSign > 0 ? "right" : "left")}-hander against {bowler}: {layout}");
    }

    private FieldPlan current;

    /// The fielding position a fielder was set at ("Point", "Deep midwicket"), for the scorecard
    /// ("c Point b Bumrah"); null if he is not in the current field.
    public string PositionName(AnimatedFielder f)
    {
        if (current == null || f == null) return null;
        for (int i = 0; i < current.places.Count && i < current.names.Count; i++)
            if (Flat(current.places[i] - f.StartPosition) < 1f)
                return current.names[i];
        return null;
    }

    /// Fewest metres walked: each place goes to the nearest fielder still without one.
    private static void Assign(List<AnimatedFielder> fielders, List<Vector3> places, bool now)
    {
        var free = new List<AnimatedFielder>(fielders);
        foreach (Vector3 place in places)
        {
            if (free.Count == 0)
                break;
            AnimatedFielder nearest = free[0];
            foreach (AnimatedFielder f in free)
                if (Flat(f.StartPosition - place) < Flat(nearest.StartPosition - place))
                    nearest = f;
            free.Remove(nearest);
            Vector3 p = place;
            p.y = BallFlight.GroundY(p);
            nearest.StartPosition = p;
            if (now && !nearest.HoldingBall)
                nearest.ReturnToStart();
        }
    }

    private static List<AnimatedFielder> Fielders()
    {
        var list = new List<AnimatedFielder>();
        foreach (AnimatedFielder f in AnimatedFielder.All)
            if (f != null && !f.IsBowler && f.isActiveAndEnabled)
                list.Add(f);
        return list;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
}
