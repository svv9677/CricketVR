using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// A floating card's words always fit it (2026-11-08: "the text goes out of the panel... make sure
/// that it never does"): the box is measured from the words at the column's width, a card grows to
/// hold them up to its cap, and past the cap the words get smaller, never wider or longer than it.
public class CardTextTests
{
    private GameObject root;
    private TextMeshProUGUI text;

    [SetUp]
    public void Card()
    {
        root = new GameObject("Card", typeof(RectTransform), typeof(ContentSizeFitter));
        var column = new GameObject("Play", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        column.transform.SetParent(root.transform, false);
        column.GetComponent<LayoutElement>().preferredWidth = 520f;
        column.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(32, 32, 24, 24);
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(column.transform, false);
        text = go.GetComponent<TextMeshProUGUI>();
    }

    [TearDown]
    public void Gone() => Object.DestroyImmediate(root);

    [Test]
    public void TheTextIsMeasuredAtTheColumnsWidth()
    {
        Assert.AreEqual(520f - 64f, CardText.ColumnWidth(text), 0.01f, "the column less its padding");
    }

    [Test]
    public void AShortBodyKeepsItsSizeAndItsBoxHoldsIt()
    {
        CardText.MakeBody(text);
        CardText.Set(text, "Middled - FOUR");
        Assert.AreEqual(CardText.BodySize, text.fontSize);
        float needs = text.GetPreferredValues(text.text, CardText.ColumnWidth(text), 0f).y;
        Assert.GreaterOrEqual(text.GetComponent<LayoutElement>().preferredHeight, needs, "the box is as tall as the words");
    }

    [Test]
    public void ALongBodyShrinksToItsCapAndNeverPastIt()
    {
        CardText.MakeBody(text, CardText.BodySize, 200f);
        string longText = string.Join("\n", System.Linq.Enumerable.Repeat("R. Venkataraghavan c Shahabuddin b Featherstonehaugh 34 (27)", 6));
        CardText.Set(text, longText);
        Assert.Less(text.fontSize, CardText.BodySize, "smaller to fit");
        Assert.GreaterOrEqual(text.fontSize, CardText.MinBodySize);
        Assert.LessOrEqual(text.GetComponent<LayoutElement>().preferredHeight, 200f + 2f, "inside the card's cap");
    }

    [Test]
    public void TheEyebrowIsSmallSpacedUpperCaseAndColoured()
    {
        CardText.MakeEyebrow(text);
        CardText.Set(text, "Next over");
        Assert.AreEqual(CardText.EyebrowSize, text.fontSize);
        Assert.IsTrue((text.fontStyle & FontStyles.UpperCase) != 0);
        StringAssert.Contains(CardText.Accent, text.text);
        Assert.Greater(CardText.TitleSize, CardText.BodySize);
        Assert.Greater(CardText.BodySize, CardText.CaptionSize);
    }
}
