using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Looks for anything that grows as the game is played: every few seconds of Play mode it
/// writes the player-loop time, the script time, the object and component counts and the
/// managed heap, so a long GameLoopTest run shows whether any of them climb ball after ball.
///   LagProbe.Start(5f);  ...  LagProbe.Report;  LagProbe.Stop();
/// </summary>
public static class LagProbe
{
    private static float every, next;
    private static readonly StringBuilder report = new StringBuilder();
    private static ProfilerRecorder playerLoop, scripts, physics, gcAlloc;
    private static float sumMs, sumScripts, sumPhysics;
    private static int frames;

    public static string Report => report.ToString();

    public static void Start(float seconds)
    {
        Stop();
        every = seconds;
        next = Time.realtimeSinceStartup + every;
        report.Clear().AppendLine("t(s)  loop ms  scripts ms  physics ms  GOs  comps  heap MB  gc/frame KB");
        playerLoop = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "PlayerLoop");
        scripts = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate");
        physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate");
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        EditorApplication.update += Tick;
    }

    public static void Stop()
    {
        EditorApplication.update -= Tick;
        playerLoop.Dispose();
        scripts.Dispose();
        physics.Dispose();
        gcAlloc.Dispose();
        sumMs = sumScripts = sumPhysics = 0f;
        frames = 0;
    }

    private static long gcSum;

    private static void Tick()
    {
        if (!Application.isPlaying)
            return;
        frames++;
        sumMs += playerLoop.LastValue * 1e-6f;
        sumScripts += scripts.LastValue * 1e-6f;
        sumPhysics += physics.LastValue * 1e-6f;
        gcSum += gcAlloc.LastValue;
        if (Time.realtimeSinceStartup < next)
            return;
        next = Time.realtimeSinceStartup + every;
        int gos = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int comps = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        float heap = System.GC.GetTotalMemory(false) / 1048576f;
        float n = Mathf.Max(1, frames);
        report.AppendLine($"{Time.time,5:F0}  {sumMs / n,7:F2}  {sumScripts / n,10:F2}  {sumPhysics / n,10:F2}  {gos,4}  {comps,5}  {heap,7:F1}  {gcSum / n / 1024f,8:F1}");
        sumMs = sumScripts = sumPhysics = 0f;
        gcSum = 0;
        frames = 0;
    }
}
