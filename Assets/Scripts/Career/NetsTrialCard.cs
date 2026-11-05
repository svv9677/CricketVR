using System.Collections.Generic;
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What the career player sees in the nets (NetsTrial):
///   * a small progress line off to the side of the net, toward the bowler - "NETS TRIAL  Ball 12
///     of 30" and the kind of bowler now on ("Off-spinner"), or the balls measured in practice;
///   * when the trial is over, the result card: the seven ratings ("Timing  62  Strong", "--" for
///     one not measured yet), the coach's verdict and "Press A to continue".
///
/// The card is a copy of the next-ball card's panel (as MatchFlow's result card is), so it looks and
/// works like every other card - the laser clicks it, A continues. A scene without that panel gets
/// the same words as plain floating text instead of no result at all.
///
/// Everything is built at runtime under the NetsTrial object, so it goes with the scene. All text is
/// Latin-1 (the TMP default font has nothing else); text from elsewhere goes through Latin1 first.
/// </summary>
public class NetsTrialCard
{
    /// The progress line: metres ahead (toward the bowler), to the side, and above the eyes.
    private const float ProgressAhead = 7f, ProgressSide = 2.8f, ProgressUp = 0.7f, ProgressSize = 3f;
    /// The result card, as MatchFlow places its own.
    private const float CardAhead = 1.5f, CardBelow = 0.1f, CardBigger = 1.4f;
    /// The plain-text fallback for the card.
    private const float FallbackAhead = 2.2f, FallbackSize = 0.9f;
    /// How far (m, level) a MenuLayout eye may be from the camera and still be used.
    private const float MaxLayoutDrift = 1.5f;
    /// Sorting order above the nets' scoreboard canvas (10), which otherwise draws over anything
    /// nearer the eye.
    private const int OverTheScoreboard = 20;
    /// The result body's width (card px).
    private const float BodyWidth = 640f;
    /// The heading ("Trial complete"), card px.
    private const float HeadingSize = 40f;

    private readonly Transform parent;
    private TextMeshPro progress, fallback;
    private GameObject card;
    private TMP_Text heading, body;
    private Vector3 baseScale;
    private Action onContinue;
    private bool progressPlaced, progressHidden;

    public NetsTrialCard(Transform parent) => this.parent = parent;

    // ---- The words (pure: NetsTrialTests checks them) -------------------------------------------

    /// "Off-spinner", for the progress line.
    public static string KindLabel(eBowlerKind k) => k switch
    {
        eBowlerKind.Fast => "Fast bowler",
        eBowlerKind.Medium => "Medium-pacer",
        eBowlerKind.OffSpin => "Off-spinner",
        eBowlerKind.LegSpin => "Leg-spinner",
        _ => "Mystery spinner",
    };

    /// ballNumber is 1-based: the ball about to be bowled.
    public static string ProgressText(int ballNumber, eBowlerKind k) =>
        $"<b>NETS TRIAL</b>  Ball {Mathf.Clamp(ballNumber, 1, NetsSession.TrialBalls)} of {NetsSession.TrialBalls}\n" +
        $"<size=70%><color=#98A2B3>{KindLabel(k)}</color></size>";

    public static string PracticeText(int measured) =>
        $"<b>NETS PRACTICE</b>\n<size=70%><color=#98A2B3>{measured} ball{(measured == 1 ? "" : "s")} measured</color></size>";

    public const string ResultHeading = "Trial complete";

    /// "Timing  62  Strong", or "Timing  --" when it is not measured yet.
    public static string RatingLine(int index, float rating)
    {
        string name = RatingSet.Names[Mathf.Clamp(index, 0, RatingSet.Count - 1)];
        if (!RatingSet.Known(rating))
            return $"{name}<pos=55%>--";
        return $"{name}<pos=55%>{Mathf.RoundToInt(rating)}<pos=70%>{Latin1(CareerRatings.Grade(rating))}";
    }

