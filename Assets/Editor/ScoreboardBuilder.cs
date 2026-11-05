using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools > CricketVR > Build Stadium Scoreboard: lays out the big scoreboard in the stands
/// (Main/Displays/Scorecard Display/HUD) as a broadcast board that reads from the crease, 95 m away
/// (2026-11-05: "the text is not visible at all" - black text on white rows, and two long lines of
/// status squeezed into 300 px boxes). Restyles the same objects HUD writes to, so nothing in the
/// game changes but how it looks:
///
///   [ logo strip                                            ]
///   [ 45/2                                   6.3 / 10 ov    ]   the score, the biggest thing on it
///   [ RR 7.12  ·  Need 23 off 14  ·  RRR 9.9                ]
///   [ Rohit *                                     34 (22)   ]
///   [ Kohli                                       12 (9)    ]
///   [ Bumrah                    Fast   2.3-14-1             ]
///   [ THIS OVER   1  •  4  W                                ]
///
/// Dark board, white figures, grey labels, gold for the striker and boundaries. 1.3x the old size.
/// </summary>
public static class ScoreboardBuilder
{
    private const string Path = "Main/Displays/Scorecard Display/HUD";
    private const float Grow = 1.3f;
    private static readonly Vector3 OriginalScale = new Vector3(2.25f, 0.96f, 0.96f);
    private static readonly Color Board = new Color32(0x0A, 0x0E, 0x14, 0xFF);
    private static readonly Color Row = new Color32(0x14, 0x1A, 0x24, 0xFF);
    private static readonly Color RowAlt = new Color32(0x10, 0x15, 0x1D, 0xFF);
    private static readonly Color White = new Color32(0xF3, 0xF5, 0xF8, 0xFF);
    private static readonly Color Muted = new Color32(0x98, 0xA2, 0xB3, 0xFF);

