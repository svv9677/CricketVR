using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CricketVR > Scene Objects > Place HUD Rates. Puts the rates bar on the stadium scoreboard in
/// Assets/Scenes/CricketVR.unity: the bar's row ("RowRates", copied from the scorecard's "this
/// over" row with its text "txtRates"), the grey backing band ("RatesBacking"), the HUDRates
/// component on the HUD, and the layout that makes room for it (the board taller at its bottom,
/// the rows and the whole display shifted so the top edge and rows stay where they were).
/// Until 2026-11-15 HUDRates built all of this when the match scene loaded.
///
/// It is a scene change, not a prefab one: Main.prefab is also in Nets, which has no rates bar.
/// Sizes and positions are set from Main.prefab's own values, so running it again gives the same
/// result. Run it after Tools > CricketVR > Build Stadium Scoreboard, which resets the board size.
/// </summary>
public static class HUDRatesBuilder
{
    private const string ScenePath = "Assets/Scenes/CricketVR.unity";
    private const string MainPrefab = "Assets/Prefabs/Scene/Main.prefab";
    private const string DisplayPath = "Displays/Scorecard Display";
    /// The board grows this much at the bottom for the bar (its rows stay where they were), and
    /// the bar sits in that band, in the Score panel's units.
    private const float Grow = 300f, BarY = -1105f, BarWidth = 4400f, BarHeight = 250f, BoardWidth = 4500f;
    private static readonly Color ScreenGrey = new Color(0.094f, 0.094f, 0.094f, 1f);

    [MenuItem("CricketVR/Scene Objects/Place HUD Rates")]
    public static void Place()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefab);
        Transform baseDisplay = prefab != null ? prefab.transform.Find(DisplayPath) : null;
        Transform baseHud = baseDisplay != null ? baseDisplay.Find("HUD") : null;
        Transform baseScore = baseHud != null ? baseHud.Find("Score") : null;
        if (baseScore == null)
        {
            Debug.LogError("[HUDRatesBuilder] " + MainPrefab + " has no " + DisplayPath + "/HUD/Score.");
            return;
        }
        SceneObjectAssets.Open(ScenePath);
        HUD hud = Object.FindFirstObjectByType<HUD>(FindObjectsInactive.Include);
        Transform score = hud != null ? hud.transform.Find("Score") : null;
        Transform row = score != null ? score.Find("RowBalls") : null;
        TMP_Text style = row != null ? row.GetComponentInChildren<TMP_Text>(true) : null;
        if (style == null)
        {
            Debug.LogError("[HUDRatesBuilder] No HUD with Score/RowBalls in " + ScenePath);
            return;
        }
        Remove(score, "RowRates");
        Remove(score, "RatesBacking");
        GrowBoard((RectTransform)hud.transform, (RectTransform)score, baseDisplay, baseHud, baseScore);
        Backing(score);
        GameObject bar = Bar(score, row, style);
        HUDRates rates = hud.GetComponent<HUDRates>();
        if (rates == null)
            rates = Undo.AddComponent<HUDRates>(hud.gameObject);
        var so = new SerializedObject(rates);
        so.FindProperty("bar").objectReferenceValue = bar;
        so.FindProperty("text").objectReferenceValue = bar.GetComponentInChildren<TMP_Text>(true);
        so.ApplyModifiedProperties();
        SceneObjectAssets.Save(hud.gameObject.scene);
        Debug.Log("[HUDRatesBuilder] Rates bar, backing and board layout placed in " + ScenePath);
    }

    private static void Remove(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
    }

    /// The board Grow taller at its bottom: its top edge and every row stay put in the world.
    /// Everything is set from Main.prefab's values, so a second run does not grow it twice.
    private static void GrowBoard(RectTransform board, RectTransform rows, Transform baseDisplay, Transform baseHud, Transform baseScore)
    {
        var baseBoard = (RectTransform)baseHud;
        board.sizeDelta = baseBoard.sizeDelta;
        board.localPosition = baseBoard.localPosition;
        rows.anchoredPosition = ((RectTransform)baseScore).anchoredPosition;
        board.parent.localPosition = baseDisplay.localPosition;
        // Metres per unit up the board, measured (its parent screen is rotated and scaled unevenly).
        var corners = new Vector3[4];
        board.GetWorldCorners(corners);
        float unit = (corners[1] - corners[0]).magnitude / Mathf.Max(1f, board.sizeDelta.y);
        board.sizeDelta += new Vector2(0f, Grow);
        board.position -= board.up * (Grow * 0.5f * unit);
        rows.anchoredPosition += new Vector2(0f, Grow * 0.5f);
        // The board's bottom edge sits on the stand: the new band would be behind its crowd. The
        // whole display goes up by the band instead, so it ends where the board used to.
        board.parent.position += Vector3.up * (Grow * unit);
    }

    /// The board's screen is a fixed plane in the stadium (Scorecard Display, flat grey): a band of
    /// its colour behind the bar carries it down, under both columns.
    private static void Backing(Transform score)
    {
        var go = new GameObject("RatesBacking", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(score, false);
        go.transform.SetAsFirstSibling();
        var r = (RectTransform)go.transform;
        r.anchoredPosition = new Vector2(0f, BarY - 10f);
        r.sizeDelta = new Vector2(BoardWidth, Grow + 20f);
        Image img = go.GetComponent<Image>();
        img.color = ScreenGrey;
        img.raycastTarget = false;
    }

    /// The bar: the "this over" row's rect and image, holding a copy of its text re-set to fill the
    /// bar and shrink to fit. Saved inactive; HUDRates shows it when there is a line to say.
    private static GameObject Bar(Transform score, Transform row, TMP_Text style)
    {
        var go = new GameObject("RowRates", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(score, false);
        var src = (RectTransform)row;
        rect.anchorMin = src.anchorMin;
        rect.anchorMax = src.anchorMax;
        rect.pivot = src.pivot;
        rect.anchoredPosition = new Vector2(0f, BarY);
        rect.sizeDelta = new Vector2(BarWidth, BarHeight);
        Image srcImage = row.GetComponent<Image>();
        if (srcImage != null)
            EditorUtility.CopySerialized(srcImage, go.GetComponent<Image>());

        var tgo = new GameObject("txtRates", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var t = (RectTransform)tgo.transform;
        t.SetParent(rect, false);
        var text = tgo.GetComponent<TextMeshProUGUI>();
        EditorUtility.CopySerialized(style, text);
        t.pivot = style.rectTransform.pivot;
        t.anchorMin = Vector2.zero;
        t.anchorMax = Vector2.one;
        t.offsetMin = new Vector2(80f, 0f);
        t.offsetMax = new Vector2(-80f, 0f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = style.fontSize * 1.1f;
        text.enableAutoSizing = true;
        text.fontSizeMin = style.fontSize * 0.6f;
        text.fontSizeMax = style.fontSize * 1.1f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        go.SetActive(false);
        return go;
    }
}
