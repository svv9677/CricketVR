using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A wicket, made a moment (2026-11-05: "improve wicket moments"): the next ball waits a few seconds
/// (ExtraPause) while the bowling side celebrates - the bowler's arms go up, the fielders near him
/// and the catcher run in to him and join in - with the crowd's roar (MatchSounds), the umpire's
/// finger for a catch (UmpireSignals), the stumps and bails for bowled, the replay (CameraReplay),
/// the controllers' rumble (Haptics) and the batter's scorecard line under the result
/// (HUD.LastDismissal on BallResultText). Not on a no-ball or free hit (NoBalls): there is no wicket.
/// </summary>
public class WicketMoment : MonoBehaviour
{
    /// Extra seconds before the next ball after a wicket.
    public const float ExtraPause = 3.2f;
    /// Fielders this close to the bowler (or the catcher) run in to celebrate.
    private const float JoinRadius = 32f;

    /// This ball is a wicket (the next ball waits ExtraPause for the celebration).
    public static bool Active { get; private set; }

    public static event System.Action Fell;

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
        Active = false;
        if (FindFirstObjectByType<Main>() == null || FindFirstObjectByType<WicketMoment>() != null)
            return;
        new GameObject("Wicket Moment").AddComponent<WicketMoment>();
    }

    private void OnEnable() => HUD.BallScored += OnScored;
    private void OnDisable() => HUD.BallScored -= OnScored;

    private void Start()
    {
        if (Main.Instance != null) Main.Instance.onGameStateChanged += OnState;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null) Main.Instance.onGameStateChanged -= OnState;
    }

    private void OnState()
    {
        if (Main.Instance.gameState == eGameState.InGame_SelectDelivery)
            Active = false;
    }

    private void OnScored(BallOutcome o)
    {
        if (o.Signal != UmpireSignal.Out) return;
        Active = true;
        Fell?.Invoke();
        StopAllCoroutines();
        StartCoroutine(Celebrate());
    }

    private IEnumerator Celebrate()
    {
        Main inst = Main.Instance;
        AnimatedBowler bowler = AnimatedBowler.Instance;
        if (inst == null || bowler == null) yield break;
        yield return new WaitForSeconds(0.25f);
        Vector3 centre = bowler.transform.position;
        Celebration.On(bowler.gameObject, Celebration.Pose.ArmsUp, 2.6f);
        // The catcher, and those near the bowler, run in to him.
        int k = 0;
        foreach (AnimatedFielder f in AnimatedFielder.All)
        {
            if (f == null || f.IsBowler || !f.isActiveAndEnabled) continue;
            bool catcher = f.name == inst.currentFielderName;
            if (!catcher && (f.transform.position - centre).sqrMagnitude > JoinRadius * JoinRadius) continue;
            float a = (k++ * 67f + 20f) * Mathf.Deg2Rad;
            f.SetTarget(centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.5f);
            StartCoroutine(JoinIn(f, centre));
        }
        // The keeper too, if he took it.
        if (inst.theKeeper != null && inst.currentFielderName != null && inst.currentFielderName.Contains("Keeper"))
            Celebration.On(inst.theKeeper, Celebration.Pose.FistPump, 2f);
    }

    /// Arms up when he gets there (or after 2 s, if he has a long way to come).
    private IEnumerator JoinIn(AnimatedFielder f, Vector3 centre)
    {
        float until = Time.time + 2f;
        while (f != null && Time.time < until && (f.transform.position - centre).sqrMagnitude > 2.6f * 2.6f)
            yield return null;
        if (f != null)
            Celebration.On(f.gameObject, Random.value < 0.5f ? Celebration.Pose.ArmsUp : Celebration.Pose.Clap, 1.8f);
    }
}

/// <summary>
/// A celebration posed over whatever the Animator is doing: both arms thrown up, a fist pumped, or
/// clapping. Added on demand (Celebration.On), eased in and out, removes itself when done.
/// </summary>
// Before FielderLook (900), which copies the pose onto the model that is drawn.
[DefaultExecutionOrder(850)]
public class Celebration : MonoBehaviour
{
    public enum Pose { ArmsUp, FistPump, Clap }

    private Pose pose;
    private float start, length;
    private Transform rUpper, rLower, rHand, lUpper, lLower, lHand;

    public static void On(GameObject who, Pose pose, float seconds)
    {
        if (who == null) return;
        Animator a = who.GetComponentInChildren<Animator>();
        if (a == null || !a.isHuman) return;
        Celebration c = a.GetComponent<Celebration>() ?? a.gameObject.AddComponent<Celebration>();
        c.Begin(a, pose, seconds);
    }

    private Transform rFinger, lFinger;
    private readonly Transform[,] rFingers = new Transform[5, 3], lFingers = new Transform[5, 3];
    private float rCurl = 1f, lCurl = 1f;
    private Vector3 rPalm = Vector3.forward, lPalm = Vector3.forward;
    private bool calibrated;

