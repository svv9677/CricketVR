using UnityEditor;
using UnityEngine;

/// <summary>
/// What FielderRagdoll used to set up in Awake on every player, now saved on the prefab: a capsule
/// on each hand and foot (the ragdoll stops at the wrists and ankles, so hands and feet went into
/// the ground), every ragdoll collider on the Body layer (the ball goes through it), continuous-
/// speculative collision on every ragdoll body, and the kinematic root body. Until 2026-11-15 the
/// game did this as each player woke. RagdollBuilderTool.BuildOn calls Bake after it builds a
/// ragdoll, and PlayerSetupBuilder calls it on David, Keeper and Bowler. Run again, it changes
/// nothing (a bone that has a collider is left alone).
/// </summary>
public static class RagdollEndColliders
{
    /// Adds the hand and foot capsules to the humanoid on `a` and sets the layer, collision mode
    /// and kinematic root body. `a` is the Animator the FielderRagdoll sits on.
    public static void Bake(Animator a)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null || hips.GetComponentsInChildren<Rigidbody>(true).Length == 0)
            return;
        AddHandAndFoot(a);
        SetLayersAndCollision(a);
        Rigidbody own = a.GetComponent<Rigidbody>();
        if (own != null)
            MakeKinematic(own);
    }

    /// The root body is moved by code, never by physics. Left dynamic (as the scene had it, with
    /// no gravity) the falling limbs nudged it and the fielder floated off upward for good.
    public static void MakeKinematic(Rigidbody own)
    {
        own.linearVelocity = Vector3.zero;
        own.isKinematic = true;
        PlayerSetupBuilder.Touch(own);
    }

    /// Every collider under the hips on the Body layer, every body under them continuous-
    /// speculative (a falling limb at a few m/s covers more than its own thickness in a physics
    /// step: swept, it cannot step through the ground's one-sided mesh).
    public static void SetLayersAndCollision(Animator a)
    {
        Transform hips = a.GetBoneTransform(HumanBodyBones.Hips);
        int body = LayerMask.NameToLayer("Body");
        int layer = body >= 0 ? body : a.gameObject.layer;
        foreach (Collider c in hips.GetComponentsInChildren<Collider>(true))
        {
            if (c.gameObject.layer == layer)
                continue;
            c.gameObject.layer = layer;
            PlayerSetupBuilder.Touch(c.gameObject);
        }
        foreach (Rigidbody b in hips.GetComponentsInChildren<Rigidbody>(true))
        {
            if (b.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative)
                continue;
            b.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            PlayerSetupBuilder.Touch(b);
        }
    }

    private static void AddHandAndFoot(Animator a)
    {
        int body = LayerMask.NameToLayer("Body");
        int layer = body >= 0 ? body : a.gameObject.layer;
        float scale = a.transform.lossyScale.y;
        // The bind pose, stood flat on the ground: the toe bones' height is the sole.
        float soleL = Sole(a, HumanBodyBones.LeftToes), soleR = Sole(a, HumanBodyBones.RightToes);
        AddEnd(a, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, soleL, 0f, layer);
        AddEnd(a, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, soleR, 0f, layer);
        float hand = FielderRagdoll.HandRadius * scale;
        float beyond = FielderRagdoll.FingersBeyond * scale;
        AddEnd(a, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleDistal, hand, beyond, layer);
        AddEnd(a, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleDistal, hand, beyond, layer);
    }

    private static float Sole(Animator a, HumanBodyBones toes)
    {
        Transform t = a.GetBoneTransform(toes);
        float scale = a.transform.lossyScale.y;
        return t == null ? 0.012f * scale : Mathf.Clamp(t.position.y - a.transform.position.y - 0.002f, 0.008f * scale, 0.04f * scale);
    }

    /// A capsule on bone `from` out to `to` and `beyond` past it (world metres), of world radius
    /// `radius`, along the bone's own axis nearest that line. With no Rigidbody of its own it is
    /// part of the parent bone's body. Saved switched off: FielderRagdoll switches the ragdoll's
    /// colliders on only while the body is under physics.
    private static void AddEnd(Animator a, HumanBodyBones from, HumanBodyBones to, float radius, float beyond, int layer)
    {
        Transform bone = a.GetBoneTransform(from), tip = a.GetBoneTransform(to);
        if (bone == null || tip == null || bone.GetComponent<Collider>() != null)
            return;
        float s = Mathf.Max(1e-4f, bone.lossyScale.x);
        Vector3 along = (tip.position - bone.position).normalized;
        Vector3 end = bone.InverseTransformPoint(tip.position + along * beyond);
        int axis = Mathf.Abs(end.x) > Mathf.Abs(end.y) ? (Mathf.Abs(end.x) > Mathf.Abs(end.z) ? 0 : 2) : (Mathf.Abs(end.y) > Mathf.Abs(end.z) ? 1 : 2);
        float r = radius / s;
        CapsuleCollider c = bone.gameObject.AddComponent<CapsuleCollider>();
        c.direction = axis;
        c.radius = r;
        c.height = Mathf.Abs(end[axis]) + 2f * r;
        c.center = end * 0.5f;
        c.enabled = false;
        bone.gameObject.layer = layer;
    }
}
