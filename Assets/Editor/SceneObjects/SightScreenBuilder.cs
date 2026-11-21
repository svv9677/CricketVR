using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A black sightscreen behind the bowler's end, so the (white) ball stands out from release. It is
/// the root object "SightScreen" in CricketVR.unity (TestLook finds it by that name and dresses it
/// for a Test); this command places it, or puts it back to these measurements. Until 2026-11-15 the
/// game built it in code as the match scene loaded (SightScreen.cs).
///
/// On the pitch's line just past the advertising boards (they run 56-61 m out, 1.4 m high) and in
/// front of the stand (its wall starts at ~65 m). 18 m wide and 8 m high: from the batter's eye the
/// ball at release sits ~3.7 m up on it, and wide or round-the-wicket lines stay inside it. Unlit
/// matte black (no light washes it grey), no shadow, no collider - it never touches the play.
/// ClubFence's frame stands just behind it (ScreenBackX).
/// </summary>
public static class SightScreenBuilder
{
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";
    public const string ObjectName = "SightScreen";
    public const float DistanceOut = 63f, Width = 18f, Height = 8f, Thickness = 0.3f;

    [MenuItem("CricketVR/Scene Objects/Place Sight Screen")]
    public static void Place()
    {
        Scene scene = SceneObjectAssets.Open(MatchScene);
        GameObject screen = SceneObjectAssets.Root(scene, ObjectName);
        if (screen == null)
        {
            screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = ObjectName;
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            SceneManager.MoveGameObjectToScene(screen, scene);
        }
        // Behind the bowler's end: the batter faces -x.
        screen.transform.SetPositionAndRotation(new Vector3(-DistanceOut, BallFlight.OutfieldY + Height * 0.5f, 0f),
                                                Quaternion.identity);
        screen.transform.localScale = new Vector3(Thickness, Height, Width);
        var renderer = screen.GetComponent<MeshRenderer>();
        Material material = Resources.Load<Material>("Materials/SightScreen");
        if (material != null)
            renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        SceneObjectAssets.Save(scene);
        Debug.Log($"[SightScreenBuilder] {ObjectName} placed in {MatchScene}");
    }
}
