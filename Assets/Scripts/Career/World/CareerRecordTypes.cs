using System;
using System.Collections.Generic;

/// The career bests the record book keeps (phase 7, CareerRecords).
public enum eRecord { HighScore, FastestFifty, BestStrikeRate, MostSixes, LongestSix }

/// <summary>One career best: whether it has been set, its value (runs, balls, strike rate, sixes or
/// metres), the innings it came in, and for the longest six the stamp of its saved clip.</summary>
[Serializable]
public class RecordEntry
{
    public bool set;
    public float value;
    public int runs, balls, sixes;
    public bool notOut;
    public string team = "", opponent = "", date = "";
    public eRung rung;
    public int season;
    /// The record clip's stamp (ReplayVault), "" when there is none.
    public string clipStamp = "";
}

/// <summary>The career's records and milestones (CareerProfile.recordBook): one entry per best, the
/// milestones already reached (so each is said once), and the latest one with its date.</summary>
[Serializable]
public class CareerRecordBook
{
    public RecordEntry highScore = new RecordEntry(), fastestFifty = new RecordEntry(), bestStrikeRate = new RecordEntry(),
                       mostSixes = new RecordEntry(), longestSix = new RecordEntry();
    /// Milestone ids reached ("fifty", "runs500", "caps10" ...).
    public List<string> reached = new List<string>();
    public string lastMilestone = "", lastMilestoneDate = "";

    public RecordEntry Get(eRecord r) => r switch
    {
        eRecord.HighScore => highScore,
        eRecord.FastestFifty => fastestFifty,
        eRecord.BestStrikeRate => bestStrikeRate,
        eRecord.MostSixes => mostSixes,
        _ => longestSix,
    };
}

/// A six the career player hit off the real blade: how far it carried (m), its clip stamp, and the
/// recording of the shot when one lines up with it (null otherwise).
public struct SixMoment
{
    public float carry;
    public string stamp;
    public ShotRecording recording;
}

/// <summary>The match's record moments as they happen, for whatever keeps them (the record clip
/// catcher). Raised by CareerMatch; nothing here changes a ball.</summary>
public static class RecordMoments
{
    /// The match's longest six so far, off his own bat.
    public static event Action<SixMoment> YourSix;

    public static void Raise(SixMoment m) => YourSix?.Invoke(m);

    public static void ResetForTests() => YourSix = null;
}