    [MenuItem("Tools/CricketVR/Build Stadium Scoreboard")]
    public static void Build()
    {
        GameObject hud = GameObject.Find(Path);
        if (hud == null) { Debug.LogError("[Scoreboard] no " + Path); return; }
        // The board's frame is a box stretched (2.25, 0.96, 0.96) with the canvas inside it
        // compensating: grow that shape, never replace it with a uniform scale (which squashed
        // the canvas to 7 x 13 m and condensed every letter).
        Transform display = hud.transform.parent;
        Vector3 want = OriginalScale * Grow;
        if ((display.localScale - want).sqrMagnitude > 1e-6f)
        {
            Undo.RecordObject(display, "Scoreboard size");
            display.localScale = want;
        }
        Image back = hud.GetComponent<Image>();
        if (back != null) { Undo.RecordObject(back, "Scoreboard"); back.sprite = null; back.color = Board; }

        Transform score = hud.transform.Find("Score");
        // Rows, top to bottom (the canvas is 2560 x 1920, centre origin).
        RowAt(score, "RowLogo", 790f, 300f, null);
        RowAt(score, "RowScore", 470f, 330f, Row);
        RowAt(score, "RowBat1", 70f, 210f, Row);
        RowAt(score, "RowBat2", -150f, 210f, RowAlt);
        RowAt(score, "RowBowler", -400f, 230f, Row);
        RowAt(score, "RowBalls", -680f, 240f, RowAlt);

        // The score: big and white on the left, the overs on the right.
        // 250 in a 320-high box, and allowed to overflow: with Ellipsis, TMP hides a line taller
        // than its box altogether (the score went missing at 270 in 300).
        Text(score, "RowScore/txtScore", -1180f, 0f, 1200f, 320f, 250f, White, TextAlignmentOptions.Left, true);
        Overflow(score, "RowScore/txtScore");
        Text(score, "RowScore/txtOvers", 1180f, 0f, 1100f, 300f, 170f, White, TextAlignmentOptions.Right, true);
        Hide(score, "RowScore/lblScore"); Hide(score, "RowScore/lblOvers");
        Hide(score, "RowScore/lblRR"); Hide(score, "RowScore/sepRRClose");
        // The run rate and the match line: a line of its own under the score (it stays a child of
        // the score row - the board is in a prefab instance, which cannot be re-parented - placed
        // 200 below it, between the score and the batters).
        Text(score, "RowScore/txtRR", -1180f, -200f, 2360f, 150f, 105f, White, TextAlignmentOptions.Left, false);
        Text(score, "RowBat1/txtBatsman1", -1180f, 0f, 1600f, 190f, 150f, White, TextAlignmentOptions.Left, true);
        Text(score, "RowBat1/txtBatsmanScore1", 1180f, 0f, 800f, 190f, 150f, White, TextAlignmentOptions.Right, true);
        Hide(score, "RowBat1/batIcon1");
        Text(score, "RowBat2/txtBatsman2", -1180f, 0f, 1600f, 190f, 150f, White, TextAlignmentOptions.Left, false);
        Text(score, "RowBat2/txtBatsmanScore2", 1180f, 0f, 800f, 190f, 150f, White, TextAlignmentOptions.Right, false);
        Text(score, "RowBowler/txtBowler", -1180f, 0f, 1200f, 190f, 140f, White, TextAlignmentOptions.Left, true);
        Text(score, "RowBowler/txtBowlerStyle", 1180f, 0f, 1300f, 190f, 120f, White, TextAlignmentOptions.Right, false);
        Hide(score, "RowBowler/sepStyleOpen"); Hide(score, "RowBowler/sepStyleClose");
        Text(score, "RowBalls/txtBalls", -1180f, 0f, 2360f, 200f, 130f, White, TextAlignmentOptions.Left, true);
        Hide(score, "txtVersion");

        // The title in place of the old low-resolution logo picture: clean white on the strip.
        Hide(score, "RowLogo/Logo");
        Transform strip = score.Find("RowLogo");
        if (strip != null)
        {
            Transform title = strip.Find("Title");
            if (title == null)
            {
                var go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(go, "Scoreboard");
                go.transform.SetParent(strip, false);
                title = go.transform;
            }
            var t = title.GetComponent<TextMeshProUGUI>();
            t.text = "CRICKET VR";
            Text(score, "RowLogo/Title", 0f, 0f, 2400f, 260f, 160f, White, TextAlignmentOptions.Center, true);
            t.characterSpacing = 18f;
            var other = score.Find("RowBat1/txtBatsman1")?.GetComponent<TextMeshProUGUI>();
            if (other != null) { t.font = other.font; t.fontSharedMaterial = other.fontSharedMaterial; }
            var rt = (RectTransform)title;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        EditorSceneManager.MarkSceneDirty(hud.scene);
        Debug.Log("[Scoreboard] built");
    }

    private static void RowAt(Transform score, string name, float y, float height, Color? colour)
    {
        var rt = score.Find(name) as RectTransform;
        if (rt == null) return;
        Undo.RecordObject(rt, "Scoreboard");
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(2500f, height);
        Image img = rt.GetComponent<Image>();
        if (img != null && colour.HasValue) { Undo.RecordObject(img, "Scoreboard"); img.color = colour.Value; img.sprite = null; }
    }

    /// Place a text by its anchor edge: left-aligned texts at `x` = their left edge, right-aligned
    /// at `x` = their right edge (so the columns line up whatever the length).
    private static void Text(Transform score, string path, float x, float y, float width, float height, float size, Color colour,
                             TextAlignmentOptions align, bool bold)
    {
        var rt = score.Find(path) as RectTransform;
        if (rt == null) { Debug.LogWarning("[Scoreboard] no " + path); return; }
        var t = rt.GetComponent<TextMeshProUGUI>();
        Undo.RecordObject(rt, "Scoreboard");
        Undo.RecordObject(t, "Scoreboard");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        bool right = align == TextAlignmentOptions.Right;
        rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
        t.fontSize = size;
        t.enableAutoSizing = false;
        t.color = colour;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true;
        t.gameObject.SetActive(true);
    }

    private static void Overflow(Transform score, string path)
    {
        var t = score.Find(path)?.GetComponent<TextMeshProUGUI>();
        if (t != null) t.overflowMode = TextOverflowModes.Overflow;
    }

    private static void Hide(Transform score, string path)
    {
        Transform t = score.Find(path);
        if (t != null && t.gameObject.activeSelf) { Undo.RecordObject(t.gameObject, "Scoreboard"); t.gameObject.SetActive(false); }
    }
}
