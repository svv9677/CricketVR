using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The per-ball measurement: every delivery the player faces, in a match or in the nets, becomes
/// one BallRecord, raised on BallFeed.Faced exactly once when the ball is settled. The career's
/// ratings are computed from these, and they measure how the player really bats - nothing here
/// changes the game, and the bat shield is measured out (contact on the real blade, RealBlade;
/// closeness to the real blade, not the widened collider).
///
/// The facts are spread over the game and each is valid at a different moment, so they are taken
/// as they come:
///   * InGame_DeliverBall: the bowler's intent (Main.currentBowlingConfig), the hand, the shield,
///     the attack's level and - in a match - the situation (HUD, MatchFlow), before scoring moves it;
///   * InGame_DeliverBallLoop: the solved delivery (Main.lastDelivery) and the umpire's no-ball call
///     (NoBalls.Called is cleared again while the ball is scored);
///   * LateUpdate (after the bat's contact and the recorder): where the ball passed the stumps, how
///     close the bat came and how fast it was moving as the ball arrived;
///   * Bat.ShotStruck: the contact (ShotInfo), its exit, launch and projected carry;
///   * the ball's dead state (Bowled, BallFielded, BallMissed, BallPastBoundary) and the last
///     HUD.BallScored outcome;
///   * InGame_ResetToReady: the score diffed against the snapshot, as FieldCaptain.ReadBall does.
///     No dead state by then (a ball that died short, the X button) is a Dead ball.
///
/// In the nets the scoreboard means nothing (every hit is a dot, Bat.AutomaticReset): the ball is
/// settled at its FIRST dead state, its runs are a runs-equivalent of the shot
/// (BallLedgerMath.NetsRunsEquivalent), and it is out only bowled at the striker's end (the nets
/// have two sets of stumps) or caught on the full off the bat.
///
/// BallFeed listeners run inside Main's state change: they must not change the game state.
/// </summary>
[DefaultExecutionOrder(1100)]   // after Bat (contact, LateUpdate) and ShotRecorder (1000)
public class BallLedger : MonoBehaviour
{
    /// The bat's speed and closeness are watched while the ball is this close to the bat (m).
    private const float Arriving = 3f;
    /// Closest approach before the bat ever came near the ball.
    private const float NeverNear = 99f;

    private Main main;
    private bool nets, leftHanded;
    /// The ball being measured; null between balls (and once settled).
    private BallRecord rec;
    private bool released;

    // The score as the ball was bowled (matches).
    private Batsman striker;
    private int strikerRuns, wicketsBefore;
    private int ballsSinceWicket = 99;

    // Tracked while the ball comes down.
    private Vector3 prevBall;
    private bool crossed, touched;
    private Vector3 exitVelocity;

    // How it ended.
    private bool haveTerminal;
    private eGameState terminal;
    private bool bouncedAtEnd;
    private float ballXAtEnd;
    private string fielderAtEnd;
    private bool haveOutcome;
    private BallOutcome lastOutcome;

    private readonly List<BallFlight.Sample> samples = new List<BallFlight.Sample>(1024);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        MakeFor();
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => MakeFor();

    private static void MakeFor()
    {
        if (FindFirstObjectByType<Main>() == null || FindFirstObjectByType<BallLedger>() != null)
            return;
        new GameObject("Ball Ledger").AddComponent<BallLedger>();
    }

    private void OnEnable()
    {
        Bat.ShotStruck += OnShot;
        HUD.BallScored += OnScored;
    }

    private void OnDisable()
    {
        Bat.ShotStruck -= OnShot;
        HUD.BallScored -= OnScored;
    }

    private void Start()
    {
        // Main.Instance is set in Main.Start, which may not have run yet (and the previous scene's
        // destroyed Main is still in it), so find this scene's own.
        main = FindFirstObjectByType<Main>();
        nets = SceneManager.GetActiveScene().name == NetsSession.NetsScene;
        if (main != null)
            main.onGameStateChanged += OnState;
    }

    private void OnDestroy()
    {
        if (main != null)
            main.onGameStateChanged -= OnState;
    }

    // ---- The ball's states ----------------------------------------------------------------------

    private void OnState()
    {
        eGameState st = main.gameState;
        switch (st)
        {
            case eGameState.InGame_SelectDelivery:
                // A released ball that never reached a reset (it should not happen): still one
                // record per delivery, as a dead ball.
                if (rec != null && released)
                    Settle();
                rec = null;
                break;
            case eGameState.InGame_DeliverBall:
                Begin();
                break;
            case eGameState.InGame_DeliverBallLoop:
                if (rec == null)
                    Begin();
                Released();
                break;
            case eGameState.InGame_Bowled:
            case eGameState.InGame_BallFielded:
            case eGameState.InGame_BallMissed:
            case eGameState.InGame_BallPastBoundary:
                Died(st);
                break;
            case eGameState.InGame_ResetToReady:
                if (rec != null && released)
                    Settle();
                rec = null;
                break;
        }
    }

