using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career nets ("careerDrills", from the hub's Nets button): a header line (the training slot
/// and the coach's pick, DrillText.Header), Measured practice - free nets where every ball still
/// counts - and the five 12-ball drills (DrillDefs.All, in its order), each a row with its name, a
/// pink COACH'S PICK on the one the coach recommends, its last and best scores (DrillText.RowDetail)
/// and Start. Everything is locked until the nets trial is done: the trial sets the ratings the
/// drills build on. Start begins a new attempt (DrillCoach.Start, saved before the nets load, so a
/// restart of the scene bowls the same shuffled plan) and goes to the nets. The ratings only
/// measure: a drill bowls the balls that test one or two of them, nothing more. There is no scroll
/// view, so the card holds exactly these six rows. Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CareerDrillsScreen : MenuScreen
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

    // A Start was pressed and the nets are loading: a second press must not begin a second attempt.
    private bool leaving;

    private void OnEnable() => CareerSaves.Saved += Refresh;

    private void OnDisable() => CareerSaves.Saved -= Refresh;

    public override void OnShow()
    {
        leaving = false;
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
    }

    private void ShowRow(int i, DrillDef d, CareerProfile p, eDrill pick, bool open)
    {
        if (i < rows.Length && rows[i] != null)
            rows[i].SetActive(d != null);
        if (d == null)
            return;
        bool coachPick = d.id == pick;
        Set(At(names, i), RowName(d, coachPick));
        Set(At(details, i), p == null ? "" : DrillText.RowDetail(d, RecordOf(p.drills, d.id), coachPick));
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

    // ---- Wired in the prefab ------------------------------------------------------------------

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
