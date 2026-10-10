using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Judges a character's pose every frame, in Play mode, for the things that look wrong at a
/// glance: feet not on the ground (hovering, or sunk in) while standing on them, toes pointing up,
/// planted feet sliding, the trunk leaning back, arms through the body, bones jumping in a frame.
/// Each problem is logged with its time (game time since Begin) so the frames can be looked up;
/// Report sums them per kind with the worst case.
///
///   PoseAudit.Begin(animator, "keeper", "/path/audit.csv");  ...  PoseAudit.End()  ->  PoseAudit.Report
/// </summary>
public static class PoseAudit
{
    /// A foot is standing on the ground when it is this close to its standing height and barely
    /// moving vertically.
    private const float StandBand = 0.06f, StandVertical = 0.4f;
    /// Toes up: against his own standing foot (an absolute angle passed a foot 28 degrees up from
    /// it). Both feet off: both this far above standing, a hover (or a jump).
    private const float HoverLimit = 0.035f, SinkLimit = -0.03f, SlideLimit = 0.3f, ToeUpLimit = 15f, BothOffLimit = 0.05f;
    private const float StandingToePitch = -27f;   // Mixamo foot-to-toe, standing (measured)
    private const float LeanBackLimit = 12f, ArmLimit = 0.03f, JumpLimit = 0.08f;

    private static Animator subject;
    private static string label, csv;
    private static float start, restAnkle;
    private static Transform[] feet = new Transform[2], toes = new Transform[2];
    private static Vector3[] lastFoot = new Vector3[2];
    private static Transform[] watched;
    private static Vector3[] lastLocal;
    private static bool havePrev;

    private class Tally { public int frames; public float worst, worstAt; public string note; }
    private static readonly Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
    private static int frames;

    public static bool Running { get; private set; }

    public static void Begin(Animator a, string name, string csvPath)
    {
        subject = a;
        label = name;
        csv = csvPath;
        if (!string.IsNullOrEmpty(csv))
            File.WriteAllText(csv, "t,kind,value,note\n");
        tallies.Clear();
        frames = 0;
        start = Time.time;
        feet[0] = a.GetBoneTransform(HumanBodyBones.LeftFoot);
        feet[1] = a.GetBoneTransform(HumanBodyBones.RightFoot);
        toes[0] = a.GetBoneTransform(HumanBodyBones.LeftToes);
        toes[1] = a.GetBoneTransform(HumanBodyBones.RightToes);
        // His standing ankle height: the lower foot right now, if he is standing; else a default.
        float h0 = Mathf.Min(Height(feet[0]), Height(feet[1]));
        restAnkle = h0 > 0.03f && h0 < 0.16f ? h0 : 0.09f;
        watched = new[]
        {
            a.GetBoneTransform(HumanBodyBones.Hips), a.GetBoneTransform(HumanBodyBones.Chest),
            a.GetBoneTransform(HumanBodyBones.Head), a.GetBoneTransform(HumanBodyBones.LeftHand),
            a.GetBoneTransform(HumanBodyBones.RightHand), feet[0], feet[1],
        };
        lastLocal = new Vector3[watched.Length];
        havePrev = false;
        Running = true;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    public static void End()
    {
        EditorApplication.update -= Tick;
        Running = false;
    }

    private static float Height(Transform t) => t == null ? 1f : t.position.y - BallFlight.GroundY(t.position);

    private static void Tick()
    {
        if (!Application.isPlaying || subject == null)
        {
            End();
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;
        frames++;
        float t = Time.time - start;
        Transform root = subject.transform;

        // Feet: on the ground when standing on them, flat, still.
        for (int i = 0; i < 2; i++)
        {
            Transform f = feet[i];
            if (f == null) continue;
            float h = Height(f) - restAnkle;
            Vector3 v = (f.position - lastFoot[i]) / dt;
            bool standing = havePrev && Mathf.Abs(h) < StandBand && Mathf.Abs(v.y) < StandVertical;
            string side = i == 0 ? "L" : "R";
            if (standing)
            {
                if (h > HoverLimit) Note("foot hovering", t, h, side);
                if (h < SinkLimit) Note("foot sunk in ground", t, -h, side);
                float slide = new Vector2(v.x, v.z).magnitude;
                if (slide > SlideLimit) Note("planted foot sliding", t, slide, side);
                if (toes[i] != null)
                {
                    Vector3 along = toes[i].position - f.position;
                    float pitch = Mathf.Atan2(along.y, new Vector2(along.x, along.z).magnitude) * Mathf.Rad2Deg;
                    if (pitch - StandingToePitch > ToeUpLimit) Note("toes pointing up", t, pitch - StandingToePitch, side);
                }
            }
            lastFoot[i] = f.position;
        }
        if (feet[0] != null && feet[1] != null)
        {
            float off = Mathf.Min(Height(feet[0]), Height(feet[1])) - restAnkle;
            if (off > BothOffLimit) Note("both feet off the ground", t, off, "");
        }

        // Trunk lean, front-back, against his own facing (+ forward, - back).
        Transform hips = watched[0], chest = watched[1];
        if (hips != null && chest != null)
        {
            Vector3 trunk = chest.position - hips.position;
            Vector3 fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            float lean = Mathf.Atan2(Vector3.Dot(trunk, fwd), trunk.y) * Mathf.Rad2Deg;
            if (-lean > LeanBackLimit) Note("leaning back", t, -lean, "");
        }

        // Arms through the body, and bones that jump.
        float clip = FieldingFilm.ArmPenetration(subject, out string where);
        if (clip > ArmLimit) Note("arm through body", t, clip, where.Split('|')[0].Trim());
        for (int i = 0; i < watched.Length; i++)
        {
            if (watched[i] == null) continue;
            Vector3 local = root.InverseTransformPoint(watched[i].position);
            if (havePrev)
            {
                float jump = (local - lastLocal[i]).magnitude;
                // Fast is fine; a step change far beyond what the body could move in the frame is not.
                if (jump > JumpLimit * Mathf.Max(1f, dt * 60f)) Note("jump in one frame", t, jump, watched[i].name);
            }
            lastLocal[i] = local;
        }
        havePrev = true;
    }

    private static void Note(string kind, float t, float value, string note)
    {
        if (!tallies.TryGetValue(kind, out Tally tally))
            tallies[kind] = tally = new Tally();
        tally.frames++;
        if (value > tally.worst) { tally.worst = value; tally.worstAt = t; tally.note = note; }
        if (!string.IsNullOrEmpty(csv))
            File.AppendAllText(csv, $"{t:F3},{kind},{value:F3},{note}\n");
    }

    public static string Report
    {
        get
        {
            var sb = new StringBuilder($"{label}: {frames} frames\n");
            if (tallies.Count == 0) sb.AppendLine("  nothing wrong found");
            foreach (var kv in tallies)
                sb.AppendLine($"  {kv.Key}: {kv.Value.frames} frames ({100f * kv.Value.frames / Mathf.Max(1, frames):F0}%), worst {kv.Value.worst:F2} at {kv.Value.worstAt:F2}s ({kv.Value.note})");
            return sb.ToString();
        }
    }
}
