using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The club ground (phase 6): the root of Resources/Prefabs/ClubGround, built by
/// Tools > CricketVR > Build Club Ground and put into the match scene by ClubGroundVenue when the
/// match is at the club. It holds what the match needs to reach in it: the outfield the ball plays
/// on, the spectators for each size of crowd, the ambience, the scoreboard, the name over the
/// pavilion, the pylon heads the night's glows hang on, and what changes under lights.
///
/// Night: the pylons' lamps come on (nightOnly) and everything but the outfield falls away
/// (dimAtNight, each by its nightFactor): the spectators and the horizon are unlit, so the night
/// key and ambient do nothing to them, and the trees and buildings would otherwise be lit as
/// brightly as the field. Dimmed through copies of their materials, not property blocks, so the
/// SRP Batcher keeps batching them; the copies are made once and shared by renderers that share a
/// material, and put back (and destroyed) for the day.
/// </summary>
public class ClubGround : MonoBehaviour
{
    public const string ResourcePath = "Prefabs/ClubGround";

    [Tooltip("The outfield's collider: tag Ground, the Outfield physic material, top at BallFlight.OutfieldY.")]
    [SerializeField] private Collider outfield;
    [Tooltip("The spectators, smallest crowd first: Empty shows the first, Half two, Packed all three.")]
    [SerializeField] private GameObject[] spectatorGroups;
    [Tooltip("The ground's looping ambience (birdsong, breeze), played on the scene's crowd source.")]
    [SerializeField] private AudioClip ambience;
    [SerializeField] private ClubScoreboard scoreboard;
    [Tooltip("The club's name on the pavilion.")]
    [SerializeField] private TMP_Text clubName;
    [Tooltip("Where the floodlight glows hang at night: one per pylon.")]
    [SerializeField] private Transform[] pylonHeads;
    [Tooltip("Shown only at night (the pylons' lamps).")]
    [SerializeField] private GameObject nightOnly;
    [Tooltip("Drawn darker at night, each by the factor at the same index in nightFactor.")]
    [SerializeField] private Renderer[] dimAtNight;
    [SerializeField] private float[] nightFactor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// Each dimmed renderer's own materials, to put back for the day.
    private readonly Dictionary<Renderer, Material[]> dayMaterials = new Dictionary<Renderer, Material[]>();
    /// The night copies, one per material and factor.
    private readonly Dictionary<(Material, float), Material> nightCopies = new Dictionary<(Material, float), Material>();

    public AudioClip Ambience => ambience;
    public Collider Outfield => outfield;
    public ClubScoreboard Scoreboard => scoreboard;

    /// The spectators for `c`: the first VenueRules.SpectatorGroups(c) groups, the rest put away.
    public void ShowCrowd(eCrowd c)
    {
        if (spectatorGroups == null)
            return;
        int shown = VenueRules.SpectatorGroups(c);
        for (int i = 0; i < spectatorGroups.Length; i++)
            if (spectatorGroups[i] != null)
                spectatorGroups[i].SetActive(i < shown);
    }

    /// The pylon heads' positions, for StadiumLighting's glows. Empty (no glows), never null, when
    /// the prefab has none: null would bring the stadium's roof-rim ring, hanging in the open sky.
    public Vector3[] GlowPoints()
    {
        var points = new List<Vector3>();
        if (pylonHeads != null)
            foreach (Transform t in pylonHeads)
                if (t != null)
                    points.Add(t.position);
        return points.ToArray();
    }

    /// The club's name on the pavilion, and across the top of the score hut's board (the home side).
    public void SetClubName(string name)
    {
        if (clubName != null)
            clubName.text = name ?? "";
        if (scoreboard != null)
            scoreboard.SetHomeName(name);
    }

    public void SetNight(bool night)
    {
        if (nightOnly != null)
            nightOnly.SetActive(night);
        if (dimAtNight == null)
            return;
        for (int i = 0; i < dimAtNight.Length; i++)
        {
            Renderer r = dimAtNight[i];
            if (r == null)
                continue;
            if (night)
                Dim(r, nightFactor != null && i < nightFactor.Length ? nightFactor[i] : 1f);
            else if (dayMaterials.TryGetValue(r, out Material[] day))
                r.sharedMaterials = day;
        }
        if (!night)
            dayMaterials.Clear();
    }

    private void Dim(Renderer r, float factor)
    {
        if (dayMaterials.ContainsKey(r))
            return;                       // already dimmed: never twice over
        Material[] day = r.sharedMaterials;
        dayMaterials[r] = day;
        var dark = new Material[day.Length];
        for (int m = 0; m < day.Length; m++)
            dark[m] = NightCopy(day[m], factor);
        r.sharedMaterials = dark;
    }

    private Material NightCopy(Material day, float factor)
    {
        if (day == null || !day.HasProperty(BaseColorId) || Mathf.Approximately(factor, 1f))
            return day;
        if (nightCopies.TryGetValue((day, factor), out Material copy) && copy != null)
            return copy;
        copy = new Material(day) { name = day.name + " (night)" };
        Color c = day.GetColor(BaseColorId);
        copy.SetColor(BaseColorId, new Color(c.r * factor, c.g * factor, c.b * factor, c.a));
        nightCopies[(day, factor)] = copy;
        return copy;
    }

    private void OnDestroy()
    {
        foreach (Material m in nightCopies.Values)
            if (m != null)
                Destroy(m);
        nightCopies.Clear();
    }
}
