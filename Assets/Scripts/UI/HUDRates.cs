using TMPro;
using UnityEngine;

/// <summary>
/// A bar across the bottom of the floating scorecard with the rates (2026-11-06):
///   chasing        "RRR 9.40   CRR 7.20   ·   47 needed off 30 balls"
///   batting first  "CRR 7.20   ·   at 7.2: 144   at 9.2: 164   at 5.2: 124" - the total if the
///                  innings goes on at the current rate, two an over quicker, and two slower.
/// Nothing in a Test (no over limit to project to) or in the nets.
///
/// The bar, its backing and the room made for it (the board taller at its bottom, under both
/// columns) are placed in CricketVR.unity by CricketVR > Scene Objects > Place HUD Rates; until
/// 2026-11-15 this component made them when the match scene loaded. It only sets the text and
/// shows or hides the bar.
/// </summary>
public class HUDRates : MonoBehaviour
{
    private const float Refresh = 0.2f;

    [Tooltip("The bar's row (RowRates under the scorecard's Score). Shown only when there is a line to say.")]
    [SerializeField]
    private GameObject bar;
    [Tooltip("The bar's text (txtRates).")]
    [SerializeField]
    private TMP_Text text;
    private float nextAt;

    private void LateUpdate()
    {
        if (text == null || bar == null || Time.time < nextAt)
            return;
        nextAt = Time.time + Refresh;
        HUD hud = HUD.Instance;
        string line = hud != null ? Line(hud.Runs, hud.Overs * 6 + hud.Balls, hud.TotalOvers, MatchFlow.Target) : null;
        bar.SetActive(!string.IsNullOrEmpty(line));
        if (!string.IsNullOrEmpty(line))
            text.text = line;
    }

    /// The bar's line, or null for none: a chase's required and current rates and the equation;
    /// setting a total, the current rate and the total at it, two quicker and two slower. Pure.
    public static string Line(int runs, int balls, int totalOvers, int target)
    {
        if (totalOvers <= 0 || FormatRules.IsTest(totalOvers))
            return null;
        int total = totalOvers * 6, left = Mathf.Max(0, total - balls);
        float crr = balls > 0 ? runs * 6f / balls : 0f;
        const string Grey = "<color=#98A2B3>", End = "</color>";
        if (target > 0)
        {
            int need = Mathf.Max(0, target - runs);
            string rrr = left > 0 ? (need * 6f / left).ToString("0.00") : "-";
            return $"{Grey}RRR{End} {rrr}   {Grey}CRR{End} {crr:0.00}   {Grey}·{End}   {need} needed off {left} ball{(left == 1 ? "" : "s")}";
        }
        if (balls < 6)
            return $"{Grey}CRR{End} {crr:0.00}   {Grey}·   projected totals after the first over{End}";
        float overs = left / 6f;
        string At(float rate) => $"{Grey}at {Mathf.Max(0f, rate):0.0}:{End} {Mathf.RoundToInt(runs + Mathf.Max(0f, rate) * overs)}";
        return $"{Grey}CRR{End} {crr:0.00}   {Grey}·{End}   {At(crr)}   {At(crr + 2f)}   {At(crr - 2f)}";
    }
}
