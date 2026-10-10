using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A black sightscreen behind the bowler's end, so the (white) ball stands out from release.
///
/// On the pitch's line just past the advertising boards (they run 56-61 m out, 1.4 m high) and in
/// front of the stand (its wall starts at ~65 m). 18 m wide and 8 m high: from the batter's eye the
/// ball at release sits ~3.7 m up on it, and wide or round-the-wicket lines stay inside it. Unlit
/// matte black (no light washes it grey), no shadow, no collider - it never touches the play.
///
/// Built when the match scene loads, not saved in it: saving the scene from here also wrote out a
/// thousand lines of the editor's pending UI-layout overrides.
/// </summary>
public static class SightScreen
{
    private const string MatchScene = "CricketVR";
    private const float DistanceOut = 63f, Width = 18f, Height = 8f, Thickness = 0.3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != MatchScene || GameObject.Find("SightScreen") != null)
            return;
        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "SightScreen";
        Object.Destroy(screen.GetComponent<Collider>());
        SceneManager.MoveGameObjectToScene(screen, scene);
        // Behind the bowler's end: the batter faces -x.
        screen.transform.position = new Vector3(-DistanceOut, BallFlight.OutfieldY + Height * 0.5f, 0f);
        screen.transform.localScale = new Vector3(Thickness, Height, Width);
        MeshRenderer renderer = screen.GetComponent<MeshRenderer>();
        Material material = Resources.Load<Material>("Materials/SightScreen");
        if (material != null)
            renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
}
