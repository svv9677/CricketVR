using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Meta;

/// <summary>
/// Runs the headset as fast as the game can keep up - 120 Hz, else 90, else Quest's default 72 -
/// with fixed foveated rendering to help it keep up.
///
/// A delivery at 130 km/h covers half a metre between frames at 72 Hz, and a hard swing 20-30
/// degrees: both read as stutter however smoothly they are simulated (the ball is already
/// interpolated). 90 Hz shortens every step by a fifth, 120 by 40%. The game is only asked to hold
/// a rate it can: after each scene settles, and every few seconds from then on, if the average
/// frame misses the rate it steps down one (120, 90, 72) for the rest of the session rather than
/// stutter worse at a rate it cannot hold.
///
/// The physics clock follows the display: one step a frame at 120 and 90 Hz, two at 72. It ran at 100 Hz
/// against either, so most frames got one step and every ninth or tenth got two (at 90 Hz) or a
/// third of them two (72 Hz): uneven motion for anything not interpolated, and a frame that costs
/// twice the physics now and then - a dropped frame waiting to happen. Everything that integrates
/// reads Time.fixedDeltaTime (BallFlight's predictions included), so aim and flight agree at any
/// step.
///
/// Uses the Meta OpenXR display utilities (com.unity.xr.meta-openxr, the Meta Quest Display
/// Utilities feature in Project Settings > XR Plug-in Management > OpenXR). No objects: a static
/// hook on the frame, installed at startup.
/// </summary>
public static class DisplayRate
{
    /// The rates tried, best first (those the headset supports): 120 since 2026-11-09 ("the ball
    /// is a little bit choppy ... make it as high frame rate as possible").
    private static readonly float[] Ladder = { 120f, 90f, 72f };
    /// Fixed foveation level, 0 (off) to 1 (strongest).
    private const float Foveation = 0.75f;
    /// Let a load and its shader warm-up pass before judging, then judge over windows this long.
    private const float SettleSeconds = 6f, JudgeSeconds = 3f;
    /// Averaging below this share of the rate over a window counts as not holding it.
    private const float Holding = 0.955f;
    /// So does this share of the window's frames each taking longer than Late frame times. An
    /// average hides frames dropped only while looking one way: facing the bowler the headset
    /// drew about three times what it drew facing the keeper (PersonLod), and a window spent
    /// partly each way averaged near the rate while every look forward stuttered (2026-11-11).
    private const float LateShare = 0.03f, Late = 1.5f;

    private static bool requested;
    private static int level = -1;
    private static float judgeFrom, sum;
    private static int frames, late;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (Application.isEditor)
            return;
        Application.onBeforeRender -= Tick;
        Application.onBeforeRender += Tick;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
    }

    /// A new scene: judged afresh once it has settled. The rate was judged once, in the menu, which
    /// is far lighter than a match: a rate the menu held, the match could not, and every dropped
    /// frame showed as the ball jumping on its way in.
    private static void OnLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Restart();

    private static void Restart()
    {
        judgeFrom = Time.realtimeSinceStartup + SettleSeconds;
        sum = 0f;
        frames = 0;
        late = 0;
    }

    private static void Tick()
    {
        XRDisplaySubsystem display = Display();
        if (display == null)
            return;
        if (!requested)
        {
            requested = true;
            // Fixed foveated rendering: full resolution where the lenses are sharp, less toward
            // their edges where nobody looks - the biggest GPU saving there is on Quest.
            display.foveatedRenderingLevel = Foveation;
            display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            Debug.Log($"[DisplayRate] Foveation {display.foveatedRenderingLevel:F2}");
            StepTo(display, 0);
            Restart();
            return;
        }
        if (level < 0 || level >= Ladder.Length - 1)
            return;                     // at the lowest rate (or none could be set): nothing to judge
        float now = Time.realtimeSinceStartup;
        if (now < judgeFrom)
            return;
        float rate = Ladder[level];
        sum += Time.unscaledDeltaTime;
        frames++;
        if (Time.unscaledDeltaTime > Late / rate)
            late++;
        if (now < judgeFrom + JudgeSeconds)
            return;
        float fps = frames / Mathf.Max(sum, 1e-3f);
        if (fps < rate * Holding || late > frames * LateShare)
        {
            Debug.Log($"[DisplayRate] Averaged {fps:F0} fps, {late} of {frames} frames late at {rate} Hz: stepping down");
            StepTo(display, level + 1);
            Restart();
            return;
        }
        // Holding: judged again over the next window, for as long as the session runs - a match can
        // get heavier than its first seconds.
        judgeFrom = now;
        sum = 0f;
        frames = 0;
        late = 0;
    }

    /// The best supported rate from `from` down the ladder, with the physics locked to it.
    private static void StepTo(XRDisplaySubsystem display, int from)
    {
        for (int i = from; i < Ladder.Length; i++)
        {
            if (!Supports(display, Ladder[i]) || !display.TryRequestDisplayRefreshRate(Ladder[i]))
                continue;
            level = i;
            Debug.Log($"[DisplayRate] Requested {Ladder[i]} Hz");
            LockPhysics(Ladder[i]);
            return;
        }
        level = Ladder.Length - 1;
    }

    /// Physics steps no longer than this (s): 90 Hz is one step a frame, 72 Hz two (144 Hz).
    private const float LongestStep = 0.0115f;

    /// A whole number of physics steps per displayed frame.
    private static void LockPhysics(float hz)
    {
        int perFrame = Mathf.Max(1, Mathf.CeilToInt(1f / (hz * LongestStep)));
        Time.fixedDeltaTime = 1f / (hz * perFrame);
        Debug.Log($"[DisplayRate] Physics at {hz * perFrame:F0} Hz ({perFrame} per frame)");
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
