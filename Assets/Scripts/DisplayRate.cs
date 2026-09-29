using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Meta;

/// <summary>
/// Runs the headset at 90 Hz instead of Quest's default 72 when the game can keep up.
///
/// A delivery at 130 km/h covers half a metre between frames at 72 Hz, and a hard swing 20-30
/// degrees: both read as stutter however smoothly they are simulated (the ball is already
/// interpolated). 90 Hz shortens every step by a fifth. The game is only asked to hold it if it
/// can: after settling, if the average frame over a few seconds misses 90, it drops back to 72
/// for the rest of the session rather than stutter worse at a rate it cannot hold.
///
/// Uses the Meta OpenXR display utilities (com.unity.xr.meta-openxr, the Meta Quest Display
/// Utilities feature in Project Settings > XR Plug-in Management > OpenXR). No objects: a static
/// hook on the frame, installed at startup.
/// </summary>
public static class DisplayRate
{
    private const float Wanted = 90f, Fallback = 72f;
    /// Let loading and shader warm-up pass before judging, then judge over this long.
    private const float SettleSeconds = 6f, JudgeSeconds = 4f;
    /// Average frame rate below this counts as not holding 90.
    private const float Holding = 86f;

    private static bool requested, judged;
    private static float requestedAt, sum;
    private static int frames;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor)
            return;
        Application.onBeforeRender -= Tick;
        Application.onBeforeRender += Tick;
    }

    private static void Tick()
    {
        if (judged)
            return;
        XRDisplaySubsystem display = Display();
        if (display == null)
            return;
        if (!requested)
        {
            requested = true;
            requestedAt = Time.realtimeSinceStartup;
            if (!Supports(display, Wanted) || !display.TryRequestDisplayRefreshRate(Wanted))
            {
                judged = true;
                return;
            }
            Debug.Log($"[DisplayRate] Requested {Wanted} Hz");
            return;
        }
        float since = Time.realtimeSinceStartup - requestedAt;
        if (since < SettleSeconds)
            return;
        sum += Time.unscaledDeltaTime;
        frames++;
        if (since < SettleSeconds + JudgeSeconds)
            return;
        judged = true;
        float fps = frames / Mathf.Max(sum, 1e-3f);
        if (fps < Holding)
        {
            display.TryRequestDisplayRefreshRate(Fallback);
            Debug.Log($"[DisplayRate] Averaged {fps:F0} fps at {Wanted} Hz; back to {Fallback} Hz");
        }
        else
        {
            Debug.Log($"[DisplayRate] Holding {Wanted} Hz ({fps:F0} fps)");
        }
    }

    private static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

    private static XRDisplaySubsystem Display()
    {
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem d in displays)
            if (d.running)
                return d;
        return null;
    }

    private static bool Supports(XRDisplaySubsystem display, float rate)
    {
        if (!display.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out NativeArray<float> rates))
            return false;
        bool found = false;
        foreach (float r in rates)
            found |= Mathf.Approximately(r, rate);
        rates.Dispose();
        return found;
    }
}
