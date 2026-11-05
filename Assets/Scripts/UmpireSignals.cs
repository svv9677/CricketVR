using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The umpire's signals, as the Laws have them, posed over his idle after the Animator runs -
/// arms, and the hands too (2026-11-05: "better umpire signals"):
///   * out - the right arm raised in front, above the head, the index finger up out of a fist;
///   * wide - both arms straight out to the sides, hands flat, palms down;
///   * no-ball - the right arm straight out to the side, hand flat: made as he calls it (the
///     moment the ball is bowled, NoBalls.OnCalled), and again for the scorers once it is dead;
///   * free hit - after a no-ball: the right arm raised, the index finger up, circling;
///   * bye - one open hand raised straight up;
///   * leg bye - a raised knee, touched with the hand;
///   * four - the arm swept side to side across the chest, hand open, palm down;
///   * six - both arms straight up above the head, palms forward.
/// Signals queue (a no-ball, then its free hit), each rising, held, and lowered. A bowled batter
/// gets no signal - he can see the stumps. Between signals he watches the ball: head and chest
/// turn to follow it, as an umpire's do.
///
/// Fingers: the Mixamo hand's fingers bend toward the palm about their local +X (BowlerGrip
/// measured the same rig family); posed on top of the Animator's, every frame.
/// </summary>
[RequireComponent(typeof(Animator))]
public class UmpireSignals : MonoBehaviour
{
    private const float Delay = 0.4f, Rise = 0.35f, Hold = 1.6f, WaveHold = 2.4f, CircleHold = 2.2f, Fall = 0.45f;
    /// Watching the ball: how far the head and chest may turn, and how quickly they follow.
    private const float HeadYaw = 70f, HeadPitch = 35f, ChestShare = 0.3f, Follow = 6f;

    private Animator animator;
    private Transform rUpper, rLower, rHand, lUpper, lLower, lHand, rThigh, rShin, rFoot, head, chest;
    private readonly Transform[,] rFingers = new Transform[5, 3], lFingers = new Transform[5, 3];
    /// Per hand, measured at start: which way about local X bends a finger toward the palm (+1 or
    /// -1, the left hand being a mirror of the right), and the palm's normal in the hand's space.
    private float rCurl = 1f, lCurl = 1f;
    private Vector3 rPalm = Vector3.forward, lPalm = Vector3.forward;

    private struct Pending { public UmpireSignal what; public float at; }
    private readonly Queue<Pending> queue = new Queue<Pending>();
    private UmpireSignal signal;
    private float startAt = -100f;
    private Quaternion look = Quaternion.identity;

