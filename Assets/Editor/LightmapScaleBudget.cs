using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    public static class LightmapScaleBudget
    {
        static readonly (float MaxDistance, float Scale)[] Bands =
        {
            (12f,   1.0f),
            (35f,   0.5f),
            (75f,   0.25f),
            (float.MaxValue, 0.1f)
        };

        [MenuItem("CricketVR/Lighting/Apply Lightmap Scale Budget")]
        public static void Run()
        {
            var pitch = GameObject.Find("Pitch");
            if (pitch == null)
            {
                Debug.LogError("[LightmapScaleBudget] no GameObject named 'Pitch' in the open scene");
                return;
            }

            var origin = pitch.transform.position;
            var applied = 0;

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0) continue;

                var d = Vector3.Distance(r.bounds.center, origin);
                var scale = 0.1f;
                foreach (var band in Bands)
                {
                    if (d <= band.MaxDistance) { scale = band.Scale; break; }
                }

                var so = new SerializedObject(r);
                so.FindProperty("m_ScaleInLightmap").floatValue = scale;
                so.ApplyModifiedProperties();
                applied++;
            }

            Debug.Log($"[LightmapScaleBudget] applied to {applied} renderer(s)");
        }
    }
}
