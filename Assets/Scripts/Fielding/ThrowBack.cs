using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Getting the ball back to the bowler once it is dead, the way a fielding side does: whoever
/// fielded it throws it in to the keeper, and the keeper passes it back to the bowler - straight
/// to him when he is near enough, otherwise through the inner fielder nearest the way. A throw in
/// to the keeper has the nearest other fielder backing it up behind him.
///
/// Throws are choreographed rather than simulated: once the ball is dead nothing can be scored
/// off it, and a missed relay catch would only leave the ball lying about. Each is a gravity arc
/// from the thrower's hand to the receiver's hands, taken on the full within reach (MaxDirect)
/// and with one bounce short of him beyond it. The thrower plays the throw animation and the ball
/// leaves as his arm comes over; the receiver faces it with his hands up where it will arrive.
///
/// Main waits for this (Busy) before setting up the next ball.
/// </summary>
public class ThrowBack : MonoBehaviour
{
    /// A fielder's throw on the full reaches this far; the keeper's pass this far.
    private const float FielderDirect = 38f, KeeperDirect = 32f;
    /// Fastest throw, m/s, and flight time the throws aim for.
    private const float MaxThrowSpeed = 27f, AimTime = 0.9f;
    /// A long throw bounces this far short of the receiver.
    private const float BounceShort = 7f;
    /// Held this long before throwing.
    private const float HoldBeforeThrow = 0.8f;
    /// Fielders this close to the middle make the inner ring (relay men).
    private const float InnerRing = 30f;
    /// A relay man further than this from the middle of the keeper-to-bowler line is no help.
    private const float RelayOffLine = 12f;
    /// The backup stands this far behind the keeper, on the line of the throw.
    private const float BackupBehind = 8f;

    public static ThrowBack Instance { get; private set; }
    /// The ball is still on its way back to the bowler.
    public static bool Busy => Instance != null && Instance.busy;

    private enum Phase { Idle, Winding, Flying }

    private struct Segment
    {
        public Vector3 start, velocity;
        public float from, to;
    }

    private readonly List<Segment> flight = new List<Segment>();
    private Phase phase;
    private bool busy;
    private IThrowPlayer holder, receiver;
    private float flightStart, flightTime;
    private bool relayToBowler;
    private AnimatedFielder backup;
    private KeeperCatcher keeper;
    private BowlerHands bowlerHands;

    private void Awake() => Instance = this;

    private void Start()
    {
        keeper = Main.Instance != null && Main.Instance.theKeeper != null
            ? Main.Instance.theKeeper.GetComponentInChildren<KeeperCatcher>() : null;
        if (AnimatedBowler.Instance != null)
            bowlerHands = new BowlerHands(AnimatedBowler.Instance);
        Main.Instance.onGameStateChanged += HandleGameState;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= HandleGameState;
        if (Instance == this)
            Instance = null;
    }

    private void HandleGameState()
    {
        // A new ball: whatever was going on is over (the bowler has it in his run-up).
        if (Main.Instance.gameState == eGameState.InGame_SelectDelivery)
            Clear();
    }

    private void Clear()
    {
        phase = Phase.Idle;
        busy = false;
        holder = receiver = thrownInBy = null;
        relayToBowler = false;
        EndBackup();
        if (bowlerHands != null)
            bowlerHands.Drop();
    }

    private static bool DeadBall(eGameState s) =>
        s == eGameState.InGame_BallFielded || s == eGameState.InGame_BallFielded_Loop ||
        s == eGameState.InGame_BallMissed || s == eGameState.InGame_BallMissedLoop;

    private void LateUpdate()
    {
        Main inst = Main.Instance;
        if (inst == null)
            return;
        switch (phase)
        {
            case Phase.Idle:
                if (!DeadBall(inst.gameState))
                {
                    busy = false;
                    if (bowlerHands != null && bowlerHands.HoldingBall)
                        bowlerHands.Carry(inst.theBall.transform);
                    return;
                }
                Decide(inst);
                break;
            case Phase.Winding:
                if (holder.ReadyToRelease)
                    Launch(inst);
                break;
            case Phase.Flying:
                Fly(inst);
                break;
        }
    }