    private static readonly HumanBodyBones[,] RightFingerBones =
    {
        { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal },
        { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
        { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
        { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
        { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
    };
    private static readonly HumanBodyBones[,] LeftFingerBones =
    {
        { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal },
        { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
        { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
        { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
        { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
    };

    public UmpireSignal Showing => signal != UmpireSignal.None && Time.time >= startAt && Time.time - startAt <= Length(signal) ? signal : UmpireSignal.None;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rUpper = Bone(HumanBodyBones.RightUpperArm);
        rLower = Bone(HumanBodyBones.RightLowerArm);
        rHand = Bone(HumanBodyBones.RightHand);
        lUpper = Bone(HumanBodyBones.LeftUpperArm);
        lLower = Bone(HumanBodyBones.LeftLowerArm);
        lHand = Bone(HumanBodyBones.LeftHand);
        rThigh = Bone(HumanBodyBones.RightUpperLeg);
        rShin = Bone(HumanBodyBones.RightLowerLeg);
        rFoot = Bone(HumanBodyBones.RightFoot);
        head = Bone(HumanBodyBones.Head);
        chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
            {
                rFingers[f, j] = Bone(RightFingerBones[f, j]);
                lFingers[f, j] = Bone(LeftFingerBones[f, j]);
            }
        Calibrate(rHand, rFingers, out rCurl, out rPalm);
        Calibrate(lHand, lFingers, out lCurl, out lPalm);
    }

    /// Bend the index finger a little about +X and see which way its tip goes: toward the side the
    /// thumb is on (the palm side) or away. That gives the curl's sign, and the palm's normal.
    private static void Calibrate(Transform hand, Transform[,] fingers, out float curl, out Vector3 palmLocal)
    {
        curl = 1f;
        palmLocal = Vector3.forward;
        Transform index = fingers[1, 0], tip = fingers[1, 2] != null ? fingers[1, 2] : fingers[1, 1], thumb = fingers[0, 2] != null ? fingers[0, 2] : fingers[0, 1];
        if (hand == null || index == null || tip == null || thumb == null)
            return;
        Vector3 along = (index.position - hand.position).normalized;
        Quaternion q = index.localRotation;
        Vector3 before = tip.position;
        index.localRotation = q * Quaternion.Euler(20f, 0f, 0f);
        Vector3 moved = Vector3.ProjectOnPlane(tip.position - before, along);
        index.localRotation = q;
        Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - hand.position, along);
        if (moved.sqrMagnitude < 1e-10f) return;
        curl = Vector3.Dot(moved, thumbSide) >= 0f ? 1f : -1f;
        palmLocal = hand.InverseTransformDirection(moved.normalized * curl).normalized;
    }

    private Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

    private void OnEnable()
    {
        HUD.BallScored += OnBallScored;
        NoBalls.OnCalled += OnNoBallCalled;
    }

    private void OnDisable()
    {
        HUD.BallScored -= OnBallScored;
        NoBalls.OnCalled -= OnNoBallCalled;
    }

    private void OnNoBallCalled() => Signal(UmpireSignal.NoBall, 0.05f);

    private void OnBallScored(BallOutcome outcome)
    {
        if (outcome.Signal == UmpireSignal.None)
            return;
        // Bowled needs no signal: the stumps say it.
        if (outcome.Signal == UmpireSignal.Out && Main.Instance != null && Main.Instance.gameState == eGameState.InGame_Bowled)
            return;
        Signal(outcome.Signal, Delay);
        if (NoBalls.NextIsFreeHit && outcome.Signal == UmpireSignal.NoBall)
            Queue(UmpireSignal.FreeHit);
    }

    /// Make a signal, `after` seconds from now (cutting short whatever he is showing).
    public void Signal(UmpireSignal what, float after = 0f)
    {
        queue.Clear();
        signal = what;
        startAt = Time.time + after;
    }

    /// Make a signal once the current one is down.
    public void Queue(UmpireSignal what)
    {
        float free = Mathf.Max(Time.time, startAt + Length(signal)) + 0.15f;
        foreach (Pending p in queue) free = Mathf.Max(free, p.at + Length(p.what) + 0.15f);
        queue.Enqueue(new Pending { what = what, at = free });
    }

    private static float HoldFor(UmpireSignal s) =>
        s == UmpireSignal.Four ? WaveHold : s == UmpireSignal.FreeHit ? CircleHold : s == UmpireSignal.NoBall ? 2f : Hold;

    private static float Length(UmpireSignal s) => s == UmpireSignal.None ? 0f : Rise + HoldFor(s) + Fall;

    private void LateUpdate()
    {
        if (queue.Count > 0 && Time.time >= queue.Peek().at && Time.time - startAt > Length(signal))
        {
            Pending p = queue.Dequeue();
            signal = p.what;
            startAt = p.at;
        }
        float t = Time.time - startAt;
        bool showing = signal != UmpireSignal.None && t >= 0f && t <= Length(signal) && rUpper != null;
        WatchBall(showing ? 0.35f : 1f);
        if (!showing)
            return;
        float hold = HoldFor(signal);
        float w = t < Rise ? Mathf.SmoothStep(0f, 1f, t / Rise)
                : t > Rise + hold ? Mathf.SmoothStep(1f, 0f, (t - Rise - hold) / Fall) : 1f;
        Pose(signal, t, w);
    }

    /// Head (and some of the chest) turned toward the ball while it is in play, within a neck's
    /// reach; eased, so he follows it rather than snapping to it. `amount` 0..1.
    private void WatchBall(float amount)
    {
        if (head == null || Main.Instance == null || Main.Instance.theBall == null)
            return;
        Vector3 to = Main.Instance.theBall.transform.position - head.position;
        Quaternion target = Quaternion.identity;
        if (to.sqrMagnitude > 0.25f)
        {
            Vector3 local = transform.InverseTransformDirection(to.normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -HeadYaw, HeadYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -HeadPitch, HeadPitch);
            target = Quaternion.Euler(pitch * amount, yaw * amount, 0f);
        }
        look = Quaternion.Slerp(look, target, 1f - Mathf.Exp(-Follow * Time.deltaTime));
        // Turn about his own up and right axes, in world space, on top of the animated pose.
        look.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 0.01f) return;
        Vector3 worldAxis = transform.TransformDirection(axis);
        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(angle * ChestShare, worldAxis) * chest.rotation;
        head.rotation = Quaternion.AngleAxis(angle * (1f - ChestShare), worldAxis) * head.rotation;
    }

    private void Pose(UmpireSignal what, float t, float w)
    {
        Vector3 f = transform.forward, r = transform.right, u = Vector3.up;
        switch (what)
        {
            case UmpireSignal.Wide:
                Arm(rUpper, rLower, rHand, r, r, w);
                Arm(lUpper, lLower, lHand, -r, -r, w);
                Palm(rHand, rLower, rPalm, -u, w); Palm(lHand, lLower, lPalm, -u, w);
                Hand(rFingers, rCurl, HandShape.Flat, w); Hand(lFingers, lCurl, HandShape.Flat, w);
                break;
            case UmpireSignal.NoBall:
                Arm(rUpper, rLower, rHand, r, r, w);
                Palm(rHand, rLower, rPalm, -u, w);
                Hand(rFingers, rCurl, HandShape.Flat, w);
                break;
            case UmpireSignal.Bye:
                Arm(rUpper, rLower, rHand, u, u, w);
                Palm(rHand, rLower, rPalm, f, w);
                Hand(rFingers, rCurl, HandShape.Flat, w);
                break;
            case UmpireSignal.LegBye:
                LegBye(f, r, u, w);
                break;
            case UmpireSignal.Four:
                // Swept side to side across the chest, a little over once a second, palm down.
                Vector3 across = Quaternion.AngleAxis(Mathf.Sin((t - Rise) * Mathf.PI * 2.4f) * 50f, u) * (f * 0.85f + r * 0.15f - u * 0.1f);
                Arm(rUpper, rLower, rHand, across.normalized, across.normalized, w);
                Palm(rHand, rLower, rPalm, -u, w);
                Hand(rFingers, rCurl, HandShape.Flat, w);
                break;
            case UmpireSignal.Six:
                Arm(rUpper, rLower, rHand, (u + r * 0.2f).normalized, u, w);
                Arm(lUpper, lLower, lHand, (u - r * 0.2f).normalized, u, w);
                Palm(rHand, rLower, rPalm, f, w); Palm(lHand, lLower, lPalm, f, w);
                Hand(rFingers, rCurl, HandShape.Flat, w); Hand(lFingers, lCurl, HandShape.Flat, w);
                break;
            case UmpireSignal.Out:
                // Up in front of him, higher than the head - not straight up (that is a bye).
                Vector3 up = (f * 0.55f + u * 0.83f).normalized;
                Arm(rUpper, rLower, rHand, up, up, w);
                Palm(rHand, rLower, rPalm, -r, w);
                Hand(rFingers, rCurl, HandShape.Finger, w);
                break;
            case UmpireSignal.FreeHit:
                // The arm up, the finger up, the forearm circling over his head.
                float a = (t - Rise) * Mathf.PI * 2f * 1.3f;
                Vector3 upper = (u * 0.9f + r * 0.25f).normalized;
                Vector3 lower = (u + (r * Mathf.Cos(a) + f * Mathf.Sin(a)) * 0.35f).normalized;
                Arm(rUpper, rLower, rHand, upper, lower, w);
                Palm(rHand, rLower, rPalm, f, w);
                Hand(rFingers, rCurl, HandShape.Finger, w);
                break;
        }
    }

    private enum HandShape { Flat, Finger }

    /// Curl (0 straight .. 1 closed) per finger, thumb first, for each hand shape.
    private static readonly float[] FlatCurl = { 0.05f, 0.02f, 0.02f, 0.04f, 0.06f };
    private static readonly float[] FingerCurl = { 0.75f, 0f, 1f, 1f, 1f };
    private static readonly float[] JointDegrees = { 70f, 95f, 65f };

    private static void Hand(Transform[,] fingers, float sign, HandShape shape, float w)
    {
        float[] curl = shape == HandShape.Flat ? FlatCurl : FingerCurl;
        for (int f = 0; f < 5; f++)
        {
            float c = curl[f];
            for (int j = 0; j < 3; j++)
            {
                Transform b = fingers[f, j];
                if (b == null) continue;
                // Straighten what the idle curls (a flat hand) or close it (a fist), toward the
                // shape's curl; the thumb folds across less.
                Quaternion target = Quaternion.Euler(sign * c * JointDegrees[j] * (f == 0 ? 0.6f : 1f), 0f, 0f);
                b.localRotation = Quaternion.Slerp(b.localRotation, ResetCurl(b.localRotation) * target, w);
            }
        }
    }

    /// The joint's rotation with its bend (about local X) taken out, keeping its twist and spread.
    private static Quaternion ResetCurl(Quaternion q)
    {
        Vector3 e = q.eulerAngles;
        return Quaternion.Euler(0f, e.y, e.z);
    }

    /// Turn the hand about the forearm so the palm faces `dir`, by `w`.
    private static void Palm(Transform hand, Transform lower, Vector3 palmLocal, Vector3 dir, float w)
    {
        if (hand == null || lower == null) return;
        Vector3 axis = (hand.position - lower.position).normalized;
        Vector3 palmNow = Vector3.ProjectOnPlane(hand.TransformDirection(palmLocal), axis).normalized;
        Vector3 want = Vector3.ProjectOnPlane(dir, axis);
        if (want.sqrMagnitude < 1e-4f || palmNow.sqrMagnitude < 1e-4f) return;
        float angle = Vector3.SignedAngle(palmNow, want.normalized, axis);
        hand.rotation = Quaternion.AngleAxis(angle * w, axis) * hand.rotation;
    }

    /// Knee raised in front, the hand down on it.
    private void LegBye(Vector3 f, Vector3 r, Vector3 u, float w)
    {
        if (rThigh == null || rShin == null || rFoot == null)
            return;
        Aim(rThigh, rShin, (f * 0.9f - u * 0.45f).normalized, w);
        Aim(rShin, rFoot, (-u + f * 0.1f).normalized, w);
        Aim(rUpper, rLower, (-u * 0.75f + f * 0.45f + r * 0.15f).normalized, w);
        Aim(rLower, rHand, (rShin.position - rLower.position).normalized, w);
    }

    private static void Arm(Transform upper, Transform lower, Transform hand, Vector3 upperDir, Vector3 lowerDir, float w)
    {
        Aim(upper, lower, upperDir, w);
        Aim(lower, hand, lowerDir, w);
    }

    /// Turn `bone` so the line from it to `child` points along `dir`, by `w`.
    private static void Aim(Transform bone, Transform child, Vector3 dir, float w)
    {
        if (bone == null || child == null)
            return;
        Vector3 now = child.position - bone.position;
        if (now.sqrMagnitude < 1e-8f || dir.sqrMagnitude < 1e-8f)
            return;
        Quaternion full = Quaternion.FromToRotation(now, dir) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, full, w);
    }
}
