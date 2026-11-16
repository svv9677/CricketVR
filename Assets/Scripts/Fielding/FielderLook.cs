using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everyone on the field in the fielders' model. The keeper (a MakeHuman body) and the bowler (a
/// different Mixamo body) keep their own skeletons - their ragdolls, reach, footwork and bowling
/// animations are built and tuned on those - but are drawn as the fielders' "David": a copy of
/// that model takes their pose every frame through the humanoid muscle space (HumanPoseHandler),
/// and their own body meshes are hidden. The copy's arms and legs are then bent onto the
/// original's hands and feet (LimbMatch), so the gloves and pads on the original - and the ball
/// in his hands - fit the copy, and its feet stand on the ground. A helmet moves onto the copy's
/// head: left on the original's, it sat 7 cm above the copy's (the two necks differ).
///
/// The copy is placed beside each of them in Main.prefab with their own meshes switched off, so
/// the editor shows the fielders' look; this component only poses it. Set up in Awake, before any
/// Start, so the replay (ReplayActors) records the copy as a player and the original only for what
/// it still shows.
/// </summary>
[DefaultExecutionOrder(900)]   // after their IK and ragdoll (LateUpdate), before the recorder (1000)
public class FielderLook : MonoBehaviour
{
    private const string MatchScene = "CricketVR";

    [SerializeField, Tooltip("The fielders' model drawn in place of this body: a sibling at the same parent's origin, saved in Main.prefab.")]
    private Transform look;
    [SerializeField, Tooltip("This body's own meshes, switched off in Main.prefab (back on outside the match scene).")]
    private SkinnedMeshRenderer[] bodySkins;

    private Animator source;
    private HumanPoseHandler from, to;
    private HumanPose pose;
    private LimbMatch[] limbs;
    private Transform sourceHips, copyHips;
    private Vector3 hipOffset;      // the copy's hips from the original's at rest, in the original's frame

    /// Before any Start runs, so the replay (ReplayActors) records the copy as a player.
    private void Awake()
    {
        Animator animator = GetComponent<Animator>();
        if (look == null || animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }
        // Main.prefab is also the nets' gameplay set, where he is drawn as he always was.
        if (gameObject.scene.name != MatchScene)
        {
            look.gameObject.SetActive(false);
            foreach (SkinnedMeshRenderer skin in bodySkins)
                if (skin != null)
                    skin.enabled = true;
            enabled = false;
            return;
        }
        Dress(animator);
    }

    /// The copy and the hidden body are placed in Main.prefab (CricketVR > Scene Objects > Bake Player
    /// Setup); what is left cannot be saved: the pose handlers, the limb solvers and the kit moved
    /// onto the copy's bones (a prefab instance cannot hold a child of another one's bone).
    /// Until 2026-11-15 the copy was built here as well.
    private void Dress(Animator animator)
    {
        source = animator;
        // A human pose's body position is in the space of the root's parent (measured: the
        // bowler's came back as his world position, and set on a copy at his feet it put the
        // copy's hips 19 m away). So the copy is a sibling, at its parent's origin, and the pose
        // lands where the original stands.
        Animator lookAnimator = look.GetComponent<Animator>();
        from = new HumanPoseHandler(animator.avatar, animator.transform);
        to = new HumanPoseHandler(lookAnimator.avatar, look);
        // Still in their bind poses, both standing: how much higher the copy's ankles are.
        Vector3 lift = Vector3.up * Mathf.Max(0f, AnkleHeight(lookAnimator, look) - AnkleHeight(animator, transform));
        from.GetHumanPose(ref pose);
        to.SetHumanPose(ref pose);         // both in the same pose now: measure the hands' and feet' turns
        limbs = new[]
        {
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.zero),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.zero),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, lift),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, lift),
        };
        sourceHips = animator.GetBoneTransform(HumanBodyBones.Hips);
        copyHips = lookAnimator.GetBoneTransform(HumanBodyBones.Hips);
        if (sourceHips != null && copyHips != null)
            hipOffset = Quaternion.Inverse(transform.rotation) * (copyHips.position - sourceHips.position);
        MoveHeadKit(animator, lookAnimator);
        LateUpdate();
    }

    private static float AnkleHeight(Animator a, Transform root)
    {
        Transform foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        return foot != null ? foot.position.y - root.position.y : 0f;
    }

    /// A helmet (a mesh hung on the original's head or neck) onto the copy's head, and the keeper's
    /// gloves onto the copy's hands, keeping where they are now. Pads stay on the original: the
    /// copy's feet are put on his. The gloves were left on his hands too, but his arms stretch to
    /// a hand target out of reach (the fielders' avatar allows 38%) and the copy's cannot
    /// (LimbMatch): bent for a ball on the ground, the glove floated off the end of the copy's arm
    /// (2026-11-09: "the keeper's gloves sometimes detach from his hands when he's picking up the
    /// ball"). On the copy's hands they are on the hands you see, always.
    private static void MoveHeadKit(Animator from, Animator to)
    {
        var bones = new Dictionary<Transform, Transform>();
        foreach (HumanBodyBones b in new[] { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
        {
            Transform s = from.GetBoneTransform(b), d = to.GetBoneTransform(b);
            if (s != null && d != null)
                bones[s] = d;
        }
        // His skeleton is never moved, only kit hung on it: a finger bone under a hand is not kit.
        var skeleton = new HashSet<Transform>();
        for (HumanBodyBones b = 0; b < HumanBodyBones.LastBone; b++)
        {
            Transform bt = from.GetBoneTransform(b);
            if (bt != null) skeleton.Add(bt);
        }
        foreach (MeshRenderer kit in from.GetComponentsInChildren<MeshRenderer>(true))
        {
            Transform t = kit.transform;     // up to the piece hung on a bone (Glove_L under hand_l)
            while (t.parent != null && t.parent != from.transform && !bones.ContainsKey(t.parent))
                t = t.parent;
            if (t.parent != null && !skeleton.Contains(t) && bones.TryGetValue(t.parent, out Transform bone))
                t.SetParent(bone, true);
        }
    }

    private void LateUpdate()
    {
        if (look == null || from == null)
            return;
        // Every frame, not once: the bowler fielding a hit is an AnimatedFielder for the rest of
        // the ball, and its KeepAnimating sets "cull when unseen" whenever he is out of the play.
        // With his own body hidden he is never seen, so his skeleton froze in whatever pose it was
        // in, his run-up never played and the next ball was never bowled (headset, after a hit).
        if (source.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            source.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        from.GetHumanPose(ref pose);
        to.SetHumanPose(ref pose);
        // The pose carries his position as a fraction of his body size and does not undo the
        // keeper's 1.11 body scale: the copy fell behind him by 11% of however far he had moved
        // (0.8 m after a run to a ball), and its hands and feet, kept on his gloves and boots,
        // stretched to reach. Placed straight on his hips instead, as far from them as at rest.
        if (sourceHips != null && copyHips != null)
            look.position += sourceHips.position + transform.rotation * hipOffset - copyHips.position;
        if (limbs != null)
            foreach (LimbMatch limb in limbs)
                limb?.Solve();
    }

    private void OnDestroy()
    {
        from?.Dispose();
        to?.Dispose();
    }
}
