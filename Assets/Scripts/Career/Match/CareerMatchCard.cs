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
/// The card is the CareerMatchCard prefab (CardPrefabBuilder), in the next-ball card's look as
/// MatchFlow's result card and the nets trial's card are, so it looks like every other card in the
/// match; its one button is relabelled (SetButton). It is made the first time one is shown.
/// Placed from the player's head, a little way in front, and drawn over the scoreboard canvases
/// (the prefab's canvas sorts above them). All text is Latin-1 (NetsTrialCard.Latin1: the TMP
/// default font has nothing else).
/// </summary>
public class CareerMatchCard
{
    private const string CardResource = "Prefabs/UI/CareerMatchCard";
    /// Metres in front of the eye, below it, and how much bigger than the next-ball card.
    private const float CardAhead = 1.5f, CardBelow = 0.1f, CardBigger = 1.25f;
    /// How far (m, level) a MenuLayout eye may be from the camera and still be used.
    private const float MaxLayoutDrift = 1.5f;

    private readonly Transform parent;
    private GameObject card;
    private CardView view;
    private TMP_Text heading, body;
    private bool clicked, failed;
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
    public void SetButton(string text) => view?.SetButton(text);

    public bool Showing => card != null && card.activeSelf;

    /// Up in front of the player with these words (Latin-1 is applied here).
    public void Show(string headingText, string bodyText)
    {
        if (!BuildCard())
            return;
        CardText.Set(heading, NetsTrialCard.Latin1(headingText ?? ""));
        CardText.Set(body, NetsTrialCard.Latin1(bodyText ?? ""));
        if (Eye(out Vector3 eye, out Vector3 forward))
        {
            card.transform.SetPositionAndRotation(eye + forward * CardAhead + Vector3.down * CardBelow,
                                                  Quaternion.LookRotation(forward, Vector3.up));
            card.transform.localScale = baseScale * CardBigger;
        }
        card.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
    }

    public void Hide()
    {
        if (card != null)
            card.SetActive(false);
    }

    /// The card prefab (CardPrefabBuilder), made on the first Show: its name line is the heading, the
    /// line under it the body, and one button.
    private bool BuildCard()
    {
        if (card != null)
            return true;
        if (failed)
            return false;
        view = CardView.Create(CardResource, parent);
        if (view == null)
        {
            failed = true;
            return false;
        }
        card = view.gameObject;
        baseScale = card.transform.localScale;
        heading = view.Heading;
        body = view.Body;
        view.Clicked += () => clicked = true;
        SetButton("Next  <size=75%>(A)</size>");
        OpenXRMenuInputModule.RegisterPanel(card);   // its button takes the laser, as the next-ball card's
        return true;
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
}
