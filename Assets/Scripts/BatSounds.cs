using UnityEngine;

/// <summary>
/// The sound of the contact, so the player can hear what they hit without looking: three sounds,
/// nothing else.
///
///   * middled - crisp and resounding: a bright crack, then the bat's own ring (~600 Hz) for
///     about half a second;
///   * good    - dense and flat: a broadband thump with no ring, gone in ~100 ms;
///   * edge    - sharp and quick: a bright tick, ~50 ms. Every mishit uses it (thick and thin
///     edges, the toe, the back, the shoulder and the handle).
///
/// Built from two real cricket recordings (CC0, Assets/Sounds/Bat/SOURCES.md): the crack from a
/// nets session, the wood's body and ring from a willow bat with a real ball. Loudness follows
/// the closing speed along the contact normal - how hard ball and bat actually met - not the exit
/// speed, so a hard swing that only clips an edge is still quiet. Played from one pooled 3D
/// source, the Impact Voice child of the bat, moved to where the ball met the blade for each hit:
/// no objects made per hit (PlayClipAtPoint makes and destroys one each time).
/// </summary>
public class BatSounds : MonoBehaviour
{
    [Tooltip("The Impact Voice child (AudioSource + AudioLowPassFilter), moved to each contact.")]
    [SerializeField] private AudioSource voice;

    [SerializeField] private AudioClip middled;
    [SerializeField] private AudioClip good;
    [SerializeField] private AudioClip edge;

    /// Closing speed (m/s) at which a contact plays at full volume (a dead-bat block is ~5-10, a
    /// full drive 30+), and how loud the softest one is.
    private const float FullVolumeImpact = 35f;
    private const float MinVolume = 0.4f;

    /// Low-pass corner (Hz) for the softest contact and for a full-blooded one. High even for a
    /// soft one: a dull soft shot took the crispness out of the sounds.
    private const float SoftCorner = 9000f, HardCorner = 22000f;

    private AudioSource source;
    private AudioLowPassFilter tone;

    private void Awake()
    {
        source = voice != null ? voice : GetComponent<AudioSource>();
        if (source == null)
        {
            Debug.LogError("BatSounds has no Impact Voice - run Tools > CricketVR > Add Bat Impact Voice.", this);
            enabled = false;
            return;
        }
        GameAudio.Route(source, GameAudio.Channel.Bat);
        tone = source.GetComponent<AudioLowPassFilter>();
        if (tone == null)
            tone = source.gameObject.AddComponent<AudioLowPassFilter>();
        tone.lowpassResonanceQ = 1f;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 1.5f;
        source.maxDistance = 60f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    /// Played from where the ball met the bat. The voice is a child of the bat, so it rides along
    /// with the follow-through for the fraction of a second the sound lasts.
    public void Play(BatContact.ContactKind kind, float impactSpeed, Vector3 contactPoint)
    {
        AudioClip clip = ClipFor(kind, out float volume);
        if (clip == null || source == null)
            return;
        if (source.transform != transform)
            source.transform.position = contactPoint;
        float loud = Mathf.Clamp01(impactSpeed / FullVolumeImpact);
        // The same sound each time apart from a hair of pitch, so it is the same shot you hear.
        source.pitch = Random.Range(0.98f, 1.02f);
        // Corner swept on a log scale, the way the ear hears pitch.
        tone.cutoffFrequency = SoftCorner * Mathf.Pow(HardCorner / SoftCorner, Mathf.Sqrt(loud));
        source.PlayOneShot(clip, volume * Mathf.Lerp(MinVolume, 1f, Mathf.Sqrt(loud)));
    }

    private AudioClip ClipFor(BatContact.ContactKind kind, out float volume)
    {
        volume = 1f;
        switch (kind)
        {
            case BatContact.ContactKind.Middled: return middled;
            case BatContact.ContactKind.Good: return good;
            case BatContact.ContactKind.ThinEdge: volume = 0.7f; return edge;   // a snick is lighter than a thick edge
            default: return edge;       // thick edge, toe, back, shoulder, handle
        }
    }
}
