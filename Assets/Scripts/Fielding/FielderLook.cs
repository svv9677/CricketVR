using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Everyone on the field in the fielders' model. The keeper (a MakeHuman body) and the bowler (a
/// different Mixamo body) keep their own skeletons - their ragdolls, reach, footwork and bowling
/// animations are built and tuned on those - but are drawn as the fielders' "David": a copy of
/// that model takes their pose every frame through the humanoid muscle space (HumanPoseHandler),
/// and their own body meshes are hidden. What they wear (the keeper's gloves, pads and helmet)
/// moves onto the copy's matching bones: left on his own, the helmet sat 7 cm above the copy's
/// head (the two bodies' necks differ).
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
        foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.enabled = false;   // the body
        // With its body hidden the original counts as unseen, and the bowler's Animator (culled
        // when unseen) stopped posing him: he stood in his bind pose, and the copy with him.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        LateUpdate();
        MoveKit(animator, lookAnimator);   // both in the same pose now
    }

    /// The kit (mesh renderers hung on the original's bones) onto the same bones of the copy,
    /// keeping where it is now.
    private static void MoveKit(Animator from, Animator to)
    {
        var bones = new Dictionary<Transform, Transform>();
        for (HumanBodyBones b = 0; b < HumanBodyBones.LastBone; b++)
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
        from.GetHumanPose(ref pose);
        to.SetHumanPose(ref pose);
    }

    private void OnDestroy()
    {
        from?.Dispose();
        to?.Dispose();
        if (look != null)
            Destroy(look.gameObject);
    }
}
