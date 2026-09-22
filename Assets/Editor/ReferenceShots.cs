using System.IO;
using UnityEditor;
using UnityEngine;

namespace CricketVR.EditorTools
{
    /// <summary>
    /// Renders three fixed viewpoints to PNG for before/after comparison across the scene
    /// realism phases.
    ///
    /// The positions below were calibrated against the real scene on 2026-09-21 (pitch runs
    /// along X: bowler at x=-9, batting stumps at x=+10.3, pitch centre at the origin).
    /// THEY MUST NOT CHANGE once the phase-0 set is captured - if the framing moves, every
    /// later comparison is meaningless.
    /// </summary>
    public static class ReferenceShots
    {
        const int Width = 1920;
        const int Height = 1080;
        const string OutDir = "Docs/Baseline";

        struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Vector3 LookAt;
            public float Fov;
        }

        static readonly Shot[] Shots =
        {
            // Striker's end, 1.2 m behind the stumps, eye height, looking down the pitch at
            // the bowler. This is as close as a flat render gets to the player's actual view.
            new Shot
            {
                Name = "batsman",
                Position = new Vector3(11.5f, 1.7f, 0f),
                LookAt = new Vector3(-9f, 1.2f, 0f),
                Fov = 90f      // approximates the Quest 3 horizontal FOV
            },

            // Beside the pitch looking along it. Frames the pitch surface, creases and stumps:
            // the view for judging turf and pitch materials.
            new Shot
            {
                Name = "midpitch",
                Position = new Vector3(1f, 1.3f, 4f),
                LookAt = new Vector3(6f, 0.3f, 0f),
                Fov = 70f
            },

            // Elevated boundary view of the whole ground. The view for judging stands,
            // crowd and sky.
            new Shot
            {
                Name = "boundary",
                Position = new Vector3(-55f, 9f, 22f),
                LookAt = new Vector3(5f, 1f, 0f),
                Fov = 65f
            }
        };

        [MenuItem("CricketVR/Capture Reference Shots")]
        public static void Capture()
        {
            var choice = EditorUtility.DisplayDialogComplex(
                "Reference Shots",
                "Tag these captures as the phase-0 control set, or as the current phase?",
                "phase0", "current", "cancel");
            if (choice == 2) return;
            var tag = choice == 0 ? "phase0" : "current";

            CaptureAll(tag);
        }

        /// <summary>Capture all three shots with the given filename tag.</summary>
        public static void CaptureAll(string tag)
        {
            Directory.CreateDirectory(OutDir);

            var go = new GameObject("__RefCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2000f;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            foreach (var shot in Shots)
            {
                go.transform.position = shot.Position;
                go.transform.rotation = Quaternion.LookRotation(
                    (shot.LookAt - shot.Position).normalized, Vector3.up);
                cam.fieldOfView = shot.Fov;

                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                var path = Path.Combine(OutDir, $"{tag}-{shot.Name}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[ReferenceShots] wrote {path}");
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            AssetDatabase.Refresh();
        }
    }
}
