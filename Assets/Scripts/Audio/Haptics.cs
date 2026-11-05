using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The controllers' feel, as shaped patterns rather than single buzzes (2026-11-05: "improve
/// haptics"). OpenXR on the Quest gives an impulse - an amplitude and a duration - and a new one
/// replaces the last, so a pattern is played by sending a short impulse every frame at the
/// pattern's amplitude for that moment: an envelope, per hand.
///
///   middled    a crisp knock: full for a moment, gone in a fifth of a second - the clean feel
///   good       a solid thump, a little longer
///   thick edge a rattle up the handle, more in the bottom hand
///   thin edge  a light tick (you barely felt it)
///   toe, shoulder, back   a jarring buzz that rings on
///   handle     the long sting, both hands
///   beaten     the faintest brush when the ball goes past the bat by a whisker
///   four, six  two pulses; three, rising
///   wicket     a heavy, falling rumble
///   no-ball    a soft double tap (a free hit is coming)
/// Strengths scale with how hard the ball and bat met, and with the Haptics.Strength preference.
/// </summary>
public static class Haptics
{
    public struct Key
    {
        public float t, amp;
        public Key(float t, float amp) { this.t = t; this.amp = amp; }
    }

    public const string StrengthPref = "Haptics.Strength";
    public static float Strength => PlayerPrefs.GetFloat(StrengthPref, 1f);

    private class Playing { public Key[] keys; public float start, scale; public bool left; public float rattle; }
    private static readonly List<Playing> playing = new List<Playing>();
    private static Runner runner;

    /// Play `keys` (time s, amplitude 0..1) on one hand, scaled; rattle (Hz) chops it into pulses.
    public static void Play(Key[] keys, bool leftHand, float scale = 1f, float rattle = 0f)
    {
        if (keys == null || keys.Length == 0 || scale <= 0f) return;
        Ensure();
        playing.Add(new Playing { keys = keys, start = Time.unscaledTime, scale = scale, left = leftHand, rattle = rattle });
    }

    public static void Both(Key[] keys, float scale = 1f, float rattle = 0f)
    {
        Play(keys, true, scale, rattle);
        Play(keys, false, scale, rattle);
    }

    // ---- The patterns ------------------------------------------------------------------------

    public static readonly Key[] Middled = { new Key(0f, 1f), new Key(0.025f, 1f), new Key(0.06f, 0.35f), new Key(0.16f, 0.08f), new Key(0.2f, 0f) };
    public static readonly Key[] GoodHit = { new Key(0f, 0.9f), new Key(0.04f, 0.75f), new Key(0.12f, 0.2f), new Key(0.18f, 0f) };
    public static readonly Key[] ThickEdge = { new Key(0f, 0.9f), new Key(0.05f, 0.7f), new Key(0.2f, 0.25f), new Key(0.26f, 0f) };
    public static readonly Key[] ThinEdge = { new Key(0f, 0.5f), new Key(0.03f, 0.3f), new Key(0.06f, 0f) };
    public static readonly Key[] Jar = { new Key(0f, 1f), new Key(0.05f, 0.9f), new Key(0.25f, 0.45f), new Key(0.36f, 0f) };
    public static readonly Key[] Sting = { new Key(0f, 1f), new Key(0.08f, 0.85f), new Key(0.35f, 0.4f), new Key(0.5f, 0f) };
    public static readonly Key[] Brush = { new Key(0f, 0.16f), new Key(0.05f, 0.1f), new Key(0.08f, 0f) };
    public static readonly Key[] Four = { new Key(0f, 0.6f), new Key(0.05f, 0.6f), new Key(0.06f, 0f), new Key(0.2f, 0f), new Key(0.21f, 0.6f), new Key(0.26f, 0.6f), new Key(0.27f, 0f) };
    public static readonly Key[] Six = { new Key(0f, 0.5f), new Key(0.05f, 0.5f), new Key(0.06f, 0f), new Key(0.18f, 0f), new Key(0.19f, 0.7f), new Key(0.24f, 0.7f), new Key(0.25f, 0f), new Key(0.37f, 0f), new Key(0.38f, 0.95f), new Key(0.48f, 0.95f), new Key(0.5f, 0f) };
    public static readonly Key[] Wicket = { new Key(0f, 0.6f), new Key(0.1f, 0.5f), new Key(0.45f, 0.25f), new Key(0.75f, 0f) };
    public static readonly Key[] DoubleTap = { new Key(0f, 0.3f), new Key(0.04f, 0f), new Key(0.12f, 0f), new Key(0.13f, 0.3f), new Key(0.17f, 0f) };

    /// The bat meeting the ball. bottomIsLeft: which controller is the bottom hand (the one the bat
    /// hangs from). hard: 0..1, how hard they met.
    public static void BatContact(BatContact.ContactKind kind, float hard, bool bottomIsLeft)
    {
        float s = Mathf.Lerp(0.65f, 1f, Mathf.Sqrt(Mathf.Clamp01(hard)));
        switch (kind)
        {
            case global::BatContact.ContactKind.Middled: Both(Middled, s); break;
            case global::BatContact.ContactKind.Good: Both(GoodHit, s); break;
            case global::BatContact.ContactKind.ThinEdge:
                Play(ThinEdge, bottomIsLeft, s); Play(ThinEdge, !bottomIsLeft, s * 0.6f); break;
            case global::BatContact.ContactKind.ThickEdge:
                Play(ThickEdge, bottomIsLeft, s, 32f); Play(ThickEdge, !bottomIsLeft, s * 0.7f, 32f); break;
            case global::BatContact.ContactKind.Handle: Both(Sting, s, 24f); break;
            default:   // toe, shoulder, back
                Play(Jar, bottomIsLeft, s, 28f); Play(Jar, !bottomIsLeft, s * 0.75f, 28f); break;
        }
    }

    // ---- Playing them ------------------------------------------------------------------------

    private static void Ensure()
    {
        if (runner != null) return;
        var go = new GameObject("Haptics");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        runner = go.AddComponent<Runner>();
    }

    private static float Evaluate(Key[] k, float t)
    {
        if (t <= k[0].t) return k[0].amp;
        for (int i = 1; i < k.Length; i++)
            if (t <= k[i].t)
                return Mathf.Lerp(k[i - 1].amp, k[i].amp, (t - k[i - 1].t) / Mathf.Max(1e-4f, k[i].t - k[i - 1].t));
        return -1f;   // done
    }

    private class Runner : MonoBehaviour
    {
        private void Update()
        {
            if (playing.Count == 0) return;
            float now = Time.unscaledTime, left = 0f, right = 0f;
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                Playing p = playing[i];
                float t = now - p.start;
                float a = Evaluate(p.keys, t);
                if (a < 0f) { playing.RemoveAt(i); continue; }
                if (p.rattle > 0f && Mathf.Repeat(t * p.rattle, 1f) > 0.5f) a *= 0.3f;
                a *= p.scale;
                if (p.left) left = Mathf.Max(left, a); else right = Mathf.Max(right, a);
            }
            float k = Strength;
            // A little longer than a frame, so the next one takes over before it ends.
            float d = Mathf.Max(0.025f, Time.unscaledDeltaTime * 1.6f);
            if (left > 0.01f) XRInput.SendHaptics(true, Mathf.Clamp01(left * k), d);
            if (right > 0.01f) XRInput.SendHaptics(false, Mathf.Clamp01(right * k), d);
        }
    }
}