    /// Who has it, and whether it is time for him to throw it on.
    private void Decide(Main inst)
    {
        holder = FindHolder();
        if (holder == null)
        {
            busy = false;
            return;
        }
        // With the bowler: home.
        if (IsBowler(holder))
        {
            busy = false;
            if (holder is BowlerHands hands)
                hands.Carry(inst.theBall.transform);
            return;
        }
        busy = true;
        if (holder.HeldFor < HoldBeforeThrow)
            return;
        bool isKeeper = holder == (IThrowPlayer)keeper;
        receiver = isKeeper || relayToBowler ? BowlerOrRelay(isKeeper) : keeper;
        if (receiver == null)
            receiver = BowlerReceiver();
        if (receiver == null)
            return;
        holder.BeginThrow(receiver.CatchPointFrom(holder.Position));
        if (receiver == (IThrowPlayer)keeper)
        {
            SendBackup(holder.Position);
            // A bowler who chased it heads back to his end for the ball to come home to.
            if (BowlerReceiver() is AnimatedFielder bowler && !ReferenceEquals(bowler, holder))
                bowler.WalkBackToStart();
            thrownInBy = holder;
        }
        phase = Phase.Winding;
    }

    private void Launch(Main inst)
    {
        Vector3 from = holder.ReleasePoint;
        Vector3 to = receiver.CatchPointFrom(holder.Position);
        bool keeperThrowing = holder == (IThrowPlayer)keeper;
        Plan(from, to, keeperThrowing ? KeeperDirect : FielderDirect);
        Debug.Log($"[ThrowBack] {holder.Name} -> {receiver.Name}: {Flat(to - from).magnitude:F0} m, " +
                  $"{(flight.Count > 1 ? "one bounce" : "on the full")}, {flightTime:F2} s");
        holder.Release();
        receiver.ExpectBall(to, holder.Position, Time.time + flightTime);
        Rigidbody rb = inst.theBallRigidBody;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        Collider c = inst.theBall.GetComponent<Collider>();
        if (c != null)
            c.enabled = false;   // Main switches it back on at the next release
        flightStart = Time.time;
        // The keeper passing it on starts the relay home; a relay man passes it to the bowler.
        relayToBowler = keeperThrowing;
        phase = Phase.Flying;
    }

    private void Fly(Main inst)
    {
        float t = Time.time - flightStart;
        Vector3 at = Evaluate(Mathf.Min(t, flightTime), out Vector3 velocity);
        inst.theBall.transform.position = at;
        inst.theBallRigidBody.position = at;
        if (t < flightTime)
            return;
        receiver.TakeThrow(velocity);
        if (receiver == (IThrowPlayer)keeper)
            EndBackup();
        // Whoever takes it from the keeper is either the bowler or the relay man, who throws on
        // to the bowler; from anyone else it came in to the keeper.
        relayToBowler = relayToBowler && !IsBowler(receiver);
        holder = receiver;
        receiver = null;
        phase = Phase.Idle;
    }

    // ---- Who ------------------------------------------------------------------------------------

