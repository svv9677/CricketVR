using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bowls a grid of deliveries at the keeper and reports which he takes, in Play mode. Each ball is
/// released from the bowler's hand at `pace` and pitches on the strip like a real delivery, aimed
/// to reach his glove plane at a lateral offset (metres to his right as he faces the bowler) and a
/// height above the ground. Per ball: taken, dived, spilled, or how close his gloves came.
///
///   KeeperGrid.Run(35f);   ...   KeeperGrid.Done / KeeperGrid.Report
/// </summary>
public static class KeeperGrid
{
    public static readonly float[] Laterals = { -2.4f, -1.8f, -1.2f, -0.6f, 0f, 0.6f, 1.2f, 1.8f, 2.4f };
    public static readonly float[] Heights = { 0.2f, 0.6f, 1.0f, 1.4f, 1.8f, 2.2f };
    private static readonly Vector3 Release = new Vector3(-9.3f, 2.15f, 0.25f);
    private const float ShotTimeout = 3f;

    private struct Shot { public float lateral, height; public Vector3 velocity; public bool solved; }

    private static readonly List<Shot> shots = new List<Shot>();
    private static readonly Dictionary<(float, float), string> results = new Dictionary<(float, float), string>();
    private static int index, phase;
    private static float shotStart, closest;
    private static bool dived, spilled;

    public static bool Done { get; private set; } = true;

    /// Set before Run to log every frame of the shots at these (lateral, height) to this file.
    public static string TraceFile;
    public static (float lateral, float height)[] TraceShots = new (float, float)[0];

    /// Just some of the grid.
    public static void Run(float pace, float[] laterals, float[] heights)
    {
        only = (laterals, heights);
        RunAll(pace);
        only = null;
    }

    private static (float[] l, float[] h)? only;

    public static void Run(float pace) => RunAll(pace);

    private static void RunAll(float pace)
    {
        Main inst = Main.Instance;
        Transform keeper = inst.theKeeper.transform;
        float plane = keeper.position.x - 0.5f;   // his glove plane, HandsAhead in front
        shots.Clear();
        results.Clear();
        foreach (float h in only.HasValue ? only.Value.h : Heights)
            foreach (float l in only.HasValue ? only.Value.l : Laterals)
            {
                Vector3 target = new Vector3(plane, BallFlight.GroundY(keeper.position) + h, keeper.position.z + l);
                shots.Add(Solve(target, pace, l, h));
            }
        index = -1;
        Done = false;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Next();
    }

    /// Release velocity at `pace` along the ground that reaches `target` at its height, by
    /// bisection on the vertical speed over the real flight model (it pitches on the way).
    private static Shot Solve(Vector3 target, float pace, float lateral, float height) => SolveFrom(Release, target, pace, lateral, height);

    /// The release velocity for a delivery from the bowler's hand reaching his glove plane at
    /// (lateral, height) at pace - for filming one (FieldingFilm.AtKeeperFromHand).
    public static bool DeliveryTo(float lateral, float height, float pace, out Vector3 from, out Vector3 velocity)
    {
        Transform keeper = Main.Instance.theKeeper.transform;
        Vector3 target = new Vector3(keeper.position.x - 0.5f, BallFlight.GroundY(keeper.position) + height, keeper.position.z + lateral);
        Shot s = SolveFrom(Release, target, pace, lateral, height);
        from = Release;
        velocity = s.velocity;
        return s.solved;
    }

    private static Shot SolveFrom(Vector3 Release, Vector3 target, float pace, float lateral, float height)
    {
        Vector3 flat = new Vector3(target.x - Release.x, 0f, target.z - Release.z);
        Vector3 dir = flat.normalized;
        var path = new List<BallFlight.Sample>(1024);
        float lo = -12f, hi = 6f, best = float.MaxValue, bestVy = 0f;
        for (int i = 0; i < 60; i++)
        {
            float vy = 0.5f * (lo + hi);
            BallFlight.Simulate(Release, dir * pace + Vector3.up * vy, BallFlight.DeliveryEffects.None, 3f, path,
                s => s.position.x >= target.x, 0.005f);
            float y = path.Count > 0 ? path[path.Count - 1].position.y : -9f;
            float err = y - target.y;
            if (Mathf.Abs(err) < best) { best = Mathf.Abs(err); bestVy = vy; }
            if (err > 0f) hi = vy; else lo = vy;
        }
        return new Shot { lateral = lateral, height = height, velocity = dir * pace + Vector3.up * bestVy, solved = best < 0.08f };
    }

