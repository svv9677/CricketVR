using UnityEngine;

/// <summary>
/// Nothing of him goes into the ground, falling or getting up.
///
/// Under physics (a dive's landing) the ragdoll's own colliders keep him out - but the ragdoll
/// stopped at the wrists and ankles, so his hands and feet went in (toes 6.5 cm, measured): they
/// get colliders of their own here, riding on the forearm's and shin's bodies.
///
/// Animated (the dive before the hand-off, the Mixamo get-ups, a slide, a pick-up) nothing knew
/// about the ground at all: the trailing feet of a dive went 8 cm in, and the get-up clips, lying,
/// put head, chest, hips and arms 7-10 cm under it (measured). So after the animation and the IK,
/// while he dives or gets up, KeepAboveGround puts back up whatever is under it: the body as a
/// whole for the trunk and head, a leg or arm swung up at the hip or shoulder, a foot or hand turned
/// up at the ankle or wrist for the toes or fingers. Nothing under it, nothing moves.
/// </summary>
public partial class FielderRagdoll
{
    /// Hand collider: radius, and how far past the last finger bone it reaches, metres at human
    /// scale. The foot's runs from the ankle to the toe bone, as thick as the toe bone is high
    /// standing (its sole): a foot stood flat touches the ground and is not "under" it. A fixed,
    /// fatter one sloping down to the toes read 3-7 cm under on every standing foot.
    private const float HandRadius = 0.025f, FingersBeyond = 0.02f;
    // The guard runs while he is falling or getting up: the dives ask for it each frame
    // (GuardGroundThisFrame), and the get-up has it. Run on every animated frame, it "fixed" the
    // run cycle's and the bowling stride's feet as they struck the grass: a leg near vertical under
    // the hip needs a huge swing to lift a few centimetres, and the bowler's legs kicked out 40-76 cm
    // in a frame running in and walking back (measured; 9.8 cm at worst with the guard off). Gated
    // on hip height instead (under 0.83 m), it missed the keeper's push-off into a dive.
    private float guardUntil = -1f;

    /// Keep him out of the ground this frame (a dive in progress calls it every frame).
    public void GuardGroundThisFrame() => guardUntil = Time.time + 0.05f;

    /// A limb is swung at most this much in a frame, and not at all if its low point is less than
    /// this share of its length out from the pivot (straight down under the hip or shoulder,
    /// swinging it cannot lift it).
    private const float MaxSwing = 10f, MinLever = 0.25f;
    private float soleL = 0.012f, soleR = 0.012f;

    private Collider hipsCollider, chestCollider, headCollider;
    private Limb[] limbs;

    private class Limb
    {
        public Transform root, end, tip;   // hip/shoulder, ankle/wrist, toes/finger bone
        public Collider[] parts;           // thigh, shin, foot / upper arm, forearm, hand
        public float tipBeyond, tipRadius;   // world metres
    }

    /// The ragdoll's layer: the ball goes through it. The keeper's was built on Stadium, which the
    /// ball hits - down after a dive he could have deflected it (checked: Stadium-Ball collide).
    private int RagdollLayer => LayerMask.NameToLayer("Body") >= 0 ? LayerMask.NameToLayer("Body") : gameObject.layer;

    private void UseRagdollLayer()
    {
        int layer = RagdollLayer;
        foreach (Collider c in colliders)
            c.gameObject.layer = layer;
    }

    private void AddHandAndFootColliders()
    {
        int layer = RagdollLayer;
        // Awake sees the bind pose, stood flat on the ground: the toe bones' height is the sole.
        soleL = Sole(HumanBodyBones.LeftToes);
        soleR = Sole(HumanBodyBones.RightToes);
        AddEndCollider(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, soleL, 0f, layer);
        AddEndCollider(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, soleR, 0f, layer);
        float hand = HandRadius * transform.lossyScale.y;
        AddEndCollider(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleDistal, hand, FingersBeyond * transform.lossyScale.y, layer);
        AddEndCollider(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleDistal, hand, FingersBeyond * transform.lossyScale.y, layer);
    }

    private float Sole(HumanBodyBones toes)
    {
        Transform t = anim.GetBoneTransform(toes);
        float scale = transform.lossyScale.y;
        return t == null ? 0.012f * scale : Mathf.Clamp(t.position.y - transform.position.y - 0.002f, 0.008f * scale, 0.04f * scale);
    }

    /// A capsule on bone `from` out to `to` and `beyond` past it (world metres), of world radius
    /// `radius`, along the bone's own axis nearest that line. With no Rigidbody of its own it is
    /// part of the parent bone's body.
    private void AddEndCollider(HumanBodyBones from, HumanBodyBones to, float radius, float beyond, int layer)
    {
        Transform a = anim.GetBoneTransform(from), b = anim.GetBoneTransform(to);
        if (a == null || b == null || a.GetComponent<Collider>() != null)
            return;
        float s = Mathf.Max(1e-4f, a.lossyScale.x);
        Vector3 along = (b.position - a.position).normalized;
        Vector3 end = a.InverseTransformPoint(b.position + along * beyond);
        int axis = Mathf.Abs(end.x) > Mathf.Abs(end.y) ? (Mathf.Abs(end.x) > Mathf.Abs(end.z) ? 0 : 2) : (Mathf.Abs(end.y) > Mathf.Abs(end.z) ? 1 : 2);
        float r = radius / s;
        CapsuleCollider c = a.gameObject.AddComponent<CapsuleCollider>();
        c.direction = axis;
        c.radius = r;
        c.height = Mathf.Abs(end[axis]) + 2f * r;
        c.center = end * 0.5f;
        c.enabled = false;
        a.gameObject.layer = layer;
    }

