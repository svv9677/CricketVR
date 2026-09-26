using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fielding. Once the ball is hit it predicts the ball's whole path - flight, bounces, roll - with
/// the same model the ball itself uses (BallFlight), and sends one fielder - the keeper included -
/// to where he can meet it. It re-plans every ReplanInterval so a prediction that drifts is
/// corrected.
///
/// "Can get to it" is: reaction time + distance / run speed, not later than the ball gets there,
/// with the ball low enough to reach - CatchHeight in the air, which makes it a catch if it has
/// not bounced, anything on the ground otherwise.
///
/// One man goes. The first to reach it calls it ("Mine!", "Keeper's!", FieldingCalls) and keeps it
/// unless someone else could get there SwitchMargin sooner - re-picking the earliest every 0.1 s
/// swapped the runner back and forth, and the old plan also sent the second-earliest to the very
/// same point, so two or three converged on every catch. A backup now only goes to back up: to the
/// path BackupBehind beyond the caller's point, where a fumble or a ball past him will run to.
///
/// Nobody takes the ball by being near it any more (that was a 1.2 m snap). The fielder reaches
/// for it with the whole body (HumanoidReach) at the point PredictPass gives, and the ball is his
/// only when it actually meets his hands (AnimatedFielder -> Gather). A ball that beats the hands
/// carries on, and the next re-plan sends the backup after it.
///
/// Class name kept from the old intercept-time sorter so the scene component stays wired.
/// </summary>
public class AnimatedFielderManagement : MonoBehaviour
{
    public const float ReactionTime = 0.15f;
    /// How far from his feet a fielder can get his hands to the ball (a squat and a lean), used
    /// for planning only - the take itself needs the hands on the ball.
    public const float CatchRadius = 1.2f;
    /// Highest the hands get: a 1.8 m fielder's reach straight up.
    public const float CatchHeight = 2.2f;
    /// A fielder stops this far short of the ball's line so it arrives in front of him, where
    /// the hands work, not under his feet.
    public const float StopShort = 0.45f;
    public const float ReplanInterval = 0.1f;
    private const float PredictStep = 0.02f;
    private const float PredictTime = 12f;

    /// Someone else takes over the call only if he gets there this much sooner.
    private const float SwitchMargin = 0.35f;
    /// The backup goes this far down the path beyond the caller, and not at all if that is closer
    /// than BackupMinGap to the caller's point (he would only be in the way).
    private const float BackupBehind = 8f, BackupMinGap = 6f;

    private readonly List<IFielder> team = new List<IFielder>();
    private readonly List<BallFlight.Sample> path = new List<BallFlight.Sample>(1024);
    private float nextPlan;
    private bool tracking;
    private float boundaryRadius = 56f;
    /// When the current path was simulated: path sample times count from here.
    private float planTime;
    /// Whoever has called this ball.
    private IFielder caller;

    void Start()
    {
        foreach (AnimatedFielder f in GetComponentsInChildren<AnimatedFielder>())
            team.Add(f);
        KeeperCatcher keeper = Main.Instance.theKeeper != null ? Main.Instance.theKeeper.GetComponentInChildren<KeeperCatcher>() : null;
        if (keeper != null)
            team.Add(keeper);
        Main.Instance.onGameStateChanged += HandleGameState;
        if (Main.Instance.theBoundaryCollider != null)
        {
            var capsule = Main.Instance.theBoundaryCollider.GetComponent<CapsuleCollider>();
            if (capsule != null)
                boundaryRadius = capsule.radius * Main.Instance.theBoundaryCollider.transform.lossyScale.x;
        }
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
    }

    private void HandleGameState()
    {
        eGameState state = Main.Instance.gameState;
        if (state == eGameState.InGame_BallHit)
        {
            tracking = true;
            path.Clear();
            nextPlan = 0f;
            caller = null;
        }
        else if (state != eGameState.InGame_BallHitLoop)
        {
            tracking = false;
            path.Clear();
        }
    }

