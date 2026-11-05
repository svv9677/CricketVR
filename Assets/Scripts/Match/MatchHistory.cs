using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finished matches, newest first, kept in PlayerPrefs (the last MaxKept): what the Splash menu
/// shows as the result of the match just played and as the last match on the main screen.
/// </summary>
public static class MatchHistory
{
    [System.Serializable]
    public class Record
    {
        public int runs, wickets, balls, overs, fours, sixes, dots, wides;
        /// The target in a chase; 0 when setting a total.
        public int target;
        public int bowling;    // eBowlingLevel
        public int pitch;      // ePitch
        public string result;  // "Won by 3 wickets", "Lost by 6 runs", "You won by 12 runs"
        public string when;    // local date and time, for the list
        /// The AI's innings (AIInnings): batting first to set the target, or chasing the player's.
        public bool hasAI, aiChased;
        public int aiRuns, aiWickets, aiBalls, aiTarget;
        public List<int> aiOverRuns = new List<int>(), aiOverWickets = new List<int>();
        /// Set when the result is decided (older records: from the chase alone).
        public bool won, tied;

        public string Score => $"{runs}/{wickets}";
        public string OversText => $"{balls / 6}.{balls % 6}";
        public float RunRate => balls > 0 ? runs * 6f / balls : 0f;
        public bool Won => won || (target > 0 && runs >= target);
        public string AIScore => $"{aiRuns}/{aiWickets}";
        public string AIOversText => $"{aiBalls / 6}.{aiBalls % 6}";
    }

    [System.Serializable]
    private class Store { public List<Record> records = new List<Record>(); }

    private const string Key = "match_history", PendingKey = "match_result_pending";
    public const int MaxKept = 10;

    public static List<Record> All => Load().records;

    public static Record Latest
    {
        get
        {
            List<Record> all = All;
            return all.Count > 0 ? all[0] : null;
        }
    }

    /// A match finished and its result has not been shown yet.
    public static bool ResultPending
    {
        get => PlayerPrefs.GetInt(PendingKey, 0) == 1;
        set { PlayerPrefs.SetInt(PendingKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void Add(Record r)
    {
        Store s = Load();
        s.records.Insert(0, r);
        while (s.records.Count > MaxKept)
            s.records.RemoveAt(s.records.Count - 1);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
        ResultPending = true;
    }

    private static Store Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json))
            return new Store();
        try { return JsonUtility.FromJson<Store>(json) ?? new Store(); }
        catch (System.ArgumentException) { return new Store(); }
    }

    /// "54/2 (5.0 ov) · Won by 3 wickets" - one line for the main menu.
    public static string Line(Record r) =>
        r == null ? "" : $"{r.Score} ({r.OversText} ov){(string.IsNullOrEmpty(r.result) ? "" : " · " + r.result)}";
}
