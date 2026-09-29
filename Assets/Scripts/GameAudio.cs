using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// The game's volumes, one per kind of sound, each 0 to 2 (0-200%) and saved. They drive the
/// GameMixer's group faders (Assets/Resources/Audio, built by Tools > CricketVR > Build Audio
/// Mixer), which is what lets a sound go above its recorded level: an AudioSource's own volume
/// stops at 1, a mixer fader goes to +6 dB for 200%.
///
/// A source is only affected if it plays through its group, so every sound in the game is routed:
/// the long-lived sources with Route, the one-shots with PlayAt in place of
/// AudioSource.PlayClipAtPoint (whose temporary source bypasses the mixer). Everything that
/// happens somewhere - bat, stumps, fielders - is 3D, played from where it happened (Unity's
/// built-in panning, no spatialiser plugin); only the crowd, which surrounds the player, is 2D.
/// </summary>
public static class GameAudio
{
    public enum Channel { Master, Bat, Crowd, Stumps, Fielders }

    public const float MaxVolume = 2f;
    private const string MixerResource = "Audio/GameMixer";

    private static AudioMixer mixer;
    private static bool applied;

    public static string Parameter(Channel c) => c + "Volume";
    private static string PrefKey(Channel c) => "Volume." + c;

    private static AudioMixer Mixer
    {
        get
        {
            if (mixer == null)
                mixer = Resources.Load<AudioMixer>(MixerResource);
            return mixer;
        }
    }

    public static float GetVolume(Channel c) => PlayerPrefs.GetFloat(PrefKey(c), 1f);

    public static void SetVolume(Channel c, float volume)
    {
        volume = Mathf.Clamp(volume, 0f, MaxVolume);
        PlayerPrefs.SetFloat(PrefKey(c), volume);
        PlayerPrefs.Save();
        ApplyOne(c);
    }

    /// Push every saved volume onto the mixer. A mixer asset's snapshot overrides SetFloat until
    /// something has played through it once, so this is re-run after routing too.
    public static void ApplyAll()
    {
        foreach (Channel c in System.Enum.GetValues(typeof(Channel)))
            ApplyOne(c);
        applied = true;
    }

    private static void ApplyOne(Channel c)
    {
        if (Mixer == null)
            return;
        float v = GetVolume(c);
        // 20 log10: 1 -> 0 dB, 2 -> +6 dB; silence is the fader's floor.
        Mixer.SetFloat(Parameter(c), v > 0.0001f ? 20f * Mathf.Log10(v) : -80f);
    }

    public static AudioMixerGroup Group(Channel c)
    {
        if (Mixer == null)
            return null;
        AudioMixerGroup[] found = Mixer.FindMatchingGroups(c == Channel.Master ? "Master" : "Master/" + c);
        return found.Length > 0 ? found[0] : null;
    }

    /// Send a scene's own AudioSource through its channel.
    public static void Route(AudioSource source, Channel c)
    {
        if (source == null)
            return;
        source.outputAudioMixerGroup = Group(c);
        if (!applied)
            ApplyAll();
    }

    /// AudioSource.PlayClipAtPoint, but through the channel's group.
    public static void PlayAt(AudioClip clip, Vector3 position, Channel c, float volume = 1f)
    {
        if (clip == null)
            return;
        var go = new GameObject("One shot audio");
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        // Heard from where it happened: fully 3D (Unity's own panning and distance falloff).
        source.spatialBlend = 1f;
        source.minDistance = 2f;
        source.maxDistance = 120f;
        source.volume = volume;
        Route(source, c);
        source.Play();
        Object.Destroy(go, clip.length * (Time.timeScale < 0.01f ? 0.01f : Time.timeScale) + 0.1f);
    }
}
