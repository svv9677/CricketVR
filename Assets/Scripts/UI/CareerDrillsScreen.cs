using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career nets ("careerDrills", from the hub's Nets button): a header line (the training slot
/// and the coach's pick, DrillText.Header), Measured practice - free nets where every ball still
/// counts - and the five 12-ball drills (DrillDefs.All, in its order), each a row with its name, a
/// pink COACH'S PICK on the one the coach recommends, its last and best scores (DrillText.RowDetail,
/// always asked with coachPick false: this screen owns the marker, on the name line) and Start. Everything is locked until the nets trial is done: the trial sets the ratings the
/// drills build on. Start begins a new attempt (DrillCoach.Start, saved before the nets load, so a
/// restart of the scene bowls the same shuffled plan) and goes to the nets. The ratings only
/// measure: a drill bowls the balls that test one or two of them, nothing more. There is no scroll
/// view, so the card holds exactly these six rows. Built by Tools > CricketVR > Build Menu Prefabs.
///
/// 2026-11-06, as floating panels (CareerDeckScreen; CareerDeckTraining builds it): the coach and
/// measured practice on the left, the drills as a grid of tiles in the middle (name and best), and
/// the chosen drill on the right - what it is, what it feeds, last and best, and Start. A tile
/// chooses; Start goes. The coach's pick is chosen when the screen opens.
/// </summary>
public class CareerDrillsScreen : CareerDeckScreen
{
    public const int Rows = DrillDefs.Count;
    public const string LockedText = "Take your nets trial first. It sets the ratings the drills build on.";
    public const string PracticeName = "Measured practice";
    public const string PracticeDetail = "Free nets, as long as you like. Every ball is measured.";
    private const string Pink = "#F43F8E";

    [SerializeField] private TextMeshProUGUI header;
    [SerializeField] private TextMeshProUGUI locked;
    [SerializeField] private TextMeshProUGUI[] names = new TextMeshProUGUI[0];
    [SerializeField] private TextMeshProUGUI[] details = new TextMeshProUGUI[0];
    [SerializeField] private Button[] starts = new Button[0];
    // The drill rows themselves, switched off for a drill DrillDefs does not have.
    [SerializeField] private GameObject[] rows = new GameObject[0];
    [SerializeField] private Button practice;
    // The chosen drill's panel.
    [SerializeField] private TextMeshProUGUI chosenTitle;
    [SerializeField] private TextMeshProUGUI chosenPick;
    [SerializeField] private TextMeshProUGUI chosenBlurb;
    [SerializeField] private TextMeshProUGUI chosenStats;
    [SerializeField] private Button chosenStart;

    private int chosen;
    private static readonly Color TileColour = new Color32(0x25, 0x2B, 0x35, 0xFF), ChosenColour = new Color32(0x3B, 0x82, 0xF6, 0xFF);

    // A Start was pressed and the nets are loading: a second press must not begin a second attempt.
    private bool leaving;

    private void OnEnable() => CareerSaves.Saved += Refresh;

    private void OnDisable() => CareerSaves.Saved -= Refresh;

    public override void OnShow()
    {
        base.OnShow();
        leaving = false;
        int pick = System.Array.FindIndex(DrillDefs.All, d => d.id == CoachPick(CareerSaves.Current));
        chosen = pick >= 0 ? pick : 0;
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        bool open = CanStart(p);
        eDrill pick = CoachPick(p);
        Set(header, p == null ? "No career loaded" : DrillText.Header(p));
        if (locked != null)
        {
            locked.gameObject.SetActive(!open);
            locked.text = LockedText;
        }
        if (practice != null)
            practice.interactable = open;
        for (int i = 0; i < Rows; i++)
            ShowRow(i, DrillAt(i), p, pick, open);
        ShowChosen(p, pick, open);
    }

    /// The chosen drill's panel, and its tile lit.
    private void ShowChosen(CareerProfile p, eDrill pick, bool open)
    {
        DrillDef d = DrillAt(chosen);
        for (int i = 0; i < starts.Length; i++)
            if (starts[i] != null)
            {
                ColorBlock c = starts[i].colors;
                c.normalColor = i == chosen ? ChosenColour : TileColour;
                c.selectedColor = c.normalColor;
                starts[i].colors = c;
            }
        Set(chosenTitle, d != null ? NetsTrialCard.Latin1(d.name) : "");
        if (chosenPick != null)
            chosenPick.gameObject.SetActive(d != null && d.id == pick);
        Set(chosenBlurb, d != null ? GoalText(d) : "");
        Set(chosenStats, d == null ? "" : ChosenStats(d, p != null ? RecordOf(p.drills, d.id) : null));
        if (chosenStart != null)
            chosenStart.interactable = open && d != null;
    }

    private void ShowRow(int i, DrillDef d, CareerProfile p, eDrill pick, bool open)
    {
        if (i < rows.Length && rows[i] != null)
            rows[i].SetActive(d != null);
        if (d == null)
            return;
        bool coachPick = d.id == pick;
        Set(At(names, i), TileName(d, coachPick));
        Set(At(details, i), p == null ? "" : TileDetail(RecordOf(p.drills, d.id)));
        Button start = At(starts, i);
        if (start != null)
            start.interactable = open;
    }

    private static T At<T>(T[] items, int i) where T : class => items != null && i >= 0 && i < items.Length ? items[i] : null;

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Pure (CareerDrillsScreenTests) ----------------------------------------------------------

    /// The drills and measured practice open once the trial is done.
    public static bool CanStart(CareerProfile p) => p != null && p.trialDone;

    /// The drill the coach recommends from the measured ratings; None before the trial (nothing to
    /// recommend from) or with no career.
    public static eDrill CoachPick(CareerProfile p) =>
        CanStart(p) && p.ratings != null ? DrillCoach.Recommend(p.ratings) : eDrill.None;

    /// Row `index`'s drill (DrillDefs.All's order), or null past the end.
    public static DrillDef DrillAt(int index)
    {
        DrillDef[] all = DrillDefs.All;
        return all != null && index >= 0 && index < all.Length ? all[index] : null;
    }

    /// The drill's record in the book, or a blank one when it has never been started. Only read:
    /// showing the screen must not add records to the save (DrillBook.For would).
    public static DrillRecord RecordOf(DrillBook book, eDrill d)
    {
        DrillRecord r = book?.records?.Find(x => x != null && x.drill == d);
        return r ?? new DrillRecord { drill = d };
    }

    /// "Yorkers", bold, with a pink "· COACH'S PICK" after the one the coach recommends.
    public static string RowName(DrillDef d, bool coachPick)
    {
        string name = $"<b>{NetsTrialCard.Latin1(d?.name)}</b>";
        return coachPick ? $"{name}  <color={Pink}><size=80%>· COACH'S PICK</size></color>" : name;
    }

    /// A tile's name: bold, with a small pink PICK on the coach's pick.
    public static string TileName(DrillDef d, bool coachPick) =>
        $"<b>{NetsTrialCard.Latin1(d?.name)}</b>" + (coachPick ? $"  <color={Pink}><size=70%>PICK</size></color>" : "");

    /// A tile's small line: "Best 72", or "New" for one never finished.
    public static string TileDetail(DrillRecord r) =>
        r == null || r.attempts <= 0 ? "New" : $"Best {r.best}";

    /// The chosen panel's goal, in place of the drill's blurb (2026-11-06: "a clear goal for each
    /// drill and a better criteria"): "Goal: dig out 8 of 12 yorkers" over what makes a ball count.
    public static string GoalText(DrillDef d)
    {
        DrillGoals.Goal g = DrillGoals.For(d.id);
        if (g.text.Length == 0)
            return NetsTrialCard.Latin1(d.blurb);
        return NetsTrialCard.Latin1($"<b>Goal: {g.text}</b>\n<size=85%>A ball counts when you {LowerFirst(g.rule)}.</size>");
    }

    private static string LowerFirst(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

    /// The chosen panel's lines: what it feeds, then last and best (or not tried).
    public static string ChosenStats(DrillDef d, DrillRecord r)
    {
        string tried = r == null || r.attempts <= 0 ? "Not tried yet"
                     : $"Last {r.last}  ·  Best {r.best}  ·  {r.attempts} attempt{(r.attempts == 1 ? "" : "s")}";
        return NetsTrialCard.Latin1(DrillText.Feeds(d) + "\n" + tried + $"\n{DrillDefs.BallsPerDrill} balls");
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// A tile: choose that drill (Start begins it).
    public void OnChoose(int index)
    {
        if (DrillAt(index) == null)
            return;
        chosen = index;
        Refresh();
    }

    /// The chosen drill's Start.
    public void OnStartChosen() => OnStart(chosen);


    /// Measured practice: the free nets with every ball measured (NetsSession Practice).
    public void OnPractice()
    {
        if (leaving || !CanStart(CareerSaves.Current))
            return;
        leaving = true;
        NetsSession.BeginPractice();
    }

    /// Row `index`'s drill: a new attempt, saved, then the nets.
    public void OnStart(int index)
    {
        CareerProfile p = CareerSaves.Current;
        DrillDef d = DrillAt(index);
        if (leaving || !CanStart(p) || d == null)
            return;
        leaving = true;
        int attempt = DrillCoach.Start(p, d.id);
        CareerSaves.SaveCurrent();
        NetsSession.BeginDrill(d.id, attempt, p.ratings);
    }
}
