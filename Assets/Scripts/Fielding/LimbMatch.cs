using UnityEngine;

/// <summary>
/// One limb of a FielderLook copy - upper bone, lower bone, hand or foot - bent so its hand or
/// foot is exactly where the original's is, turned the same way. The copy's arms and legs are not
/// the original's length, so the muscle-space pose alone left its hands a few centimetres off the
/// original's (where the gloves hang and the ball is held: the keeper seemed to take the ball out
/// of the air beside his hands) and its feet off the ground (through it, at times).
///
/// A two-bone solve: the elbow or knee goes in the plane of the original's own (`pole`), the end
/// bone takes the original end bone's turn plus the difference measured between the two in the
/// same pose. `lift` raises a foot's target by how much higher the copy's ankle sits over its sole.
/// </summary>
public sealed class LimbMatch
{
    private readonly Transform upper, lower, end;
    private readonly Transform sourceLower, sourceEnd;
    private readonly Quaternion endTurn;
    private readonly Vector3 lift;

    private LimbMatch(Transform upper, Transform lower, Transform end, Transform sourceLower, Transform sourceEnd, Vector3 lift)
    {
        this.upper = upper;
        this.lower = lower;
        this.end = end;
        this.sourceLower = sourceLower;
        this.sourceEnd = sourceEnd;
        this.lift = lift;
        endTurn = Quaternion.Inverse(sourceEnd.rotation) * end.rotation;
    }

    /// For a limb of `copy` matching the same limb of `source`, both in the same pose now. Null
    /// if either avatar lacks one of its bones.
    public static LimbMatch Make(Animator source, Animator copy, HumanBodyBones u, HumanBodyBones l, HumanBodyBones e, Vector3 lift)
    {
        Transform cu = copy.GetBoneTransform(u), cl = copy.GetBoneTransform(l), ce = copy.GetBoneTransform(e);
        Transform sl = source.GetBoneTransform(l), se = source.GetBoneTransform(e);
        return cu == null || cl == null || ce == null || sl == null || se == null ? null : new LimbMatch(cu, cl, ce, sl, se, lift);
    }

    public void Solve()
    {
        Vector3 target = sourceEnd.position + lift;
        Vector3 root = upper.position;
        float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, end.position);
        Vector3 toTarget = target - root;
        if (toTarget.sqrMagnitude < 1e-8f || a < 1e-4f || b < 1e-4f)
            return;
        Vector3 along = toTarget.normalized;
        float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        Vector3 bend = Vector3.ProjectOnPlane(sourceLower.position - root, along);
        if (bend.sqrMagnitude < 1e-8f)
            bend = Vector3.ProjectOnPlane(lower.position - root, along);
        bend.Normalize();
        float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        Vector3 joint = root + along * (a * cos) + bend * (a * Mathf.Sqrt(1f - cos * cos));
        upper.rotation = Quaternion.FromToRotation(lower.position - root, joint - root) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
        end.rotation = sourceEnd.rotation * endTurn;
    }
}
