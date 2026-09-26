using UnityEngine;

/// <summary>
/// The sound of the contact, so the player can hear what they hit without looking: a middled shot
/// cracks, a good one knocks, a thick edge clacks, a thin edge ticks, the toe thuds, the shoulder
/// and handle go dead and buzz, the back of the bat is a muffled knock.
///
/// Each kind has a few takes (Assets/Sounds/Bat/, made by Tools/synth_bat_sounds.py), one picked at
/// random and detuned slightly so no two shots sound identical. Loudness follows the closing speed
/// along the contact normal - how hard ball and bat actually met - not the exit speed, so a hard
/// swing that only clips an edge is still quiet. Played from one pooled 3D source on the bat: no
/// objects made per hit (PlayClipAtPoint makes and destroys one each time).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BatSounds : MonoBehaviour
{
    [SerializeField] private AudioClip[] middled;
    [SerializeField] private AudioClip[] good;
    [SerializeField] private AudioClip[] thickEdge;
    [SerializeField] private AudioClip[] thinEdge;
    [SerializeField] private AudioClip[] toe;
    [SerializeField] private AudioClip[] shoulder;
    [SerializeField] private AudioClip[] handle;
    [SerializeField] private AudioClip[] back;

    /// Closing speed (m/s) at which a contact plays at full volume; a dead-bat block is ~5-10.
    private const float FullVolumeImpact = 45f;
    private const float MinVolume = 0.25f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.minDistance = 1.5f;
        source.maxDistance = 60f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    /// Played from the bat itself: the source is on the bat, within half a metre of any contact.
    public void Play(BatContact.ContactKind kind, float impactSpeed)
    {
        AudioClip[] takes = Takes(kind);
        if (takes == null || takes.Length == 0 || source == null)
            return;
        AudioClip clip = takes[Random.Range(0, takes.Length)];
        if (clip == null)
            return;
        float loud = Mathf.Clamp01(impactSpeed / FullVolumeImpact);
        // A harder strike rings a touch higher, as a struck plank does.
        source.pitch = Random.Range(0.96f, 1.04f) * Mathf.Lerp(0.95f, 1.05f, loud);
        source.PlayOneShot(clip, Mathf.Lerp(MinVolume, 1f, Mathf.Sqrt(loud)));
    }

    private AudioClip[] Takes(BatContact.ContactKind kind) => kind switch
    {
        BatContact.ContactKind.Middled => middled,
        BatContact.ContactKind.Good => good,
        BatContact.ContactKind.ThickEdge => thickEdge,
        BatContact.ContactKind.ThinEdge => thinEdge,
        BatContact.ContactKind.Toe => toe,
        BatContact.ContactKind.Shoulder => shoulder,
        BatContact.ContactKind.Handle => handle,
        _ => back,
    };

#if UNITY_EDITOR
    /// Fill the takes from Assets/Sounds/Bat/<kind>_<n>.wav.
    [ContextMenu("Load takes from Assets/Sounds/Bat")]
    public void LoadTakes()
    {
        middled = Load("middled"); good = Load("good"); thickEdge = Load("thick_edge"); thinEdge = Load("thin_edge");
        toe = Load("toe"); shoulder = Load("shoulder"); handle = Load("handle"); back = Load("back");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static AudioClip[] Load(string kind)
    {
        var clips = new System.Collections.Generic.List<AudioClip>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets(kind + "_ t:AudioClip", new[] { "Assets/Sounds/Bat" }))
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
