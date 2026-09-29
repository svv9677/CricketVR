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
    /// The backup stands this far behind the keeper, on the line of the throw.
    private const float BackupBehind = 8f;

    public static ThrowBack Instance { get; private set; }
    /// The ball is still on its way back to the bowler.
    public static bool Busy => Instance != null && Instance.busy;
    /// The keeper has had it this ball: the next ball can be offered (the relay carries on).
    public static bool KeeperHasHadIt => Instance != null && Instance.keeperHad;

    private enum Phase { Idle, Winding, Flying }

    private struct Segment
    {
        public Vector3 start, velocity;
        public float from, to;
    }

    private readonly List<Segment> flight = new List<Segment>();
    private Phase phase;
    private bool busy;
    /// From the moment the ball is dead until it is with the bowler or the next ball starts. Not
    /// tied to the dead-ball states: Ready comes as soon as the keeper has it, mid-relay.
    private bool active, keeperHad;
    private Vector3 flightTo, flightThrower;
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
        busy = active = keeperHad = false;
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
                if (!active && DeadBall(inst.gameState))
                    active = true;
                if (!active)
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
        // With the bowler: home. If he fielded it himself, he stops fielding and walks back to
        // his mark with it in his bowling hand.
        if (IsBowler(holder))
        {
            if (holder is AnimatedFielder && bowlerHands != null)
            {
                // Finish the take first (the give, the ball up to his chest), then walk back.
                if (holder.HeldFor < HoldBeforeThrow)
                {
                    busy = false;
                    return;
                }
                SendBowlerBack();
                bowlerHands.TakeThrow(Vector3.zero);
                holder = bowlerHands;
            }
            busy = false;
            active = false;
            if (holder is BowlerHands hands)
                hands.Carry(inst.theBall.transform);
            return;
        }
        busy = true;
        // The keeper has it: the next ball can be offered, and the bowler starts back to his mark
        // for the relay to find him there.
        if (holder == (IThrowPlayer)keeper && !keeperHad)
        {
            keeperHad = true;
            SendBowlerBack();
        }
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
            thrownInBy = holder;
        }
        phase = Phase.Winding;
    }

    /// The bowler stops fielding (if he was) and walks back toward his mark.
    private void SendBowlerBack()
    {
        AnimatedBowler bowler = AnimatedBowler.Instance;
        if (bowler == null)
            return;
        BowlerFielding fielding = bowler.GetComponent<BowlerFielding>();
        if (fielding != null && fielding.Fielding)
            fielding.StopFielding();
        bowler.WalkBackToMark();
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
        flightTo = to;
        flightThrower = holder.Position;
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
        // A receiver on the move (the bowler walking back) is where the throw ends up: the arc
        // bends onto where his hands are now, more toward the end, so there is no jump at the take.
        float along = Mathf.Clamp01(t / Mathf.Max(flightTime, 1e-3f));
        at += (receiver.CatchPointFrom(flightThrower) - flightTo) * (along * along * (3f - 2f * along));
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
        foreach (AnimatedFielder f in AnimatedFielder.All)
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
        foreach (AnimatedFielder f in AnimatedFielder.All)
            if (f.IsBowler)
                return f;
        return bowlerHands;
    }

    /// Whoever threw it in to the keeper: never the relay man for the same ball.
    private IThrowPlayer thrownInBy;

    /// From the keeper: always through an inner-circle fielder - the one on the shortest way
    /// round (keeper to him to the bowler) - and straight to the bowler only when there is nobody
    /// inside the circle at all. From the relay man: the bowler.
    private IThrowPlayer BowlerOrRelay(bool fromKeeper)
    {
        IThrowPlayer bowler = BowlerReceiver();
        if (bowler == null || !fromKeeper)
            return bowler;
        Vector3 from = Flat(holder.Position), to = Flat(bowler.Position);
        AnimatedFielder best = null;
        float bestWay = float.MaxValue;
        foreach (AnimatedFielder f in AnimatedFielder.All)
        {
            if (f.IsBowler || f.HoldingBall || ReferenceEquals(f, thrownInBy) || Flat(f.StartPosition).magnitude > InnerRing)
                continue;
            Vector3 p = Flat(f.Position);
            float way = (p - from).magnitude + (to - p).magnitude;
            if (way < bestWay) { bestWay = way; best = f; }
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
        foreach (AnimatedFielder f in AnimatedFielder.All)
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

        private BowlerFielding Hands => bowler.GetComponent<BowlerFielding>();

        /// In front of his chest on the thrower's side: where his hands go up to take it.
        public Vector3 CatchPointFrom(Vector3 from)
        {
            Vector3 at = bowler.transform.position;
            Vector3 toThrower = Flat(from - at);
            Vector3 side = toThrower.sqrMagnitude > 1e-4f ? toThrower.normalized : bowler.transform.forward;
            return at + Vector3.up * 1.3f + side * 0.45f;
        }

        public void BeginThrow(Vector3 target) { }
        public void Release() => holding = false;

        /// He sees it coming: hands up to it as he walks (BowlerFielding).
        public void ExpectBall(Vector3 point, Vector3 from, float arrivalTime)
        {
            BowlerFielding hands = Hands;
            if (hands != null)
                hands.ExpectThrow(point, from, arrivalTime);
        }

        public void TakeThrow(Vector3 incoming)
        {
            holding = true;
            since = Time.time;
            BowlerFielding hands = Hands;
            if (hands != null)
                hands.TookThrow();
        }

        public void Drop() => holding = false;

        /// Between his hands while he takes it, then in his bowling hand.
        public void Carry(Transform ball)
        {
            BowlerFielding hands = Hands;
            ball.position = hands != null && hands.HoldingInBothHands(out Vector3 at) ? at : Hand;
        }
    }
}
