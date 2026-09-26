using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fielding. Once the ball is hit it predicts the ball's whole path - flight, bounces, roll - with
/// the same model the ball itself uses (BallFlight), and sends the fielder who can get to it first,
/// plus one backup, to where they can meet it. It re-plans every ReplanInterval so a prediction
/// that drifts is corrected, and any fielder the ball passes within reach takes it.
///
/// "Can get to it" is: reaction time + distance / run speed, not later than the ball gets there,
/// with the ball low enough to reach - CatchHeight in the air, which makes it a catch if it has
/// not bounced, anything on the ground otherwise.
///
/// Class name kept from the old intercept-time sorter so the scene component stays wired.
/// </summary>
public class AnimatedFielderManagement : MonoBehaviour
{
    public const float ReactionTime = 0.15f;
    /// How far a fielder can reach (or dive) to take the ball, horizontally and in height.
    public const float CatchRadius = 1.2f;
    public const float CatchHeight = 2.4f;
    public const float ReplanInterval = 0.1f;
    private const float PredictStep = 0.02f;
    private const float PredictTime = 12f;

    private readonly List<AnimatedFielder> fielders = new List<AnimatedFielder>();
    private readonly List<BallFlight.Sample> path = new List<BallFlight.Sample>(1024);
    private float nextPlan;
    private Vector3 previousBall;
    private bool tracking;
    private float boundaryRadius = 56f;

    void Start()
    {
        foreach (AnimatedFielder f in GetComponentsInChildren<AnimatedFielder>())
            fielders.Add(f);
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
            previousBall = Main.Instance.theBallRigidBody.position;
            nextPlan = 0f;
        }
        else if (state != eGameState.InGame_BallHitLoop)
        {
            tracking = false;
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

        // Swept reach test: the ball's path over this step against each fielder's reach.
        Vector3 now = ball.position;
        foreach (AnimatedFielder f in fielders)
        {
            if (f.isActiveAndEnabled && WithinReach(previousBall, now, f.transform.position, CatchRadius, CatchHeight))
            {
                Take(f, f.name);
                return;
            }
        }
        previousBall = now;

        if (Time.time >= nextPlan)
        {
            nextPlan = Time.time + ReplanInterval;
            Plan(now, ball.linearVelocity);
        }
    }

    /// Plan who goes where, from the ball's current state.
    private void Plan(Vector3 position, Vector3 velocity)
    {
        float edge = boundaryRadius + 1f;
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, PredictTime, path,
            s => new Vector2(s.position.x, s.position.z).magnitude > edge, PredictStep);
        if (path.Count == 0)
            return;

        AnimatedFielder first = null, second = null;
        float firstTime = float.MaxValue, secondTime = float.MaxValue;
        Vector3 firstPoint = Vector3.zero, secondPoint = Vector3.zero;
        foreach (AnimatedFielder f in fielders)
        {
            if (!f.isActiveAndEnabled)
                continue;
            if (EarliestMeet(f, out float t, out Vector3 point))
            {
                if (t < firstTime)
                {
                    second = first; secondTime = firstTime; secondPoint = firstPoint;
                    first = f; firstTime = t; firstPoint = point;
                }
                else if (t < secondTime)
                {
                    second = f; secondTime = t; secondPoint = point;
                }
            }
        }

        if (first == null)
        {
            // Nobody gets there before the rope: send whoever is least late to cut it off.
            first = LeastLate(out firstPoint);
        }

        foreach (AnimatedFielder f in fielders)
        {
            if (f == first) f.SetTarget(firstPoint);
            else if (f == second) f.SetTarget(secondPoint);
            else f.Stop();
        }
    }

    private bool EarliestMeet(AnimatedFielder f, out float time, out Vector3 point)
    {
        Vector3 at = f.transform.position;
        float speed = f.RunSpeed;
        float head = f.HasTarget ? 0f : ReactionTime;
        for (int i = 0; i < path.Count; i++)
        {
            BallFlight.Sample s = path[i];
            if (s.position.y > CatchHeight)
                continue;
            if (new Vector2(s.position.x, s.position.z).magnitude > boundaryRadius)
                break;
            float run = Mathf.Max(0f, new Vector2(s.position.x - at.x, s.position.z - at.z).magnitude - CatchRadius * 0.5f);
            if (head + run / speed <= s.time)
            {
                time = s.time;
                point = s.position;
                return true;
            }
        }
        time = 0f;
        point = Vector3.zero;
        return false;
    }

    private AnimatedFielder LeastLate(out Vector3 point)
    {
        AnimatedFielder best = null;
        float bestLate = float.MaxValue;
        point = Vector3.zero;
        foreach (AnimatedFielder f in fielders)
        {
            if (!f.isActiveAndEnabled)
                continue;
            Vector3 at = f.transform.position;
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

    /// Stop the ball dead and give it to this fielder.
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
