using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career hub for the active slot (CareerSaves.Current): the player card, the seven measured
/// ratings and the overall in two columns (a bar, the number and its grade - or "--" while there
/// are too few balls to say), the coach's one-line verdict, and the way into the nets: the 30-ball
/// trial first, measured practice after it. The ratings only describe how the player bats; nothing
/// here changes how the game plays. Coming back from the trial it says so once (NetsSession.
/// TrialResult). Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CareerHubScreen : MenuScreen
{
    /// The rating cells: RatingSet's seven, then the overall.
    public const int Cells = RatingSet.Count + 1;
    private const string Muted = "#98A2B3", Good = "#22C55E";

    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI playerDetail;
    [SerializeField] private Image kitBar;
    [SerializeField] private TextMeshProUGUI stage;
    [SerializeField] private TextMeshProUGUI season;
    [SerializeField] private TextMeshProUGUI banner;
    [SerializeField] private Image[] bars = new Image[0];
    [SerializeField] private TextMeshProUGUI[] values = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI verdict;
    [SerializeField] private Button primary;
    [SerializeField] private TextMeshProUGUI primaryLabel;

    private bool showedTrial;

    private void OnEnable() => CareerSaves.Saved += Refresh;

    private void OnDisable()
    {
        CareerSaves.Saved -= Refresh;
        ForgetTrial();
    }

    public override void OnShow() => Refresh();

    public override void OnHide() => ForgetTrial();

    /// The trial's result is said once: forget it as the hub goes (to the slots, or to the nets).
    private void ForgetTrial()
    {
        if (!showedTrial)
            return;
        showedTrial = false;
        NetsSession.TrialResult = null;
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        if (primary != null) primary.interactable = p != null;
        if (p == null)
        {
            Set(playerName, "No career loaded");
            Set(playerDetail, "Pick a slot on the career screen.");
            if (banner != null) banner.gameObject.SetActive(false);
            ShowRatings(new RatingSet());
            Set(verdict, "");
            return;
        }
        Set(playerName, p.name);
        Set(playerDetail, $"#{p.look.number}  ·  {Nations.Name(p.nation)}  ·  {p.HandName}");
        if (kitBar != null)
            kitBar.color = Appearance.KitColours[Mathf.Clamp(p.look.kit, 0, Appearance.KitColours.Length - 1)];
        Set(stage, "Club cricket");
        Set(season, "Season 1");
        ShowTrialBanner();
        ShowRatings(p.ratings ?? new RatingSet());
        Set(verdict, Verdict(p));
        Set(primaryLabel, p.trialDone ? "Measured nets practice" : $"Take your nets trial · {NetsSession.TrialBalls} balls");
    }

    private void ShowTrialBanner()
    {
        RatingSet result = NetsSession.TrialResult;
        if (banner == null)
            return;
        banner.gameObject.SetActive(result != null);
        if (result == null)
            return;
        showedTrial = true;
        float overall = result.Overall;
        banner.text = RatingSet.Known(overall)
            ? $"<color={Good}>Trial complete.</color>  Your starting overall is <b>{Mathf.RoundToInt(overall)}</b>."
            : $"<color={Good}>Trial complete.</color>  Keep batting and your ratings will fill in.";
    }

    private void ShowRatings(RatingSet r)
    {
        for (int i = 0; i < Cells; i++)
        {
            float rating = i < RatingSet.Count ? r[i] : r.Overall;
            bool known = RatingSet.Known(rating);
            if (i < values.Length && values[i] != null)
                values[i].text = known
                    ? $"<b>{Mathf.RoundToInt(rating)}</b>  <size=20><color={Muted}>{CareerRatings.Grade(rating)}</color></size>"
                    : $"<color={Muted}>--</color>";
            if (i < bars.Length && bars[i] != null)
            {
                float t = known ? Mathf.Clamp01(rating / 99f) : 0f;
                bars[i].enabled = t > 0f;
                bars[i].rectTransform.anchorMax = new Vector2(t, 1f);
            }
        }
    }

    private static string Verdict(CareerProfile p)
    {
        string coach = CareerRatings.Verdict(p.ratings);
        if (!string.IsNullOrEmpty(coach))
            return $"<color={Muted}>Coach:</color>  {coach}";
        return p.trialDone
            ? $"<color={Muted}>Coach:</color>  Bat some more and I will tell you what I see."
            : $"<color={Muted}>Coach:</color>  Show me {NetsSession.TrialBalls} balls in the nets and your ratings start from how you really bat.";
    }

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// The trial until it is done, then measured practice.
    public void OnPrimary()
    {
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        ForgetTrial();
        if (p.trialDone)
            NetsSession.BeginPractice();
        else
            NetsSession.BeginTrial();
    }
}
