using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Dumps editor-side scene statistics for phase-to-phase comparison during the scene
    /// realism work. These are NOT a substitute for on-device measurement - they cannot see
    /// tile-GPU cost, bandwidth or stereo overhead. They catch gross regressions only.
    /// </summary>
    public static class SceneStats
    {
        const string OutDir = "Docs/Baseline";

        [MenuItem("CricketVR/Dump Scene Stats")]
        public static void Dump()
        {
            var sb = new StringBuilder();

            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            var skinned = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);

            long tris = 0;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    tris += mf.sharedMesh.triangles.Length / 3;
                }
            }

            var materials = renderers
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Distinct()
                .ToList();

            var withNormal = materials.Count(m =>
                m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null);

            var staticGI = renderers.Count(r =>
                (GameObjectUtility.GetStaticEditorFlags(r.gameObject) &
                 StaticEditorFlags.ContributeGI) != 0);

            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

            sb.AppendLine($"Render pipeline:       {(pipeline == null ? "Built-in" : pipeline.GetType().Name)}");
            sb.AppendLine($"MeshRenderers:         {renderers.Length}");
            sb.AppendLine($"SkinnedMeshRenderers:  {skinned.Length}");
            sb.AppendLine($"Triangles (sum):       {tris}");
            sb.AppendLine($"Unique materials:      {materials.Count}");
            sb.AppendLine($"Materials w/ normal:   {withNormal}");
            sb.AppendLine($"Lights:                {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Reflection probes:     {Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Light probe groups:    {Object.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Lightmaps:             {LightmapSettings.lightmaps.Length}");
            sb.AppendLine($"Static (ContributeGI): {staticGI}");
            sb.AppendLine($"Ambient mode:          {RenderSettings.ambientMode}");
            sb.AppendLine($"Skybox material:       {(RenderSettings.skybox == null ? "<NONE>" : RenderSettings.skybox.name)}");
            sb.AppendLine($"Sun assigned:          {(RenderSettings.sun == null ? "no" : RenderSettings.sun.name)}");
            sb.AppendLine($"Fog:                   {RenderSettings.fog}");

            Directory.CreateDirectory(OutDir);
            var path = Path.Combine(OutDir, "stats-current.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SceneStats] wrote {path}\n{sb}");
        }
    }
}