    private static readonly HumanBodyBones[] RightRoots = { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
    private static readonly HumanBodyBones[] LeftRoots = { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal };

    private void Begin(Animator a, Pose p, float seconds)
    {
        rUpper = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rLower = a.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rHand = a.GetBoneTransform(HumanBodyBones.RightHand);
        lUpper = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lLower = a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        lHand = a.GetBoneTransform(HumanBodyBones.LeftHand);
        if (!calibrated)
        {
            Fingers(rHand, RightRoots, rFingers); Fingers(lHand, LeftRoots, lFingers);
            rFinger = rFingers[2, 0] ?? rFingers[1, 0]; lFinger = lFingers[2, 0] ?? lFingers[1, 0];
            Calibrate(rHand, rFingers, out rCurl, out rPalm);
            Calibrate(lHand, lFingers, out lCurl, out lPalm);
            calibrated = true;
        }
        pose = p;
        start = Time.time;
        length = seconds;
        enabled = true;
    }

    /// Each finger's three joints, followed down from its root (the rig maps few of them).
    internal static void Fingers(Transform hand, HumanBodyBones[] roots, Transform[,] into)
    {
        Animator a = hand != null ? hand.GetComponentInParent<Animator>() : null;
        for (int f = 0; f < 5; f++)
        {
            Transform b = a != null ? a.GetBoneTransform(roots[f]) : null;
            for (int j = 0; j < 3 && b != null; j++)
            {
                into[f, j] = b;
                b = b.childCount > 0 ? b.GetChild(0) : null;
            }
        }
    }

    /// Bend the index finger a little and see which way its tip goes - toward the thumb's side (the
    /// palm) or away: the curl's sign, and the palm's normal in the hand's space (the left hand is a
    /// mirror of the right).
    internal static void Calibrate(Transform hand, Transform[,] fingers, out float curl, out Vector3 palmLocal)
    {
        curl = 1f; palmLocal = Vector3.forward;
        Transform index = fingers[1, 0], tip = fingers[1, 2] ?? fingers[1, 1], thumb = fingers[0, 2] ?? fingers[0, 1];
        if (hand == null || index == null || tip == null || thumb == null) return;
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

    private void LateUpdate()
    {
        float t = Time.time - start;
        if (t > length) { enabled = false; return; }
        float w = Mathf.SmoothStep(0f, 1f, Mathf.Min(t / 0.25f, (length - t) / 0.4f));
        Vector3 f = transform.forward, r = transform.right, u = Vector3.up;
        switch (pose)
        {
            case Pose.ArmsUp:
                // Both arms thrown up in a V, a little bounce in them; hands open, palms forward,
                // wrists straight (left to the animation, they folded back over the head).
                float lift = 0.85f + 0.15f * Mathf.Sin(t * 9f);
                Arm(rUpper, rLower, rHand, (u * lift + r * 0.35f + f * 0.1f).normalized, (u + r * 0.2f).normalized, w);
                Arm(lUpper, lLower, lHand, (u * lift - r * 0.35f + f * 0.1f).normalized, (u - r * 0.2f).normalized, w);
                Hand(rHand, rLower, rFinger, rPalm, f, rFingers, rCurl, 0.1f, w);
                Hand(lHand, lLower, lFinger, lPalm, f, lFingers, lCurl, 0.1f, w);
                break;
            case Pose.FistPump:
                float pump = Mathf.Abs(Mathf.Sin(t * 7f));
                Arm(rUpper, rLower, rHand, (r * 0.55f + f * 0.35f + u * (0.2f + 0.5f * pump)).normalized, (u * (0.6f + pump) + f * 0.2f).normalized, w);
                Hand(rHand, rLower, rFinger, rPalm, -r, rFingers, rCurl, 1f, w);     // a fist, knuckles out
                break;
            case Pose.Clap:
                // At the chest: upper arms down and forward, forearms in to meet, palms to each other.
                float open = 0.5f + 0.5f * Mathf.Sin(t * 14f);
                Arm(rUpper, rLower, rHand, (f * 0.45f - u * 0.4f + r * 0.3f).normalized, (f * 0.5f + u * 0.7f - r * (0.45f + 0.25f * open)).normalized, w);
                Arm(lUpper, lLower, lHand, (f * 0.45f - u * 0.4f - r * 0.3f).normalized, (f * 0.5f + u * 0.7f + r * (0.45f + 0.25f * open)).normalized, w);
                Hand(rHand, rLower, rFinger, rPalm, -r, rFingers, rCurl, 0.1f, w);
                Hand(lHand, lLower, lFinger, lPalm, r, lFingers, lCurl, 0.1f, w);
                break;
        }
    }

    /// The hand straight on from the forearm, turned so the palm faces `palmDir`, the fingers
    /// curled to `curl` (0 open .. 1 fist) - by `w`.
    private static void Hand(Transform hand, Transform lower, Transform finger, Vector3 palmLocal, Vector3 palmDir,
                             Transform[,] fingers, float curlSign, float curl, float w)
    {
        if (hand == null || lower == null) return;
        Vector3 forearm = (hand.position - lower.position).normalized;
        if (finger != null) Aim(hand, finger, forearm, w);                      // wrist straight
        Vector3 palmNow = Vector3.ProjectOnPlane(hand.TransformDirection(palmLocal), forearm);
        Vector3 want = Vector3.ProjectOnPlane(palmDir, forearm);
        if (palmNow.sqrMagnitude > 1e-4f && want.sqrMagnitude > 1e-4f)
            hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(palmNow, want, forearm) * w, forearm) * hand.rotation;
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
            {
                Transform b = fingers[f, j];
                if (b == null) continue;
                Vector3 e = b.localEulerAngles;
                float bend = curlSign * curl * (j == 0 ? 70f : j == 1 ? 95f : 65f) * (f == 0 ? 0.5f : 1f);
                b.localRotation = Quaternion.Slerp(b.localRotation, Quaternion.Euler(bend, e.y, e.z), w);
            }
    }

    private static void Arm(Transform upper, Transform lower, Transform hand, Vector3 upperDir, Vector3 lowerDir, float w)
    {
        Aim(upper, lower, upperDir, w);
        Aim(lower, hand, lowerDir, w);
    }

    private static void Aim(Transform bone, Transform child, Vector3 dir, float w)
    {
        if (bone == null || child == null) return;
        Vector3 now = child.position - bone.position;
        if (now.sqrMagnitude < 1e-8f) return;
        bone.rotation = Quaternion.Slerp(bone.rotation, Quaternion.FromToRotation(now, dir) * bone.rotation, w);
    }
}
