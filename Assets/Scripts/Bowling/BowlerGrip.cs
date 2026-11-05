using UnityEngine;

/// <summary>
/// The bowler's fingers on the ball (2026-11-05: "correct hand positions for swinging balls,
/// cutting balls, and variations... the fingers holding the ball correctly and the ball released
/// with the right grip"). Posed over the animation every frame, after the Animator and before
/// AnimatedBowler carries the ball (it carries it to where this puts it).
///
/// A grip (BowlerGrips) says where the ball sits against the hand, which fingers are on it and how
/// far apart, where the seam runs, and what the wrist does into the release. The fingers are not
/// keyed: each one on the ball curls until its tip meets the ball's surface (a short search per
/// finger), the thumb finds the point under the ball it should be on, and the fingers off the ball
/// tuck in - so the hold stays right whatever the arm animation does with the hand.
///
/// Into the release (the last ReleaseWindow seconds before the clip's ReleaseBall event) the wrist
/// does the grip's work - cocked to angle the seam for swing, rolled across the ball for a cutter
/// or a spinner, snapped forward for a top-spinner - and at the release the spinning finger flicks
/// and the hand opens. The ball leaves with the seam where the fingers had it and the spin they
/// gave it (BallVisual).
///
/// Bones: the Mixamo rig's right hand (mixamorig5:RightHand{Thumb,Index,Middle,Ring,Pinky}1-4);
/// measured on it: each finger bends toward the palm about its local +X and spreads toward the
/// index side about local +Z.
/// </summary>
[DefaultExecutionOrder(-10)]
public class BowlerGrip : MonoBehaviour
{
    public const float BlendIn = 0.25f, BlendOut = 0.35f;
    /// The wrist's work happens over this long before the release, and unwinds after it.
    public const float ReleaseWindow = 0.32f, AfterRelease = 0.3f;
    /// Fingertip skin: the tip is this far outside the ball's surface when it touches.
    private const float Skin = 0.009f;
    private const float BallRadius = 0.036f;

    private Animator animator;
    private AnimatedBowler bowler;
    private Transform hand, forearm, offset;
    private Vector3 offsetLocal;
    /// [finger, joint]: joints 0-2 bend, 3 is the tip.
    private readonly Transform[,] bones = new Transform[5, 4];
    private readonly Quaternion[,] neutral = new Quaternion[5, 3];
    private readonly Quaternion[,] lastWritten = new Quaternion[5, 3];
    private Quaternion handLastWritten;
    private BallVisual ballVisual;

    private float weight;
    private float releasedAt = -100f;
    private Vector3 releaseSpin;
    private bool wasCarrying;
    private BowlerGrips.Grip grip = BowlerGrips.SeamUp;

    /// The grip in use (for tests and the debug overlay).
    public BowlerGrips.Grip Current => grip;
    public float Weight => weight;

