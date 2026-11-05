using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// On the headset only: every LogEvery seconds, one line in the device log with the frame rate,
/// the slowest frames, garbage collections and memory, so a session that "gets laggier as I keep
/// playing" (2026-11-03) can be read back with `adb logcat -s Unity | grep PerfLog` and the cause
/// told apart: memory or GCs climbing (something accumulating), or the same work at a falling
/// frame rate (the headset heating up and slowing its clocks).
/// </summary>
public static class PerfLog
{
    private const float LogEvery = 30f;
    private static float windowStart, worst, sum;
    private static int frames, slow, gcAtStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor)
            return;
        Application.onBeforeRender -= Tick;
        Application.onBeforeRender += Tick;
        windowStart = Time.realtimeSinceStartup;
        gcAtStart = System.GC.CollectionCount(0);
    }

    private static void Tick()
    {
        float dt = Time.unscaledDeltaTime;
        frames++;
        sum += dt;
        if (dt > worst) worst = dt;
        // Longer than one frame at 72 Hz with a little slack: a dropped frame.
        if (dt > 1.25f / 72f) slow++;
        float now = Time.realtimeSinceStartup;
        if (now - windowStart < LogEvery)
            return;
        int gcs = System.GC.CollectionCount(0);
        Debug.Log($"[PerfLog] t={now:F0}s fps={frames / Mathf.Max(sum, 1e-3f):F1} worst={worst * 1000f:F0}ms " +
                  $"dropped={slow}/{frames} gc={gcs - gcAtStart} mono={Profiler.GetMonoUsedSizeLong() / 1048576f:F0}MB " +
                  $"heap={Profiler.GetMonoHeapSizeLong() / 1048576f:F0}MB total={Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F0}MB " +
                  $"objects={Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        windowStart = now;
        gcAtStart = gcs;
        frames = slow = 0;
        sum = worst = 0f;
    }
}
