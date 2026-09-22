using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    public static class StaticFlagSetup
    {
        static readonly string[] StaticPrefabs =
        {
            "Assets/Resources/Prefabs/Stadium.prefab",
            "Assets/Resources/Prefabs/Pitch.prefab"
        };

        const StaticEditorFlags Flags =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        [MenuItem("CricketVR/Lighting/Flag Static Environment Geometry")]
        public static void Run()
        {
            foreach (var path in StaticPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                {
                    Debug.LogError($"[StaticFlagSetup] could not load {path}");
                    continue;
                }

                var count = 0;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, Flags);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    count++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log($"[StaticFlagSetup] flagged {count} renderer(s) in {path}");
            }
        }
    }
}
