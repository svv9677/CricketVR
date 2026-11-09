using TMPro;
using UnityEngine;

/// <summary>
/// Quick Play, from the main menu: a slot for each length - 5, 10, 20 and 50 overs and a Test -
/// each saying whether a match is on and how it stands, with Continue when one is, and New Game
/// always (no Restart, 2026-11-09) (QuickPlaySlots). New Game goes through the two setup steps (Match Setup, then
/// the ground: MatchSetupScreen, CustomiseScreen) and starts from there. Built by Tools > CricketVR
/// > Build Menu Prefabs, one row per MatchFormat.Order entry.
/// </summary>
public class QuickPlayScreen : MenuScreen
{
    [System.Serializable]
    public class SlotRow
    {
        public TextMeshProUGUI title;
        public TextMeshProUGUI detail;
        public GameObject continueButton;
    }

    [SerializeField] private SlotRow[] slots;

    public override void OnShow() => Refresh();

    private void Refresh()
    {
        if (slots == null)
            return;
        for (int i = 0; i < slots.Length && i < MatchFormat.Order.Length; i++)
        {
            SlotRow row = slots[i];
            eMatchFormat f = MatchFormat.Order[i];
            QuickPlaySlot slot = QuickPlaySlots.Load(f);
            if (row.title != null)
                row.title.text = MatchFormat.Label(f);
            if (row.detail != null)
            {
                string progress = QuickPlaySlots.Progress(slot);
                string state = slot.inProgress ? $"<color=#22C55E>In progress</color>  ·  <color=#E5E7EB>{progress}</color>"
                             : string.IsNullOrEmpty(progress) ? "No match on" : progress;
                string setup = slot.hasSetup ? slot.setup.Summary : "Set it up and play";
                row.detail.text = $"{state}\n{setup}";
            }
            if (row.continueButton != null) row.continueButton.SetActive(slot.inProgress);
        }
    }

    // ---- Wired in the prefab, each with its row's index in MatchFormat.Order ---------------------

    public void OnContinue(int i) => QuickPlaySlots.Continue(Format(i));

    public void OnNew(int i)
    {
        QuickPlaySlots.BeginNew(Format(i));
        if (Root != null)
            Root.Push("setup");
    }

    private static eMatchFormat Format(int i) => MatchFormat.Order[Mathf.Clamp(i, 0, MatchFormat.Order.Length - 1)];
}
