using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the career player wears in a scene (phase 7 locker): his bat, gloves and helmet trim, his
/// side's colour (the replay helmet's shell, side-colour items) and his skin (the replay head). Only
/// in a career match (CricketVR with CareerMatchSession.Active) or career nets (Nets with
/// NetsSession.Career); everywhere else there is no look and the game is as it always was.
/// Applying it puts copies of the materials on the renderers, with `_BaseColor` (and for the gloves
/// `_EmissionColor`) set; the shared material assets are never touched, and the copies go when the
/// caller destroys them (CareerCosmetics, with its scene). Looks only - nothing here changes how he bats.
/// </summary>
public sealed class CosmeticLook
{
    /// The hand material's own emission against its base colour (XRHands.mat: 0.09..0.12 over
    /// 0.72..0.86), kept for a tinted glove so it glows no more or less than before.
    public const float GloveEmission = 0.13f;
    // Names, not Shader.PropertyToID: the look is worked out (For, SideColour) with no graphics.
    public const string BaseColor = "_BaseColor", EmissionColor = "_EmissionColor";

    public CosmeticDef bat, gloves, trim;
    public Color side = Color.white, skin = ReplayBatterLook.DefaultSkin;

    /// The look for the scene being loaded, from the career and session now running.
    public static CosmeticLook ForScene(string sceneName) =>
        For(sceneName, CareerSaves.Current, CareerMatchSession.Active, CareerMatchSession.Plan, NetsSession.Career);

    /// The look for `sceneName` with these facts (null when the scene is not a career one).
    public static CosmeticLook For(string sceneName, CareerProfile p, bool careerMatch, MatchPlan plan, bool careerNets)
    {
        bool match = sceneName == CareerMatchSession.MatchScene && careerMatch;
        bool nets = sceneName == NetsSession.NetsScene && careerNets;
        if (p == null || (!match && !nets))
            return null;
        int tone = p.look != null ? p.look.skin : 0;
        return new CosmeticLook
        {
            bat = Cosmetics.Equipped(p, eCosmeticSlot.Bat),
            gloves = Cosmetics.Equipped(p, eCosmeticSlot.Gloves),
            trim = Cosmetics.Equipped(p, eCosmeticSlot.Trim),
            side = SideColour(p, match ? plan : null),
            skin = Appearance.SkinTones[Mathf.Clamp(tone, 0, Appearance.SkinTones.Length - 1)],
        };
    }

    /// His side's kit colour: the match's side (the plan's), else the season's, else his own kit.
    /// (PartnerBatter.KitColour picks the partner's the same way; that one is private to it.)
    public static Color SideColour(CareerProfile p, MatchPlan plan)
    {
        Season s = p?.season;
        string id = plan != null ? plan.yourTeam : s?.yourTeam;
        Team yours = s != null && !string.IsNullOrEmpty(id) ? s.Team(id) : null;
        int kit = yours != null ? yours.kit : p?.look != null ? p.look.kit : 0;
        return Appearance.KitColours[Mathf.Clamp(kit, 0, Appearance.KitColours.Length - 1)];
    }

    /// An item's one colour (side colour resolved); null for the default, which changes nothing.
    public Color? ColourOf(CosmeticDef d) =>
        d == null || d.IsDefault ? (Color?)null : d.sideColour ? side : d.colour;

    public Color? GloveColour => ColourOf(gloves);
    public Color? TrimColour => ColourOf(trim);

    /// The bat in its finish: a copy of each of its BatSubmesh.Count materials with the finish's
    /// colour (a Keep part keeps the original material). Nothing for the default or for a renderer
    /// that is not the bat model (another material count). Returns the copies made.
    public List<Material> ApplyBat(MeshRenderer r)
    {
        var made = new List<Material>();
        Color[] parts = bat != null && !bat.IsDefault ? bat.bat : null;
        if (r == null || parts == null || parts.Length != BatSubmesh.Count)
            return made;
        Material[] shared = r.sharedMaterials;
        if (shared.Length != BatSubmesh.Count)
        {
            Debug.LogWarning($"[Locker] the bat has {shared.Length} materials, not {BatSubmesh.Count}: its finish is not shown");
            return made;
        }
        var dressed = (Material[])shared.Clone();
        for (int i = 0; i < BatSubmesh.Count; i++)
        {
            if (shared[i] == null || parts[i] == CosmeticCatalog.Keep)
                continue;
            dressed[i] = Tinted(shared[i], parts[i], null);
            made.Add(dressed[i]);
        }
        r.sharedMaterials = dressed;
        return made;
    }

    /// The hands in the gloves' colour: `_BaseColor`, and `_EmissionColor` at GloveEmission of it.
    /// One copy per material however many renderers share it. Returns the copies made.
    public List<Material> ApplyGloves(IEnumerable<Renderer> renderers)
    {
        var made = new List<Material>();
        Color? c = GloveColour;
        if (renderers == null || c == null)
            return made;
        var copies = new Dictionary<Material, Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null)
                continue;
            Material[] shared = r.sharedMaterials;
            var dressed = (Material[])shared.Clone();
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null || !shared[i].HasProperty(BaseColor))
                    continue;
                if (!copies.TryGetValue(shared[i], out Material copy))
                {
                    copy = Tinted(shared[i], c.Value, c.Value * GloveEmission);
                    copies[shared[i]] = copy;
                    made.Add(copy);
                }
                dressed[i] = copy;
            }
            r.sharedMaterials = dressed;
        }
        return made;
    }

    /// A copy of `m` in `colour` (and `emission` when given).
    public static Material Tinted(Material m, Color colour, Color? emission)
    {
        var copy = new Material(m) { name = m.name + " (Locker)" };
        copy.SetColor(BaseColor, colour);
        if (emission != null && copy.HasProperty(EmissionColor))
            copy.SetColor(EmissionColor, emission.Value);
        return copy;
    }

    /// Destroy material copies this made (now in play, at once in the editor).
    public static void Destroy(List<Material> made)
    {
        if (made == null)
            return;
        foreach (Material m in made)
        {
            if (m == null)
                continue;
            if (Application.isPlaying)
                Object.Destroy(m);
            else
                Object.DestroyImmediate(m);
        }
        made.Clear();
    }
}