    /// The ball is being bowled: what the bowler meant and the situation, before scoring moves it.
    private void Begin()
    {
        BowlingParams cfg = main.currentBowlingConfig;
        leftHanded = main.BattingStyle == eBattingStyle.LeftHanded;
        rec = new BallRecord
        {
            source = !nets ? eBallSource.Match : NetsSession.Mode == eNetsMode.Trial ? eBallSource.Trial : eBallSource.Nets,
            level = (int)MatchSetup.Bowling,
            shield = (int)main.Difficulty,
            bowlerKind = cfg != null ? cfg.kind : eBowlerKind.Fast,
            delivery = cfg != null ? cfg.delivery : eDelivery.Stock,
            lengthBand = cfg != null ? cfg.lengthBand : eLengthBand.GoodLength,
        };
        released = crossed = touched = haveTerminal = haveOutcome = false;
        rec.closestApproach = NeverNear;
        rec.swingSpeed = 0f;
        fielderAtEnd = null;
        Situation();
    }

    /// The chase as this ball is bowled (matches only; all false/0 in the nets).
    private void Situation()
    {
        HUD hud = HUD.Instance;
        striker = null;
        if (nets || hud == null)
            return;
        striker = hud.CurrentBatsman;
        strikerRuns = striker != null ? striker.Runs : 0;
        wicketsBefore = hud.Wickets;
        int target = MatchFlow.Target;
        int need = Mathf.Max(0, target - hud.Runs);
        int left = Mathf.Max(0, hud.TotalOvers * 6 - (hud.Overs * 6 + hud.Balls));
        rec.chasing = target > 0;
        rec.requiredRate = rec.chasing ? BallLedgerMath.RequiredRate(need, left) : 0f;
        rec.wicketJustFell = ballsSinceWicket < 2;
        rec.pressure = BallLedgerMath.UnderPressure(rec.chasing, need, left, rec.wicketJustFell);
    }

    /// Out of the hand: the solved delivery and the umpire's call.
    private void Released()
    {
        BallDelivery.Solution d = main.lastDelivery;
        rec.speedKph = d.SpeedKph;
        rec.noBall = NoBalls.Called;
        rec.freeHit = NoBalls.FreeHit;
        // As aimed, until the ball is seen to pass the stumps (LateUpdate).
        rec.lineAtStumps = BallLedgerMath.LineInBatterFrame(d.lineZ, leftHanded);
        rec.heightAtStumps = d.heightAtStumps;
        prevBall = main.theBall != null ? main.theBall.transform.position : d.releasePosition;
        released = true;
    }

    /// The ball is dead. A match keeps the latest dead state and settles at the reset (the score is
    /// in by then); the nets settle at the first and ignore the rest.
    private void Died(eGameState st)
    {
        if (rec == null || !released)
            return;
        if (nets && haveTerminal)
            return;
        haveTerminal = true;
        terminal = st;
        Ball ball = main.theBallScript;
        bouncedAtEnd = ball != null && ball.bounced;
        ballXAtEnd = main.theBall != null ? main.theBall.transform.position.x : 0f;
        fielderAtEnd = main.currentFielderName;
        if (nets)
            Settle();
    }

    private void OnScored(BallOutcome o)
    {
        if (rec == null)
            return;
        haveOutcome = true;
        lastOutcome = o;
    }

    /// The bat met it (the shield's contact; RealBlade's verdict is in the ShotInfo).
    private void OnShot(ShotInfo info)
    {
        if (rec == null || !released || touched)
            return;
        touched = true;
        exitVelocity = info.exitVelocity;
        rec.contact = (int)info.kind;
        rec.quality = info.quality;
        rec.realQuality = info.realQuality;
        rec.realContact = info.realContact;
        rec.exitSpeed = info.exitSpeed;
        rec.launchDeg = BallLedgerMath.LaunchDegrees(info.exitVelocity);
        rec.carry = Carry(info.contactPoint, info.exitVelocity);
    }

    /// Where the shot first lands, ignoring fielders - exactly as ShotDistance.RecordHit projects it
    /// for the shot card: the same flight model, from the contact, until the first bounce.
    private float Carry(Vector3 from, Vector3 velocity)
    {
        BallFlight.Simulate(from, velocity, BallFlight.DeliveryEffects.None, 8f, samples, s => s.bounced);
        Vector3 to = samples.Count > 0 ? samples[samples.Count - 1].position : from;
        return new Vector2(to.x - from.x, to.z - from.z).magnitude;
    }

    // ---- Watching it arrive ---------------------------------------------------------------------