    private IThrowPlayer FindHolder()
    {
        if (keeper != null && keeper.HoldingBall)
            return keeper;
        foreach (AnimatedFielder f in FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
            if (f.HoldingBall)
                return f;
        if (bowlerHands != null && bowlerHands.HoldingBall)
            return bowlerHands;
        return null;
    }

    private static bool IsBowler(IThrowPlayer p) => p is BowlerHands || (p is AnimatedFielder f && f.IsBowler);

    /// The bowler: fielding (his AnimatedFielder), or stood at his end (his hands).
    private IThrowPlayer BowlerReceiver()
    {
        foreach (AnimatedFielder f in FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
            if (f.IsBowler)
                return f;
        return bowlerHands;
    }

    /// Whoever threw it in to the keeper: never the relay man for the same ball.
    private IThrowPlayer thrownInBy;

    /// From the keeper: the bowler if he is within a pass, otherwise the inner fielder nearest
    /// the middle of the way (and near it: RelayOffLine), else the bowler with a bounce.
    /// From the relay man: the bowler.
    private IThrowPlayer BowlerOrRelay(bool fromKeeper)
    {
        IThrowPlayer bowler = BowlerReceiver();
        if (bowler == null || !fromKeeper)
            return bowler;
        Vector3 from = holder.Position;
        if (Flat(bowler.Position - from).magnitude <= KeeperDirect)
            return bowler;
        Vector3 middle = 0.5f * (from + bowler.Position);
        AnimatedFielder best = null;
        float bestD = float.MaxValue;
        foreach (AnimatedFielder f in FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
        {
            if (f.IsBowler || f.HoldingBall || ReferenceEquals(f, thrownInBy) || Flat(f.StartPosition).magnitude > InnerRing)
                continue;
            float d = Flat(f.Position - middle).magnitude;
            if (d < bestD && d < RelayOffLine) { bestD = d; best = f; }
        }
        return best != null ? best : bowler;
    }

    /// The fielder nearest the spot behind the keeper runs to back up the throw.
    private void SendBackup(Vector3 thrower)
    {
        EndBackup();
        if (keeper == null)
            return;
        Vector3 line = Flat(keeper.Position - thrower);
        if (line.sqrMagnitude < 1e-4f)
            return;
        Vector3 spot = keeper.Position + line.normalized * BackupBehind;
        float best = float.MaxValue;
        foreach (AnimatedFielder f in FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.None))
        {
            if (f.IsBowler || f.HoldingBall || (IThrowPlayer)f == holder)
                continue;
            float d = Flat(f.Position - spot).magnitude;
            if (d < best && d < 35f) { best = d; backup = f; }
        }
        if (backup != null)
            backup.BackUp(spot);
    }

    private void EndBackup()
    {
        if (backup != null)
            backup.EndBackUp();
        backup = null;
    }

    // ---- The flight -----------------------------------------------------------------------------

    /// A gravity arc from `from` to `to`, aimed at about AimTime in the air (faster for a long
    /// one, up to MaxThrowSpeed); beyond `direct` metres, one bounce BounceShort before `to`.
    private void Plan(Vector3 from, Vector3 to, float direct)
    {
        flight.Clear();
        Vector3 flat = Flat(to - from);
        float d = Mathf.Max(0.1f, flat.magnitude);
        Vector3 dir = flat / d;
        if (d <= direct)
        {
            float h = Mathf.Clamp(d / AimTime, 8f, MaxThrowSpeed);
            AddSegment(from, to, dir, h, 0f);
        }
        else
        {
            Vector3 bounce = to - dir * BounceShort;
            bounce.y = BallFlight.GroundY(bounce) + BallFlight.Radius;
            float t1 = AddSegment(from, bounce, dir, MaxThrowSpeed, 0f);
            AddSegment(bounce, to, dir, MaxThrowSpeed * 0.7f, t1);
        }
        flightTime = flight[flight.Count - 1].to;
    }

    private float AddSegment(Vector3 from, Vector3 to, Vector3 dir, float h, float startTime)
    {
        float d = Flat(to - from).magnitude;
        float t = Mathf.Max(0.05f, d / h);
        float g = -Physics.gravity.y;
        float vy = (to.y - from.y + 0.5f * g * t * t) / t;
        flight.Add(new Segment { start = from, velocity = dir * h + Vector3.up * vy, from = startTime, to = startTime + t });
        return startTime + t;
    }

    private Vector3 Evaluate(float t, out Vector3 velocity)
    {
        Segment s = flight[flight.Count - 1];
        foreach (Segment seg in flight)
            if (t <= seg.to) { s = seg; break; }
        float dt = t - s.from;
        velocity = s.velocity + Physics.gravity * dt;
        return s.start + s.velocity * dt + 0.5f * Physics.gravity * dt * dt;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    /// The bowler when he is not fielding (the ball was not hit): the ball goes to his bowling
    /// hand, which is where the next run-up takes it from.
    private class BowlerHands : IThrowPlayer
    {
        private readonly AnimatedBowler bowler;
        private bool holding;
        private float since;

        public BowlerHands(AnimatedBowler b) => bowler = b;

        public string Name => "Bowler";
        public bool HoldingBall => holding;
        public float HeldFor => holding ? Time.time - since : 0f;
        public Vector3 Position => bowler.transform.position;
        public Vector3 ReleasePoint => Hand;
        public bool ReadyToRelease => true;
        private Vector3 Hand => bowler.HandTransform != null ? bowler.HandTransform.position
                                                              : bowler.transform.position + Vector3.up * 1.3f;

        public Vector3 CatchPointFrom(Vector3 from) => Hand;
        public void BeginThrow(Vector3 target) { }
        public void Release() => holding = false;
        public void ExpectBall(Vector3 point, Vector3 from, float arrivalTime) { }
        public void TakeThrow(Vector3 incoming) { holding = true; since = Time.time; }
        public void Drop() => holding = false;
        public void Carry(Transform ball) => ball.position = Hand;
    }
}
