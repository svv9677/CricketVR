using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The match's one-shot sounds (Resources/Sounds/Sfx, built by Tools/sfx/build_sfx.py), played from
/// a pool of AudioSources rather than a new GameObject per sound (GameAudio.PlayAt): there are
/// footsteps from a dozen players several times a second. A clip group is the files' name before
/// "_N" ("step_grass" = step_grass_1..6); a play picks one at random, not the last one played.
/// Every source goes through its GameAudio channel, so the volume sliders apply.
/// </summary>
public static class GameSfx
{
    private const int Voices = 24;
    private const string Folder = "Sounds/Sfx";

    private static Dictionary<string, AudioClip[]> groups;
    private static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
    private static AudioSource[] pool;
    private static float[] startedAt;
    private static Transform poolRoot;
    private static int next;
    /// How many times each group has played (checks: GameLoopTest, the editor).
    public static readonly Dictionary<string, int> Played = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        groups = null; pool = null; poolRoot = null; startedAt = null;
        lastPick.Clear();
        Played.Clear();
    }

    public static bool Has(string group)
    {
        Load();
        return groups.ContainsKey(group);
    }

    private static void Load()
    {
        if (groups != null) return;
        groups = new Dictionary<string, AudioClip[]>();
        var lists = new Dictionary<string, List<AudioClip>>();
        foreach (AudioClip c in Resources.LoadAll<AudioClip>(Folder))
        {
            string n = c.name;
            int u = n.LastIndexOf('_');
            string g = u > 0 && int.TryParse(n.Substring(u + 1), out _) ? n.Substring(0, u) : n;
            if (!lists.TryGetValue(g, out var l)) lists[g] = l = new List<AudioClip>();
            l.Add(c);
        }
        foreach (var kv in lists) groups[kv.Key] = kv.Value.ToArray();
    }

    private static void EnsurePool()
    {
        if (pool != null && poolRoot != null) return;
        var go = new GameObject("Sfx Voices");
        poolRoot = go.transform;
        pool = new AudioSource[Voices];
        startedAt = new float[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var v = new GameObject("Voice " + i);
            v.transform.SetParent(poolRoot, false);
            var s = v.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            pool[i] = s;
        }
    }

    /// Play one of `group` at `position` (3D) on `channel`. minDistance: full volume within it.
    /// Quietly does nothing for a group that is not there.
    public static AudioSource Play(string group, Vector3 position, GameAudio.Channel channel, float volume = 1f,
                                   float pitch = 1f, float minDistance = 1.5f, float maxDistance = 70f)
    {
        AudioClip clip = Pick(group);
        if (clip == null || volume <= 0.001f) return null;
        // Out of earshot: not worth a voice.
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - position).sqrMagnitude > maxDistance * maxDistance)
            return null;
        AudioSource s = Voice();
        s.transform.position = position;
        s.spatialBlend = 1f;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        return Start(s, clip, channel, volume, pitch);
    }

    /// Play one of `group` all round the player (2D): the crowd.
    public static AudioSource Play2D(string group, GameAudio.Channel channel, float volume = 1f, float pitch = 1f)
    {
        AudioClip clip = Pick(group);
        if (clip == null || volume <= 0.001f) return null;
        AudioSource s = Voice();
        s.spatialBlend = 0f;
        return Start(s, clip, channel, volume, pitch);
    }

    private static AudioSource Start(AudioSource s, AudioClip clip, GameAudio.Channel channel, float volume, float pitch)
    {
        GameAudio.Route(s, channel);
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume);
        s.pitch = pitch;
        s.Play();
        startedAt[System.Array.IndexOf(pool, s)] = Time.unscaledTime;
        return s;
    }

    private static AudioClip Pick(string group)
    {
        Load();
        if (!groups.TryGetValue(group, out AudioClip[] clips) || clips.Length == 0) return null;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && lastPick.TryGetValue(group, out int last) && i == last)
            i = (i + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
        lastPick[group] = i;
        Played[group] = Played.TryGetValue(group, out int n) ? n + 1 : 1;
        return clips[i];
    }

    /// A free voice, or the one that has been playing longest.
    private static AudioSource Voice()
    {
        EnsurePool();
        for (int k = 0; k < Voices; k++)
        {
            int i = (next + k) % Voices;
            if (!pool[i].isPlaying) { next = (i + 1) % Voices; return pool[i]; }
        }
        int oldest = 0;
        for (int i = 1; i < Voices; i++) if (startedAt[i] < startedAt[oldest]) oldest = i;
        pool[oldest].Stop();
        return pool[oldest];
    }
}
