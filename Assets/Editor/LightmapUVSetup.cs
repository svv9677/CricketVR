using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    public static class LightmapUVSetup
    {
        static readonly string[] StaticModels =
        {
            "Assets/Resources/Models/ModiStadium.fbx",
            "Assets/Resources/Models/Pitch.fbx",
            "Assets/Resources/Models/Stump.fbx",
            "Assets/Resources/Models/BowlingMachine.fbx"
        };

        [MenuItem("CricketVR/Lighting/Enable Lightmap UVs on Static Models")]
        public static void Run()
        {
            var changed = 0;

            foreach (var path in StaticModels)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Debug.LogError($"[LightmapUVSetup] not a model: {path}");
                    continue;
                }

                if (importer.generateSecondaryUV &&
                    Mathf.Approximately(importer.secondaryUVHardAngle, 60f))
                {
                    Debug.Log($"[LightmapUVSetup] already configured: {path}");
                    continue;
                }

                importer.generateSecondaryUV = true;
                importer.secondaryUVHardAngle = 60f;
                importer.secondaryUVPackMargin = 8;
                importer.secondaryUVAngleDistortion = 8f;
                importer.secondaryUVAreaDistortion = 15f;

                importer.SaveAndReimport();
                changed++;
                Debug.Log($"[LightmapUVSetup] configured {path}");
            }

            Debug.Log($"[LightmapUVSetup] done, {changed} model(s) changed");
        }
    }
}
