using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
/// Built when the match scene loads, before any Start, so the replay (ReplayActors) records the
/// copy as a player and the original only for what it still shows.
/// </summary>
[DefaultExecutionOrder(900)]   // after their IK and ragdoll (LateUpdate), before the recorder (1000)
public class FielderLook : MonoBehaviour
{
    private const string MatchScene = "CricketVR";

    private Animator source;
    private Transform look;
    private HumanPoseHandler from, to;
    private HumanPose pose;
    private LimbMatch[] limbs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != MatchScene)
            return;
        foreach (string path in new[] { "Main/Bowler", "Main/Fielders/Fielder_Keeper/Body" })
        {
            GameObject who = GameObject.Find(path);
            if (who != null && who.GetComponent<FielderLook>() == null && who.TryGetComponent(out Animator animator) && animator.isHuman)
                who.AddComponent<FielderLook>().Dress(animator);
        }
    }

    private void Dress(Animator animator)
    {
        GameObject model = Resources.Load<GameObject>("Models/David");
        if (model == null)
            return;
        source = animator;
        // A human pose's body position is in the space of the root's parent (measured: the
        // bowler's came back as his world position, and set on a copy at his feet it put the
        // copy's hips 19 m away). So the copy is a sibling, at its parent's origin, and the pose
        // lands where the original stands.
        GameObject copy = Instantiate(model, transform.parent, false);
        copy.name = name + " (fielder look)";
        if (transform.parent == null)
            SceneManager.MoveGameObjectToScene(copy, gameObject.scene);
        look = copy.transform;
        look.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        Animator lookAnimator = copy.GetComponent<Animator>();
        lookAnimator.runtimeAnimatorController = null;      // posed from the original, not animated
        lookAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        // Body positions are in units of each avatar's human scale: match them, as tall as the original.
        look.localScale = Vector3.one * (animator.humanScale * transform.localScale.x / Mathf.Max(0.1f, lookAnimator.humanScale));
        from = new HumanPoseHandler(animator.avatar, animator.transform);
        to = new HumanPoseHandler(lookAnimator.avatar, look);
        // Still in their bind poses, both standing: how much higher the copy's ankles are.
        Vector3 lift = Vector3.up * Mathf.Max(0f, AnkleHeight(lookAnimator, look) - AnkleHeight(animator, transform));
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.enabled = false;   // the body
        // With its body hidden the original counts as unseen, and the bowler's Animator (culled
        // when unseen) stopped posing him: he stood in his bind pose, and the copy with him.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        from.GetHumanPose(ref pose);
        to.SetHumanPose(ref pose);         // both in the same pose now: measure the hands' and feet' turns
        limbs = new[]
        {
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, Vector3.zero),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, Vector3.zero),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, lift),
            LimbMatch.Make(animator, lookAnimator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, lift),
        };
        MoveHeadKit(animator, lookAnimator);
        LateUpdate();
    }

    private static float AnkleHeight(Animator a, Transform root)
    {
        Transform foot = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        return foot != null ? foot.position.y - root.position.y : 0f;
    }

    /// A helmet (a mesh hung on the original's head or neck) onto the copy's head, keeping where
    /// it is now. Gloves and pads stay on the original: the copy's hands and feet are put on his.
    private static void MoveHeadKit(Animator from, Animator to)
    {
        var bones = new Dictionary<Transform, Transform>();
        foreach (HumanBodyBones b in new[] { HumanBodyBones.Head, HumanBodyBones.Neck })
        {
            Transform s = from.GetBoneTransform(b), d = to.GetBoneTransform(b);
            if (s != null && d != null)
                bones[s] = d;
        }
        foreach (MeshRenderer kit in from.GetComponentsInChildren<MeshRenderer>(true))
        {
            Transform t = kit.transform;     // up to the piece hung on a bone (Glove_L under hand_l)
            while (t.parent != null && t.parent != from.transform && !bones.ContainsKey(t.parent))
                t = t.parent;
            if (t.parent != null && bones.TryGetValue(t.parent, out Transform bone))
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
        if (limbs != null)
            foreach (LimbMatch limb in limbs)
                limb?.Solve();
    }

    private void OnDestroy()
    {
        from?.Dispose();
        to?.Dispose();
        if (look != null)
            Destroy(look.gameObject);
    }
}
