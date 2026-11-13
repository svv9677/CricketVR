using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// The game's bowling actions written out for hand animation in Blender (Tools/mocap/handanim):
/// each played on the real Bowler prefab exactly as the game plays it - its state's speed, the
/// Time a pace ball gives (1.24; 1.2 for spin), and BowlerSnap's quickening into the release - and
/// every bone's world position and rotation recorded at <see cref="Fps"/>, with root motion, from
/// the clip's start to a little past the release. The bind pose comes first, so Blender can turn
/// world rotations into its own bones' poses whatever the axes.
///
/// Out: Tools/mocap/handanim/actions/&lt;Name&gt;.json. Menu: CricketVR/Bowling/Export Bowling Actions For Blender.
/// </summary>
public static class BowlingClipExport
{
    public const int Fps = 30;
    private const string Prefab = "Assets/Resources/Prefabs/Bowler.prefab";
    private const string Controller = "Assets/Resources/Animations/Bowling/Steyn.controller";

    private struct Source
    {
        public string state, name;
        public float time;          // the controller's Time parameter for a typical ball of this kind
        public float after;         // seconds kept past the release
    }

    private static readonly Source[] Sources =
    {
        new Source { state = "Steyn", name = "Steyn", time = 1.24f, after = 0.6f },
        new Source { state = "Bumrah", name = "Bumrah", time = 1.15f, after = 0.6f },
        new Source { state = "Lyon", name = "Lyon", time = 1.2f, after = 0.6f },
    };

    [MenuItem("CricketVR/Bowling/Export Bowling Actions For Blender")]
    public static void ExportAll()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "mocap", "handanim", "actions"));
        Directory.CreateDirectory(dir);
        var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(Controller);
        var states = ctrl.layers[0].stateMachine.states.Select(c => c.state).ToList();
        var log = new StringBuilder("[BowlingClipExport]\n");
        foreach (Source s in Sources)
        {
            var st = states.FirstOrDefault(x => x.name == s.state);
            if (st == null || !(st.motion is AnimationClip clip))
            {
                log.AppendLine($"  {s.name}: no state {s.state} with a clip");
                continue;
            }
            string path = Path.Combine(dir, s.name + ".json");
            log.AppendLine("  " + Export(clip, st.speed * s.time, s, path));
        }
        Debug.Log(log.ToString());
    }

    /// One clip to JSON. `rate` is clip seconds per game second before BowlerSnap.
    private static string Export(AnimationClip clip, float rate, Source s, string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var go = Object.Instantiate(prefab);
        go.hideFlags = HideFlags.DontSave;
        PlayableGraph graph = default;
        try
        {
            foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                Object.DestroyImmediate(mb);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var an = go.GetComponent<Animator>();
            an.runtimeAnimatorController = null;
            an.applyRootMotion = true;
            an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            an.fireEvents = false;

            // Every bone of the rig (the transforms under the armature root that the meshes skin to).
            Transform hips = an.GetBoneTransform(HumanBodyBones.Hips);
            Transform armRoot = hips.parent != null ? hips.parent : go.transform;
            List<Transform> bones = hips.GetComponentsInChildren<Transform>(true).ToList();
            var rest = bones.Select(t => Pose(t)).ToList();

            float release = clip.events.Where(e => e.functionName == "ReleaseBall").Select(e => e.time).DefaultIfEmpty(clip.length).First();
            BowlerSnap.Swing? swing = BowlerSnap.Swings.Cast<BowlerSnap.Swing?>().FirstOrDefault(w => w.Value.state == s.state);

            graph = PlayableGraph.Create("BowlingClipExport");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "out", an);
            var cp = AnimationClipPlayable.Create(graph, clip);
            cp.SetApplyFootIK(true);
            output.SetSourcePlayable(cp);
            graph.Play();
            graph.Evaluate(0f);

            Vector3 root = Vector3.zero;
            Quaternion rootRot = Quaternion.identity;
            var frames = new List<List<float[]>>();
            float dt = 1f / Fps, t = 0f;
            int releaseFrame = -1;
            while (t < clip.length && (releaseFrame < 0 || frames.Count < releaseFrame + s.after * Fps))
            {
                // Pose at clip time t, root motion summed (the transform does not move in edit mode).
                var f = new List<float[]>();
                foreach (Transform b in bones)
                {
                    Vector3 p = root + rootRot * (b.position - go.transform.position);
                    Quaternion q = rootRot * b.rotation;
                    f.Add(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w });
                }
                frames.Add(f);
                if (releaseFrame < 0 && t >= release)
                    releaseFrame = frames.Count - 1;
                float speed = rate * (swing.HasValue ? BowlerSnap.SpeedAt(swing.Value, t / clip.length) : 1f);
                float step = dt * speed;
                graph.Evaluate(step);
                root += rootRot * an.deltaPosition;
                rootRot = an.deltaRotation * rootRot;
                t += step;
            }

            var sb = new StringBuilder();
            sb.Append("{\"name\":\"").Append(s.name).Append("\",\"source_clip\":\"").Append(clip.name)
              .Append("\",\"fps\":").Append(Fps).Append(",\"release_frame\":").Append(releaseFrame)
              .Append(",\"axes\":\"unity world: x right, y up, z forward (left-handed), metres\"")
              .Append(",\"bones\":[").Append(string.Join(",", bones.Select(b => "\"" + b.name + "\""))).Append("]")
              .Append(",\"rest\":[").Append(string.Join(",", rest.Select(Arr))).Append("]")
              .Append(",\"frames\":[");
            sb.Append(string.Join(",", frames.Select(f => "[" + string.Join(",", f.Select(Arr)) + "]")));
            sb.Append("]}");
            File.WriteAllText(path, sb.ToString());
            return $"{s.name}: {frames.Count} frames at {Fps} fps, release frame {releaseFrame}, {bones.Count} bones -> {path}";
        }
        finally
        {
            if (graph.IsValid())
                graph.Destroy();
            Object.DestroyImmediate(go);
        }
    }

    private static float[] Pose(Transform t)
    {
        Vector3 p = t.position;
        Quaternion q = t.rotation;
        return new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
    }

    private static string Arr(float[] a) =>
        "[" + string.Join(",", a.Select(v => v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture))) + "]";
}
