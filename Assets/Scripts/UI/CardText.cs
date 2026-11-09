using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The words on a floating card, always inside it, in a clear type scale (2026-11-08: "the text goes
/// out of the panel... when I'm autoplaying or when... the non-striker is batting... make sure that
/// it never does... vary the size, like title, heading").
///
/// The match's reading cards (CareerMatchCard, NetsTrialCard, MatchFlow's result, OverBreak) are
/// copies of the next-ball panel, which sizes itself to its content (ContentSizeFitter over layout
/// groups). Each had pinned its text to a fixed box with overflow on: a 640 px body in a 520 px
/// column ran off the right edge, a long catch-up off the bottom, a 38 px heading in the 28 px label
/// row over the line below. Here a text's box is measured from its words at the column's width every
/// time it is set (Set), so the card grows to hold it; past a card's cap the text steps down in size
/// until it fits, and only past the smallest size is the end cut with an ellipsis.
///
/// The scale (one font, Liberation Sans - the only one the project ships that is ours to use; the
/// levels are told apart by size, weight, case, spacing and colour):
///   Eyebrow  small, bold, upper case, letter-spaced, in the card's colour: what the card is
///   Title    big and bold: the one thing to read first (a score, a name)
///   Body     the lines that follow
///   Caption  small and grey: the detail under a line
/// </summary>
public static class CardText
{
    public const float EyebrowSize = 22f, TitleSize = 44f, BodySize = 28f, CaptionSize = 22f;
    /// The smallest a body is allowed to shrink to before its end is cut.
    public const float MinBodySize = 18f;
    public const string Accent = "#3B82F6", Win = "#22C55E", Muted = "#98A2B3";

    /// Rich-text pieces for the body (sizes relative to BodySize).
    public static string Title(string s) => $"<size={TitleSize / BodySize * 100f:0}%><b>{s}</b></size>";
    public static string Caption(string s) => $"<size={CaptionSize / BodySize * 100f:0}%><color={Muted}>{s}</color></size>";

    /// A card's eyebrow: what the card is ("NEXT OVER"), small, spaced, in `colour`.
    public static void MakeEyebrow(TMP_Text t, string colour = Accent)
    {
        CardTextFit fit = Prepare(t, EyebrowSize, EyebrowSize * 0.8f, 120f);
        fit.prefix = $"<color={colour}>";
        fit.suffix = "</color>";
        t.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        t.characterSpacing = 8f;
    }

    /// A card's body: wraps, top-left, its box as tall as its words up to `maxHeight` px.
    public static void MakeBody(TMP_Text t, float size = BodySize, float maxHeight = 520f)
    {
        Prepare(t, size, MinBodySize, maxHeight);
        t.fontStyle = FontStyles.Normal;
        t.characterSpacing = 0f;
        t.lineSpacing = 4f;
    }

    /// Put `text` on a card text made by MakeEyebrow or MakeBody, and size its box to it.
    public static void Set(TMP_Text t, string text)
    {
        if (t == null)
            return;
        CardTextFit fit = t.GetComponent<CardTextFit>();
        if (fit == null)
        {
            t.text = text;
            return;
        }
        t.text = fit.prefix + (text ?? "") + fit.suffix;
        fit.Apply(t);
    }

    private static CardTextFit Prepare(TMP_Text t, float size, float minSize, float maxHeight)
    {
        CardTextFit fit = t.GetComponent<CardTextFit>();
        if (fit == null) fit = t.gameObject.AddComponent<CardTextFit>();
        fit.size = size;
        fit.minSize = Mathf.Min(minSize, size);
        fit.maxHeight = maxHeight;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.alignment = TextAlignmentOptions.TopLeft;
        t.fontSize = size;
        LayoutElement layout = t.GetComponent<LayoutElement>();
        if (layout == null) layout = t.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = -1f;   // the column's width, never wider
        layout.flexibleWidth = 1f;
        return fit;
    }

    /// The width a text in a layout column gets: the column's preferred width less its padding.
    public static float ColumnWidth(TMP_Text t)
    {
        RectTransform column = t.rectTransform.parent as RectTransform;
        if (column == null)
            return t.rectTransform.rect.width;
        float width = column.rect.width;
        LayoutElement le = column.GetComponent<LayoutElement>();
        if (le != null && le.preferredWidth > 0f)
            width = le.preferredWidth;
        LayoutGroup group = column.GetComponent<LayoutGroup>();
        if (group != null)
            width -= group.padding.left + group.padding.right;
        return width > 1f ? width : t.rectTransform.rect.width;
    }

    /// The largest size from `size` down to `minSize` at which `text` fits `maxHeight` when wrapped
    /// to `width`; its height there. Pure: CardTextTests.
    public static float SizeToFit(TMP_Text t, string text, float width, float size, float minSize, float maxHeight, out float height)
    {
        float s = size;
        for (; ; s = Mathf.Max(minSize, s - 1f))
        {
            t.fontSize = s;
            height = t.GetPreferredValues(text, width, 0f).y;
            if (height <= maxHeight || s <= minSize)
                return s;
        }
    }
}
