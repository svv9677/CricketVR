using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pictures of the bowler's grip, for checking BowlerGrip by eye: in Play mode, Begin bowls the
/// given delivery with a bowler of that kind and pauses the editor `beforeRelease` seconds before
/// the ball leaves his hand (0: the release frame; negative: after it); Shoot then renders the hand
/// from three sides into Captures/grips/&lt;name&gt;_{palm,side,behind}.png. Resume carries on.
///   GripCapture.Begin(eBowlerKind.Fast, eDelivery.OutSwinger, 0.4f);  ... GripCapture.Shoot("out")
/// </summary>
public static class GripCapture
{
    private static float pauseBefore;
    private static bool waiting;
    private static float releasedAt;
    public static string Status = "idle";

    public static void Begin(eBowlerKind kind, eDelivery delivery, float beforeRelease)
    {
        Main m = Main.Instance;
        if (m == null || !Application.isPlaying) { Status = "not playing"; return; }
        EditorApplication.isPaused = false;
        m.theHUD.SelectBowlerOfType(BowlerRepertoire.BaseType(kind));
        BowlingAttack.ForceKind = kind;
        BowlingAttack.ForceDelivery = delivery;
        if (m.gameState != eGameState.InGame_Ready)
            m.gameState = eGameState.InGame_Ready;
        m.StartNextBall();
        pauseBefore = beforeRelease;
        waiting = true;
        releasedAt = -1f;
        Status = "waiting";
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
    }

    private static void Watch()
    {
        if (!waiting || !Application.isPlaying) { EditorApplication.update -= Watch; return; }
        AnimatedBowler b = AnimatedBowler.Instance;
        var grip = b != null ? b.GetComponent<BowlerGrip>() : null;
        if (grip == null) return;
        if (pauseBefore >= 0f)
        {
            if (b.CarryingBall && grip.Weight > 0.99f && grip.TimeToRelease() <= pauseBefore)
                Stop($"paused {grip.TimeToRelease():0.00}s before release, grip {grip.Current.name}");
        }
        else
        {
            if (!b.CarryingBall && releasedAt < 0f) releasedAt = Time.time;
            if (releasedAt >= 0f && Time.time - releasedAt >= -pauseBefore)
                Stop($"paused {Time.time - releasedAt:0.00}s after release");
        }
    }

    private static void Stop(string why)
    {
        waiting = false;
        EditorApplication.isPaused = true;
        EditorApplication.update -= Watch;
        Status = why;
    }

    public static void Resume() => EditorApplication.isPaused = false;

    private static readonly System.Collections.Generic.Queue<(eBowlerKind, eDelivery, float, string)> queue =
        new System.Collections.Generic.Queue<(eBowlerKind, eDelivery, float, string)>();
    private static string current;
    private static double nextAt;
    public static string Log = "";

    /// Capture each (kind, delivery, seconds before release, name) in turn; Log lists the files.
    public static void RunAll(params (eBowlerKind, eDelivery, float, string)[] shots)
    {
        queue.Clear();
        foreach (var s in shots) queue.Enqueue(s);
        Log = "";
        current = null;
        nextAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Step;
        EditorApplication.update += Step;
    }

    private static void Step()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Step; Log += "stopped\n"; return; }
        if (current != null)
        {
            if (waiting || !EditorApplication.isPaused) return;
            Log += Status + " -> " + Shoot(current) + "\n";
            current = null;
            EditorApplication.isPaused = false;
            nextAt = EditorApplication.timeSinceStartup + 2.5;
            return;
        }
        if (EditorApplication.timeSinceStartup < nextAt) return;
        if (queue.Count == 0) { EditorApplication.update -= Step; Log += "DONE\n"; return; }
        if (Main.Instance == null || Main.Instance.gameState == eGameState.None ||
            Main.Instance.gameState == eGameState.InGame_ResetToReadyLoop) return;
        var (k, d, before, name) = queue.Dequeue();
        current = name;
        Begin(k, d, before);
    }

    /// Render the hand from the palm side, the thumb side and from behind (the bowler's view).
    public static string Shoot(string name, float distance = 0.32f)
    {
        AnimatedBowler b = AnimatedBowler.Instance;
        if (b == null) return "no bowler";
        Animator a = b.GetComponent<Animator>();
        Transform hand = a.GetBoneTransform(HumanBodyBones.RightHand);
        Transform middle = a.GetBoneTransform(HumanBodyBones.RightMiddleProximal) ?? hand.GetChild(0);
        Vector3 ball = Main.Instance.theBall.transform.position;
        Vector3 fwd = (middle.position - hand.position).normalized;
        Vector3 travel = Vector3.ProjectOnPlane(b.transform.forward, Vector3.up).normalized;
        Vector3 focus = Vector3.Lerp(hand.position, ball, 0.6f);
        string dir = "Captures/grips";
        Directory.CreateDirectory(dir);
        var go = new GameObject("Grip Camera");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 50f;
        var rt = new RenderTexture(640, 640, 24);
        cam.targetTexture = rt;
        string result = "";
        (string, Vector3)[] views =
        {
            ("front", travel),                                     // from the batter's end: the palm and seam
            ("side", Vector3.Cross(Vector3.up, travel)),           // from the bowler's right
            ("behind", -travel),                                   // the bowler's own view
        };
        foreach (var (label, from) in views)
        {
            go.transform.position = focus + (from + Vector3.up * 0.15f).normalized * distance;
            go.transform.LookAt(focus, Vector3.up);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = $"{dir}/{name}_{label}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            result += path + " ";
        }
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        return result;
    }
}
