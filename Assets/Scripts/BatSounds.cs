using UnityEngine;

/// <summary>
/// The sound of the contact, so the player can hear what they hit without looking: three sounds
/// for the ball, and one for the bat knocking the ground.
///
///   * middled - crisp and resounding: a bright crack, then the bat's own ring (~600 Hz) for
///     about half a second;
///   * good    - dense and flat: a broadband thump with no ring, gone in ~100 ms;
///   * edge    - sharp and quick: a bright tick, ~50 ms. Every mishit uses it (thick and thin
///     edges, the toe, the back, the shoulder and the handle).
///   * ground  - the bat touching down on the pitch: a dull, short knock of wood on hard turf, no
///     ring and nothing bright, so it is never mistaken for a hit (BatPose.CheckGroundTap).
///
/// Each kind has four takes (middledTakes etc., from every clean strike in the recordings, lightly
/// processed); one is picked at random so the same shot is never the identical sample twice.
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
    [SerializeField] private AudioClip ground;

    [Tooltip("Takes of each kind (Tools/bat_sounds/build_variants.py): one is picked at random, never the same twice running, so two drives in a row are not the identical sample. Empty: the single clip above.")]
    [SerializeField] private AudioClip[] middledTakes;
    [SerializeField] private AudioClip[] goodTakes;
    [SerializeField] private AudioClip[] edgeTakes;
    private int lastMiddled = -1, lastGood = -1, lastEdge = -1;

    /// Speed coming down (m/s) at which a knock is at full volume, the softest knock's volume,
    /// and full volume against a hit's (a knock is never as loud as a shot).
    private const float FullVolumeTap = 3f, MinTapVolume = 0.25f, TapVolume = 0.6f;
    /// The knock goes through the same low-pass as the hits: duller for a soft tap.
    private const float SoftTapCorner = 2500f, HardTapCorner = 6000f;

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

    /// The bat knocking the ground at `point`, coming down at `speed` m/s.
    public void PlayGround(Vector3 point, float speed)
    {
        if (ground == null || source == null)
            return;
        if (source.transform != transform)
            source.transform.position = point;
        float hard = Mathf.Clamp01(speed / FullVolumeTap);
        source.pitch = Random.Range(0.95f, 1.05f);
        tone.cutoffFrequency = Mathf.Lerp(SoftTapCorner, HardTapCorner, hard);
        source.PlayOneShot(ground, TapVolume * Mathf.Lerp(MinTapVolume, 1f, Mathf.Sqrt(hard)));
    }

    private AudioClip ClipFor(BatContact.ContactKind kind, out float volume)
    {
        volume = 1f;
        switch (kind)
        {
            case BatContact.ContactKind.Middled: return Pick(middledTakes, ref lastMiddled, middled);
            case BatContact.ContactKind.Good: return Pick(goodTakes, ref lastGood, good);
            case BatContact.ContactKind.ThinEdge: volume = 0.7f; return Pick(edgeTakes, ref lastEdge, edge);   // a snick is lighter than a thick edge
            default: return Pick(edgeTakes, ref lastEdge, edge);       // thick edge, toe, back, shoulder, handle
        }
    }

    /// A random take, not the one played last time; `single` when there are no takes.
    private static AudioClip Pick(AudioClip[] takes, ref int last, AudioClip single)
    {
        if (takes == null || takes.Length == 0)
            return single;
        int i = Random.Range(0, takes.Length);
        if (takes.Length > 1 && i == last)
            i = (i + 1 + Random.Range(0, takes.Length - 1)) % takes.Length;
        last = i;
        return takes[i] != null ? takes[i] : single;
    }
}
