using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The crowd's reaction as people, not a recording (2026-11-05: "each person should cheer
/// individually, not just playing a combined sound"). Voices sit round the stands, each its own 3D
/// source; on a four, a six or a wicket each one reacts on its own - its own moment (a few at once,
/// the rest catching up over a second), its own pitch (a different person), its own loudness - with
/// people clapping and the odd whistle among them; a six gets a second wave. Clips are single
/// people (Resources/Sounds/Crowd, Tools/sfx/build_crowd_voices.py). Rao's looping crowd
/// (CrowdController) stays underneath as the murmur and swell. How many react follows the crowd
/// setting (MatchSetup.Crowd): packed, half, or nobody.
/// </summary>
public class CrowdVoices : MonoBehaviour
{
    private const int Emitters = 20;
    private const float Radius = 74f, RadiusSpread = 10f, MinHeight = 6f, MaxHeight = 24f;

    private readonly List<AudioSource> emitters = new List<AudioSource>();
    private AudioClip[] voices, claps, whistles;
    private float crowdScale = 1f;

    private void Start()
    {
        voices = Load("voice_"); claps = Load("clap_"); whistles = Load("whistle_");
        crowdScale = MatchSetup.Crowd == eCrowd.Packed ? 1f : MatchSetup.Crowd == eCrowd.Half ? 0.55f : 0f;
        for (int i = 0; i < Emitters; i++)
        {
            float a = (i + Random.value * 0.6f) / Emitters * Mathf.PI * 2f;
            float r = Radius + Random.Range(-RadiusSpread, RadiusSpread);
            var go = new GameObject("Crowd Voice " + i);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(Mathf.Cos(a) * r, Random.Range(MinHeight, MaxHeight), Mathf.Sin(a) * r);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 12f;
            s.maxDistance = 200f;
            s.dopplerLevel = 0f;
            s.priority = 190;                    // the game's own sounds come first if voices run out
            GameAudio.Route(s, GameAudio.Channel.Crowd);
            emitters.Add(s);
        }
        HUD.BallScored += OnScored;
    }

    private void OnDestroy() => HUD.BallScored -= OnScored;

    private static AudioClip[] Load(string prefix)
    {
        var list = new List<AudioClip>();
        foreach (AudioClip c in Resources.LoadAll<AudioClip>("Sounds/Crowd"))
            if (c.name.StartsWith(prefix)) list.Add(c);
        return list.ToArray();
    }

    private void OnScored(BallOutcome o)
    {
        switch (o.Signal)
        {
            case UmpireSignal.Four: StartCoroutine(React(12, 6, 1, 1.0f)); break;
            case UmpireSignal.Six: StartCoroutine(React(18, 7, 2, 1.2f)); StartCoroutine(Later(1.6f, 8, 4, 1)); break;
            case UmpireSignal.Out: StartCoroutine(React(16, 6, 2, 1.1f)); break;
        }
    }

    private IEnumerator Later(float after, int v, int c, int w)
    {
        yield return new WaitForSeconds(after);
        yield return React(v, c, w, 0.8f);
    }

    /// `v` voices, `c` clappers and `w` whistlers react, over about `spread` seconds.
    private IEnumerator React(int v, int c, int w, float spread)
    {
        v = Mathf.RoundToInt(v * crowdScale); c = Mathf.RoundToInt(c * crowdScale); w = Mathf.RoundToInt(w * crowdScale);
        var plan = new List<(float at, AudioClip[] set, float vol)>();
        for (int i = 0; i < v; i++)
        {
            // A few go up at once; the rest join in through the next second.
            float at = i < v / 3 ? Random.Range(0f, 0.2f) : Random.Range(0.15f, spread);
            plan.Add((at, voices, Random.Range(0.55f, 1f)));
        }
        for (int i = 0; i < c; i++) plan.Add((Random.Range(0.25f, spread + 0.4f), claps, Random.Range(0.5f, 0.85f)));
        for (int i = 0; i < w; i++) plan.Add((Random.Range(0.3f, spread + 0.6f), whistles, Random.Range(0.4f, 0.7f)));
        plan.Sort((a, b) => a.at.CompareTo(b.at));
        float t = 0f;
        foreach (var (at, set, vol) in plan)
        {
            if (at > t) { yield return new WaitForSeconds(at - t); t = at; }
            Say(set, vol);
        }
    }

    private void Say(AudioClip[] set, float volume)
    {
        if (set == null || set.Length == 0 || emitters.Count == 0) return;
        // A free voice somewhere in the stands (or, all busy, any of them).
        AudioSource s = null;
        int start = Random.Range(0, emitters.Count);
        for (int k = 0; k < emitters.Count && s == null; k++)
        {
            AudioSource e = emitters[(start + k) % emitters.Count];
            if (!e.isPlaying) s = e;
        }
        if (s == null) s = emitters[start];
        s.clip = set[Random.Range(0, set.Length)];
        s.pitch = Random.Range(0.88f, 1.14f);   // a different person
        s.volume = volume;
        s.Play();
    }
}
