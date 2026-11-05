using TMPro;
using UnityEngine;

/// <summary>
/// Career, from the Splash main menu: the three save slots (CareerSaves). An empty slot offers
/// Create player (CreatePlayerScreen, then CreateLookScreen); a filled one shows the player - name,
/// nation, hand and overall rating - with Play (the career hub) and a small Delete that asks first,
/// in the slot's own row. Built by Tools > CricketVR > Build Menu Prefabs, each slot's buttons wired
/// to the handlers below with the slot's index.
/// </summary>
public class CareerScreen : MenuScreen
{
    /// One slot's row as built in the prefab: the normal row and the delete-confirm row that
    /// replaces it while the question is asked.
    [System.Serializable]
    public class SlotRow
    {
        public TextMeshProUGUI title;
        public TextMeshProUGUI detail;
        public GameObject create;
        public GameObject play;
        public GameObject delete;
        public GameObject normal;
        public GameObject confirm;
        public TextMeshProUGUI question;
    }

    [SerializeField] private SlotRow[] slots = new SlotRow[0];

    private readonly CareerProfile[] profiles = new CareerProfile[CareerSaves.Slots];

    public override void OnShow()
    {
        for (int i = 0; i < slots.Length; i++)
            ShowConfirm(i, false);
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length && i < CareerSaves.Slots; i++)
        {
            CareerProfile p = profiles[i] = CareerSaves.Load(i);
            SlotRow row = slots[i];
            if (row == null)
                continue;
            bool filled = p != null;
            if (row.title != null)
                row.title.text = filled ? $"<b>{p.name}</b>" : $"Slot {i + 1}  ·  <color=#98A2B3>Empty</color>";
            if (row.detail != null)
                row.detail.text = filled ? Detail(p) : "Start a new career here.";
            if (row.create != null) row.create.SetActive(!filled);
            if (row.play != null) row.play.SetActive(filled);
            if (row.delete != null) row.delete.SetActive(filled);
        }
    }

    /// "India · Right-hand bat · Overall 54" ("Overall --" until the trial has measured it).
    public static string Detail(CareerProfile p)
    {
        float overall = p.ratings != null ? p.ratings.Overall : RatingSet.Unknown;
        string rating = RatingSet.Known(overall) ? Mathf.RoundToInt(overall).ToString() : "--";
        return $"{Nations.Name(p.nation)} · {p.HandName} · Overall {rating}";
    }

    private void ShowConfirm(int i, bool on)
    {
        if (i < 0 || i >= slots.Length || slots[i] == null)
            return;
        if (slots[i].normal != null) slots[i].normal.SetActive(!on);
        if (slots[i].confirm != null) slots[i].confirm.SetActive(on);
    }

    private static bool Valid(int i) => i >= 0 && i < CareerSaves.Slots;

    // ---- Wired in the prefab, with the slot index -----------------------------------------------

    public void OnPlay(int i)
    {
        if (Valid(i) && CareerSaves.Activate(i) && Root != null)
            Root.Push("careerHub");
        else
            Refresh();
    }

    public void OnCreate(int i)
    {
        if (!Valid(i))
            return;
        CareerDraft.Begin(i);
        if (Root != null)
            Root.Push("createPlayer");
    }

    /// Delete asks first, in the slot's own row.
    public void OnDelete(int i)
    {
        if (!Valid(i) || i >= slots.Length || slots[i] == null || profiles[i] == null)
            return;
        for (int j = 0; j < slots.Length; j++)
            ShowConfirm(j, j == i);
        if (slots[i].question != null)
            slots[i].question.text = $"Delete {profiles[i].name}'s career?\n" +
                                     "<size=20><color=#98A2B3>Every ball and rating in it goes for good.</color></size>";
    }

    public void OnConfirmDelete(int i)
    {
        if (!Valid(i))
            return;
        CareerSaves.Delete(i);
        ShowConfirm(i, false);
        Refresh();
    }

    public void OnKeep(int i) => ShowConfirm(i, false);
}
