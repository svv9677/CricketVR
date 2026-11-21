using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CricketVR > Scene Objects > Place Replay Overlay. Makes the broadcast graphics over the replay
/// (the "REPLAY" tag and the score bar) as the prefab Assets/Prefabs/Scene/ReplayOverlay.prefab,
/// and places it under the replay camera (the object with CameraReplay) in Main.prefab, with the
/// canvas pointed at that camera and CameraReplay's `overlay` field set. Until 2026-11-15
/// ReplayOverlay built all of this in code when the replay started.
/// Run it again to rebuild: the prefab is rewritten in place and the placed copy is replaced.
/// </summary>
public static class ReplayOverlayBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Scene/ReplayOverlay.prefab";
    private const string MainPrefab = "Assets/Prefabs/Scene/Main.prefab";
    private const string RootName = "Replay Overlay";
    private static readonly Color Bar = new Color(0.04f, 0.06f, 0.12f, 0.88f);
    private static readonly Color Accent = new Color(0.86f, 0.12f, 0.2f, 0.95f);

    [MenuItem("CricketVR/Scene Objects/Place Replay Overlay")]
    public static void Place()
    {
        GameObject prefab = SavePrefab();
        GameObject main = PrefabUtility.LoadPrefabContents(MainPrefab);
        try
        {
            var replay = main.GetComponentInChildren<CameraReplay>(true);
            var camera = replay != null ? replay.GetComponent<Camera>() : null;
            if (camera == null)
            {
                Debug.LogError("[ReplayOverlayBuilder] No CameraReplay with a Camera in " + MainPrefab);
                return;
            }
            foreach (Transform old in replay.GetComponentsInChildren<Transform>(true))
                if (old != replay.transform && old.name == RootName)
                {
                    Object.DestroyImmediate(old.gameObject);
                    break;
                }
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, replay.transform);
            var canvas = placed.GetComponent<Canvas>();
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
            var so = new SerializedObject(replay);
            so.FindProperty("overlay").objectReferenceValue = placed.GetComponent<ReplayOverlay>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(main, MainPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(main);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ReplayOverlayBuilder] Replay overlay prefab saved and placed under the replay camera in Main.prefab.");
    }

    private static GameObject SavePrefab()
    {
        SceneObjectAssets.EnsureFolder("Assets/Prefabs/Scene");
        var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(ReplayOverlay));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        // The score bar: three blocks across the bottom.
        RectTransform bar = Box(root.transform, "Score Bar", Bar, new Vector2(0f, 0f), new Vector2(1f, 0f), 0f, 64f);
        RectTransform tag = Box(bar, "Team", Accent, new Vector2(0f, 0f), new Vector2(0.3f, 1f), 0f, 0f);
        TMP_Text score = Text(tag, "Score", 26f, TextAlignmentOptions.Center);
        TMP_Text batters = Text(Region(bar, 0.31f, 0.66f), "Batters", 17f, TextAlignmentOptions.Left);
        TMP_Text bowler = Text(Region(bar, 0.67f, 0.99f), "Bowler", 17f, TextAlignmentOptions.Left);

        RectTransform replay = Box(root.transform, "Replay Tag", Accent, new Vector2(0f, 1f), new Vector2(0f, 1f), 0f, 0f);
        replay.sizeDelta = new Vector2(120f, 34f);
        replay.anchoredPosition = new Vector2(24f + 60f, -24f - 17f);
        Text(replay, "Replay", 20f, TextAlignmentOptions.Center).text = "<b>REPLAY</b>";
        canvas.enabled = false;

        var so = new SerializedObject(root.GetComponent<ReplayOverlay>());
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("score").objectReferenceValue = score;
        so.FindProperty("batters").objectReferenceValue = batters;
        so.FindProperty("bowler").objectReferenceValue = bowler;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static RectTransform Box(Transform parent, string name, Color color, Vector2 min, Vector2 max, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, y);
        rt.offsetMax = new Vector2(0f, y + height);
        if (height <= 0f)
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static RectTransform Region(RectTransform parent, float from, float to)
    {
        var rt = (RectTransform)new GameObject("Region", typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(from, 0f);
        rt.anchorMax = new Vector2(to, 1f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 4f);
        rt.offsetMax = new Vector2(-10f, -4f);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;   // the font has no ellipsis glyph
        text.raycastTarget = false;
        return text;
    }
}