    private static void Next()
    {
        index++;
        if (index >= shots.Count)
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            Done = true;
            return;
        }
        if (!shots[index].solved)
        {
            results[(shots[index].lateral, shots[index].height)] = "  n/a ";
            Next();
            return;
        }
        Reset();
        phase = 0;
    }

    private static void Reset()
    {
        Main inst = Main.Instance;
        inst.StopAllCoroutines();
        Rigidbody rb = inst.theBallRigidBody;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        inst.theBall.transform.SetParent(null);
        rb.position = Release;
        inst.theBall.transform.position = Release;
        Physics.SyncTransforms();
        KeeperCatcher keeper = inst.theKeeper.GetComponentInChildren<KeeperCatcher>();
        typeof(KeeperCatcher).GetField("holding", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(keeper, false);
        typeof(KeeperCatcher).GetMethod("ResetStance", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(keeper, null);
        Extras.Clear();
        inst.theBallScript.wide = false;
        inst.gameState = eGameState.InGame_ResetToReady;
        shotStart = Time.time;
    }

    private static void Tick()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= Tick;
            Done = true;
            return;
        }
        Main inst = Main.Instance;
        KeeperCatcher keeper = inst.theKeeper.GetComponentInChildren<KeeperCatcher>();
        if (phase == 0)
        {
            // He has to be set: in his crouch as the ball is about to be bowled, as in a real
            // delivery. (The delivery state with the ball still held: no bowler's run-up, which
            // would carry the ball off to his hand.)
            if (Time.time - shotStart < 1.2f || inst.gameState != eGameState.InGame_Ready)
                return;
            inst.gameState = eGameState.InGame_DeliverBallLoop;
            phase = 1;
            shotStart = Time.time;
            return;
        }
        if (phase == 1)
        {
            if (Time.time - shotStart < 0.8f)
                return;
            Launch(inst, shots[index]);
            phase = 2;
            return;
        }
        Vector3 ball = inst.theBall.transform.position;
        HumanoidReach reach = keeper.GetComponentInChildren<HumanoidReach>();
        if (reach != null && reach.Ready && !inst.theBallRigidBody.isKinematic)
            closest = Mathf.Min(closest, Mathf.Min(Vector3.Distance(ball, reach.PalmCentre(true)), Vector3.Distance(ball, reach.PalmCentre(false))));
        if (!string.IsNullOrEmpty(TraceFile) && System.Array.Exists(TraceShots, t => Mathf.Approximately(t.lateral, shots[index].lateral) && Mathf.Approximately(t.height, shots[index].height)) && reach != null)
        {
            var f = BindingFlags.NonPublic | BindingFlags.Instance;
            Vector3 icpt = (Vector3)typeof(KeeperCatcher).GetField("intercept", f).GetValue(keeper);
            float at = (float)typeof(KeeperCatcher).GetField("interceptAt", f).GetValue(keeper);
            bool has = (bool)typeof(KeeperCatcher).GetField("hasIntercept", f).GetValue(keeper);
            System.IO.File.AppendAllText(TraceFile,
                $"{shots[index].lateral},{shots[index].height},{Time.time - shotStart:F3},ball {ball.x:F2} {ball.y:F2} {ball.z:F2},palmL {reach.PalmCentre(true).ToString("F2")},palmR {reach.PalmCentre(false).ToString("F2")}," +
                $"target {reach.handTarget.ToString("F2")},w {reach.weight:F2}/{reach.SmoothedWeight:F2},has {has} icpt {icpt.ToString("F2")} eta {at - Time.time:F2},state {inst.gameState}\n");
        }
        dived |= (bool)typeof(KeeperCatcher).GetField("diving", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(keeper);
        spilled |= (float)typeof(KeeperCatcher).GetField("spilledUntil", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(keeper) > shotStart;
        bool taken = keeper.HoldingBall;
        bool past = ball.x > inst.theKeeper.transform.position.x + 3f || Time.time - shotStart > ShotTimeout;
        if (!taken && !past)
            return;
        Shot s = shots[index];
        results[(s.lateral, s.height)] = taken ? (dived ? " DIVE " : " take ") : spilled ? " spill" : $" {closest:0.00} ";
        Next();
    }

    private static void Launch(Main inst, Shot s)
    {
        Rigidbody rb = inst.theBallRigidBody;
        Collider c = inst.theBall.GetComponent<Collider>();
        if (c != null) c.enabled = true;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.position = Release;
        inst.theBall.transform.position = Release;
        rb.linearVelocity = s.velocity;
        rb.angularVelocity = Vector3.zero;
        inst.theBallScript.fresh = true;
        inst.theBallScript.bounced = false;
        inst.theBallScript.deliveryEffects = BallFlight.DeliveryEffects.None;
        inst.theBatScript.hasHitBall = true;   // the idle bat in the Editor must not meet it
        inst.gameState = eGameState.InGame_DeliverBallLoop;
        shotStart = Time.time;
        closest = float.MaxValue;
        dived = spilled = false;
    }

    /// Rows by height (top first), columns by lateral offset (his left to his right).
    public static string Report
    {
        get
        {
            var sb = new StringBuilder("height\\lat");
            foreach (float l in Laterals) sb.Append($" {l,5:+0.0;-0.0;0.0}");
            sb.AppendLine();
            int taken = 0, total = 0;
            for (int i = Heights.Length - 1; i >= 0; i--)
            {
                sb.Append($"{Heights[i],6:0.0} m  ");
                foreach (float l in Laterals)
                {
                    string r = results.TryGetValue((l, Heights[i]), out string v) ? v : "  ... ";
                    sb.Append(r);
                    if (r.Contains("take") || r.Contains("DIVE")) taken++;
                    if (!r.Contains("n/a") && !r.Contains("...")) total++;
                }
                sb.AppendLine();
            }
            sb.AppendLine($"taken {taken} of {total}  (take / DIVE = held, spill = in the gloves and out, number = closest glove, m)");
            return sb.ToString();
        }
    }
}
