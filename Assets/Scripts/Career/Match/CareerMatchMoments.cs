using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The career player's own shots, for the record book (phase 7): the shot he struck, and the four
/// or six it went for. Only watching - nothing here changes a ball.
///
/// The shot is kept at Bat.ShotStruck (its real-blade carry, as BallLedger measures it) and used when
/// the ball's runs are counted (YourBoundary, from OnFaced or, for a ball that ended the match
/// before it was settled, FinishFromScene). Two listeners would take a partner's ball for his since
/// the partner bats on screen (phase 3): Bat.ShotStruck also fires for the partner's contact
/// (BatPartner.ReportShot), and ShotRecorder records the partner's bat. So a shot is kept only off
/// a ball he was on strike for, with the partner not the striker.
/// </summary>
public partial class CareerMatch
{
    /// Recording and shot within this many seconds are the same contact (the recorder stamps it the
    /// same frame; this allows for a frame or two either way).
    private const float SameContact = 0.05f;
    /// A kept shot older than this is from a ball that was never settled: a new contact replaces it.
    private const float StaleShot = 5f;

    // The shot off this ball, kept from Bat.ShotStruck until its runs are counted.
    private bool shotKept;
    private float shotCarry, shotTime;
    private eRealContact shotContact;
    private ShotRecording shotRecording;
    private readonly List<BallFlight.Sample> carryBuffer = new List<BallFlight.Sample>(1024);

    /// A shot off the bat (Bat.ShotStruck): kept until the ball is settled. Only his own ball, as
    /// played on his own bat; the first contact of a ball, as BallLedger keeps it.
    private void OnShotStruck(ShotInfo info)
    {
        if (!youAtRelease || Striker.IsPartner)
            return;
        if (shotKept && Time.time - shotTime < StaleShot)
            return;
        shotKept = true;
        shotCarry = BallLedgerMath.Carry(info.contactPoint, info.exitVelocity, carryBuffer);
        shotContact = info.ActualContact;
        ShotRecorder recorder = ShotRecorder.Instance;
        shotRecording = recorder != null ? recorder.Latest : null;
        shotTime = Time.time;
    }

    /// A four or six to his name; a six also goes to the records (NoteSix, RecordMoments.YourSix).
    private void YourBoundary(Batsman b, int runs)
    {
        CountBoundary(b, runs);
        bool kept = shotKept;
        ShotRecording recording = shotRecording;
        shotKept = false;
        shotRecording = null;
        if (runs != 6 || !kept || b == null || !b.IsHuman || state == null || plan == null)
            return;
        string stamp = $"{plan.seed:x8}-b{b.Balls}";
        if (!CareerMatchLogic.NoteSix(state, shotCarry, shotContact, stamp))
            return;
        // The six into the pending match now: OnFaced saves straight after this.
        CareerMatchRecovery.Note(CareerSaves.Current, state);
        RecordMoments.Raise(new SixMoment
        {
            carry = shotCarry,
            stamp = stamp,
            recording = LinesUp(recording, shotTime) ? recording : null,
        });
    }

    /// The recording is this shot's: its contact was stamped at the shot's time (ShotRecorder keeps
    /// two recordings and reuses them, so the reference alone does not say which ball it holds).
    private static bool LinesUp(ShotRecording r, float time) =>
        r != null && r.HasContact && Mathf.Abs(r.StartedAt + r.ContactTime - time) <= SameContact;
}