    private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    private void Awake()
    {
        animator = GetComponent<Animator>();
        bowler = GetComponent<AnimatedBowler>();
        hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        if (hand == null)
        {
            enabled = false;
            return;
        }
        forearm = hand.parent;
        string prefix = hand.name.Substring(0, hand.name.Length - "RightHand".Length) + "RightHand";
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 4; j++)
                bones[f, j] = FindDeep(hand, prefix + FingerNames[f] + (j + 1));
        for (int f = 0; f < 5; f++)
        {
            if (bones[f, 0] == null || bones[f, 3] == null)
            {
                Debug.LogWarning($"BowlerGrip: no {FingerNames[f]} bones under {hand.name}; grips off.", this);
                enabled = false;
                return;
            }
            for (int j = 0; j < 3; j++)
                neutral[f, j] = lastWritten[f, j] = bones[f, j].localRotation;
        }
        handLastWritten = hand.localRotation;
        if (bowler != null && bowler.HandTransform != null)
        {
            offset = bowler.HandTransform;
            offsetLocal = offset.localPosition;
        }
    }

    private void Start()
    {
        Main inst = Main.Instance;
        if (inst != null && inst.theBall != null)
        {
            ballVisual = inst.theBall.GetComponent<BallVisual>();
            if (ballVisual == null)
                ballVisual = inst.theBall.AddComponent<BallVisual>();
        }
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            Transform r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    private void LateUpdate()
    {
        Main inst = Main.Instance;
        if (inst == null || bowler == null)
            return;
        bool carrying = bowler.CarryingBall;
        if (carrying && !wasCarrying)
            grip = BowlerGrips.For(inst.currentBowlingConfig);
        if (!carrying && wasCarrying)
            OnReleased(inst);
        wasCarrying = carrying;

        float sinceRelease = Time.time - releasedAt;
        bool following = !carrying && sinceRelease < AfterRelease + BlendOut;
        float target = carrying ? 1f : following ? Mathf.Clamp01(1f - (sinceRelease - AfterRelease) / BlendOut) : 0f;
        weight = carrying ? Mathf.MoveTowards(weight, 1f, Time.deltaTime / BlendIn) : target;

        RestoreUnanimated();
        if (weight <= 0.001f)
        {
            if (offset != null) offset.localPosition = offsetLocal;
            return;
        }

        // The wrist's work into the release, held through the follow-through.
        float into = carrying ? Mathf.Clamp01(1f - TimeToRelease() / ReleaseWindow) : 1f;
        into = Mathf.SmoothStep(0f, 1f, into);
        float offSign = inst.BattingStyle == eBattingStyle.LeftHanded ? -1f : 1f;
        lastOffSign = offSign;
        Wrist(into * weight, offSign);

        Frame(out Vector3 fwd, out Vector3 palm, out Vector3 side);
        Vector3 centre = hand.position + fwd * grip.ball.x + palm * grip.ball.y + side * grip.ball.z;

        float flick = carrying ? 0f : Mathf.Clamp01(sinceRelease / 0.08f);
        PoseFingers(centre, fwd, palm, side, flick);
        handLastWritten = hand.localRotation;

        if (carrying)
        {
            if (offset != null)
                offset.position = centre;            // AnimatedBowler carries the ball to here
            if (ballVisual != null)
                ballVisual.Hold(SeamRotation(fwd, palm, side, into, offSign));
        }
    }

    /// Bones the Animator does not drive (the rig maps few finger bones) would keep last frame's
    /// pose and the curl would stack: put those back to neutral first.
    private void RestoreUnanimated()
    {
        for (int f = 0; f < 5; f++)
            for (int j = 0; j < 3; j++)
                if (bones[f, j].localRotation == lastWritten[f, j])
                    bones[f, j].localRotation = neutral[f, j];
    }

    /// The hand's frame: fingers' direction, palm normal (the side the fingers curl to), and
    /// across the knuckles toward the index finger.
    private void Frame(out Vector3 fwd, out Vector3 palm, out Vector3 side)
    {
        fwd = (bones[2, 0].position - hand.position).normalized;
        side = Vector3.ProjectOnPlane(bones[1, 0].position - bones[4, 0].position, fwd).normalized;
        palm = Vector3.Cross(fwd, side);
    }

    private Quaternion SeamRotation(Vector3 fwd, Vector3 palm, Vector3 side, float into, float offSign)
    {
        // The seam's axis (BallVisual local X), and the ball's local Y along the fingers.
        Vector3 axis = SeamAxisNow(fwd, palm, side, into, offSign);
        Vector3 up = Vector3.ProjectOnPlane(fwd, axis);
        if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(palm, axis);
        // LookRotation(z, y): local Z = X x Y.
        return Quaternion.LookRotation(Vector3.Cross(axis, up.normalized), up.normalized);
    }

    /// Seconds to the ReleaseBall event of the clip playing; large if none is coming.
    public float TimeToRelease()
    {
        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        for (int c = 0; c < clips.Length; c++)
        {
            AnimationClip clip = clips[c].clip;
            if (clip == null) continue;
            foreach (AnimationEvent e in clip.events)
            {
                if (e.functionName != "ReleaseBall") continue;
                float t = (st.normalizedTime % 1f) * clip.length;
                float rate = Mathf.Max(0.01f, Mathf.Abs(st.speed * st.speedMultiplier * animator.speed));
                return e.time >= t ? (e.time - t) / rate : 99f;
            }
        }
        return 99f;
    }

    /// The wrist: the grip's turn about the travel, up and right axes, by `w`.
    private void Wrist(float w, float offSign)
    {
        if (w <= 0f) return;
        Vector3 travel = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, travel);
        // Swing: the seam angled toward the slips (outswing) or fine leg (inswing). A turn about
        // up by +a takes the seam's line from the batter toward the bowler's right (-off side).
        float seamTurn = -grip.seamAngle * offSign;
        Quaternion q = Quaternion.AngleAxis(seamTurn * w, Vector3.up) *
                       Quaternion.AngleAxis(grip.palmTurn * w, Vector3.up) *
                       Quaternion.AngleAxis(grip.roll * w, travel) *      // + is clockwise from behind: off-break
                       Quaternion.AngleAxis(grip.snap * w, right);       // + takes the fingers forward over the ball
        hand.rotation = q * hand.rotation;
    }

    private void PoseFingers(Vector3 centre, Vector3 fwd, Vector3 palm, Vector3 side, float flick)
    {
        for (int f = 1; f < 5; f++)
        {
            float spread = grip.spread[f] * weight;
            bool flicking = f == grip.flickFinger && flick > 0f;
            float curl;
            if (flicking)
                curl = Mathf.Lerp(grip.onBall[f] ? 0.7f : grip.tuck[f], 0.15f, flick);
            else if (wasCarrying || flick <= 0f)
                curl = grip.onBall[f] ? Contact(f, spread, centre, grip.tuck[f]) : TuckTo(f, spread, centre, grip.tuck[f]);
            else
                curl = Mathf.Lerp(grip.onBall[f] ? 0.6f : grip.tuck[f], 0.25f, flick);   // the hand opens
            SetFinger(f, spread, curl * weight);
        }
        Thumb(centre, fwd, palm, side);
    }

    /// Curl (0 straight .. 1 fully) at which finger f first meets the ball, curling from straight.
    /// Swept, then refined: a finger curled past the ball comes out the other side of it, so
    /// "touching at full curl" is no test (searched that way, the fingers closed into a fist beside
    /// the ball).
    private float Contact(int f, float spread, Vector3 centre, float max)
    {
        const float Step = 0.05f;
        float top = Mathf.Max(0.2f, max);
        float lo = 0f;
        for (float t = 0f; t <= top + 1e-4f; t += Step)
        {
            SetFinger(f, spread, t);
            if (Touching(f, centre))
            {
                float hi = t;
                for (int i = 0; i < 5; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    SetFinger(f, spread, mid);
                    if (Touching(f, centre)) hi = mid; else lo = mid;
                }
                return hi;
            }
            lo = t;
        }
        return RelaxedCurl;                                          // can't reach it: relaxed, not clenched
    }

    private const float RelaxedCurl = 0.3f;

    /// A tucked finger curls to `curl`, or less if it would go into the ball.
    private float TuckTo(int f, float spread, Vector3 centre, float curl)
    {
        float t = curl;
        for (int i = 0; i < 6; i++)
        {
            SetFinger(f, spread, t);
            if (!Inside(f, centre)) return t;
            t *= 0.8f;
        }
        return t;
    }

    private bool Touching(int f, Vector3 centre)
    {
        float r = BallRadius + Skin;
        return (bones[f, 3].position - centre).sqrMagnitude <= r * r || Inside(f, centre);
    }

    /// Any joint of the finger inside the ball.
    private bool Inside(int f, Vector3 centre)
    {
        float r = BallRadius + Skin * 0.6f;
        for (int j = 1; j < 4; j++)
            if ((bones[f, j].position - centre).sqrMagnitude < r * r) return true;
        return false;
    }

    /// Joint angles for a curl: the knuckle bends least, the middle joint most.
    private static readonly float[] CurlDegrees = { 75f, 100f, 70f };

    private void SetFinger(int f, float spread, float curl)
    {
        for (int j = 0; j < 3; j++)
        {
            Quaternion q = neutral[f, j] * Quaternion.Euler(curl * CurlDegrees[j], 0f, j == 0 ? spread : 0f);
            bones[f, j].localRotation = q;
            lastWritten[f, j] = q;
        }
    }

    /// The thumb under the ball: the joint angles (searched on a small grid) that put its tip
    /// nearest the grip's thumb point on the ball's surface.
    private void Thumb(Vector3 centre, Vector3 fwd, Vector3 palm, Vector3 side)
    {
        Vector3 dir = (fwd * grip.thumbAt.x + palm * grip.thumbAt.y + side * grip.thumbAt.z).normalized;
        Vector3 goal = centre + dir * (BallRadius + Skin);
        float best = float.MaxValue, bx = 0f, bz = 0f, bc = 0f;
        for (int ix = 0; ix <= 6; ix++)
            for (int iz = 0; iz <= 6; iz++)
                for (int ic = 0; ic <= 3; ic++)
                {
                    float x = ix * 12f, z = -20f + iz * 10f, c = ic * 0.33f;
                    PoseThumb(x, z, c);
                    float d = (bones[0, 3].position - goal).sqrMagnitude;
                    if (Inside(0, centre)) d += 0.01f;
                    if (d < best) { best = d; bx = x; bz = z; bc = c; }
                }
        PoseThumb(bx * weight, bz * weight, bc * weight);
    }

    private void PoseThumb(float x, float z, float curl)
    {
        Quaternion q0 = neutral[0, 0] * Quaternion.Euler(x, 0f, z);
        Quaternion q1 = neutral[0, 1] * Quaternion.Euler(curl * 45f, 0f, 0f);
        Quaternion q2 = neutral[0, 2] * Quaternion.Euler(curl * 50f, 0f, 0f);
        bones[0, 0].localRotation = lastWritten[0, 0] = q0;
        bones[0, 1].localRotation = lastWritten[0, 1] = q1;
        bones[0, 2].localRotation = lastWritten[0, 2] = q2;
    }

    /// The ball has gone: give it the spin the fingers put on it.
    private void OnReleased(Main inst)
    {
        releasedAt = Time.time;
        if (offset != null) offset.localPosition = offsetLocal;
        if (ballVisual == null) return;
        Frame(out Vector3 fwd, out Vector3 palm, out Vector3 side);
        Vector3 travel = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        BowlingParams p = inst.currentBowlingConfig;
        float turn = p != null ? p.Effects(inst.BattingStyle == eBattingStyle.LeftHanded ? -1f : 1f).turnDegrees : 0f;
        releaseSpin = BowlerGrips.Spin(grip, travel, SeamAxisWorld(fwd, palm, side), turn);
        ballVisual.Release(releaseSpin);
    }

    /// The seam's axis now: as the fingers hold it, turning into the release to the delivery's own
    /// (for a seam-up grip: across the line of the ball, so the seam points down the pitch, turned
    /// seamAngle toward the off side for swing - the same turn the wrist makes).
    private Vector3 SeamAxisNow(Vector3 fwd, Vector3 palm, Vector3 side, float into, float offSign)
    {
        Vector3 held = (fwd * grip.seamAxis.x + palm * grip.seamAxis.y + side * grip.seamAxis.z).normalized;
        if (!grip.seamDownThePitch || into <= 0f)
            return held;
        Vector3 travel = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 across = Quaternion.AngleAxis(-grip.seamAngle * offSign, Vector3.up) * Vector3.Cross(Vector3.up, travel);
        if (Vector3.Dot(across, held) < 0f) across = -across;       // the nearer way round
        return Vector3.Slerp(held, across, into).normalized;
    }

    private float lastOffSign = 1f;

    private Vector3 SeamAxisWorld(Vector3 fwd, Vector3 palm, Vector3 side) =>
        SeamAxisNow(fwd, palm, side, 1f, lastOffSign);
}
