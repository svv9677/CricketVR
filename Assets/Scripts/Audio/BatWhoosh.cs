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
/// The noise is made, not recorded (pink-ish, 2 s, seamless loop, so there is no recording to
/// license): BatAudioMarksBuilder writes it to Assets/Art/Generated/Bat/Bat Air.wav and puts it on the
/// Whoosh Voice child of the bat prefab's sweet spot.
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

    [Tooltip("The Whoosh Voice child of the sweet spot (AudioSource with the noise loop, low-pass and high-pass filters).")]
    [SerializeField] private AudioSource voice;

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
        source = voice;
        low = voice != null ? voice.GetComponent<AudioLowPassFilter>() : null;
        high = voice != null ? voice.GetComponent<AudioHighPassFilter>() : null;
        if (source == null || source.clip == null || low == null || high == null)
        {
            Debug.LogError("BatWhoosh has no Whoosh Voice - run CricketVR > Scene Objects > Place Bat Audio And Marks.", this);
            enabled = false;
            return;
        }
        GameAudio.Route(source, GameAudio.Channel.Bat);
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
}