    /// The seven ratings, the verdict, and how to go on.
    public static string ResultBody(RatingSet r, string verdict)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < RatingSet.Count; i++)
            sb.Append(RatingLine(i, r != null ? r[i] : RatingSet.Unknown)).Append('\n');
        if (!string.IsNullOrEmpty(verdict))
            sb.Append("<size=80%>").Append(Latin1(verdict)).Append("</size>\n");
        sb.Append("<size=70%><color=#98A2B3>Press A to continue</color></size>");
        return sb.ToString();
    }

    /// The TMP default font is Latin-1 only: the usual typographic marks become their plain forms,
    /// anything else beyond Latin-1 a '?', rather than an empty box on the headset.
    public static string Latin1(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s ?? "";
        StringBuilder sb = null;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c <= '\u00FF')
            {
                sb?.Append(c);
                continue;
            }
            sb ??= new StringBuilder(s, 0, i, s.Length);
            sb.Append(PlainForm(c));
        }
        return sb != null ? sb.ToString() : s;
    }

    private static string PlainForm(char c)
    {
        switch (c)
        {
            case '\u2018':
            case '\u2019': return "'";
            case '\u201C':
            case '\u201D': return "\"";
            case '\u2013':
            case '\u2014': return "-";
            case '\u2026': return "...";
            default: return "?";
        }
    }

    // ---- Progress line --------------------------------------------------------------------------

    /// The words now; it shows once it has been placed (not at the origin before the first ball).
    public void ShowProgress(string text)
    {
        if (progress == null)
            progress = MakeText("Nets Trial Progress", ProgressSize, new Vector2(10f, 2.4f));
        progress.text = text;
        SetProgressVisible(progressPlaced && !progressHidden);
    }

    /// Off to the side of the net toward the bowler, facing the player: readable at a glance between
    /// balls, out of the line of the ball. Called between balls, once the player is at the crease.
    public void PlaceProgress()
    {
        if (progress == null || !Eye(false, out Vector3 eye, out Vector3 forward))
            return;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 at = eye + forward * ProgressAhead + right * ProgressSide + Vector3.up * ProgressUp;
        progress.transform.SetPositionAndRotation(at, Facing(at, eye));
        progressPlaced = true;
        SetProgressVisible(!progressHidden);
    }

    /// For good (the trial is over).
    public void HideProgress()
    {
        progressHidden = true;
        SetProgressVisible(false);
    }

    private void SetProgressVisible(bool on)
    {
        if (progress != null && progress.gameObject.activeSelf != on)
            progress.gameObject.SetActive(on);
    }

    // ---- Result card ----------------------------------------------------------------------------

    /// Show the result; onContinue runs when its button is clicked (A is NetsTrial's).
    public void ShowResult(string headingText, string bodyText, Action continueAction)
    {
        onContinue = continueAction;
        if (BuildCard())
        {
            heading.text = $"<color=#22C55E>{headingText}</color>";
            body.text = bodyText;
            if (Eye(true, out Vector3 eye, out Vector3 forward))
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
            fallback = MakeText("Nets Trial Result", FallbackSize, new Vector2(3.2f, 3f));
        fallback.text = $"<b><color=#22C55E>{headingText}</color></b>\n{bodyText}";
        if (Eye(true, out Vector3 e, out Vector3 f))
        {
            Vector3 at = e + f * FallbackAhead;
            fallback.transform.SetPositionAndRotation(at, Facing(at, e));
        }
        fallback.gameObject.SetActive(true);
    }

    public void HideResult()
    {
        if (card != null)
            card.SetActive(false);
        if (fallback != null)
            fallback.gameObject.SetActive(false);
    }

    /// A copy of the next-ball card's panel with the result and one button, Continue (the same
    /// surgery as MatchFlow.BuildCard: keep the heading, one label row for the body, the Bowl button).
    private bool BuildCard()
    {
        if (card != null)
            return body != null && heading != null;
        NextBallMenu menu = UnityEngine.Object.FindFirstObjectByType<NextBallMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.Panel == null)
            return false;
        card = UnityEngine.Object.Instantiate(menu.Panel, parent);
        card.name = "Nets Trial Result Card";
        baseScale = card.transform.localScale;
        card.SetActive(false);
        Transform replay = card.transform.Find("Replay");
        if (replay != null)
            UnityEngine.Object.Destroy(replay.gameObject);
        Transform play = card.transform.Find("Play");
        if (play == null)
            return false;
        // The play card: a name line, a line under it, Bowl, and a row of small buttons. The name
        // line becomes the heading, the line under it the body, Bowl the Continue button; the row goes.
        var doomed = new List<GameObject>();
        foreach (Transform child in play)
        {
            TMP_Text text = child.GetComponent<TMP_Text>();
            if (child.name.StartsWith("Bowl"))
                WireContinueButton(child);
            else if (text != null && heading == null)
            {
                heading = text;
                heading.enableAutoSizing = false;
                heading.fontSize = HeadingSize;
                heading.overflowMode = TextOverflowModes.Overflow;
            }
            else if (text != null && body == null)
                MakeBody(text);
            else
                doomed.Add(child.gameObject);
        }
        foreach (GameObject g in doomed)
            UnityEngine.Object.Destroy(g);
        // Over the nets' scoreboard, which is drawn at sorting order 10 whatever its depth.
        Canvas canvas = card.GetComponent<Canvas>();
        if (canvas != null)
            canvas.sortingOrder = OverTheScoreboard;
        OpenXRMenuInputModule.RegisterPanel(card);
        return body != null && heading != null;
    }

    /// The line under the heading becomes the body: it grows to hold the seven ratings and a verdict
    /// that may wrap.
    private void MakeBody(TMP_Text text)
    {
        body = text;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.enableAutoSizing = false;
        body.overflowMode = TextOverflowModes.Overflow;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.fontSize = 30f;
        var layout = body.GetComponent<LayoutElement>();
        if (layout == null)
            layout = body.gameObject.AddComponent<LayoutElement>();
        {
            // Wide enough for "Temperament  62  Developing" on one line (the play card's line under
            // the name is sized for a few words); the verdict wraps inside it.
            layout.preferredHeight = 400f;
            layout.minWidth = layout.preferredWidth = BodyWidth;
        }
    }

    private void WireContinueButton(Transform bowl)
    {
        Button button = bowl.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => onContinue?.Invoke());
        }
        TMP_Text label = bowl.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = "Continue  <size=75%>(A)</size>";
    }

    // ---- Helpers --------------------------------------------------------------------------------

    /// Floating world-space text with a soft shadow (BallResultText's look), hidden until shown.
    private TextMeshPro MakeText(string name, float size, Vector2 rect)
    {
        var go = new GameObject(name);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        go.transform.SetParent(parent, false);
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = rect;
        t.color = Color.white;
        t.outlineWidth = 0f;
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

    /// The player's eye and the level way they face: the scene's MenuLayout if it has one; else the
    /// camera, facing down the pitch toward the bowler (XRRigSetup turns the player to -X) or, for
    /// gaze, where they are looking.
    private static bool Eye(bool gaze, out Vector3 eye, out Vector3 forward)
    {
        Camera cam = Camera.main;
        // The Nets scene's MenuLayout spot is the menu's default at the world origin, ten metres up
        // the pitch from the crease: trust a layout eye only where the player actually is.
        if (MenuLayout.TryGetEye(out eye, out forward) &&
            (cam == null || Vector3.ProjectOnPlane(eye - cam.transform.position, Vector3.up).magnitude < MaxLayoutDrift))
            return true;
        if (cam == null)
            return false;
        eye = cam.transform.position;
        forward = gaze ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.left;
        forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.left;
        return true;
    }

    /// Turned about the vertical to face the eye (TMP text reads from its -Z side).
    private static Quaternion Facing(Vector3 at, Vector3 eye)
    {
        Vector3 away = at - eye;
        away.y = 0f;
        return away.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(away, Vector3.up) : Quaternion.identity;
    }
}
