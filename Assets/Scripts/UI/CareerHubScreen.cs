using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The career hub for the active slot (CareerSaves.Current): a compact player card (the kit bar,
/// the name, "#18 · India · Right-hand bat", the rung and season on the right), two chips - the
/// measured overall and the form - and the next match of the season: who against, which round, the
/// format and where the player bats. The primary button is the 30-ball nets trial until it is done
/// (matches stay locked: the trial sets the ratings they start from), then Play match. Ratings,
/// Season and the nets are a row below. The ratings only describe how the player bats; nothing here
/// changes how the game plays. Coming back from the trial it says so once (NetsSession.TrialResult);
/// coming back from a match, the result screen (CareerResultScreen) says how it went. Phase 4 adds
/// the Inbox (with the unread count), a line when an offer is waiting, "A Pro League scout is
/// watching" on a scouted next match, and - left out of the side - Sim the match in place of Play
/// match. Built by Tools > CricketVR > Build Menu Prefabs.
/// </summary>
public class CareerHubScreen : MenuScreen
{
    /// The rating cells: RatingSet's seven, then the overall (laid out by CareerRatingsScreen).
    public const int Cells = RatingSet.Count + 1;
    private const string Muted = "#98A2B3", Good = "#22C55E", Pink = "#F43F8E", Bad = "#FF6B4A";

    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI playerDetail;
    [SerializeField] private Image kitBar;
    [SerializeField] private TextMeshProUGUI stage;
    [SerializeField] private TextMeshProUGUI season;
    [SerializeField] private TextMeshProUGUI overallChip;
    [SerializeField] private TextMeshProUGUI formChip;
    [SerializeField] private TextMeshProUGUI nextTitle;
    [SerializeField] private TextMeshProUGUI nextDetail;
    [SerializeField] private TextMeshProUGUI nextSlot;
    [SerializeField] private TextMeshProUGUI banner;
    [SerializeField] private Button primary;
    [SerializeField] private TextMeshProUGUI primaryLabel;
    [SerializeField] private TextMeshProUGUI locked;
    [SerializeField] private Button nets;
    // Phase 4: the inbox button's label, the offer waiting, a scout at the next match.
    [SerializeField] private TextMeshProUGUI inboxLabel;
    [SerializeField] private TextMeshProUGUI offerLine;
    [SerializeField] private TextMeshProUGUI nextScout;

    private bool showedTrial;
    // How the match he was left out of went, said once on the banner (until the hub goes).
    private string leftOutNote;

    private void OnEnable() => CareerSaves.Saved += Refresh;

    private void OnDisable()
    {
        CareerSaves.Saved -= Refresh;
        ForgetTrial();
    }

    /// A career made before matches existed (a version-1 save) or a new one has no season yet:
    /// make it here, once, and save it, so the next match below is a real fixture.
    public override void OnShow()
    {
        CareerProfile p = CareerSaves.Current;
        if (p != null && p.season == null)
        {
            CareerProgress.EnsureSeason(p, new System.Random());
            CareerSaves.SaveCurrent();
        }
        Refresh();
    }

    public override void OnHide()
    {
        ForgetTrial();
        leftOutNote = null;
    }

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
        if (nets != null) nets.interactable = p != null && p.trialDone;
        ShowInbox(p);
        if (p == null)
        {
            Set(playerName, "No career loaded");
            Set(playerDetail, "Pick a slot on the career screen.");
            if (banner != null) banner.gameObject.SetActive(false);
            Set(overallChip, OverallChip(RatingSet.Unknown));
            Set(formChip, FormChip(RatingSet.Unknown));
            ShowNext(null, null);
            return;
        }
        Set(playerName, p.name);
        Set(playerDetail, $"#{p.look.number}  ·  {Nations.Name(p.nation)}  ·  {p.HandName}");
        if (kitBar != null)
            kitBar.color = Appearance.KitColours[Mathf.Clamp(p.look.kit, 0, Appearance.KitColours.Length - 1)];
        Set(stage, Ladder.Name(p.rung));
        Set(season, $"Season {(p.season != null ? p.season.number : p.seasonsPlayed + 1)}");
        Set(overallChip, OverallChip(p.ratings != null ? p.ratings.Overall : RatingSet.Unknown));
        Set(formChip, FormChip(CareerProgress.Form(p)));
        ShowTrialBanner();
        ShowLeftOutNote();
        Fixture next = CareerProgress.NextFixture(p);
        ShowNext(p, next);
        ShowScout(p, next);
        ShowPrimary(p, next);
    }

    /// The unread count on the Inbox button, and the offer line while an offer waits for an answer.
    private void ShowInbox(CareerProfile p)
    {
        Set(inboxLabel, InboxScreen.InboxLabel(p != null ? p.Unread : 0));
        CareerMessage offer = p != null ? CareerProgress.PendingOffer(p) : null;
        if (offerLine == null)
            return;
        offerLine.gameObject.SetActive(offer != null);
        offerLine.text = InboxScreen.OfferLine(offer);
    }

    /// "A Pro League scout is watching" under a scouted next match he plays in.
    private void ShowScout(CareerProfile p, Fixture next)
    {
        if (nextScout == null)
            return;
        bool on = p != null && next != null && next.scout && !CareerProgress.LeftOutNext(p);
        nextScout.gameObject.SetActive(on);
        nextScout.text = on ? CareerResultScreen.ScoutWatching(p.season != null ? p.season.rung : p.rung) : "";
    }

    /// The banner says how the match he sat out went (the trial's word, when there is one, first).
    private void ShowLeftOutNote()
    {
        if (banner == null || leftOutNote == null || NetsSession.TrialResult != null)
            return;
        banner.gameObject.SetActive(true);
        banner.text = leftOutNote;
    }

    /// The trial until it is done (matches locked), then the next match, or the next season.
    private void ShowPrimary(CareerProfile p, Fixture next)
    {
        bool seasonOver = next == null && p.season != null && p.season.finished;
        if (locked != null) locked.gameObject.SetActive(!p.trialDone);
        if (primary != null) primary.interactable = !p.trialDone || next != null || seasonOver;
        if (!p.trialDone)
            Set(primaryLabel, $"Take your nets trial · {NetsSession.TrialBalls} balls");
        else if (next != null)
            Set(primaryLabel, CareerProgress.LeftOutNext(p) ? "Sim the match (left out)" : "Play match");
        else
            Set(primaryLabel, seasonOver ? "Start next season" : "No match to play");
    }

    private void ShowNext(CareerProfile p, Fixture next)
    {
        Season s = p?.season;
        if (s == null)
        {
            Set(nextTitle, "No season yet");
            Set(nextDetail, "");
            Set(nextSlot, "");
            return;
        }
        if (next == null && !s.finished)
        {
            // Between the league rounds and the final (or the rest of the round still to come):
            // not over, just nothing for his side to play yet.
            Set(nextTitle, "Waiting for the other results");
            Set(nextDetail, "Your next match appears here once it is drawn.");
            Set(nextSlot, "");
            return;
        }
        if (next == null)
        {
            Set(nextTitle, "Season complete");
            int pos = LeagueBuilder.Table(s).FindIndex(t => t.id == s.yourTeam) + 1;
            Set(nextDetail, pos > 0 ? $"{s.Yours?.name} finished {CareerResultScreen.Ordinal(pos)} of {s.teams.Count}." : "");
            Set(nextSlot, "");
            return;
        }
        Set(nextTitle, $"vs {TeamName(s, next.Opponent(s.yourTeam))}");
        Set(nextDetail, NextDetail(next, Rounds(s), s.rung));
        Set(nextSlot, CareerProgress.LeftOutNext(p) ? LeftOutLine(p.benchedFor) : SlotLine(p.battingSlot, next.final));
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

    private static void Set(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }

    // ---- Formatting (pure; CareerScreensTests) -----------------------------------------------------

    /// "Overall 54 Solid", the number bold and the grade muted; "Overall --" while unmeasured.
    public static string OverallChip(float overall) =>
        RatingSet.Known(overall)
            ? $"Overall  <b>{Round(overall)}</b>  <color={Muted}>{CareerRatings.Grade(overall)}</color>"
            : $"Overall  <color={Muted}>--</color>";

    /// "Form 61"; "Form --" before the first innings.
    public static string FormChip(float form) =>
        RatingSet.Known(form) ? $"Form  <b>{Round(form)}</b>" : $"Form  <color={Muted}>--</color>";

    /// "Round 3 of 7 · 6 overs · Club ground · Afternoon", or "Final · 20 overs · Stadium · Night".
    public static string NextDetail(Fixture f, int rounds, eRung rung)
    {
        string when = f.final ? "Final" : rounds > 0 ? $"Round {f.round} of {rounds}" : $"Round {f.round}";
        return $"{when} · {Ladder.Overs(rung)} overs · {VenueName(Ladder.Venue(rung))} · {(f.night ? "Night" : "Afternoon")}";
    }

    /// "You bat at 7", with a pink FINAL badge in front for the final.
    public static string SlotLine(int slot, bool final) =>
        (final ? $"<color={Pink}><b>FINAL</b></color>  ·  " : "") + $"You bat at {Mathf.Clamp(slot, 1, 11)}";

    public static string VenueName(eVenue v) => v == eVenue.Stadium ? "Stadium" : "Club ground";

    /// The league rounds of a season (the final not counted): the highest round of its fixtures.
    public static int Rounds(Season s)
    {
        int rounds = 0;
        if (s?.fixtures != null)
            foreach (Fixture f in s.fixtures)
                if (!f.final && f.round > rounds)
                    rounds = f.round;
        return rounds;
    }

    /// A side's name by its id (the id itself if the side is not in the season).
    public static string TeamName(Season s, string id)
    {
        Team t = s?.Team(id);
        return t != null && !string.IsNullOrEmpty(t.name) ? t.name : id;
    }

    private static string Round(float v) => Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture);

    // ---- Phase 4 formatting (pure; InboxScreensTests) ---------------------------------------------

    /// "Left out of the side for this one", or "... · 2 matches to sit out" with more to come.
    public static string LeftOutLine(int benchedFor) =>
        $"<color={Bad}>Left out of the side</color>" + (benchedFor > 1 ? $"  ·  {benchedFor} matches to sit out" : " for this one");

    /// "Left out: Harbour CC won by 12 runs" for the banner, from the record of the match he sat out.
    public static string LeftOutNote(InningsRecord r)
    {
        if (r == null)
            return $"<color={Muted}>Left out:</color> the match was played without you.";
        string how = !string.IsNullOrEmpty(r.result) ? r.result : $"vs {r.opponent}";
        return $"<color={Muted}>Left out:</color> {how}";
    }

    /// The newest match in the record book he was left out of, or null.
    public static InningsRecord LastLeftOut(CareerProfile p)
    {
        if (p?.innings == null || p.innings.Count == 0)
            return null;
        InningsRecord r = p.innings[p.innings.Count - 1];
        return r != null && r.leftOut ? r : null;
    }

    // ---- Wired in the prefab ------------------------------------------------------------------

    /// The trial until it is done, then the next match (or, the season over, the next season).
    public void OnPrimary()
    {
        CareerProfile p = CareerSaves.Current;
        if (p == null)
            return;
        ForgetTrial();
        if (!p.trialDone)
        {
            NetsSession.BeginTrial();
            return;
        }
        var rng = new System.Random();
        if (CareerProgress.NextFixture(p) == null)
        {
            if (p.season != null && p.season.finished)
            {
                CareerProgress.EndSeason(p, rng);
                CareerSaves.SaveCurrent();
                Refresh();
            }
            return;
        }
        if (CareerProgress.LeftOutNext(p))
        {
            SimLeftOut(p, rng);
            return;
        }
        MatchPlan plan = CareerProgress.PlanNext(p, rng);
        if (plan == null)
            return;
        CareerResultScreen.RememberBefore(p);
        CareerMatchSession.Begin(plan);
    }

    public void OnRatings() => Push("careerRatings");

    public void OnSeason() => Push("careerSeason");

    public void OnInbox()
    {
        InboxScreen.StartAtNewest();
        Push("careerInbox");
    }

    /// He sits this one out: the match is simulated without him, and the banner says how it went.
    private void SimLeftOut(CareerProfile p, System.Random rng)
    {
        CareerProgress.PlayLeftOut(p, rng);
        leftOutNote = LeftOutNote(LastLeftOut(p));
        CareerSaves.SaveCurrent();
        Refresh();
    }

    private void Push(string screen)
    {
        if (Root != null)
            Root.Push(screen);
    }

    /// The nets: measured practice and the drills (CareerDrillsScreen; after the trial, the button is off before it).
    public void OnNets()
    {
        CareerProfile p = CareerSaves.Current;
        if (p == null || !p.trialDone)
            return;
        ForgetTrial();
        Push("careerDrills");
    }
}
