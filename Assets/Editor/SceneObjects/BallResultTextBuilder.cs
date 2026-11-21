using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places "Ball Result Text" (BallResultText: what happened on the last ball) in CricketVR.unity,
/// over the bowler's stumps, with its TextMeshPro set up and its soft-shadow font material saved
/// as an asset. Run again to put it back to these settings. Until 2026-11-15 the game made the
/// object, the text and the material in code as the match scene loaded.
/// </summary>
public static class BallResultTextBuilder
{
    private const string MatchScene = "Assets/Scenes/CricketVR.unity";
    public const string ObjectName = "Ball Result Text";
    public const string MaterialPath = SceneObjectAssets.GeneratedFolder + "/BallResultText/BallResultText Underlay.mat";

    [MenuItem("CricketVR/Scene Objects/Place Ball Result Text")]
    public static void Place()
    {
        Scene scene = SceneObjectAssets.Open(MatchScene);
        GameObject go = SceneObjectAssets.Root(scene, ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        GameObject end = GameObject.Find("Main/Stumps_BowlingEnd");
        float x = end != null ? end.transform.position.x : -10.4f;
        go.transform.SetPositionAndRotation(new Vector3(x, BallResultText.Height, 0f), Quaternion.identity);
        go.transform.localScale = Vector3.one;      // the game hides it at scale 0 until a ball is scored

        TextMeshPro text = SceneObjectAssets.Ensure<TextMeshPro>(go.gameObject);
        text.fontSize = 14f;
        text.enableAutoSizing = true;       // a long result shrinks to its 12 m line, never past it
        text.fontSizeMax = 14f;
        text.fontSizeMin = 6f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.sizeDelta = new Vector2(12f, 3f);
        text.outlineWidth = 0f;
        text.fontSharedMaterial = Underlay(text.font.material);
        text.characterSpacing = 6f;
        text.text = "";
        if (go.GetComponent<BallResultText>() == null)
            go.AddComponent<BallResultText>();

        SceneObjectAssets.Save(scene);
        Debug.Log($"[BallResultTextBuilder] {ObjectName} placed in {MatchScene}");
    }

    /// A soft shadow under the letters rather than a heavy black outline round them.
    private static Material Underlay(Material fontMaterial)
    {
        var m = new Material(fontMaterial) { name = "BallResultText Underlay" };
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
        m.SetFloat("_UnderlayOffsetX", 0.6f);
        m.SetFloat("_UnderlayOffsetY", -0.6f);
        m.SetFloat("_UnderlaySoftness", 0.5f);
        return SceneObjectAssets.SaveMaterial(m, MaterialPath);
    }
}