    private void LateUpdate()
    {
        if (rec == null || !released || main == null || main.theBall == null)
            return;
        Vector3 ball = main.theBall.transform.position;
        eGameState st = main.gameState;
        // The ball as drawn passing the batter's stumps, before any contact - still being bowled, or
        // already called wide (the side wide is called as it leaves the wide line, which can be
        // before the stumps), never a return throw.
        bool coming = st == eGameState.InGame_DeliverBallLoop || st == eGameState.InGame_BallMissed ||
                      st == eGameState.InGame_BallMissedLoop;
        float x = BallDelivery.BatsmanStumpsX;
        if (coming && !touched && !crossed && prevBall.x < x && ball.x >= x)
        {
            float f = (x - prevBall.x) / Mathf.Max(1e-5f, ball.x - prevBall.x);
            rec.lineAtStumps = BallLedgerMath.LineInBatterFrame(Mathf.Lerp(prevBall.z, ball.z, f), leftHanded);
            rec.heightAtStumps = Mathf.Lerp(prevBall.y, ball.y, f);
            crossed = true;
        }
        prevBall = ball;

        Bat bat = main.theBatScript;
        if (st != eGameState.InGame_DeliverBallLoop || bat == null)
            return;
        if ((ball - bat.transform.position).sqrMagnitude > Arriving * Arriving)
            return;
        rec.closestApproach = Mathf.Min(rec.closestApproach, RealBladeDistance(bat, ball));
        rec.swingSpeed = Mathf.Max(rec.swingSpeed, bat.trackerVelocity.magnitude);
    }

    /// Ball centre to the real blade (m). The bat's trigger box is widened by the shield
    /// (Main.updateBatColliderSize), so its own ClosestPoint would measure a 4x bat on Easy; this
    /// is the same box at the size it was made (Bat.originalSize).
    private static float RealBladeDistance(Bat bat, Vector3 p)
    {
        BoxCollider box = bat.batCollider;
        if (box == null)
            return NeverNear;
        Vector3 size = bat.originalSize.sqrMagnitude > 0f ? bat.originalSize : box.size;
        Vector3 half = size * 0.5f;
        Transform t = box.transform;
        Vector3 local = t.InverseTransformPoint(p) - box.center;
        Vector3 nearest = Vector3.Max(-half, Vector3.Min(half, local));
        return (t.TransformPoint(nearest + box.center) - p).magnitude;
    }

    // ---- Settling -------------------------------------------------------------------------------

    private void Settle()
    {
        BallRecord r = rec;
        rec = null;
        Ball ball = main.theBallScript;
        // Never got to the stumps (hit in front, or called wide over the head the moment it got
        // there): the line Ball saw, or the aim.
        if (!crossed && ball != null && !float.IsNaN(ball.lastLineAtStumps) && !touched)
            r.lineAtStumps = BallLedgerMath.LineInBatterFrame(ball.lastLineAtStumps, leftHanded);
        r.wouldHitStumps = BallLedgerMath.WouldHitStumps(r.lineAtStumps, r.heightAtStumps);
        r.wide = (ball != null && ball.wide) || (haveOutcome && lastOutcome.Signal == UmpireSignal.Wide);
        r.action = BallLedgerMath.Action(touched, r.realContact, r.closestApproach, r.swingSpeed);
        if (!touched)
            r.realContact = eRealContact.None;

        if (!haveTerminal)
        {
            r.end = eBallEnd.Dead;
            r.runs = 0;
            r.howOut = eHowOut.None;
        }
        else if (nets)
            SettleNets(r);
        else
            SettleMatch(r);

        if (r.end != eBallEnd.Dead)
            ballsSinceWicket = r.Out ? 0 : ballsSinceWicket + 1;
        BallFeed.Raise(r);
    }

    /// A match: the score against the snapshot - runs off the bat to the striker, a wicket.
    /// HUD has already kept the no-ball and free-hit batter in (NoBalls.Protected).
    private void SettleMatch(BallRecord r)
    {
        HUD hud = HUD.Instance;
        int runs = striker != null ? Mathf.Max(0, striker.Runs - strikerRuns) : 0;
        bool wicket = hud != null && hud.Wickets > wicketsBefore;
        r.runs = wicket ? 0 : runs;
        r.end = BallLedgerMath.End(wicket, r.wide, r.noBall, r.runs, terminal == eGameState.InGame_BallPastBoundary);
        r.howOut = wicket ? BallLedgerMath.HowOut(terminal, fielderAtEnd) : eHowOut.None;
        if (wicket && r.howOut == eHowOut.None)
            r.howOut = eHowOut.Caught;
    }

    /// The nets: a runs-equivalent of the shot; out bowled at the striker's end (x > 0: the far set
    /// of stumps is at the bowler's end) or caught on the full off the bat. Not off a no-ball or a
    /// free hit, as in a match.
    private void SettleNets(BallRecord r)
    {
        bool protectedBall = r.noBall || r.freeHit;
        bool bowled = terminal == eGameState.InGame_Bowled && ballXAtEnd > 0f;
        bool caught = terminal == eGameState.InGame_BallFielded && touched && !bouncedAtEnd &&
                      r.realContact != eRealContact.WouldHaveMissed;
        bool wicket = !protectedBall && (bowled || caught);
        int runs = touched && !wicket ? BallLedgerMath.NetsRunsEquivalent(exitVelocity, r.carry, r.realContact, leftHanded) : 0;
        r.runs = runs;
        r.end = BallLedgerMath.End(wicket, r.wide, r.noBall, runs, runs == 4);
        r.howOut = !wicket ? eHowOut.None : bowled ? eHowOut.Bowled : BallLedgerMath.HowOut(terminal, fielderAtEnd);
    }
}
