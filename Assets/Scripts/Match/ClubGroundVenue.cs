using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The match scene dressed as the club ground (phase 6): the same scene and the same game, with the
/// stadium put away and the club's prefab (Resources/Prefabs/ClubGround) put down in its place.
/// Called by StadiumConditions as the match scene loads, when the match is at the club ground.
///
/// All of it is runtime state on the loaded scene; no asset is changed, and the next scene load
/// starts from the stadium as saved. Nothing here runs for a stadium match.
/// (Only the placement and the strip's material differ from before: both are saved assets now.)
///
/// What is put away, and why each is not simply deleted:
///   * Stadium (the root): its stands, roof, poles, boards and its outfield. The club's outfield
///     (tag Ground, the Outfield physic material, its top at BallFlight.OutfieldY) is in the scene
///     before the stadium goes, so the ball never has a frame with no ground under it.
///   * Crowd/CrowdLower and CrowdUpper: the stands' spectators. The Crowd root stays: its
///     CrowdController and AudioSource play the club's ambience instead (public fields only; Rao's
///     code is not changed).
///   * Main/Displays (all but the Replay Display): drawn off (forceRenderingOff, so nothing can turn
///     them back on), their canvases off and their solid colliders off (the scorecard's floats 28 m
///     up). The objects stay active: HUD keeps the score on its canvas's GameObject, and
///     BoundaryCollider reports sixes to ShotDistance on the Distance Display.
///   * ReflectionProbe_Pitch: its baked cubemap is of the stands. Off, reflections come from the sky.
///   * Main/Pitch leaves its lightmaps: they were baked with the stands blocking the sky, and the
///     club's outfield is lit in realtime, so the pitch's edge would show as a seam.
///   * Main/Pitch/Pitch Base gets the club's edge (ClubPitchSurface): a neat 3.05 m strip on its
///     edge lines instead of the stadium's metre-wide frayed fade, which at the club fell on grass.
/// The replay screen's ClubReplayScreen (placed switched off) is switched on: no screen in the sky,
/// the replay comes to the player.
///
/// The club ground itself is in the scene already, switched off (CricketVR > Scene Objects > Place
/// Club Ground); Dress switches it on. (Until 2026-11-15 it was instantiated from Resources here.)
///</summary>
public static class ClubGroundVenue
{
    private const string ReplayDisplay = "Replay Display";

    /// The club ground in `scene` for a crowd of `crowd`, or null when the prefab is missing or
    /// unusable (logged): the caller then plays the match at the stadium, as it is.
    public static ClubGround Dress(Scene scene, eCrowd crowd)
    {
        ClubGround existing = Object.FindFirstObjectByType<ClubGround>();   // active ones only: already switched on
        if (existing != null)
            return existing;
        ClubGround club = SwitchOn();
        if (club == null)
            return null;
        HideStadium(scene);
        HideDisplays(scene);
        SwitchOffProbe(scene);
        UnbakePitch(scene);
        PrepareStrip();
        CrowdAudio(scene, club, crowd);
        GuardReplayScreen(scene);
        club.SetClubName(VenueRules.ClubName(CareerMatchSession.Active, CareerMatchSession.Plan?.yourTeamName));
        club.ShowCrowd(crowd);
        return club;
    }

    /// The club ground placed in the match scene (switched off, by CricketVR > Scene Objects >
    /// Place Club Ground), switched on; null (nothing changed) if it is not there or has no outfield
    /// to play on.
    private static ClubGround SwitchOn()
    {
        ClubGround club = Object.FindFirstObjectByType<ClubGround>(FindObjectsInactive.Include);
        if (club == null)
        {
            Debug.LogWarning("[ClubGround] The match scene has no ClubGround (run CricketVR > Scene Objects > Place Club Ground): the match is played at the stadium.");
            return null;
        }
        if (club.Outfield == null)
        {
            Debug.LogWarning("[ClubGround] The ClubGround in the scene has no outfield: the match is played at the stadium.");
            return null;
        }
        club.gameObject.SetActive(true);
        return club;
    }

    private static void HideStadium(Scene scene)
    {
        GameObject stadium = Root(scene, "Stadium");
        if (stadium != null)
            stadium.SetActive(false);
        GameObject crowd = Root(scene, "Crowd");
        if (crowd == null)
            return;
        foreach (string stand in new[] { "CrowdLower", "CrowdUpper" })
        {
            Transform t = crowd.transform.Find(stand);
            if (t != null)
                t.gameObject.SetActive(false);
        }
    }

