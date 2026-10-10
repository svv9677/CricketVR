using UnityEngine;

/// <summary>
/// Anyone the ball is thrown between once it is dead (ThrowBack): a fielder (the bowler too,
/// while he fields) throws it in to the keeper, the keeper passes it back to the bowler - direct,
/// or through an inner fielder when the bowler is too far off.
/// </summary>
public interface IThrowPlayer
{
    string Name { get; }
    /// Has the ball in his hands (taken, or caught from a throw).
    bool HoldingBall { get; }
    /// Seconds he has had it.
    float HeldFor { get; }
    /// Where his hands take a ball thrown to him from `from`: in front of his chest, facing it.
    Vector3 CatchPointFrom(Vector3 from);
    /// Where the ball leaves his hand as he throws.
    Vector3 ReleasePoint { get; }
    /// Where he is, on the ground.
    Vector3 Position { get; }

    /// Turn to `target` and start the throw.
    void BeginThrow(Vector3 target);
    /// The throw has reached the moment the ball leaves the hand.
    bool ReadyToRelease { get; }
    /// Let go of it: ThrowBack flies it from here.
    void Release();
    /// A throw is coming: face it and get the hands to `point` for when it arrives at `arrivalTime`.
    void ExpectBall(Vector3 point, Vector3 from, float arrivalTime);
    /// It has arrived: it is in his hands.
    void TakeThrow(Vector3 incoming);
}
