using UnityEngine;

/// <summary>
/// The sound of the contact, so the player can hear what they hit without looking.
///
/// Real recordings (Assets/Sounds/Bat/Real/, all CC0 - sources and licenses in SOURCES.md there),
/// in five kinds: middled, good, edge, toe and handle. The contact kinds share them: a thin edge
/// is the edge played quieter and higher, the back of the bat the toe's thud, the shoulder the
/// handle's dead knock. The synthesised set these replace (Tools/synth_bat_sounds.py) never
/// sounded like wood.
///
/// Each kind has a few takes, one picked at random and detuned slightly so no two shots sound
/// identical. Loudness and brightness follow the closing speed along the contact normal - how
/// hard ball and bat actually met - not the exit speed, so a hard swing that only clips an edge
/// is still quiet. Played from one pooled 3D source, the Impact Voice child of the
/// bat, moved to where the ball met the blade for each hit: no objects made per hit
/// (PlayClipAtPoint makes and destroys one each time).
/// </summary>
public class BatSounds : MonoBehaviour
{
    [Tooltip("The Impact Voice child (AudioSource + AudioLowPassFilter), moved to each contact.")]
    [SerializeField] private AudioSource voice;

    [SerializeField] private AudioClip[] middled;
    [SerializeField] private AudioClip[] good;
    [SerializeField] private AudioClip[] edge;
    [SerializeField] private AudioClip[] toe;
    [SerializeField] private AudioClip[] handle;

    /// Closing speed (m/s) at which a contact plays at full volume (a dead-bat block is ~5-10, a
    /// full drive 30+), and how loud the softest one is. Louder and quicker to full volume than
    /// before (45 m/s, 0.25): a firm shot should crack.
    private const float FullVolumeImpact = 35f;
    private const float MinVolume = 0.4f;

    /// Low-pass corner (Hz) for the softest contact and for a full-blooded one: a dead-bat block
    /// is dull as well as quiet, a hard strike is bright.
    private const float SoftCorner = 3500f, HardCorner = 22000f;

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
        Voice(kind, out AudioClip[] takes, out float pitch, out float volume);
        if (takes == null || takes.Length == 0 || source == null)
            return;
        if (source.transform != transform)
            source.transform.position = contactPoint;
        AudioClip clip = takes[Random.Range(0, takes.Length)];
        if (clip == null)
            return;
        float loud = Mathf.Clamp01(impactSpeed / FullVolumeImpact);
        // A harder strike rings a touch higher, as a struck plank does.
        source.pitch = pitch * Random.Range(0.97f, 1.03f) * Mathf.Lerp(0.96f, 1.04f, loud);
        // Corner swept on a log scale, the way the ear hears pitch.
        tone.cutoffFrequency = SoftCorner * Mathf.Pow(HardCorner / SoftCorner, Mathf.Sqrt(loud));
        source.PlayOneShot(clip, volume * Mathf.Lerp(MinVolume, 1f, Mathf.Sqrt(loud)) * Random.Range(0.92f, 1f));
    }

    /// The bat coming down on the ground (Bat's ground check): a deep knock where it touched,
    /// louder the harder it lands. Speed is how fast the bat was coming down, m/s.
    public void PlayGround(Vector3 point, float speed)
    {
        if (ground == null || source == null)
            return;
        float loud = Mathf.Clamp01(speed / GroundFullSpeed);
        if (source.transform != transform)
            source.transform.position = point;
        // One sound, every time: the deep knock, louder (and a touch brighter) the harder it lands.
        source.pitch = 0.85f;
        tone.cutoffFrequency = Mathf.Lerp(900f, 1600f, loud);
        source.PlayOneShot(ground, Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(loud)));
    }

    /// The bat on the ground: the deepest recording in the set (handle_1: spectral centroid 640 Hz,
    /// over half its energy below 400 Hz). Picking among the toe takes, as it did, sometimes played
    /// toe_2 (centroid 4.8 kHz) - a hollow, bright knock.
    [SerializeField] private AudioClip ground;

    /// Coming down this fast (m/s) is as loud as a knock on the ground gets.
    private const float GroundFullSpeed = 4f;

    /// Which recordings a contact uses, and how it is played.
    private void Voice(BatContact.ContactKind kind, out AudioClip[] takes, out float pitch, out float volume)
    {
        pitch = 1f;
        volume = 1f;
        switch (kind)
        {
            case BatContact.ContactKind.Middled: takes = middled; break;
            case BatContact.ContactKind.Good: takes = good; break;
            case BatContact.ContactKind.ThickEdge: takes = edge; break;
            case BatContact.ContactKind.ThinEdge: takes = edge; pitch = 1.15f; volume = 0.55f; break;
            case BatContact.ContactKind.Toe: takes = toe; break;
            case BatContact.ContactKind.Back: takes = toe; pitch = 1.05f; volume = 0.8f; break;
            case BatContact.ContactKind.Shoulder: takes = handle; pitch = 1.08f; break;
            default: takes = handle; break;
        }
    }

#if UNITY_EDITOR
    /// Fill the takes from Assets/Sounds/Bat/Real/<kind>_<n>.wav.
    [ContextMenu("Load takes from Assets/Sounds/Bat/Real")]
    public void LoadTakes()
    {
        middled = Load("middled"); good = Load("good"); edge = Load("edge"); toe = Load("toe"); handle = Load("handle");
        ground = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Bat/Real/handle_1.wav");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static AudioClip[] Load(string kind)
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets(kind + "_ t:AudioClip", new[] { "Assets/Sounds/Bat/Real" }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(kind + "_"))
                clips.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return clips.ToArray();
    }
#endif
}
