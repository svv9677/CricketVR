using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The match scene dressed as the club ground (phase 6): the same scene and the same game, with the
/// stadium put away and the club's prefab (Resources/Prefabs/ClubGround) put down in its place.
/// Called by StadiumConditions as the match scene loads, when the match is at the club ground.
///
/// All of it is runtime state on the loaded scene; no asset is changed, and the next scene load
/// starts from the stadium as saved. Nothing here runs for a stadium match.
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
/// The replay screen gets ClubReplayScreen: no screen in the sky, the replay comes to the player.
/// </summary>
public static class ClubGroundVenue
{
    private const string ReplayDisplay = "Replay Display";

    /// The club ground in `scene` for a crowd of `crowd`, or null when the prefab is missing or
    /// unusable (logged): the caller then plays the match at the stadium, as it is.
    public static ClubGround Dress(Scene scene, eCrowd crowd)
    {
        ClubGround existing = Object.FindFirstObjectByType<ClubGround>();
        if (existing != null)
            return existing;
        ClubGround club = Place(scene);
        if (club == null)
            return null;
        HideStadium(scene);
        HideDisplays(scene);
        SwitchOffProbe(scene);
        UnbakePitch(scene);
        PrepareStrip(scene);
        CrowdAudio(scene, club, crowd);
        GuardReplayScreen(scene);
        club.SetClubName(VenueRules.ClubName(CareerMatchSession.Active, CareerMatchSession.Plan?.yourTeamName));
        club.ShowCrowd(crowd);
        return club;
    }

    /// The prefab, instantiated into the match scene; null (nothing placed) if it is not there or
    /// has no outfield to play on.
    private static ClubGround Place(Scene scene)
    {
        GameObject prefab = Resources.Load<GameObject>(ClubGround.ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[ClubGround] Resources/{ClubGround.ResourcePath} is missing: the match is played at the stadium.");
            return null;
        }
        if (!prefab.TryGetComponent(out ClubGround template) || template.Outfield == null)
        {
            Debug.LogWarning($"[ClubGround] Resources/{ClubGround.ResourcePath} has no ClubGround with an outfield: the match is played at the stadium.");
            return null;
        }
        GameObject go = Object.Instantiate(prefab);
        go.name = prefab.name;
        if (scene.IsValid() && scene.isLoaded && go.scene != scene)
            SceneManager.MoveGameObjectToScene(go, scene);
        return go.GetComponent<ClubGround>();
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
    private static void PrepareStrip(Scene scene)
    {
        Transform t = Child(scene, "Main", "Pitch/Pitch Base");
        Renderer r = t != null ? t.GetComponent<Renderer>() : null;
        if (!ClubPitchSurface.Apply(r))
            Debug.LogWarning("[ClubGround] Main/Pitch/Pitch Base has no PitchBlend material: the pitch keeps the stadium's edge.");
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
        if (screen != null && screen.GetComponent<ClubReplayScreen>() == null)
            screen.gameObject.AddComponent<ClubReplayScreen>();
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
