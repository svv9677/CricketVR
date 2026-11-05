using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One inbox message ("careerMessage", opened from InboxScreen): its kind and date, the title, who
/// it is from, the body, and up to MaxOptions choices - an offer's sides to join (up to three) and
/// stay, a call-up to accept, a plain OK - the first in the pink call-to-action. Opening it marks it read. A choice goes to
/// CareerProgress.Choose (which acts on it and marks the message answered), is saved, and returns
/// to the inbox; an answered message shows its choices greyed out and says so. Built by Tools >
/// CricketVR > Build Menu Prefabs.
/// </summary>
public class MessageScreen : MenuScreen
{
    public const int MaxOptions = 4;
    private const string Muted = "#98A2B3", Good = "#22C55E";

    [SerializeField] private TextMeshProUGUI meta;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI from;
    [SerializeField] private TextMeshProUGUI body;
    [SerializeField] private Button[] options = new Button[0];
    [SerializeField] private TextMeshProUGUI[] optionLabels = new TextMeshProUGUI[0];

    // The message to show and the slot it belongs to (statics, like CareerResultScreen's: the
    // inbox sets it just before it pushes this screen).
    private static CareerMessage open;
    private static int openSlot = -1;

    /// The inbox calls this as it opens a message.
    public static void Open(CareerMessage m)
    {
        open = m;
        openSlot = CareerSaves.Active;
    }

    /// The message opened, while it is still in the active career's inbox; otherwise null.
    public static CareerMessage Showing
    {
        get
        {
            CareerProfile p = CareerSaves.Current;
            return open != null && openSlot == CareerSaves.Active && p?.inbox != null && p.inbox.Contains(open) ? open : null;
        }
    }

    public override void OnShow()
    {
        CareerMessage m = Showing;
        if (m != null && !m.read)
        {
            m.read = true;
            CareerSaves.SaveCurrent();
        }
        Refresh();
    }

    private void Refresh()
    {
        CareerMessage m = Showing;
        Set(meta, m != null ? MetaLine(m) : "");
        Set(title, m != null ? m.title : "No message");
        Set(from, m != null ? m.from : "");
        Set(body, m != null ? m.body : "Open a message from your inbox.");
        int count = m?.options != null ? Mathf.Min(m.options.Count, MaxOptions) : 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null)
                continue;
            options[i].gameObject.SetActive(i < count);
            options[i].interactable = i < count && !m.answered;
            if (i < count && i < optionLabels.Length)
                Set(optionLabels[i], OptionLabel(m.options[i]));
        }
    }

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value ?? "";
    }

    // ---- Formatting (pure; InboxScreensTests) ------------------------------------------------------

    /// "OFFER  ·  5 Nov 2026", with "Answered" after it once a choice has been made.
    public static string MetaLine(CareerMessage m)
    {
        if (m == null)
            return "";
        string tag = $"<b><color={InboxScreen.TagColour(m.kind)}>{InboxScreen.Tag(m.kind)}</color></b>";
        string date = InboxScreen.DateText(m.date);
        string line = string.IsNullOrEmpty(date) ? tag : $"{tag}  <color={Muted}>·  {date}</color>";
        return Answered(m) ? $"{line}  <color={Muted}>·</color>  <color={Good}>Answered</color>" : line;
    }

    /// Answered, and it had something to answer (a note with no choices is just read).
    public static bool Answered(CareerMessage m) => m != null && m.answered && m.options != null && m.options.Count > 0;

    /// An option's label; "OK" when it has none.
    public static string OptionLabel(MessageOption o) => o == null || string.IsNullOrEmpty(o.label) ? "OK" : o.label;

    // ---- Wired in the prefab, with the option's index ---------------------------------------------

    public void OnOption(int i)
    {
        CareerProfile p = CareerSaves.Current;
        CareerMessage m = Showing;
        if (p == null || m == null || m.answered || m.options == null || i < 0 || i >= m.options.Count || i >= MaxOptions)
            return;
        CareerProgress.Choose(p, m, m.options[i], new System.Random());
        CareerSaves.SaveCurrent();
        if (Root != null)
            Root.Back();          // to the inbox, where the answer (and anything it brought) shows
        else
            Refresh();
    }
}
