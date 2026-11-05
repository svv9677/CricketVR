using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career player's measured ratings ("careerRatings", from the hub): the seven ratings and the
/// overall in two columns, each a bar, the number and its grade - or "--" while there are too few
/// balls of its kind to say - then the coach's one-line verdict and, muted, what the nets trial
/// first measured. The ratings only describe how the player bats; nothing here changes how the game
/// plays. Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CareerRatingsScreen : MenuScreen
{
    private const string Muted = "#98A2B3";

    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private Image[] bars = new Image[0];
    [SerializeField] private TextMeshProUGUI[] values = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI verdict;
    [SerializeField] private TextMeshProUGUI trial;

    public override void OnShow()
    {
        CareerProfile p = CareerSaves.Current;
        if (p == null)
        {
            Set(subtitle, "No career loaded.");
            ShowRatings(new RatingSet());
            Set(verdict, "");
            Set(trial, "");
            return;
        }
        RatingSet r = p.ratings ?? new RatingSet();
        Set(subtitle, r.sample > 0
            ? $"{p.name}  ·  measured from your last {r.sample} balls"
            : $"{p.name}  ·  measured from every ball you face");
        ShowRatings(r);
        Set(verdict, Verdict(p));
        Set(trial, TrialLine(p));
    }

    private void ShowRatings(RatingSet r)
    {
        for (int i = 0; i < CareerHubScreen.Cells; i++)
        {
            float rating = i < RatingSet.Count ? r[i] : r.Overall;
            bool known = RatingSet.Known(rating);
            if (i < values.Length && values[i] != null)
                values[i].text = Value(rating);
            if (i < bars.Length && bars[i] != null)
            {
                float t = known ? Mathf.Clamp01(rating / 99f) : 0f;
                bars[i].enabled = t > 0f;
                bars[i].rectTransform.anchorMax = new Vector2(t, 1f);
            }
        }
    }

    /// "54  Solid" (the grade small and muted), or a muted "--".
    public static string Value(float rating) =>
        RatingSet.Known(rating)
            ? $"<b>{Round(rating)}</b>  <size=20><color={Muted}>{CareerRatings.Grade(rating)}</color></size>"
            : $"<color={Muted}>--</color>";

    /// "Trial ratings: Overall 32 from 30 balls"; before the trial, what it is for.
    public static string TrialLine(CareerProfile p)
    {
        if (p == null || !p.trialDone)
            return $"Your nets trial ({NetsSession.TrialBalls} balls) sets your starting ratings.";
        RatingSet t = p.trialRatings;
        float overall = t != null ? t.Overall : RatingSet.Unknown;
        if (!RatingSet.Known(overall))
            return "Trial ratings: too few balls to say.";
        int balls = t.sample > 0 ? t.sample : NetsSession.TrialBalls;
        return $"Trial ratings: Overall {Round(overall)} from {balls} balls";
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

    private static string Round(float v) => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
