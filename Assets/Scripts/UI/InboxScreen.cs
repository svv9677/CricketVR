using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career inbox ("careerInbox", from the hub's Inbox button): the slot's messages newest first,
/// PerPage to a page - each a row with an unread dot, the kind's tag (OFFER, SELECTION, SCOUT,
/// COACH, MILESTONE) and the title, the date muted on the right - and a "‹ Page 1 of 3 ›" stepper.
/// A row opens the message (MessageScreen), which marks it read and holds any choice. There is no
/// scroll view, so the inbox pages rather than scrolls; the rows keep their places when a page is
/// short, so the card keeps its height. Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class InboxScreen : MenuScreen
{
    public const int PerPage = 5;
    private const string Muted = "#98A2B3", Good = "#22C55E", Bad = "#FF6B4A", Accent = "#8AB8FF", Pink = "#F43F8E";

    /// One row as built in the prefab: the button (its Image the row's background), the unread
    /// dot, the tag-and-title text and the date.
    [Serializable]
    public class Row
    {
        public Button button;
        public Image background;
        public Image dot;
        public TextMeshProUGUI text;
        public TextMeshProUGUI date;
    }

    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private Row[] rows = new Row[0];
    [SerializeField] private TextMeshProUGUI pageText;
    [SerializeField] private Button prevPage;
    [SerializeField] private Button nextPage;

    private readonly CareerMessage[] shown = new CareerMessage[PerPage];
    private int page;
    // Arriving from the hub starts on the newest (it calls StartAtNewest); coming back from a
    // message keeps the page it was opened from. A flag the hub raises rather than one this
    // screen keeps: a menu closed while a message is open never comes back here to clear it.
    private static bool startAtNewest = true;

    /// The hub calls this as it opens the inbox.
    public static void StartAtNewest() => startAtNewest = true;

    public override void OnShow()
    {
        if (startAtNewest)
            page = 0;
        startAtNewest = false;
        Refresh();
    }

    private void Refresh()
    {
        CareerProfile p = CareerSaves.Current;
        List<CareerMessage> inbox = p?.inbox ?? new List<CareerMessage>();
        int pages = Pages(inbox.Count);
        page = Mathf.Clamp(page, 0, pages - 1);
        Set(subtitle, Subtitle(p != null ? p.Unread : 0, inbox.Count));
        for (int i = 0; i < rows.Length; i++)
        {
            int at = IndexAt(inbox.Count, page, i);
            CareerMessage m = at >= 0 ? inbox[at] : null;
            if (i < shown.Length)
                shown[i] = m;
            ShowRow(rows[i], m, i == 0 && inbox.Count == 0);
        }
        Set(pageText, PageText(page, pages));
        if (prevPage != null) prevPage.interactable = page > 0;
        if (nextPage != null) nextPage.interactable = page < pages - 1;
    }

    /// A row with a message, or an empty place (no background, nothing to press); the first row
    /// of an empty inbox says so.
    private static void ShowRow(Row row, CareerMessage m, bool emptyNote)
    {
        if (row == null)
            return;
        if (row.button != null) row.button.interactable = m != null;
        if (row.background != null) row.background.enabled = m != null;
        if (row.dot != null) row.dot.enabled = m != null && !m.read;
        Set(row.text, m != null ? RowText(m) : emptyNote ? $"<color={Muted}>Nothing here yet.</color>" : "");
        Set(row.date, m != null ? DateText(m.date) : "");
    }

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Formatting (pure; InboxScreensTests) ------------------------------------------------------

    /// Pages for `count` messages: at least one, so an empty inbox is "Page 1 of 1".
    public static int Pages(int count) => Math.Max(1, (count + PerPage - 1) / PerPage);

    /// "Page 1 of 3" (0-based page in).
    public static string PageText(int page, int pages) =>
        $"Page {Math.Max(0, page) + 1} of {Math.Max(1, pages)}";

    /// The inbox index (newest last) of `row` on `page` when shown newest first, or -1 past the end.
    public static int IndexAt(int count, int page, int row)
    {
        if (page < 0 || row < 0 || row >= PerPage)
            return -1;
        int fromNewest = page * PerPage + row;
        return fromNewest < count ? count - 1 - fromNewest : -1;
    }

    /// "OFFER", "SELECTION", "SCOUT", "COACH", "MILESTONE", "GOAL", "LOCKER"; "NEWS" for the rest (the season review).
    public static string Tag(eMessageKind kind) => kind switch
    {
        eMessageKind.Offer => "OFFER",
        eMessageKind.Selection => "SELECTION",
        eMessageKind.Scout => "SCOUT",
        eMessageKind.Coach => "COACH",
        eMessageKind.Milestone => "MILESTONE",
        eMessageKind.Goal => "GOAL",
        eMessageKind.Locker => "LOCKER",
        _ => "NEWS",
    };

    /// The tag's colour: offers pink, selection orange, scouts and the locker the accent, milestones and goals green.
    public static string TagColour(eMessageKind kind) => kind switch
    {
        eMessageKind.Offer => Pink,
        eMessageKind.Selection => Bad,
        eMessageKind.Scout => Accent,
        eMessageKind.Milestone => Good,
        eMessageKind.Goal => Good,
        eMessageKind.Locker => Accent,
        _ => Muted,
    };

    /// The tag small and coloured in its own column, then the title (bold while unread; who it is
    /// from when it has none).
    public static string RowText(CareerMessage m)
    {
        if (m == null)
            return "";
        string title = !string.IsNullOrEmpty(m.title) ? m.title : m.from;
        if (!m.read)
            title = $"<b>{title}</b>";
        return $"<pos=0%><size=75%><b><color={TagColour(m.kind)}>{Tag(m.kind)}</color></b></size><pos=26%>{title}";
    }

    /// "5 Nov 2026" from the saved "2026-11-05"; anything else as it is.
    public static string DateText(string date)
    {
        if (string.IsNullOrEmpty(date))
            return "";
        return DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
            ? d.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : date;
    }

    /// "2 unread  ·  14 messages", "1 message", or what the inbox is for while it is empty.
    public static string Subtitle(int unread, int count)
    {
        if (count <= 0)
            return "Scouts, selectors and your coach write to you here.";
        string all = count == 1 ? "1 message" : $"{count} messages";
        return unread > 0 ? $"{unread} unread  ·  {all}" : all;
    }

    // ---- The hub's lines about the inbox (pure; InboxScreensTests) ---------------------------------

    /// "Inbox (2)" with unread messages, else "Inbox".
    public static string InboxLabel(int unread) => unread > 0 ? $"Inbox ({unread})" : "Inbox";

    /// "An offer from Riverside Rovers - open your inbox" (bold up to the dash); "Offers from 2
    /// sides", "An International call-up" when the offer is several sides or the national side.
    public static string OfferLine(CareerMessage m)
    {
        if (m == null)
            return "";
        int joins = 0;
        string team = null;
        bool callUp = false;
        if (m.options != null)
            foreach (MessageOption o in m.options)
            {
                if (o?.action == "accept-callup")
                    callUp = true;
                else if (JoinTeam(o?.action) is string name)
                {
                    joins++;
                    team ??= name;
                }
            }
        string what = callUp ? "An International call-up"
            : joins > 1 ? $"Offers from {joins} sides"
            : team != null ? $"An offer from {team}"
            : "An offer";
        return $"<color={Pink}><b>{what}</b></color> - open your inbox";
    }

    /// The side in an offer's "join:<rung>:<team name>" action, or null for any other action.
    public static string JoinTeam(string action)
    {
        if (string.IsNullOrEmpty(action) || !action.StartsWith("join:", System.StringComparison.Ordinal))
            return null;
        string[] parts = action.Split(new[] { ':' }, 3);
        if (parts.Length < 3)
            return null;
        // The rung first, then the name; the other way round (an older comment's order) is read too.
        string name = int.TryParse(parts[1], out _) ? parts[2] : int.TryParse(parts[2], out _) ? parts[1] : parts[2];
        return string.IsNullOrEmpty(name) ? null : name;
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// A row, with its index on the page: open that message.
    public void OnRow(int i)
    {
        CareerMessage m = i >= 0 && i < shown.Length ? shown[i] : null;
        if (m == null || Root == null)
            return;
        MessageScreen.Open(m);
        Root.Push("careerMessage");
    }

    /// The pager's arrows, -1 and +1.
    public void OnPage(int step)
    {
        page += step;
        Refresh();
    }
}
