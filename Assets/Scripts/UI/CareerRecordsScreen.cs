using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career's records ("careerRecords", from Ratings, phase 7): a header line with the career
/// figures (HeaderLine), then six rows in the drills screen's shape - Highest score, Fastest fifty,
/// Best strike rate, Most sixes, Longest six and Milestones - each its name and value over a muted
/// line saying where it came from (RowDetail), a note that a six's distance is its carry off the
/// real blade, and Back. Watch, on the Longest six row only, opens the record's clip in the replay
/// theatre; it is on only when the clip is saved (ReplayVault.Has) and stays off if the theatre
/// says no. The records only measure what he did. Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CareerRecordsScreen : CareerDeckScreen
{
    /// The record rows, in order; the Milestones row comes after them.
    public static readonly eRecord[] RecordRows =
        { eRecord.HighScore, eRecord.FastestFifty, eRecord.BestStrikeRate, eRecord.MostSixes, eRecord.LongestSix };
    public const int Rows = 6;
    /// Under the rows: why the six may be shorter than the shot card said (the card measures the live ball).
    public const string NoteText = "Longest six is the carry off the real blade, not the shield.";
    private const string Muted = "#98A2B3";
    private const string Sep = "  ·  ";
    // The header carries six figures on one 20 px line: single spaces keep a big career's inside the card.
    private const string HeaderSep = " · ";

    [SerializeField] private TextMeshProUGUI header;
    [SerializeField] private TextMeshProUGUI[] names = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI[] details = new TextMeshProUGUI[0];
    [SerializeField] private Button watch;

    // Watch was pressed and the theatre is loading: a second press must not start it again.
    private bool leaving;

    public override void OnShow()
    {
        base.OnShow();
        leaving = false;
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        CareerRecordBook book = p?.recordBook;
        Set(header, p == null ? "No career loaded" : HeaderLine(p.stats));
        for (int i = 0; i < Rows; i++)
        {
            Set(At(names, i), NameLine(i, book));
            Set(At(details, i), i < RecordRows.Length ? RowDetail(book?.Get(RecordRows[i])) : MilestoneDetail(book));
        }
        if (watch != null)
            watch.interactable = CanWatch(book, CareerSaves.Active);
    }

    private static TextMeshProUGUI At(TextMeshProUGUI[] items, int i) =>
        items != null && i >= 0 && i < items.Length ? items[i] : null;

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// The longest six's clip in the replay theatre (ReplayTheatre loads the match scene).
    public void OnWatch()
    {
        CareerRecordBook book = CareerSaves.Current?.recordBook;
        if (leaving || !CanWatch(book, CareerSaves.Active))
            return;
        if (ReplayTheatre.Begin(CareerSaves.Active, eRecord.LongestSix))
            leaving = true;
        else if (watch != null)
            watch.interactable = false;   // it cannot play now; the next visit asks again
    }

    // ---- Pure (CareerRecordsScreenTests) ------------------------------------------------------

    /// Watch is on when the longest six's clip is on disk for this slot.
    public static bool CanWatch(CareerRecordBook book, int slot) =>
        book?.longestSix != null && book.longestSix.set && ReplayVault.Has(slot, book.longestSix.clipStamp);

    /// The row's name ("Highest score", ..., "Milestones").
    public static string RowName(int row) => row switch
    {
        0 => "Highest score",
        1 => "Fastest fifty",
        2 => "Best strike rate",
        3 => "Most sixes",
        4 => "Longest six",
        _ => "Milestones",
    };

    /// The row's top line: the name bold, then its value ("--" muted when unset).
    public static string NameLine(int row, CareerRecordBook book)
    {
        string value = row < RecordRows.Length ? RowValue(RecordRows[row], book?.Get(RecordRows[row])) : MilestoneValue(book);
        string shown = value == "--" ? $"<color={Muted}>--</color>" : value;
        return $"<b>{RowName(row)}</b>   {shown}";
    }

    /// "112*", "23 balls", "245.5", "9", "104 m"; "--" while the record is unset.
    public static string RowValue(eRecord which, RecordEntry e)
    {
        if (e == null || !e.set)
            return "--";
        return which switch
        {
            eRecord.HighScore => $"{e.runs}{(e.notOut ? "*" : "")}",
            eRecord.FastestFifty => $"{Whole(e.value)} balls",
            eRecord.BestStrikeRate => e.value.ToString("0.0", CultureInfo.InvariantCulture),
            eRecord.MostSixes => Whole(e.value).ToString(CultureInfo.InvariantCulture),
            _ => $"{Whole(e.value)} m",
        };
    }

    /// Where the record came from: "64* off 41  ·  vs Harbour CC  ·  Club  ·  2026-11-12". One worked
    /// out from an older save may not know the match: "From your earlier matches". "" when unset.
    public static string RowDetail(RecordEntry e)
    {
        if (e == null || !e.set)
            return "";
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(e.opponent))
        {
            if (e.balls > 0)
                parts.Add($"{e.runs}{(e.notOut ? "*" : "")} off {e.balls}");
            parts.Add("vs " + NetsTrialCard.Latin1(e.opponent));
            if (e.season > 0)
                parts.Add(Ladder.Short(e.rung));
        }
        if (!string.IsNullOrEmpty(e.date))
            parts.Add(e.date);
        return parts.Count > 0 ? string.Join(Sep, parts) : "From your earlier matches";
    }

    /// How many milestones are reached ("0" with none).
    public static string MilestoneValue(CareerRecordBook book) =>
        (book?.reached?.Count ?? 0).ToString(CultureInfo.InvariantCulture);

    /// "Latest: 1,000 career runs  ·  2026-11-12", or what the milestones are before the first.
    public static string MilestoneDetail(CareerRecordBook book)
    {
        if (book == null || string.IsNullOrEmpty(book.lastMilestone))
            return "Fifties, hundreds, career runs and caps.";
        string latest = "Latest: " + NetsTrialCard.Latin1(book.lastMilestone);
        return string.IsNullOrEmpty(book.lastMilestoneDate) ? latest : latest + Sep + book.lastMilestoneDate;
    }

    /// "1,234 runs · avg 41.1 · SR 132 · 50s 7 · 100s 2 · 6s 41"; before his first innings,
    /// what the screen will hold.
    public static string HeaderLine(CareerStats st)
    {
        if (st == null || st.innings <= 0)
            return "No innings yet. Your bests start with your first match.";
        CultureInfo c = CultureInfo.InvariantCulture;
        string h = HeaderSep;
        return $"{st.runs.ToString("N0", c)} runs{h}avg {st.Average.ToString("0.0", c)}{h}SR {Whole(st.StrikeRate)}" +
               $"{h}50s {st.fifties}{h}100s {st.hundreds}{h}6s {st.sixes}";
    }

    /// The result screen's line naming the records a match broke ("" when none): both by name when
    /// there are two, else the first and how many more, so it fits one 22 px line of the card.
    public static string BestLine(List<eRecord> bests, CareerRecordBook book)
    {
        if (bests == null || bests.Count == 0 || book == null)
            return "";
        int name = bests.Count == 2 ? 2 : 1;   // two names plus "and N more" would run off the card
        var named = new List<string>();
        for (int i = 0; i < bests.Count && named.Count < name; i++)
            named.Add(BestName(bests[i], book.Get(bests[i])));
        string line = (bests.Count == 1 ? "New career best: " : "New career bests: ") + string.Join(", ", named);
        int more = bests.Count - named.Count;
        return more > 0 ? $"{line} and {more} more" : line;
    }

    /// "highest score 112*", "fastest fifty 23 balls", "strike rate 245.5", "most sixes 9", "longest six 104 m".
    private static string BestName(eRecord which, RecordEntry e)
    {
        string value = RowValue(which, e);
        return which switch
        {
            eRecord.HighScore => "highest score " + value,
            eRecord.FastestFifty => "fastest fifty " + value,
            eRecord.BestStrikeRate => "strike rate " + value,
            eRecord.MostSixes => "most sixes " + value,
            _ => "longest six " + value,
        };
    }

    private static int Whole(float v) => Mathf.RoundToInt(v);
}
