using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the pitch's ready-made surfaces and places them under the match scene's Systems (object
/// "Pitch Surfaces", component PitchSurfaces). Until 2026-11-15 the game made a PhysicsMaterial and
/// a tinted copy of the pitch material in code on every match load (StadiumConditions).
///
/// Per kind of pitch (Flat, Green, Dusty), saved under Assets/Art/Generated/PitchSurfaces:
///   * a PhysicsMaterial: the pitch collider's own friction and combine modes, with the bounciness
///     that gives the pitch's restitution against the ball (PitchSurfaces.BouncinessFor);
///   * the pitch material: Flat is the scene's own, Green and Dusty its _BaseColor times the tint;
///   * the club ground's version of that material: its edge settings from ClubPitchSurface, worked
///     out from the pitch quad's size here instead of when a club match loads.
/// The pitch in the scene is left as it is (Flat); the game switches at load. Run again to update.
/// </summary>
public static class PitchSurfacesBuilder
{
    private const string Folder = SceneObjectAssets.GeneratedFolder + "/PitchSurfaces";
    private const string PitchPath = "Pitch/Pitch Base";

    [MenuItem("CricketVR/Scene Objects/Build Pitch Surfaces")]
    public static void Build()
    {
        Scene scene = SceneObjectAssets.Open(StadiumBuilderUtil.MatchScene);
        Transform pitch = StadiumBuilderUtil.Find(scene, "Main", PitchPath);
        if (pitch == null || !pitch.TryGetComponent(out Collider collider) || !pitch.TryGetComponent(out Renderer renderer))
        {
            Debug.LogError("[PitchSurfacesBuilder] Main/" + PitchPath + " (with a collider and a renderer) is not in " + StadiumBuilderUtil.MatchScene);
            return;
        }
        PhysicsMaterial original = collider.sharedMaterial;
        Material flat = renderer.sharedMaterial;
        if (original == null || flat == null)
        {
            Debug.LogError("[PitchSurfacesBuilder] The pitch has no physics material or no material to start from.");
            return;
        }
        // The scene's Pitch Base is only ever saved as Flat, so its material is the original.
        var physics = new Object[3];
        var surfaces = new Object[3];
        var club = new Object[3];
        for (int i = 0; i < 3; i++)
        {
            var kind = (ePitch)i;
            StadiumConditions.Pitch p = StadiumConditions.For(kind);
            physics[i] = PhysicsFor(original, kind, p);
            surfaces[i] = Surface(flat, kind, p);
            club[i] = ClubSurface((Material)surfaces[i], renderer, kind);
        }
        GameObject go = StadiumBuilderUtil.SystemsChild(scene, "Pitch Surfaces");
        PitchSurfaces surfacesComponent = StadiumBuilderUtil.Ensure<PitchSurfaces>(go);
        StadiumBuilderUtil.SetRef(surfacesComponent, "pitchCollider", collider);
        StadiumBuilderUtil.SetRef(surfacesComponent, "pitchRenderer", renderer);
        StadiumBuilderUtil.SetRefs(surfacesComponent, "physics", physics);
        StadiumBuilderUtil.SetRefs(surfacesComponent, "surfaces", surfaces);
        StadiumBuilderUtil.SetRefs(surfacesComponent, "clubSurfaces", club);
        SceneObjectAssets.Save(scene);
        Debug.Log("[PitchSurfacesBuilder] Pitch Surfaces placed under Systems in " + StadiumBuilderUtil.MatchScene);
    }

    private static PhysicsMaterial PhysicsFor(PhysicsMaterial original, ePitch kind, StadiumConditions.Pitch p)
    {
        var m = new PhysicsMaterial(original.name + " " + kind)
        {
            bounciness = PitchSurfaces.BouncinessFor(p.restitution),
            dynamicFriction = original.dynamicFriction,
            staticFriction = original.staticFriction,
            bounceCombine = original.bounceCombine,
            frictionCombine = original.frictionCombine,
        };
        return SceneObjectAssets.Save(m, Folder + "/Pitch Physics " + kind + ".asset");
    }

    /// Flat is the scene's own material; the others are tinted copies (only if it has _BaseColor,
    /// as at runtime before).
    private static Material Surface(Material flat, ePitch kind, StadiumConditions.Pitch p)
    {
        if (p.tint == Color.white || !flat.HasProperty("_BaseColor"))
            return flat;
        var m = new Material(flat) { name = flat.name + " " + kind };
        m.SetColor("_BaseColor", flat.GetColor("_BaseColor") * p.tint);
        return SceneObjectAssets.SaveMaterial(m, Folder + "/Pitch " + kind + ".mat");
    }

    /// `surface` with the club's prepared strip (ClubPitchSurface.Apply, worked out here); null if
    /// the material has no PitchBlend edge settings (the runtime then logs it and leaves the pitch).
    private static Material ClubSurface(Material surface, Renderer renderer, ePitch kind)
    {
        Vector3 half = renderer.bounds.extents;
        if (!surface.HasProperty(ClubPitchSurface.InsetY) || half.x <= 0f || half.z <= 0f)
        {
            Debug.LogWarning("[PitchSurfacesBuilder] " + surface.name + " has no PitchBlend edge settings: no club strip for " + kind + ".");
            return null;
        }
        ClubPitchSurface.Settings s = ClubPitchSurface.For(half.z, half.x);
        var m = new Material(surface) { name = surface.name + " Club " + kind };
        m.SetFloat(ClubPitchSurface.InsetY, s.insetY);
        m.SetFloat(ClubPitchSurface.FadeWidthY, s.fadeY);
        m.SetFloat(ClubPitchSurface.FadeWidthX, s.fadeX);
        m.SetFloat(ClubPitchSurface.EdgeNoise, s.edgeNoise);
        return SceneObjectAssets.SaveMaterial(m, Folder + "/Pitch Club " + kind + ".mat");
    }
}
