using UnityEngine;

/// <summary>
/// Anyone the fielding plan (AnimatedFielderManagement) can send after a hit ball: the nine
/// fielders and the wicketkeeper. The plan picks one to call it and go; the rest stand down.
/// </summary>
public interface IFielder
{
    /// Where he is now, on the ground.
    Vector3 Position { get; }
    float RunSpeed { get; }
    /// Already on the move (no reaction time to add).
    bool HasTarget { get; }
    bool Available { get; }
    bool IsKeeper { get; }
    string Name { get; }
    /// Run to this point on the ground.
    void SetTarget(Vector3 point);
    void Stop();
}
