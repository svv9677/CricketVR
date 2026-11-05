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
/// Dark board, white figures, grey labels, gold for the striker and boundaries. 1.9x the old size
/// (1.3x was still too small from the crease, 2026-11-05).
/// </summary>
public static class ScoreboardBuilder
{
    private const string Path = "Main/Displays/Scorecard Display/HUD";
    private const float Grow = 1.9f;
    /// Canvas width, px: fills the frame (42.8 m at 0.009 m/px) with a margin.
    private const float Width = 4500f;
    /// Where the board's centre goes: 70 m out from the middle (from 80, in front of the upper
    /// tier's fascia, which hid its bottom half once it grew) and 28.5 m up (clear of the roof).
    private const float CentreRadius = 70f, CentreHeight = 28.5f;
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
        Vector3 flat = new Vector3(display.position.x, 0f, display.position.z);
        Vector3 place = flat.normalized * CentreRadius + Vector3.up * CentreHeight;
        if ((display.position - place).sqrMagnitude > 1e-4f)
        {
            Undo.RecordObject(display, "Scoreboard place");
            display.position = place;
        }
        Image back = hud.GetComponent<Image>();
        if (back != null) { Undo.RecordObject(back, "Scoreboard"); back.sprite = null; back.color = Board; }

        // Fill the frame: it is 42.8 x 18.2 m and the canvas was 23 m wide, half the board black.
        var canvasRect = (RectTransform)hud.transform;
        Undo.RecordObject(canvasRect, "Scoreboard");
        canvasRect.sizeDelta = new Vector2(Width, 1920f);
        Transform score = hud.transform.Find("Score");
        // Two columns: the score on the left, the players on the right (canvas centre origin).
        const float L = -1150f, R = 1150f, Col = 2150f, In = 1000f;
        RowAt(score, "RowLogo", 0f, 790f, Width - 100f, 300f, null);
        RowAt(score, "RowScore", L, 250f, Col, 760f, Row);
        RowAt(score, "RowBalls", L, -560f, Col, 300f, RowAlt);
        RowAt(score, "RowBat1", R, 470f, Col, 300f, Row);
        RowAt(score, "RowBat2", R, 150f, Col, 300f, RowAlt);
        RowAt(score, "RowBowler", R, -300f, Col, 560f, Row);

        // The score, huge; the overs beside it; the run rate and the match line under them.
        Text(score, "RowScore/txtScore", -In, 120f, 1300f, 480f, 430f, White, TextAlignmentOptions.Left, true);
        Overflow(score, "RowScore/txtScore");
        Text(score, "RowScore/txtOvers", In, 120f, 900f, 300f, 230f, White, TextAlignmentOptions.Right, true);
        Text(score, "RowScore/txtRR", -In, -250f, 2000f, 200f, 150f, White, TextAlignmentOptions.Left, false);
        Hide(score, "RowScore/lblScore"); Hide(score, "RowScore/lblOvers");
        Hide(score, "RowScore/lblRR"); Hide(score, "RowScore/sepRRClose");
        Text(score, "RowBalls/txtBalls", -In, 0f, 2000f, 240f, 175f, White, TextAlignmentOptions.Left, true);
        // The batters, one to a row: name left, runs (balls) right.
        Text(score, "RowBat1/txtBatsman1", -In, 0f, 1300f, 240f, 215f, White, TextAlignmentOptions.Left, true);
        Text(score, "RowBat1/txtBatsmanScore1", In, 0f, 700f, 240f, 215f, White, TextAlignmentOptions.Right, true);
        Hide(score, "RowBat1/batIcon1");
        Text(score, "RowBat2/txtBatsman2", -In, 0f, 1300f, 240f, 215f, White, TextAlignmentOptions.Left, false);
        Text(score, "RowBat2/txtBatsmanScore2", In, 0f, 700f, 240f, 215f, White, TextAlignmentOptions.Right, false);
        // The bowler, and under him how he bowls and his figures.
        Text(score, "RowBowler/txtBowler", -In, 110f, 2000f, 240f, 205f, White, TextAlignmentOptions.Left, true);
        Text(score, "RowBowler/txtBowlerStyle", -In, -120f, 2000f, 200f, 160f, White, TextAlignmentOptions.Left, false);
        Hide(score, "RowBowler/sepStyleOpen"); Hide(score, "RowBowler/sepStyleClose");
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
            Text(score, "RowLogo/Title", 0f, 0f, 3000f, 260f, 180f, White, TextAlignmentOptions.Center, true);
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

    private static void RowAt(Transform score, string name, float x, float y, float width, float height, Color? colour)
    {
        var rt = score.Find(name) as RectTransform;
        if (rt == null) return;
        Undo.RecordObject(rt, "Scoreboard");
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, height);
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
        // Shrink a long line to fit its width rather than cutting it ("0.2 / 10...", "Over 1 of
        // 10 · ..."), and never hide a line for being taller than its box (with Ellipsis, TMP drew
        // nothing at all for a 215 px name in a 240 px row).
        t.fontSize = size;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.55f;
        t.color = colour;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
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
