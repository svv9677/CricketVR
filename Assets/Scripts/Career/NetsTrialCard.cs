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
/// The card is the NetsTrialCard prefab and the progress line the NetsTrialProgress prefab
/// (Resources/Prefabs/UI, saved by Scene Objects > Build Nets Trial Prefabs), in the next-ball card's
/// look like MatchFlow's result card, so it works like every other card - the laser clicks it, A
/// continues. Each is instantiated under the NetsTrial object the first time it is shown, so it
/// goes with the scene. All text is Latin-1 (the TMP default font has nothing else); text from
/// elsewhere goes through Latin1 first.
/// </summary>
public class NetsTrialCard
{
    /// The progress line: metres ahead (toward the bowler), to the side, and above the eyes.
    // Near enough to read at a glance and on the leg side of the dock, out of the ball's line (it
    // stood 7 m down the pitch, small, and against the side net once the nets were 6 m wide).
    private const float ProgressAhead = 3f, ProgressSide = -1.8f, ProgressUp = 0.25f;
    /// The result card, as MatchFlow places its own.
    private const float CardAhead = 1.5f, CardBelow = 0.1f, CardBigger = 1.4f;
    /// How far (m, level) a MenuLayout eye may be from the camera and still be used.
    private const float MaxLayoutDrift = 1.5f;
    private const string CardResource = "Prefabs/UI/NetsTrialCard";
    private const string ProgressResource = "Prefabs/UI/NetsTrialProgress";

    private readonly Transform parent;
    private TextMeshPro progress;
    /// A dark panel behind the progress and the last ball's reading, so the lines read against the
    /// stands and the net (2026-11-06: grey on a busy stadium was hard to make out). Part of the
    /// progress prefab; sized to the text here.
    private SpriteRenderer progressBack;
    private const float BackPadding = 0.12f;
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
    public const string PracticeHeading = "Practice done";

    /// Under the progress: the last ball's reading (ShotReading.Read, already rich text), and how
    /// to finish (B opens the pause card).
    public static string Footer(string reading) =>
        "\n<size=58%>" + (string.IsNullOrEmpty(reading) ? "" : Latin1(reading) + "\n") +
        "<size=50%><color=#98A2B3>B, then End session, to finish</color></size></size>";

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
            progress = MakeProgress();
        if (progress == null)
            return;
        progress.text = text;
        FitBack();
        SetProgressVisible(progressPlaced && !progressHidden);
    }

    /// The progress prefab under the nets' object, hidden until placed.
    private TextMeshPro MakeProgress()
    {
        GameObject prefab = Resources.Load<GameObject>(ProgressResource);
        if (prefab == null)
        {
            Debug.LogError($"[NetsTrialCard] No prefab at Resources/{ProgressResource}: run CricketVR > Scene Objects > Build Nets Trial Prefabs.");
            return null;
        }
        GameObject go = UnityEngine.Object.Instantiate(prefab, parent);
        go.name = prefab.name;
        go.SetActive(false);
        progressBack = go.GetComponentInChildren<SpriteRenderer>(true);
        return go.GetComponent<TextMeshPro>();
    }

    /// The backing panel sized to the text as it renders, just behind it.
    private void FitBack()
    {
        if (progressBack == null)
            return;
        progress.ForceMeshUpdate();
        Bounds b = progress.textBounds;
        progressBack.transform.localPosition = new Vector3(b.center.x, b.center.y, 0.02f);
        progressBack.transform.localScale = new Vector3(b.size.x + 2f * BackPadding, b.size.y + 2f * BackPadding, 1f);
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
        if (!BuildCard())
            return;
        CardText.Set(heading, headingText);
        CardText.Set(body, bodyText);
        if (Eye(true, out Vector3 eye, out Vector3 forward))
        {
            card.transform.SetPositionAndRotation(eye + forward * CardAhead + Vector3.down * CardBelow,
                                                  Quaternion.LookRotation(forward, Vector3.up));
            card.transform.localScale = baseScale * CardBigger;
        }
        card.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)card.transform);
    }

    public void HideResult()
    {
        if (card != null)
            card.SetActive(false);
    }

    /// The card prefab: the heading, the result lines and one button, Continue.
    private bool BuildCard()
    {
        if (card != null)
            return true;
        CardView view = CardView.Create(CardResource, parent);
        if (view == null)
            return false;
        card = view.gameObject;
        baseScale = card.transform.localScale;
        heading = view.Heading;
        body = view.Body;
        view.Clicked += () => onContinue?.Invoke();
        OpenXRMenuInputModule.RegisterPanel(card);
        return true;
    }

    // ---- Helpers --------------------------------------------------------------------------------

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
