using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The stadium prefab is marked batching-static, and a static-batched mesh cannot move at runtime -
/// but the ground turns half round every over (EndChange). As the match scene is processed (the
/// build, and entering play mode), its stadium's renderers lose only the batching flag: lightmaps,
/// occlusion and reflection-probe flags stay, and the prefab itself is untouched. They are 17
/// renderers with as many materials, so batching them saved little.
/// </summary>
public class EndChangeUnbatch : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.name != "CricketVR")
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Stadium")
                continue;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                StaticEditorFlags f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                if ((f & StaticEditorFlags.BatchingStatic) != 0)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, f & ~StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