    private void FixedUpdate()
    {
        if (!tracking)
            return;
        Main inst = Main.Instance;
        Rigidbody ball = inst.theBallRigidBody;
        if (ball.isKinematic)
            return;

        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + ReplanInterval;
            Plan(ball.position, ball.linearVelocity);
        }
    }

    /// Plan who goes where, from the ball's current state.
    private void Plan(Vector3 position, Vector3 velocity)
    {
        float edge = boundaryRadius + 1f;
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, PredictTime, path,
            s => new Vector2(s.position.x, s.position.z).magnitude > edge, PredictStep);
        planTime = Time.time;
        if (path.Count == 0)
            return;

        // Who gets there first - and does the man who has called it still get there in time?
        IFielder first = null;
        float firstTime = float.MaxValue;
        int firstIndex = -1;
        foreach (IFielder f in team)
        {
            if (f.Available && EarliestMeet(f, out float t, out int i) && t < firstTime)
            {
                first = f; firstTime = t; firstIndex = i;
            }
        }
        if (caller != null && caller != first && caller.Available &&
            EarliestMeet(caller, out float callerTime, out int callerIndex) && callerTime <= firstTime + SwitchMargin)
        {
            first = caller; firstIndex = callerIndex;
        }

        Vector3 firstPoint;
        if (first != null)
            firstPoint = path[firstIndex].position;
        else
            first = LeastLate(out firstPoint, out firstIndex);   // nobody beats the rope: cut it off
        if (first == null)
            return;
        if (first != caller)
        {
            caller = first;
            FieldingCalls.Call(first);
        }

        IFielder backup = Backup(first, firstIndex, out Vector3 backupPoint);
        foreach (IFielder f in team)
        {
            if (f == first) f.SetTarget(ShortOf(f, firstPoint));
            else if (f == backup) f.SetTarget(ShortOf(f, backupPoint));
            else f.Stop();
        }
    }

    /// Someone to back up the caller: whoever gets soonest to the path BackupBehind beyond the
    /// caller's point, if that is far enough from him to be worth it. Never to the same ball.
    private IFielder Backup(IFielder caller, int callerIndex, out Vector3 point)
    {
        point = Vector3.zero;
        int i = Mathf.Clamp(callerIndex, 0, path.Count - 1);
        Vector3 from = path[i].position;
        while (i < path.Count - 1 && Flat(path[i].position - from) < BackupBehind)
            i++;
        if (Flat(path[i].position - from) < BackupMinGap)
            return null;
        point = path[i].position;
        IFielder best = null;
        float bestTime = float.MaxValue;
        foreach (IFielder f in team)
        {
            if (f == caller || !f.Available)
                continue;
            float t = Flat(point - f.Position) / f.RunSpeed;
            if (t < bestTime) { best = f; bestTime = t; }
        }
        return best;
    }

    private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    /// The first point of the predicted path this fielder can be at in time: its index in `path`,
    /// and the time (from the plan) it gets there.
    private bool EarliestMeet(IFielder f, out float time, out int index)
    {
        Vector3 at = f.Position;
        float speed = f.RunSpeed;
        float head = f.HasTarget ? 0f : ReactionTime;
        for (int i = 0; i < path.Count; i++)
        {
            BallFlight.Sample s = path[i];
            if (s.position.y > CatchHeight)
                continue;
            if (new Vector2(s.position.x, s.position.z).magnitude > boundaryRadius)
                break;
            float run = Mathf.Max(0f, Flat(s.position - at) - CatchRadius * 0.5f);
            if (head + run / speed <= s.time)
            {
                index = ComfortableCatch(i, at, speed, head);
                time = path[index].time;
                return true;
            }
        }
        time = 0f;
        index = -1;
        return false;
    }

    /// Catching height a fielder settles for when he has the time: about the chest.
    private const float ComfortHeight = 1.4f;

    /// A ball still in the air at the first point he can reach (anywhere under CatchHeight - a full
    /// stretch overhead): if he can also be where it has come down to ComfortHeight, take it there.
    /// Measured before this: a skier's first reachable point was 2.2 m up, he stopped there, and it
    /// dropped on over his head (1.98 m up, 9 cm behind his feet) out of his hands.
    private int ComfortableCatch(int first, Vector3 at, float speed, float head)
    {
        if (path[first].bounced || path[first].position.y <= ComfortHeight)
            return first;
        for (int i = first + 1; i < path.Count; i++)
        {
            BallFlight.Sample s = path[i];
            if (s.bounced)
                break;
            if (s.position.y > ComfortHeight)
                continue;
            float run = Mathf.Max(0f, Flat(s.position - at) - CatchRadius * 0.5f);
            return head + run / speed <= s.time ? i : first;
        }
        return first;
    }

    private IFielder LeastLate(out Vector3 point, out int index)
    {
        IFielder best = null;
        float bestLate = float.MaxValue;
        point = Vector3.zero;
        index = -1;
        foreach (IFielder f in team)
        {
            if (!f.Available)
                continue;
            Vector3 at = f.Position;
            for (int i = 0; i < path.Count; i++)
            {
                BallFlight.Sample s = path[i];
                if (new Vector2(s.position.x, s.position.z).magnitude > boundaryRadius)
                    break;
                float late = ReactionTime + new Vector2(s.position.x - at.x, s.position.z - at.z).magnitude / f.RunSpeed - s.time;
                if (late < bestLate)
                {
                    bestLate = late;
                    best = f;
                    point = s.position;
                    index = i;
                }
            }
        }
        return best;
    }

    /// Does the segment a->b pass within `radius` (horizontally) of the vertical line through
    /// `centre`, at a height between the ground and `height`?
    public static bool WithinReach(Vector3 a, Vector3 b, Vector3 centre, float radius, float height)
    {
        Vector2 a2 = new Vector2(a.x - centre.x, a.z - centre.z);
        Vector2 b2 = new Vector2(b.x - centre.x, b.z - centre.z);
        Vector2 d = b2 - a2;
        float len2 = d.sqrMagnitude;
        float t = len2 > 1e-8f ? Mathf.Clamp01(-Vector2.Dot(a2, d) / len2) : 0f;
        Vector2 closest = a2 + d * t;
        float y = Mathf.Lerp(a.y, b.y, t) - centre.y;
        return closest.sqrMagnitude <= radius * radius && y <= height && y >= -0.5f;
    }

    /// Where to stand for a meeting point: StopShort before it on the way in, so the ball arrives
    /// in front of the body. Already that close: stay put and let the hands do it.
    private static Vector3 ShortOf(IFielder f, Vector3 point)
    {
        Vector3 at = f.Position;
        Vector3 to = new Vector3(point.x - at.x, 0f, point.z - at.z);
        float d = to.magnitude;
        if (d <= StopShort)
            return at;
        return point - to / d * StopShort;
    }

    /// <summary>
    /// Where the ball will come closest to a fielder's hands, from the latest prediction: the
    /// first pass of the path within `radius` of `spot` (horizontally, at or below CatchHeight),
    /// taking the sample nearest `spot` in 3D. `timeFromNow` is when it gets there.
    /// </summary>
    public bool PredictPass(Vector3 spot, float radius, out Vector3 point, out float timeFromNow)
    {
        point = Vector3.zero;
        timeFromNow = 0f;
        if (!tracking || path.Count == 0)
            return false;
        float elapsed = Time.time - planTime;
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0; i < path.Count; i++)
        {
            BallFlight.Sample s = path[i];
            if (s.time < elapsed)
                continue;
            float flat = new Vector2(s.position.x - spot.x, s.position.z - spot.z).magnitude;
            bool near = flat <= radius && s.position.y <= CatchHeight;
            if (!near)
            {
                if (inside) break;   // the ball has been through the reach and is leaving it
                continue;
            }
            inside = true;
            float d = (s.position - spot).sqrMagnitude;
            if (d < best)
            {
                best = d;
                point = s.position;
                timeFromNow = s.time - elapsed;
            }
        }
        return inside;
    }

    /// The ball has reached this fielder's hands. False if it is no longer his to take.
    public bool Gather(AnimatedFielder fielder)
    {
        Main inst = Main.Instance;
        if (!tracking || inst.theBallRigidBody.isKinematic || inst.gameState != eGameState.InGame_BallHitLoop)
            return false;
        tracking = false;
        Take(fielder, fielder.name);
        return true;
    }

    /// Stop the ball dead and give it to this fielder (or the keeper, fielder null). Only called
    /// once the ball is actually in the hands - the holder carries it from where it was met.
    public static void Take(AnimatedFielder fielder, string name)
    {
        Main inst = Main.Instance;
        Rigidbody rb = inst.theBallRigidBody;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        // A kinematic body with interpolation overwrites the transform writes that carry the ball.
        rb.interpolation = RigidbodyInterpolation.None;
        inst.theBallScript.myParticles.Clear();
        inst.theBallScript.myParticles.enabled = false;
        if (fielder != null)
            fielder.TakeBall();
        inst.currentFielderName = name;
        inst.gameState = eGameState.InGame_BallFielded;
    }
}
