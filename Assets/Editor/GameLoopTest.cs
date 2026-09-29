using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plays real balls end to end in Play mode, the way a player does: start the ball as the A
/// button does (Main.StartNextBall), let the bowler run in and deliver, hit it when it reaches the
/// batter (straight back at the bowler, or out into the field), let the fielding play out, and
/// wait for the game to come back to Ready for the next one. Anything that does not come back
/// in time is reported STUCK with the state it was stuck in.
///
/// Driven from the Editor once Play mode is running:
///   GameLoopTest.Run(4);   ...   GameLoopTest.Done / GameLoopTest.Log
/// </summary>
public static class GameLoopTest
{
    private const float StartTimeout = 20f, BallTimeout = 30f, HitPace = 17f;

    private static int balls, total, phase;
    private static float phaseStart;
    private static readonly StringBuilder log = new StringBuilder();
    private static Vector3 bowlerAtHit;
    private static float bowlerTravel;
    private static Vector3 lastBowler;
    private static bool bowlerFielded;

    public static bool Done { get; private set; } = true;
    public static string Log => log.ToString();

    /// Where each ball is sent, in turn: 0 = straight back past the bowler, 1-9 = at that fielder,
    /// -1 = not hit at all (left to the keeper). Null: alternate bowler and fielder 6.
    private static int[] targets;

    public static void Run(int count, int[] hitTargets)
    {
        Run(count);
        targets = hitTargets;
    }

    public static void Run(int count)
    {
        targets = null;
        total = count;
        balls = 0;
        phase = 0;
        phaseStart = Time.time;
        log.Clear();
        Done = false;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Finish(string why)
    {
        log.AppendLine(why);
        EditorApplication.update -= Tick;
        Done = true;
    }

    private static void Tick()
    {
        if (!Application.isPlaying)
        {
            Finish("stopped: left Play mode");
            return;
        }
        Main inst = Main.Instance;
        eGameState s = inst.gameState;
        float t = Time.time - phaseStart;
        switch (phase)
        {
            case 0:   // waiting for Ready, then start the ball as the A button does
                if (s == eGameState.InGame_Ready)
                {
                    inst.StartNextBall();
                    Next(1, $"ball {balls + 1}: started");
                }
                else if (t > StartTimeout)
                    Stuck("never reached Ready");
                break;
            case 1:   // bowled; hit it as it reaches the batter
                if (s == eGameState.InGame_DeliverBallLoop && targets != null && targets[balls % targets.Length] < 0)
                {
                    Next(2, "  left alone for the keeper");
                }
                else if (s == eGameState.InGame_DeliverBallLoop &&
                    inst.theBall.transform.position.x > BallDelivery.BatsmanStumpsX - 1.6f)
                {
                    Hit(inst);
                    Next(2, $"  delivered and hit at {t:F1}s after start");
                }
                else if (s == eGameState.InGame_Bowled || s == eGameState.InGame_BallMissed || s == eGameState.InGame_ResetToReady)
                    Next(2, $"  not hit ({s})");
                else if (t > StartTimeout)
                    Stuck("no delivery");
                break;
            case 2:   // fielding, then back to Ready
                TrackBowler();
                if (s == eGameState.InGame_Ready)
                {
                    balls++;
                    log.AppendLine($"  back to Ready after {t:F1}s; fielded by '{inst.currentFielderName}', bowler moved {bowlerTravel:F1} m while fielding{(bowlerFielded ? " (was fielding)" : "")}");
                    if (balls >= total)
                        Finish($"DONE: {balls} balls played, none stuck");
                    else
                        Next(0, null);
                }
                else if (t > BallTimeout)
                    Stuck("never came back to Ready");
                break;
        }
    }

    private static void Next(int nextPhase, string line)
    {
        if (line != null)
            log.AppendLine(line);
        phase = nextPhase;
        phaseStart = Time.time;
    }

    private static void Stuck(string what)
    {
        Main inst = Main.Instance;
        AnimatedBowler b = AnimatedBowler.Instance;
        Animator an = b != null ? b.GetComponent<Animator>() : null;
        BowlerFielding bf = b != null ? b.GetComponent<BowlerFielding>() : null;
        Finish($"STUCK on ball {balls + 1}: {what}. state={inst.gameState}, bowler at {b?.transform.position:F1}, " +
               $"controller={(an != null ? an.runtimeAnimatorController.name : "?")}, Action={(an != null ? an.GetInteger("Action") : 0)}, " +
               $"bowler fielding={bf?.Fielding}, bowling flag={b?.Fielding}");
    }

    /// Alternate: straight back past the bowler, then out to a fielder square of the wicket.
    private static void Hit(Main inst)
    {
        Rigidbody rb = inst.theBallRigidBody;
        Vector3 from = inst.theBall.transform.position;
        int target = targets != null ? targets[balls % targets.Length] : (balls % 2 == 0 ? 0 : 6);
        Vector3 to = target == 0 ? AnimatedBowler.Instance.transform.position + Vector3.forward * 1.2f
                                 : Constants.fieldingPositions[target - 1];
        Vector3 dir = new Vector3(to.x - from.x, 0f, to.z - from.z).normalized;
        rb.linearVelocity = dir * HitPace + Vector3.up * 1.5f;
        inst.theBatScript.hasHitBall = true;
        inst.gameState = eGameState.InGame_BallHit;
        bowlerAtHit = lastBowler = AnimatedBowler.Instance.transform.position;
        bowlerTravel = 0f;
        bowlerFielded = false;
    }

    private static void TrackBowler()
    {
        AnimatedBowler b = AnimatedBowler.Instance;
        if (b == null)
            return;
        if (b.Fielding)
        {
            bowlerFielded = true;
            bowlerTravel += Vector3.Distance(b.transform.position, lastBowler);
        }
        lastBowler = b.transform.position;
    }
}
