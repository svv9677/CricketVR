using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// What CardText.MakeEyebrow and MakeBody put on a card text: its sizes and its cap, so CardText.Set
/// can measure the box from the words each time they change. Runtime only (the cards are built then).
public sealed class CardTextFit : MonoBehaviour
{
    public float size, minSize, maxHeight;
    public string prefix = "", suffix = "";

    public void Apply(TMP_Text t)
    {
        float width = CardText.ColumnWidth(t);
        CardText.SizeToFit(t, t.text, width, size, minSize, maxHeight, out float height);
        // Still too tall at the smallest size: the end is cut with an ellipsis, never spilled.
        t.overflowMode = height > maxHeight ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
        LayoutElement layout = t.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = Mathf.Ceil(Mathf.Min(height, maxHeight)) + 2f;
        for (Transform p = t.transform; p != null; p = p.parent)
            if (p.GetComponent<ContentSizeFitter>() != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)p);
                break;
            }
    }
}
