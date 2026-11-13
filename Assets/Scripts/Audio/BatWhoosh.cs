using UnityEngine;

/// <summary>
/// The air off the bat as it is swung (2026-11-08: "if I swing the bat hard, it should make a
/// sound... at different speeds, it should sound louder or softer, like how it would actually sound
/// in real life"). Not a sample played per swing: a bed of air noise that runs all the time from
/// the blade's sweet spot, its loudness, pitch and brightness following how fast the blade is
/// moving through the air this frame - so a slow lift is silent, a firm push a soft hush, and a
/// full swing a real whoosh that sweeps past the ear with the bat.
///
/// How it scales: noise from air flowing past a body grows as about the sixth power of its speed
/// (intensity), so amplitude as about the cube; here a slightly gentler 2.5 power, so a VR swing
/// (often 12-20 m/s at the sweet spot against a real batter's 25-30) is still heard. Below
/// SilentBelow nothing; at FullAt full volume. The noise's band moves up with speed (a faster
/// stream sheds smaller, quicker eddies): low-pass and high-pass corners and the pitch all rise.
///
/// The noise is made here (pink-ish, 2 s, seamless loop), so there is no recording to license.
/// Played through the Bat channel; doppler off (the bat is half a metre from the ear, and the rig's
/// blinks between ends would have warbled it).
/// </summary>
[DisallowMultipleComponent]
public class BatWhoosh : MonoBehaviour
{
    /// Sweet-spot speed (m/s) below which the bat is silent, and at which it is at full volume.
    public const float SilentBelow = 6f, FullAt = 30f;
    /// Loudness grows as speed to this power (see above).
    public const float Exponent = 2.5f;
    /// Volume at FullAt (the Bat channel lifts it a further 4 dB).
    private const float MaxVolume = 0.75f;
    /// A jump bigger than this in one frame is the rig moving (an end change, a recentre), not a swing.
    private const float TeleportMetres = 0.8f;
    /// The speed follows quickly as a swing starts and eases off a little slower as it ends.
    private const float AttackSeconds = 0.02f, ReleaseSeconds = 0.08f;

    private Transform spot;
    private AudioSource source;
    private AudioLowPassFilter low;
    private AudioHighPassFilter high;
    private Vector3 lastPosition;
    private bool havePosition;
    private float speed;

    /// The blade's speed through the air this frame, smoothed (m/s): for checks.
    public float Speed => speed;

    private void Start()
    {
        var geometry = GetComponent<BatGeometry>();
        spot = geometry != null && geometry.sweetSpot != null ? geometry.sweetSpot : transform;
        var voice = new GameObject("Whoosh Voice");
        voice.transform.SetParent(spot, false);
        source = voice.AddComponent<AudioSource>();
        source.clip = Noise();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 0.6f;
        source.maxDistance = 12f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.volume = 0f;
        source.priority = 64;
        GameAudio.Route(source, GameAudio.Channel.Bat);
        low = voice.AddComponent<AudioLowPassFilter>();
        low.lowpassResonanceQ = 1.2f;
        high = voice.AddComponent<AudioHighPassFilter>();
        high.highpassResonanceQ = 1f;
        source.timeSamples = Random.Range(0, source.clip.samples);
        source.Play();
    }

    private void LateUpdate()
    {
        if (source == null)
            return;
        float dt = Time.unscaledDeltaTime;
        Vector3 p = spot.position;
        float now = 0f;
        if (havePosition && dt > 1e-4f)
        {
            float moved = (p - lastPosition).magnitude;
            now = moved > TeleportMetres ? 0f : moved / dt;
        }
        lastPosition = p;
        havePosition = true;
        float tau = now > speed ? AttackSeconds : ReleaseSeconds;
        speed += (now - speed) * (1f - Mathf.Exp(-dt / tau));
        Apply(speed);
    }

    private void Apply(float s)
    {
        source.volume = Loudness(s) * MaxVolume;
        source.pitch = Mathf.Lerp(0.8f, 1.3f, Mathf.InverseLerp(SilentBelow, FullAt, s));
        low.cutoffFrequency = Mathf.Clamp(500f + 150f * s, 600f, 7000f);
        high.cutoffFrequency = Mathf.Clamp(80f + 14f * s, 100f, 600f);
    }

    /// 0 to 1: how loud the air off a blade moving at `s` m/s is, against a full swing.
    public static float Loudness(float s)
    {
        if (s <= SilentBelow)
            return 0f;
        // Faded in over the first 3 m/s above the floor, so it never clicks on.
        float gate = Mathf.Clamp01((s - SilentBelow) / 3f);
        return gate * Mathf.Clamp01(Mathf.Pow(s / FullAt, Exponent));
    }

    /// Two seconds of pink-ish noise with a slow flutter, its ends crossfaded so the loop has no seam.
    private static AudioClip Noise()
    {
        const int rate = 44100, n = rate * 2, fade = rate / 10;
        var data = new float[n + fade];
        var rng = new System.Random(1108);
        float b0 = 0f, b1 = 0f, b2 = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            // Paul Kellet's economy pink filter.
            b0 = 0.99765f * b0 + w * 0.0990460f;
            b1 = 0.96300f * b1 + w * 0.2965164f;
            b2 = 0.57000f * b2 + w * 1.0526913f;
            float pink = (b0 + b1 + b2 + w * 0.1848f) * 0.18f;
            float t = i / (float)rate;
            float flutter = 1f + 0.12f * Mathf.Sin(t * 2f * Mathf.PI * 7f) + 0.08f * Mathf.Sin(t * 2f * Mathf.PI * 11.5f);
            data[i] = pink * flutter;
        }
        var loop = new float[n];
        for (int i = 0; i < n; i++)
            loop[i] = data[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            loop[i] = data[i] * k + data[n + i] * (1f - k);
        }
        var clip = AudioClip.Create("Bat Air", n, 1, rate, false);
        clip.SetData(loop, 0);
        return clip;
    }
}
