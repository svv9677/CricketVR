using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor captures for checking things by eye in Play mode.
///   SceneSnap.Render(pos, lookAt, fov, "Captures/x.png") - one frame from a temporary camera.
///   SceneSnap.Umpire(...) - each umpire signal in turn, rendered from the batter's end (where the
///   player sees it) and close up, at the middle of its hold: Captures/umpire/&lt;signal&gt;_{far,near}.png.
/// </summary>
public static class SceneSnap
{
    public static string Render(Vector3 position, Vector3 lookAt, float fov, string path, int width = 720, int height = 720)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var go = new GameObject("Snap Camera");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 600f;
        go.transform.position = position;
        go.transform.LookAt(lookAt, Vector3.up);
        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        return path;
    }

    private static readonly Queue<UmpireSignal> signals = new Queue<UmpireSignal>();
    private static UmpireSignal showing;
    private static double shootAt;
    public static string Log = "";

    public static void Umpire(params UmpireSignal[] which)
    {
        signals.Clear();
        foreach (var s in which) signals.Enqueue(s);
        showing = UmpireSignal.None;
        Log = "";
        shootAt = 0;
        EditorApplication.update -= StepUmpire;
        EditorApplication.update += StepUmpire;
    }

    private static void StepUmpire()
    {
        var u = Object.FindFirstObjectByType<UmpireSignals>();
        if (!Application.isPlaying || u == null) { EditorApplication.update -= StepUmpire; Log += "stopped\n"; return; }
        if (showing != UmpireSignal.None)
        {
            if (EditorApplication.timeSinceStartup < shootAt) return;
            Animator a = u.GetComponent<Animator>();
            Vector3 chest = a.GetBoneTransform(HumanBodyBones.Chest).position;
            Vector3 hand = a.GetBoneTransform(HumanBodyBones.RightHand).position;
            Log += Render(new Vector3(9.5f, 1.65f, 0.3f), chest + Vector3.up * 0.3f, 14f, $"Captures/umpire/{showing}_far.png") + " ";
            Log += Render(chest + u.transform.forward * 2.6f + u.transform.right * 0.6f + Vector3.up * 0.3f, Vector3.Lerp(chest, hand, 0.5f), 45f, $"Captures/umpire/{showing}_near.png") + "\n";
            showing = UmpireSignal.None;
            return;
        }
        if (signals.Count == 0) { EditorApplication.update -= StepUmpire; Log += "DONE\n"; return; }
        showing = signals.Dequeue();
        u.Signal(showing, 0f);
        shootAt = EditorApplication.timeSinceStartup + 0.9;
    }
}
