using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The cards a career match shows between the career player's own balls (CareerMatch):
///   * the catch-up - what happened while his partners batted ("While you were at the other end",
///     who fell and how, the score), or, the first time he walks out, "You come in at 45/4";
///   * the summary when he is out - his innings, then the side's total once the rest is simulated.
/// Each stays up until Next (A, or its button) is pressed - none goes on its own any more
/// (2026-11-09: "I should not be waiting for anything. Everything should be driven by clicks ... I
/// should always see a button that says Next with the button A"); no ball can be started while it
/// is up (MatchGate, held by CareerMatch).
///
/// The card is a copy of the next-ball card's panel (as MatchFlow's result card and the nets
/// trial's card are), so it looks like every other card in the match; its one button is the panel's
/// Bowl button, relabelled (SetButton). A scene without that panel gets the same words as plain
/// floating text, and A.
/// Placed from the player's head, a little way in front, and drawn over the scoreboard canvases.
/// All text is Latin-1 (NetsTrialCard.Latin1: the TMP default font has nothing else).
/// </summary>
public class CareerMatchCard
{
    /// Metres in front of the eye, below it, and how much bigger than the next-ball card.
    private const float CardAhead = 1.5f, CardBelow = 0.1f, CardBigger = 1.25f;
    private const float FallbackAhead = 2.2f, FallbackSize = 0.8f;
    /// How far (m, level) a MenuLayout eye may be from the camera and still be used.
    private const float MaxLayoutDrift = 1.5f;
    /// Above the HUD and stadium scoreboard canvases (sorting order 0 in the match scene; 10 in the
    /// nets), which otherwise draw over anything nearer the eye.
    private const int OverTheScoreboard = 20;
    /// The card's text column (px): wider than the next-ball card's, for a few lines of scorecard.
    private const float ColumnWidth = 640f;

    private readonly Transform parent;
    private GameObject card;
    private TMP_Text heading, body, buttonLabel;
    private bool clicked;
    private TextMeshPro fallback;
    private Vector3 baseScale;

    public CareerMatchCard(Transform parent) => this.parent = parent;

    /// The card's button was clicked since the last call (the laser; A is read by CareerMatch).
    public bool TakeClick()
    {
        bool c = clicked;
        clicked = false;
        return c;
    }

    /// The button's words: "Next  (A)", "Skip  (A)".
    public void SetButton(string text)
    {
        if (buttonLabel != null)
            buttonLabel.text = text;
    }

    public bool Showing => (card != null && card.activeSelf) || (fallback != null && fallback.gameObject.activeSelf);

    /// Up in front of the player with these words (Latin-1 is applied here).
    public void Show(string headingText, string bodyText)
    {
        headingText = NetsTrialCard.Latin1(headingText ?? "");
        bodyText = NetsTrialCard.Latin1(bodyText ?? "");
        if (BuildCard())
        {
            CardText.Set(heading, headingText);
            CardText.Set(body, bodyText);
            if (Eye(out Vector3 eye, out Vector3 forward))
            {
                card.transform.SetPositionAndRotation(eye + forward * CardAhead + Vector3.down * CardBelow,
                                                      Quaternion.LookRotation(forward, Vector3.up));
                card.transform.localScale = baseScale * CardBigger;
            }
            card.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
            return;
        }
        if (fallback == null)
            fallback = MakeText("Career Match Card", FallbackSize, new Vector2(3.4f, 2.4f));
        fallback.text = $"<b><color=#3B82F6>{headingText}</color></b>\n{bodyText}";
        if (Eye(out Vector3 e, out Vector3 f))
        {
            Vector3 at = e + f * FallbackAhead;
            Vector3 away = at - e;
            away.y = 0f;
            fallback.transform.SetPositionAndRotation(at, away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away, Vector3.up) : Quaternion.identity);
        }
        fallback.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (card != null)
            card.SetActive(false);
        if (fallback != null)
            fallback.gameObject.SetActive(false);
    }

    /// A copy of the next-ball card's panel: its name line is the heading, the line under it the
    /// body; Bowl and the row of small buttons go (the same surgery as MatchFlow.BuildCard).
    private bool BuildCard()
    {
        if (card != null)
            return body != null && heading != null;
        NextBallMenu menu = Object.FindFirstObjectByType<NextBallMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.Panel == null)
            return false;
        card = Object.Instantiate(menu.Panel, parent);
        card.name = "Career Match Card";
        baseScale = card.transform.localScale;
        card.SetActive(false);
        Transform replay = card.transform.Find("Replay");
        if (replay != null)
            Object.Destroy(replay.gameObject);
        Transform play = card.transform.Find("Play");
        if (play == null)
            return false;
        var doomed = new List<GameObject>();
        foreach (Transform child in play)
        {
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (child.name.StartsWith("Bowl"))
            {
                WireButton(child);
                continue;
            }
            if (text != null && heading == null)
            {
                heading = text;
                CardText.MakeEyebrow(heading);
            }
            else if (text != null && body == null)
            {
                body = text;
                CardText.MakeBody(body);
            }
            else
                doomed.Add(child.gameObject);
        }
        foreach (GameObject g in doomed)
            Object.Destroy(g);
        Canvas canvas = card.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = OverTheScoreboard;
        OpenXRMenuInputModule.RegisterPanel(card);   // its button takes the laser, as the next-ball card's
        LayoutElement column = play.GetComponent<LayoutElement>();
        if (column != null)
            column.preferredWidth = ColumnWidth;   // the panel widens with it; the text never past it
        return body != null && heading != null;
    }

    /// Floating world-space text with a soft shadow, hidden until shown (the fallback).
    private TextMeshPro MakeText(string name, float size, Vector2 rect)
    {
        var go = new GameObject(name);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        go.transform.SetParent(parent, false);
        t.fontSize = size;
        t.enableAutoSizing = true;          // shrinks to stay in its box rather than spill out of it
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.4f;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = rect;
        t.color = Color.white;
        t.fontSharedMaterial = new Material(t.fontSharedMaterial);
        t.fontSharedMaterial.EnableKeyword("UNDERLAY_ON");
        t.fontSharedMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
        t.fontSharedMaterial.SetFloat("_UnderlayOffsetX", 0.6f);
        t.fontSharedMaterial.SetFloat("_UnderlayOffsetY", -0.6f);
        t.fontSharedMaterial.SetFloat("_UnderlaySoftness", 0.5f);
        t.text = "";
        t.GetComponent<MeshRenderer>().sortingOrder = OverTheScoreboard;
        go.SetActive(false);
        return t;
    }

    /// The player's eye and the level way he faces: the match's MenuLayout (at the crease, facing
    /// the bowler) when the player is really there, else the camera and where it looks.
    private static bool Eye(out Vector3 eye, out Vector3 forward)
    {
        Camera cam = Camera.main;
        if (MenuLayout.TryGetEye(out eye, out forward) &&
            (cam == null || Vector3.ProjectOnPlane(eye - cam.transform.position, Vector3.up).magnitude < MaxLayoutDrift))
            return true;
        if (cam == null)
            return false;
        eye = cam.transform.position;
        forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.left;
        return true;
    }

    private void WireButton(Transform bowl)
    {
        Button button = bowl.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => clicked = true);
        }
        buttonLabel = bowl.GetComponentInChildren<TMP_Text>(true);
        SetButton("Next  <size=75%>(A)</size>");
    }
}
