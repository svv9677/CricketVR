using UnityEngine;

/// <summary>
/// The pitch's ready-made surfaces, one per kind of pitch (ePitch: Flat, Green, Dusty), and the
/// switch between them. Built by CricketVR > Scene Objects > Build Pitch Surfaces and placed under
/// the match scene's Systems; StadiumConditions calls Apply as the scene loads. (Until 2026-11-15
/// the game made a new PhysicsMaterial and a tinted copy of the pitch material in code on every
/// match load.)
///
///   * Physics: the pitch collider's PhysicsMaterial per kind. Its bounciness is PhysX's half of
///     the restitution the pitch is meant to have (averaged with the ball's own 0.5), so the
///     fielders and the aim (BallFlight) agree with what the ball does.
///   * Surface: the pitch's material per kind (Flat is the scene's own; Green and Dusty are
///     tinted copies of it).
///   * Club strip: the same surfaces with the club ground's neat prepared edge (ClubPitchSurface),
///     put on instead of the surface when the match is at the club.
///
/// All arrays are indexed by (int)ePitch.
/// </summary>
public class PitchSurfaces : MonoBehaviour
{
    /// The ball's own bounciness, which PhysX averages with the pitch's.
    public const float BallBounciness = 0.5f;

    [SerializeField] private Collider pitchCollider;
    [SerializeField] private Renderer pitchRenderer;
    [Tooltip("The pitch collider's physics material for each kind of pitch (index = ePitch).")]
    [SerializeField] private PhysicsMaterial[] physics = new PhysicsMaterial[0];
    [Tooltip("The pitch's material for each kind of pitch (index = ePitch).")]
    [SerializeField] private Material[] surfaces = new Material[0];
    [Tooltip("The surface with the club ground's prepared strip, for each kind of pitch (index = ePitch).")]
    [SerializeField] private Material[] clubSurfaces = new Material[0];

    /// The PhysicsMaterial bounciness that gives a pitch of `restitution` against the ball.
    public static float BouncinessFor(float restitution) => Mathf.Clamp01(2f * restitution - BallBounciness);

    /// Puts the physics and the surface of `pitch` on the pitch.
    public void Apply(ePitch pitch)
    {
        int i = (int)pitch;
        if (pitchCollider != null && At(physics, i) != null)
            pitchCollider.sharedMaterial = physics[i];
        if (pitchRenderer != null && At(surfaces, i) != null)
            pitchRenderer.sharedMaterial = surfaces[i];
    }

    /// The club's prepared strip on the pitch, for a pitch of `pitch`. False, and nothing changed,
    /// if there is no such surface (its material has no edge settings: see the builder).
    public bool ShowClubStrip(ePitch pitch)
    {
        Material strip = At(clubSurfaces, (int)pitch);
        if (pitchRenderer == null || strip == null)
            return false;
        pitchRenderer.sharedMaterial = strip;
        return true;
    }

    private static T At<T>(T[] list, int i) where T : Object => list != null && i >= 0 && i < list.Length ? list[i] : null;
}
