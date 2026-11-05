using UnityEngine;

/// <summary>
/// The last word on the pose: no arm through the body. After the animation, the IK and the
/// wrists (HumanoidReach, -50) and before the ball is carried in the hands (AnimatedFielder /
/// KeeperCatcher, 0), the arms as posed are tested against the body as capsules - torso, head,
/// thighs and shins - and any arm that is in it is moved out:
///   1. the elbow swung round the shoulder-to-wrist line (the wrist stays where the IK put it, and
///      the hand keeps its turn): the smallest swing that clears it;
///   2. if the hand itself is in the body, the whole arm turned at the shoulder, just enough.
/// HumanoidReach keeps its goals clear of the body, but the clip under them (a pick-up, a catch,
/// the keeper's crouch) and the arm between goal and shoulder were never checked: forearms went
/// through thighs on pick-ups and gloves into the chest on catches (reported 2026-11-03).
///
/// The animator rewrites every bone each frame, so this never accumulates; the swing is eased
/// frame to frame so a correction comes in and goes out without a pop. Off while physics has the
/// body (FielderRagdoll), which has its own colliders.
/// </summary>
[DefaultExecutionOrder(-40)]
public class BodySelfCollision : MonoBehaviour
{
    /// Body capsules at human scale 1: radius (metres) about the bone-to-bone line. The torso's is
    /// its depth, not its width (an arm hanging at the side is ~0.2 m off the spine and clear).
    /// Matched to the surfaces as drawn: an arm at the side (~0.2 m off the spine) is clear, a
    /// forearm laid across the chest is not. At 0.13 the torso flagged a keeper's arms hanging
    /// in front of him as 10 cm in when nothing showed.
    private const float TorsoR = 0.105f, PelvisR = 0.12f, HeadR = 0.11f, ThighR = 0.08f, ShinR = 0.055f;
    /// Arm thickness at the points tested: upper arm, forearm, hand (a glove is thicker).
    private const float UpperArmR = 0.045f, ForearmR = 0.04f, HandR = 0.045f;
    /// Swing tried, degrees either way, smallest first; and how fast the swing may change.
    private static readonly float[] Swings = { 10f, 20f, 30f, 45f, 60f, 80f, 100f, 125f };
    private static readonly float[] Signs = { 1f, -1f };
    private const float SwingRate = 720f, Slack = 0.004f;

    public float handRadiusScale = 1f;

    private Animator anim;
    private HumanoidReach reach;
    private FielderRagdoll ragdoll;
    private float scale = 1f;
    private Transform hips, spine, neck, head, lThigh, lKnee, lFoot, rThigh, rKnee, rFoot;
    private readonly Arm left = new Arm(), right = new Arm();
    private Vector3 w0R;

    private class Arm
    {
        public Transform upper, lower, hand, middle;
        public float swing;   // eased, degrees
        /// The upper arm as this left it: still that at the next frame, the animator did not pose
        /// it (culled) and correcting it again would add up frame on frame.
        public Quaternion lastOut;
        public bool corrected;
        public float wanted, predicted, actual;
    }

    /// Deepest the arms went into the body this frame before and after the correction (metres).
    public float LastDepthBefore { get; private set; }
    public float LastDepthAfter { get; private set; }
    /// Worst over every body since reset (test tools): before and after the correction.
    public static float WorstBefore, WorstAfter;
    public static string WorstWhere;

