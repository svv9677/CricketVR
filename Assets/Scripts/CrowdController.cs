using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the stadium crowd's reactions. The crowd's motion is entirely GPU-side (see
/// CrowdStand.shader); this controller only nudges a handful of GLOBAL shader uniforms a few
/// times per delivery, so it costs effectively nothing per frame and needs no reference to the
/// crowd meshes or material.
///
/// Behaviour:
///   * At rest the crowd plays a randomised idle bob (handled in the shader).
///   * A six or a wicket triggers a full cheer: bigger/faster bobbing, spectators jumping,
///     a travelling Mexican wave, a burst of night camera flashes, and the crowd audio.
///   * A four triggers a slightly smaller cheer.
///   * Everything decays smoothly back to idle over a few seconds.
///
/// It listens to Main.onGameStateChanged and reads Ball.bounced to tell a four (ball bounced
/// before the rope) from a six (cleared it on the full).
/// </summary>
public class CrowdController : MonoBehaviour
{
    [Header("Cheer levels (0..1)")]
    [Range(0f, 1f)] public float sixExcitement    = 1.0f;
    [Range(0f, 1f)] public float fourExcitement   = 0.8f;
    [Range(0f, 1f)] public float wicketExcitement = 1.0f;

    [Header("Timing (seconds)")]
    public float cheerHold  = 2.5f;   // how long the crowd stays at full excitement
    public float rampUp     = 6f;     // excitement units/sec climbing to a cheer (fast)
    public float rampDown   = 0.35f;  // excitement units/sec settling back to idle (slow)
    public float waveDuration = 3f;   // time for the Mexican wave to travel once around

    [Header("Audio")]
    public AudioClip cheerClip;       // e.g. Assets/Sounds/Crowd.mp3
    [Range(0f, 1f)] public float cheerVolume = 0.9f;
    [Tooltip("Crowd level between cheers. The clip loops for as long as the game runs.")]
    [Range(0f, 1f)] public float ambienceVolume = 0.45f;

    // One looping crowd track whose level follows the excitement. It used to be a 53-second
    // one-shot fired on each boundary or wicket over a near-silent loop, so after a spell with no
    // four, six or wicket the "music" simply ran out.
    private AudioSource _audio;
    private float _excitement;        // current, smoothed
    private float _target;            // where we're heading (0 at idle)
    private float _holdTimer;
    private Coroutine _wave;
    private eGameState _lastState = eGameState.None;
    private bool _subscribed;

    private static readonly int IdExcitement  = Shader.PropertyToID("_CrowdExcitement");
    private static readonly int IdWaveEnabled = Shader.PropertyToID("_CrowdWaveEnabled");
    private static readonly int IdWavePhase   = Shader.PropertyToID("_CrowdWavePhase");
    private static readonly int IdFlash       = Shader.PropertyToID("_CrowdFlash");

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;     // crowd surrounds the player; keep it 2D
        _audio.clip = cheerClip;
        _audio.loop = true;
        _audio.volume = ambienceVolume;
        if (cheerClip != null)
            _audio.Play();
        // A Quest audio-device change (headset sleep, headphones) stops every source.
        AudioSettings.OnAudioConfigurationChanged += RestartAmbience;
    }

    private void OnDestroy()
    {
        AudioSettings.OnAudioConfigurationChanged -= RestartAmbience;
    }

    private void RestartAmbience(bool deviceWasChanged)
    {
        if (_audio != null && cheerClip != null && !_audio.isPlaying)
            _audio.Play();
    }

    private void OnEnable()
    {
        // Start from a clean idle in case a previous run left the globals hot.
        Shader.SetGlobalFloat(IdExcitement, 0f);
        Shader.SetGlobalFloat(IdWaveEnabled, 0f);
        Shader.SetGlobalFloat(IdWavePhase, 0f);
        Shader.SetGlobalFloat(IdFlash, 0f);
    }

    private void OnDisable()
    {
        if (_subscribed && Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameStateChanged;
        _subscribed = false;
    }

    // Subscribe as soon as Main exists. We can't do this in OnEnable because script execution
    // order isn't guaranteed to have created Main.Instance yet, so we retry from Update.
    private void EnsureSubscribed()
    {
        if (_subscribed || Main.Instance == null) return;
        _lastState = Main.Instance.gameState;
        Main.Instance.onGameStateChanged += OnGameStateChanged;
        _subscribed = true;
    }

    // Fired synchronously on every gameState assignment - so it catches the one-frame transient
    // states (InGame_BallPastBoundary, InGame_Bowled) that Main immediately overwrites with their
    // ...Loop variants. Act only on the transition edge.
    private void OnGameStateChanged()
    {
        eGameState s = Main.Instance.gameState;
        if (s == _lastState) return;
        _lastState = s;

        switch (s)
        {
            case eGameState.InGame_BallPastBoundary:
            {
                bool bounced = Main.Instance.theBallScript != null &&
                               Main.Instance.theBallScript.bounced;
                if (bounced) Cheer(fourExcitement, wave: true);   // four along the ground
                else         Cheer(sixExcitement, wave: true);    // six on the full
                break;
            }
            case eGameState.InGame_Bowled:
                // A stump was knocked over during play: bowled, or a fielder's throw hitting the
                // stumps (a run-out). Both are wickets (the HUD scores this as "W").
                Cheer(wicketExcitement, wave: false);             // wicket: roar, no wave
                break;

            case eGameState.InGame_BallFielded:
            {
                // A fielder collected the ball. Caught on the full = a wicket (same rule the HUD
                // uses); if it bounced first it's just fielding, so the crowd stays idle.
                bool bounced = Main.Instance.theBallScript != null &&
                               Main.Instance.theBallScript.bounced;
                if (!bounced) Cheer(wicketExcitement, wave: false);   // caught!
                break;
            }
        }
    }

    private void Cheer(float level, bool wave)
    {
        _target = Mathf.Max(_target, level);
        _holdTimer = cheerHold;

        if (wave)
        {
            if (_wave != null) StopCoroutine(_wave);
            _wave = StartCoroutine(WaveSweep());
        }
    }

    private IEnumerator WaveSweep()
    {
        const float twoPi = 6.28318530718f;
        Shader.SetGlobalFloat(IdWaveEnabled, 1f);
        float t = 0f;
        while (t < waveDuration)
        {
            t += Time.deltaTime;
            Shader.SetGlobalFloat(IdWavePhase, (t / waveDuration) * twoPi);
            yield return null;
        }
        Shader.SetGlobalFloat(IdWaveEnabled, 0f);
        _wave = null;
    }

    private void Update()
    {
        EnsureSubscribed();

        // Hold at full cheer, then let the target fall back to idle.
        if (_holdTimer > 0f) _holdTimer -= Time.deltaTime;
        else _target = 0f;

        float speed = (_target > _excitement) ? rampUp : rampDown;
        _excitement = Mathf.MoveTowards(_excitement, _target, speed * Time.deltaTime);

        Shader.SetGlobalFloat(IdExcitement, _excitement);
        Shader.SetGlobalFloat(IdFlash, _excitement);   // harmless in daylight (_FlashStrength gates)

        // The crowd swells with the excitement and settles back with it.
        _audio.volume = Mathf.Lerp(ambienceVolume, cheerVolume, _excitement);
        if (cheerClip != null && !_audio.isPlaying)
            _audio.Play();
    }
}
