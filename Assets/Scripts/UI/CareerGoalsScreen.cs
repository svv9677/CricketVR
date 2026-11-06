using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The coach's goals for the season ("careerGoals", from the hub, phase 7): the season in the
/// subtitle, then up to three goals, each its text, its progress (CoachGoalsText.Value) and a bar
/// (CoachGoalsText.Fraction); before the trial or between seasons a line says why there are none.
/// Under them, the goals reached in the whole career, which unlock kit in the Locker (Back | Locker).
/// The goals only measure: nothing here changes how the game plays. Built by Tools > CricketVR >
/// Build Menu Prefabs (CareerGoalsMenuBuilder).
/// </summary>
public class CareerGoalsScreen : CareerDeckScreen
{
    public const int Rows = 3;

    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private TextMeshProUGUI empty;
    [SerializeField] private TextMeshProUGUI career;
    [SerializeField] private GameObject[] rows = new GameObject[0];
    [SerializeField] private TextMeshProUGUI[] labels = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI[] values = new TextMeshProUGUI[0];
    [SerializeField] private Image[] bars = new Image[0];

    private void OnEnable() => CareerSaves.Saved += Refresh;

    private void OnDisable() => CareerSaves.Saved -= Refresh;

    public override void OnShow()
    {
        base.OnShow();
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        Set(subtitle, CoachGoalsText.Subtitle(p));
        string none = CoachGoalsText.Empty(p);
        if (empty != null)
        {
            empty.text = none;
            empty.gameObject.SetActive(none.Length > 0);
        }
        Set(career, CoachGoalsText.CareerLine(p));
        var goals = none.Length > 0 || p?.season?.goals == null ? null : p.season.goals.FindAll(g => g != null);
        for (int i = 0; i < Rows; i++)
            ShowRow(i, goals != null && i < goals.Count ? goals[i] : null);
    }

    private void ShowRow(int i, SeasonGoal g)
    {
        GameObject row = At(rows, i);
        if (row != null)
            row.SetActive(g != null);
        if (g == null)
            return;
        Set(At(labels, i), g.text);
        Set(At(values, i), CoachGoalsText.Value(g));
        Image bar = At(bars, i);
        if (bar != null)
        {
            float t = CoachGoalsText.Fraction(g);
            bar.enabled = t > 0f;
            bar.rectTransform.anchorMax = new Vector2(t, 1f);
        }
    }

    /// The Locker, from the button beside Back.
    public void OnLocker() { if (Root != null) Root.Push("careerLocker"); }

    private static T At<T>(T[] items, int i) where T : class => items != null && i >= 0 && i < items.Length ? items[i] : null;

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value ?? "";
    }
}
