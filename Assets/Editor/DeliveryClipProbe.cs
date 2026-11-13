using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Where a delivery clip takes the bowler: plays it on the real Bowler prefab with root motion and
/// reports, from the clip's start, the root, front toe and bowling hand at the ReleaseBall event,
/// straight and mirrored (a left-armer). A captured action's mark (MocapDelivery.Mark) is the
/// difference between its numbers and Steyn's, so re-run this whenever a clip changes.
/// Edit mode only: root motion is summed from the Animator's deltas (the transform does not move
/// outside play mode), and the bowler's scripts are switched off so the event goes nowhere.
/// </summary>
public static class DeliveryClipProbe
{
    public static readonly string[] Clips =
    {
        "Assets/Resources/Animations/Bowling/Steyn Cleaned.anim",
        "Assets/Resources/Animations/Bowling/Bumrah.anim",
        "Assets/Resources/Animations/Bowling/Mocap/MocapPace.fbx",
    };

    public struct Result
    {
        public string clip;
        public bool mirrored;
        public Vector3 root, frontToe, hand, end;
    }

    [MenuItem("CricketVR/Bowling/Measure Delivery Clips")]
    public static void Report()
    {
        var sb = new StringBuilder("[DeliveryClipProbe] from the clip's start, facing +z (x = his right), at the release:\n");
        foreach (string path in Clips)
            foreach (bool mirror in new[] { false, true })
            {
                Result r = Measure(path, mirror);
                sb.AppendLine($"  {r.clip}{(mirror ? " (mirrored)" : "")}: root {r.root:F2} frontToe {r.frontToe:F2} " +
                              $"hand {r.hand:F2} | end {r.end:F2}");
            }
        Debug.Log(sb.ToString());
    }

    public static Result Measure(string path, bool mirror)
    {
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview"));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Bowler.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var ctrl = new AnimatorController();
        ctrl.AddLayer("probe");
        AnimatorState st = ctrl.layers[0].stateMachine.AddState("clip");
        st.motion = clip;
        st.mirror = mirror;
        st.iKOnFeet = true;
        PlayableGraph graph = PlayableGraph.Create("DeliveryClipProbe");
        try
        {
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = go.GetComponent<Animator>();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = true;
            anim.runtimeAnimatorController = null;
            anim.fireEvents = false;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "out", anim);
            output.SetSourcePlayable(AnimatorControllerPlayable.Create(graph, ctrl));
            graph.Play();
            graph.Evaluate(0f);

            float release = clip.events.Length > 0 ? clip.events[0].time : clip.length;
            Transform hand = anim.GetBoneTransform(mirror ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform toe = anim.GetBoneTransform(mirror ? HumanBodyBones.RightToes : HumanBodyBones.LeftToes);
            Vector3 root = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            var r = new Result { clip = clip.name, mirrored = mirror };
            bool got = false;
            const float dt = 1f / 120f;
            for (float t = 0f; t < clip.length - 1e-4f; t += dt)
            {
                graph.Evaluate(dt);
                root += rot * anim.deltaPosition;
                rot = anim.deltaRotation * rot;
                if (!got && t + dt >= release)
                {
                    got = true;
                    r.root = root;
                    r.frontToe = root + rot * (toe.position - go.transform.position);
                    r.hand = root + rot * (hand.position - go.transform.position);
                }
            }
            r.end = root;
            return r;
        }
        finally
        {
            graph.Destroy();
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(ctrl);
        }
    }
}