    private void CacheGroundGuard()
    {
        Collider C(HumanBodyBones bone)
        {
            Transform t = anim.GetBoneTransform(bone);
            return t != null ? t.GetComponent<Collider>() : null;
        }
        hipsCollider = C(HumanBodyBones.Hips);
        chestCollider = C(HumanBodyBones.Chest) ?? C(HumanBodyBones.Spine) ?? C(HumanBodyBones.UpperChest);
        headCollider = C(HumanBodyBones.Head);
        Limb L(HumanBodyBones root, HumanBodyBones mid, HumanBodyBones end, HumanBodyBones tip, float beyond, float tipRadius) => new Limb
        {
            root = anim.GetBoneTransform(root),
            end = anim.GetBoneTransform(end),
            tip = anim.GetBoneTransform(tip),
            parts = new[] { C(root), C(mid), C(end) },
            tipBeyond = beyond,
            tipRadius = tipRadius,
        };
        float scale = transform.lossyScale.y;
        limbs = new[]
        {
            L(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0f, soleL),
            L(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0f, soleR),
            L(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleDistal, FingersBeyond * scale, 0.012f * scale),
            L(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleDistal, FingersBeyond * scale, 0.012f * scale),
        };
    }

    private void KeepAboveGround()
    {
        if (limbs == null || hips == null || state == State.Down)
            return;
        if (state != State.GettingUp && Time.time > guardUntil)
            return;
        float scale = transform.lossyScale.y;
        float ground = BallFlight.GroundY(hips.position);
        // The trunk and head under it: the whole of him up.
        float under = Mathf.Max(Under(hipsCollider, ground, out _), Mathf.Max(Under(chestCollider, ground, out _), Under(headCollider, ground, out _)));
        if (under > 0f)
            hips.position += Vector3.up * under;
        foreach (Limb limb in limbs)
        {
            // A limb under it: swung up at the hip or shoulder until its lowest point is out.
            float deepest = 0f;
            Vector3 low = Vector3.zero;
            foreach (Collider c in limb.parts)
            {
                float d = Under(c, ground, out Vector3 p);
                if (d > deepest) { deepest = d; low = p; }
            }
            if (deepest > 0f && limb.root != null)
                SwingUp(limb.root, low, deepest);
            // Toes or finger ends under it: the foot or hand turned up at the ankle or wrist.
            if (limb.end != null && limb.tip != null)
            {
                Vector3 dir = (limb.tip.position - limb.end.position).normalized;
                Vector3 tipEnd = limb.tip.position + dir * limb.tipBeyond - Vector3.up * limb.tipRadius;
                float d = ground - tipEnd.y;
                if (d > 0f)
                    SwingUp(limb.end, tipEnd, d);
            }
        }
    }

    /// Turn `bone` about its own pivot so the point `p` on it rises by `rise` (within MaxSwing).
    private static void SwingUp(Transform bone, Vector3 p, float rise)
    {
        Vector3 r = p - bone.position;
        Vector3 flat = new Vector3(r.x, 0f, r.z);
        if (flat.magnitude < MinLever * r.magnitude || flat.sqrMagnitude < 1e-6f)
            return;   // (nearly) straight below the pivot: turning it cannot lift it
        float angle = Mathf.Min(MaxSwing, Mathf.Atan2(rise, flat.magnitude) * Mathf.Rad2Deg);
        Vector3 axis = Vector3.Cross(Vector3.up, flat).normalized;
        Quaternion turn = Quaternion.AngleAxis(angle, axis);
        if ((turn * r).y < r.y)
            turn = Quaternion.AngleAxis(-angle, axis);
        bone.rotation = turn * bone.rotation;
    }

    /// How far collider `c`'s lowest point is under `ground` (0 if not), and that point.
    private static float Under(Collider c, float ground, out Vector3 lowest)
    {
        lowest = Vector3.zero;
        if (c == null)
            return 0f;
        Transform t = c.transform;
        float s = t.lossyScale.x;
        switch (c)
        {
            case SphereCollider sphere:
                lowest = t.TransformPoint(sphere.center) - Vector3.up * (sphere.radius * s);
                break;
            case CapsuleCollider capsule:
            {
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                Vector3 a = t.TransformPoint(capsule.center + axis * half), b = t.TransformPoint(capsule.center - axis * half);
                lowest = (a.y < b.y ? a : b) - Vector3.up * (capsule.radius * s);
                break;
            }
            case BoxCollider box:
            {
                float m = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                    Vector3 w = t.TransformPoint(box.center + Vector3.Scale(corner, box.size));
                    if (w.y < m) { m = w.y; lowest = w; }
                }
                break;
            }
            default:
                return 0f;
        }
        return Mathf.Max(0f, ground - lowest.y);
    }
}
