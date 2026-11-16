using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the four reading cards as prefabs under Assets/Resources/Prefabs/UI, in the next-ball
/// card's look (a "Play" card on a world-space canvas: a small heading, a body, one big button):
///   * ResultCard - the match result (MatchFlow), button Home;
///   * NextOverCard - who bowls next (OverBreak), button Next;
///   * NetsTrialCard - the nets trial's result (NetsTrialCard), button Continue, over the scoreboard;
///   * CareerMatchCard - a career match's catch-up and summary (CareerMatchCard), button Next, over
///     the scoreboard.
/// Each is saved inactive, with its text set up for CardText and its button wired to CardView.OnButton;
/// the game only fills in the words and listens to Clicked. Run again to put them back to these
/// settings (the prefabs are written in place). Until 2026-11-15 each card was a copy of
/// NextBallMenu's panel that code cut down by child name every time one was first shown; this
/// builds the pieces itself, so it does not depend on that panel's layout.
/// </summary>
public static class CardPrefabBuilder
{
    public const string Folder = "Assets/Resources/Prefabs/UI";
    private const string A = "  <size=75%>(A)</size>";

    private struct Spec
    {
        public string name, button, eyebrowColour;
        public float bodySize, bodyMaxHeight, columnWidth;
        public int sortingOrder;
    }

    private static readonly Spec[] Cards =
    {
        new Spec { name = "ResultCard", button = "Home" + A, eyebrowColour = CardText.Accent,
                   bodySize = 32f, bodyMaxHeight = 520f },
        new Spec { name = "NextOverCard", button = "Next" + A, eyebrowColour = CardText.Accent,
                   bodySize = CardText.BodySize, bodyMaxHeight = 520f },
        // Over the nets' scoreboard (sorting order 10); wide enough for "Temperament  62  Developing".
        new Spec { name = "NetsTrialCard", button = "Continue" + A, eyebrowColour = CardText.Win,
                   bodySize = CardText.BodySize, bodyMaxHeight = 600f, columnWidth = 640f, sortingOrder = 20 },
        // Above the HUD and stadium scoreboard canvases; wider for a few lines of scorecard.
        new Spec { name = "CareerMatchCard", button = "Next" + A, eyebrowColour = CardText.Accent,
                   bodySize = CardText.BodySize, bodyMaxHeight = 520f, columnWidth = 640f, sortingOrder = 20 },
    };

    [MenuItem("CricketVR/Scene Objects/Build Card Prefabs")]
    public static void Build()
    {
        SceneObjectAssets.EnsureFolder(Folder);
        UIStyle.EnsureSprites();
        foreach (Spec spec in Cards)
            MakeCard(spec);
        AssetDatabase.SaveAssets();
        Debug.Log("[CardPrefabBuilder] ResultCard, NextOverCard, NetsTrialCard and CareerMatchCard saved under " + Folder);
    }

    private static void MakeCard(Spec spec)
    {
        // The canvas is the prefab's root (the card is placed and scaled as one object).
        var temp = new GameObject("temp");
        GameObject card = WorldPanelBuilder.Canvas(temp, 100f, 0.0009f, true);
        card.transform.SetParent(null, false);
        Object.DestroyImmediate(temp);
        card.name = spec.name;
        var row = card.AddComponent<HorizontalLayoutGroup>();
        row.spacing = UIStyle.Gap;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        row.childAlignment = TextAnchor.UpperCenter;
        WorldPanelBuilder.FitContent(card, true);
        if (spec.sortingOrder != 0)
            card.GetComponent<Canvas>().sortingOrder = spec.sortingOrder;

        var view = card.AddComponent<CardView>();
        Transform play = Nested(card.transform, "Play", spec.columnWidth > 0f ? spec.columnWidth : 520f);
        TextMeshProUGUI heading = WorldPanelBuilder.Section(play, "");
        TextMeshProUGUI body = WorldPanelBuilder.Line(play, "", UIStyle.LabelSize + 6f, FontStyles.Bold, UIStyle.Text,
                                                      78f, TextAlignmentOptions.Left);
        Button button = UIControls.ActionButton(play, spec.button, view.OnButton, true, 88f);
        button.gameObject.name = "Button";
        // Sized to its words (CardText): the lines wrap inside the card, never past it.
        CardText.MakeEyebrow(heading, spec.eyebrowColour);
        CardText.MakeBody(body, spec.bodySize, spec.bodyMaxHeight);

        var so = new SerializedObject(view);
        so.FindProperty("heading").objectReferenceValue = heading;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("buttonLabel").objectReferenceValue = UIControls.LabelOf(button);
        so.ApplyModifiedProperties();

        card.SetActive(false);      // shown by the game when the card is wanted
        PrefabUtility.SaveAsPrefabAsset(card, $"{Folder}/{spec.name}.prefab");
        Object.DestroyImmediate(card);
    }

    /// A card inside the canvas: the dark rounded panel with its column of lines.
    private static Transform Nested(Transform parent, string name, float width)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        WorldPanelBuilder.MakeCard(go);
        go.GetComponent<LayoutElement>().preferredWidth = width;
        return go.transform;
    }
}