    private static void HideDisplays(Scene scene)
    {
        Transform displays = Child(scene, "Main", "Displays");
        if (displays == null)
            return;
        foreach (Transform display in displays)
        {
            if (display.name == ReplayDisplay)
                continue;
            foreach (Renderer r in display.GetComponentsInChildren<Renderer>(true))
                r.forceRenderingOff = true;
            foreach (Canvas c in display.GetComponentsInChildren<Canvas>(true))
                c.enabled = false;
            foreach (Collider c in display.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger)
                    c.enabled = false;
        }
    }

    private static void SwitchOffProbe(Scene scene)
    {
        GameObject probe = Root(scene, "ReflectionProbe_Pitch");
        if (probe != null && probe.TryGetComponent(out ReflectionProbe p))
            p.enabled = false;
    }

    /// Safe with StadiumLighting's night: NightLightmaps saves only renderers still on a lightmap,
    /// so these are never "restored" onto the stadium's bake.
    private static void UnbakePitch(Scene scene)
    {
        Transform pitch = Child(scene, "Main", "Pitch");
        if (pitch == null)
            return;
        foreach (MeshRenderer r in pitch.GetComponentsInChildren<MeshRenderer>(true))
            r.lightmapIndex = -1;
    }

    /// The club's neat strip on the pitch quad (ClubPitchSurface); logged if the quad or its blend
    /// material is not there, and the pitch is then left as the stadium has it.
    private static void PrepareStrip()
    {
        PitchSurfaces surfaces = Object.FindFirstObjectByType<PitchSurfaces>();
        if (surfaces == null || !surfaces.ShowClubStrip(MatchSetup.Pitch))
            Debug.LogWarning("[ClubGround] No club strip for the pitch (PitchSurfaces, or its PitchBlend material, is missing): the pitch keeps the stadium's edge.");
    }

    /// The club's birdsong and breeze on the crowd's looping source, at the club's level, with no
    /// swell to speak of on a boundary (VenueRules.ClubCrowd). CrowdController has already started
    /// the stadium's clip (its Awake): the clip is swapped and played again here. Without an
    /// ambience clip in the prefab the crowd source is muted (both volumes 0, which
    /// CrowdController lerps between every frame): a silent ground beats a stadium roar at a club.
    private static void CrowdAudio(Scene scene, ClubGround club, eCrowd crowd)
    {
        GameObject root = Root(scene, "Crowd");
        if (root == null || !root.TryGetComponent(out CrowdController controller))
            return;
        if (club.Ambience == null)
        {
            Debug.LogWarning("[ClubGround] The prefab has no ambience clip; the crowd source is muted.");
            controller.ambienceVolume = controller.cheerVolume = 0f;
            return;
        }
        VenueRules.CrowdAudio level = VenueRules.ClubCrowd(crowd);
        controller.ambienceVolume = level.ambience;
        controller.cheerVolume = level.cheer;
        controller.cheerClip = club.Ambience;
        if (root.TryGetComponent(out AudioSource source))
        {
            source.clip = club.Ambience;
            source.loop = true;
            source.volume = level.ambience;
            source.Play();
        }
    }

    private static void GuardReplayScreen(Scene scene)
    {
        Transform displays = Child(scene, "Main", "Displays");
        Transform screen = displays != null ? displays.Find(ReplayDisplay) : null;
        ClubReplayScreen guard = screen != null ? screen.GetComponent<ClubReplayScreen>() : null;
        if (guard != null)
            guard.enabled = true;       // placed switched off, by Place Club Ground
        else if (screen != null)
            Debug.LogWarning("[ClubGround] The Replay Display has no ClubReplayScreen (run CricketVR > Scene Objects > Place Club Ground): its screen hangs in the sky.");
    }

    /// A root object of the loaded scene by name (active or not: GameObject.Find sees only active
    /// ones, and would also find anything else of that name).
    private static GameObject Root(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;
        foreach (GameObject g in scene.GetRootGameObjects())
            if (g.name == name)
                return g;
        return null;
    }

    private static Transform Child(Scene scene, string root, string path)
    {
        GameObject r = Root(scene, root);
        return r != null ? r.transform.Find(path) : null;
    }
}
