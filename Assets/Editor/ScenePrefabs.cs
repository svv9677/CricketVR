using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The match scene's building blocks as prefabs, so every scene that uses one shares it: change the
/// prefab once and the match scene and the nets both have it.
///
/// Tools/CricketVR/Make Scene Prefabs saves each root of CricketVR.unity that is not a prefab
/// already - the lights, the gameplay set (Main: pitch, stumps, bat, ball, bowler, fielders,
/// keeper, the displays), the XR rig (Controllers), the floating UI, the replay, the fader, the
/// post-processing and the reflection probe, the crowd - as Assets/Prefabs/Scene/&lt;root&gt;.prefab
/// and connects the scene's object to it. The Stadium (its model) and UIHelpers are prefab
/// instances already and stay as they are. Run again, it only saves roots that are still plain.
///
/// What a prefab asset cannot hold - a reference from one root to an object in another (Main to
/// the rig's camera, say) - stays on the scene's instance as an override, exactly as it was.
/// NetsSceneBuilder makes the nets by copying this scene, so the nets get instances of the same
/// prefabs, with its own changes (no crowd, no fielders, no scoreboard) as overrides.
/// </summary>
public static class ScenePrefabs
{
    private const string MatchPath = "Assets/Scenes/CricketVR.unity";
    public const string Folder = "Assets/Prefabs/Scene";

    [MenuItem("Tools/CricketVR/Make Scene Prefabs")]
    public static void Make()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[ScenePrefabs] Leave Play mode first.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var scene = EditorSceneManager.OpenScene(MatchPath, OpenSceneMode.Single);
        Directory.CreateDirectory(Folder);
        int made = 0, kept = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(root))
            {
                kept++;
                continue;
            }
            string path = $"{Folder}/{root.name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction, out bool ok);
            if (!ok || saved == null)
            {
                Debug.LogError($"[ScenePrefabs] Could not save {root.name} as {path}.");
                continue;
            }
            made++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ScenePrefabs] {made} roots saved as prefabs in {Folder}, {kept} already prefabs.");
    }
}