    private struct Capsule { public Vector3 a, b; public float r; public string name; }
    private static readonly string[] PartNames = { "torso", "pelvis", "head", "L thigh", "R thigh", "L shin", "R shin" };
    private string lastPart;
    private readonly Capsule[] body = new Capsule[8];
    private int count;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        ragdoll = GetComponentInParent<FielderRagdoll>();
        reach = GetComponent<HumanoidReach>();
        if (anim == null || !anim.isHuman)
        {
            enabled = false;
            return;
        }
        Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
        hips = B(HumanBodyBones.Hips);
        spine = B(HumanBodyBones.Spine);
        neck = B(HumanBodyBones.Neck) ?? B(HumanBodyBones.Head);
        head = B(HumanBodyBones.Head);
        lThigh = B(HumanBodyBones.LeftUpperLeg); lKnee = B(HumanBodyBones.LeftLowerLeg); lFoot = B(HumanBodyBones.LeftFoot);
        rThigh = B(HumanBodyBones.RightUpperLeg); rKnee = B(HumanBodyBones.RightLowerLeg); rFoot = B(HumanBodyBones.RightFoot);
        Set(left, B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), B(HumanBodyBones.LeftMiddleProximal));
        Set(right, B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), B(HumanBodyBones.RightMiddleProximal));
        if (GetComponentInParent<KeeperCatcher>() != null)
            handRadiusScale = 1.35f;   // keeping gloves
        if (hips == null || neck == null || lThigh == null || rThigh == null || left.upper == null || right.upper == null)
            enabled = false;
    }

    private static void Set(Arm arm, Transform u, Transform l, Transform h, Transform m)
    {
        arm.upper = u; arm.lower = l; arm.hand = h; arm.middle = m;
    }

    private void LateUpdate()
    {
        if (ragdoll == null)
            ragdoll = GetComponentInParent<FielderRagdoll>();
        if (ragdoll != null && ragdoll.Busy)
        {
            left.swing = right.swing = 0f;
            return;
        }
        scale = Mathf.Max(0.01f, anim.humanScale * transform.lossyScale.y);
        BuildBody();
        float before = 0f, after = 0f;
        w0R = right.hand != null ? right.hand.position : Vector3.zero;
        Solve(left, ref before, ref after);
        Solve(right, ref before, ref after);
        LastDepthBefore = before;
        LastDepthAfter = after;
        if (before > WorstBefore) WorstBefore = before;
        if (after > WorstAfter)
        {
            WorstAfter = after;
            Vector3 L(Transform t) => t != null ? transform.InverseTransformPoint(t.position) : Vector3.zero;
            WorstWhere = $"{transform.parent?.name}/{name}: {worstPoint}, swing L {left.swing:F0} R {right.swing:F0} (R wanted {right.wanted:F0} predicted {right.predicted:F3} actual {right.actual:F3} now-hand {Vector3.Distance(right.hand.position, w0R):F3}), scale {scale:F2}; " +
                         $"local: hips {L(hips):F2} spine {L(spine):F2} neck {L(neck):F2} R shoulder {L(right.upper):F2} elbow {L(right.lower):F2} hand {L(right.hand):F2} " +
                         $"L shoulder {L(left.upper):F2} elbow {L(left.lower):F2} hand {L(left.hand):F2}; lossy {transform.lossyScale.y:F2} human {anim.humanScale:F2}";
        }
    }

    private void BuildBody()
    {
        count = 0;
        Vector3 chestTop = neck.position;
        // Torso: from the pelvis to the base of the neck; the pelvis itself as a short wide one.
        Add(spine != null ? spine.position : hips.position, chestTop, TorsoR);
        Add(lThigh.position, rThigh.position, PelvisR);
        if (head != null)
            Add(head.position + (head.position - neck.position) * 0.4f, head.position + (head.position - neck.position) * 0.4f, HeadR);
        if (lKnee != null) Add(lThigh.position, lKnee.position, ThighR);
        if (rKnee != null) Add(rThigh.position, rKnee.position, ThighR);
        if (lKnee != null && lFoot != null) Add(lKnee.position, lFoot.position, ShinR);
        if (rKnee != null && rFoot != null) Add(rKnee.position, rFoot.position, ShinR);
    }

    private void Add(Vector3 a, Vector3 b, float r)
    {
        if (count >= body.Length) return;
        body[count] = new Capsule { a = a, b = b, r = r * scale, name = count < PartNames.Length ? PartNames[count] : "?" };
        count++;
    }

    /// Fix one arm: the swing about the shoulder-wrist line, then the shoulder if the hand is in.
    private void Solve(Arm arm, ref float worstBefore, ref float worstAfter)
    {
        if (arm.upper == null || arm.lower == null || arm.hand == null)
            return;
        if (arm.corrected && arm.upper.localRotation == arm.lastOut)
            return;
        Vector3 s = arm.upper.position, e = arm.lower.position, w = arm.hand.position;
        Vector3 tip = HandTip(arm);
        worstBefore = Mathf.Max(worstBefore, Depth(s, e, w, tip, out _));
        float now = ArmDepth(s, e, w);

        // 1. The elbow round the shoulder-wrist line: of the swings that clear it, the one nearest
        // the swing it has (no jumping between two equally good answers); if none clears, the one
        // that leaves it least deep.
        Vector3 axis = w - s;
        float wanted = 0f;
        if (now > Slack && axis.sqrMagnitude > 1e-6f)
        {
            axis.Normalize();
            float best = now, bestGap = float.MaxValue;
            bool cleared = false;
            foreach (float a in Swings)
            {
                foreach (float sign in Signs)
                {
                    float cand = a * sign;
                    Quaternion q = Quaternion.AngleAxis(cand, axis);
                    float d = ArmDepth(s, s + q * (e - s), w);
                    float gap = Mathf.Abs(cand - arm.swing);
                    if (d <= Slack)
                    {
                        if (!cleared || gap < bestGap) { cleared = true; bestGap = gap; wanted = cand; best = d; }
                    }
                    else if (!cleared && d < best - 1e-4f)
                    {
                        best = d;
                        wanted = cand;
                    }
                }
            }
        }
        arm.wanted = wanted;
        if (axis.sqrMagnitude > 1e-6f) arm.predicted = ArmDepth(s, s + Quaternion.AngleAxis(wanted, axis.normalized) * (e - s), w);
        // In the body: straight to the swing that clears it - easing toward it carried the arm
        // through angles still inside the chest (measured: wanted -20, eased at 66, 15 cm in).
        // Clear as animated: back to the clip's own arm, eased, but only through angles that
        // stay clear; otherwise at once.
        if (now > Slack)
            arm.swing = wanted;
        else
        {
            float eased = Mathf.MoveTowards(arm.swing, 0f, SwingRate * Time.deltaTime);
            bool clear = axis.sqrMagnitude < 1e-6f ||
                         ArmDepth(s, s + Quaternion.AngleAxis(eased, axis.normalized) * (e - s), w) <= Slack;
            arm.swing = clear ? eased : 0f;
        }
        if (Mathf.Abs(arm.swing) > 0.01f && axis.sqrMagnitude > 1e-6f)
        {
            Quaternion handWas = arm.hand.rotation;
            arm.upper.rotation = Quaternion.AngleAxis(arm.swing, axis.normalized) * arm.upper.rotation;
            arm.hand.rotation = handWas;   // the hand keeps its turn
            e = arm.lower.position;
            arm.actual = ArmDepth(s, e, arm.hand.position);
        }

        // 1b. Still in after the swing (a straight arm, or the upper arm itself): turn the arm at
        // the shoulder, just enough to bring that point out - unless the hands are going for the
        // ball, which must not be pulled off it (the take is in the hands).
        bool handsBusy = reach != null && reach.SmoothedWeight > 0.3f;
        if (!handsBusy)
        {
            float left = ArmPointDepth(s, e, arm.hand.position, out Vector3 point, out Vector3 outward);
            if (left > Slack && (point - s).sqrMagnitude > 1e-6f)
            {
                Quaternion handWas = arm.hand.rotation;
                arm.upper.rotation = Quaternion.FromToRotation(point - s, point + outward * left - s) * arm.upper.rotation;
                arm.hand.rotation = handWas;
                e = arm.lower.position;
                w = arm.hand.position;
                tip = HandTip(arm);
            }
        }

        // 2. The hand itself in the body: turn the arm at the shoulder, just enough to clear it -
        // a few times, as one push can leave it in the other thigh.
        float handDepth = 0f;
        for (int pass = 0; pass < 3; pass++)
        {
            w = arm.hand.position;
            tip = HandTip(arm);
            handDepth = PointDepth(w, HandR * handRadiusScale * scale, out Vector3 push);
            float tipDepth = PointDepth(tip, HandR * handRadiusScale * scale, out Vector3 tipPush);
            Vector3 at = w;
            if (tipDepth > handDepth) { handDepth = tipDepth; push = tipPush; at = tip; }
            if (handDepth <= Slack)
                break;
            Vector3 from = at - s, to = at + push * (handDepth + 0.002f) - s;
            Quaternion handWas = arm.hand.rotation;
            arm.upper.rotation = Quaternion.FromToRotation(from, to) * arm.upper.rotation;
            arm.hand.rotation = handWas;
        }
        arm.corrected = true;   // any correction, or none: compare next frame either way
        arm.lastOut = arm.upper.localRotation;
        worstAfter = Mathf.Max(worstAfter, Depth(arm.upper.position, arm.lower.position, arm.hand.position, HandTip(arm), out _));
    }

    private Vector3 HandTip(Arm arm) =>
        arm.middle != null ? arm.hand.position + (arm.middle.position - arm.hand.position) * 1.3f : arm.hand.position;

    /// The deepest point of the upper arm and forearm, and the way out of the body for it.
    private float ArmPointDepth(Vector3 s, Vector3 e, Vector3 w, out Vector3 point, out Vector3 outward)
    {
        point = e; outward = Vector3.zero;
        float worst = 0f;
        foreach (float t in ArmSamples)
        {
            bool upperPart = t <= 1f;
            Vector3 p = upperPart ? Vector3.Lerp(s, e, t) : Vector3.Lerp(e, w, t - 1f);
            float d = PointDepth(p, (upperPart ? UpperArmR : ForearmR) * scale, out Vector3 n);
            if (d > worst) { worst = d; point = p; outward = n; }
        }
        return worst;
    }

    private static readonly float[] ArmSamples = { 0.55f, 0.8f, 1f, 1.35f, 1.7f };

    /// The upper arm and forearm only: what swinging the elbow can move.
    private float ArmDepth(Vector3 s, Vector3 e, Vector3 w)
    {
        Vector3 push = Vector3.zero;
        float worst = 0f;
        Test(Vector3.Lerp(s, e, 0.55f), UpperArmR, ref worst, ref push);
        Test(Vector3.Lerp(s, e, 0.8f), UpperArmR, ref worst, ref push);
        Test(e, UpperArmR, ref worst, ref push);
        Test(Vector3.Lerp(e, w, 0.35f), ForearmR, ref worst, ref push);
        Test(Vector3.Lerp(e, w, 0.7f), ForearmR, ref worst, ref push);
        return worst;
    }

    /// Deepest any point along the arm is inside the body (metres, 0 if clear).
    private float Depth(Vector3 s, Vector3 e, Vector3 w, Vector3 tip, out Vector3 push)
    {
        push = Vector3.zero;
        float worst = 0f;
        // Not the top of the upper arm: the shoulder joint sits against the torso by design.
        Test(Vector3.Lerp(s, e, 0.55f), UpperArmR, ref worst, ref push, "upper arm");
        Test(Vector3.Lerp(s, e, 0.8f), UpperArmR, ref worst, ref push, "upper arm");
        Test(e, UpperArmR, ref worst, ref push, "elbow");
        Test(Vector3.Lerp(e, w, 0.35f), ForearmR, ref worst, ref push, "forearm");
        Test(Vector3.Lerp(e, w, 0.7f), ForearmR, ref worst, ref push, "forearm");
        Test(w, HandR * handRadiusScale, ref worst, ref push, "wrist");
        Test(tip, HandR * handRadiusScale, ref worst, ref push, "hand");
        return worst;
    }

    private string worstPoint;
    private void Test(Vector3 p, float r, ref float worst, ref Vector3 push, string what = "")
    {
        float d = PointDepth(p, r * scale, out Vector3 n);
        if (d > worst) { worst = d; push = n; worstPoint = what + " in " + lastPart; }
    }

    /// How far a sphere at p of radius r is inside the body, and the way out.
    private float PointDepth(Vector3 p, float r, out Vector3 outward)
    {
        outward = Vector3.zero;
        float worst = 0f;
        for (int i = 0; i < count; i++)
        {
            Capsule c = body[i];
            Vector3 ab = c.b - c.a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - c.a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 off = p - (c.a + ab * t);
            float dist = off.magnitude;
            float depth = c.r + r - dist;
            if (depth > worst)
            {
                worst = depth;
                lastPart = c.name;
                outward = dist > 1e-5f ? off / dist : transform.forward;
            }
        }
        return worst;
    }
}
