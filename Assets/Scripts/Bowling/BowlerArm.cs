using UnityEngine;

/// <summary>
/// Which arm a bowler bowls with (2026-11-06: "add left handed bowling please, just have to flip
/// the animation or flip the person"). A left-armer is the right-arm bowler mirrored across the
/// pitch: his animation plays mirrored (the humanoid Mirror on every bowling state), his mark is
/// on the other side of the stumps (over the wicket for a left-armer: z < 0), and the ball rides
/// his left hand, so it is released from that side and comes in at the angle a left-armer's does.
/// Where the ball goes - its line, swing and length - is still the delivery's, judged against the
/// batter, so an outswinger still leaves a right-hander.
///
/// Pace only. A left-arm spinner's stock ball turns the other way to a right-armer's of the same
/// kind (left-arm orthodox spins like a leg-break), and the repertoire, the drills' "with the turn"
/// and the delivery names are all written for right-arm spin.
///
/// In a match a bowler keeps his arm (from his name, so the same side always has the same
/// left-armers); in the nets, where the first bowler of a kind bowls every ball of it, the arm is
/// drawn per ball so a session sees both.
/// </summary>
public static class BowlerArm
{
    /// The share of pace balls in the nets that come from a left-armer.
    public const float NetsLeftShare = 0.35f;

    /// Every ball from this arm while set (tests, and checking a left-armer in the editor).
    public static bool? Force;

    public static bool CanBeLeftArm(eBowlerKind k) => k == eBowlerKind.Fast || k == eBowlerKind.Medium;

    /// A match bowler's arm: about one pace bowler in three, fixed by his name.
    public static bool IsLeftArm(string name, eBowlerKind kind)
    {
        if (!CanBeLeftArm(kind) || string.IsNullOrEmpty(name))
            return false;
        return Hash(name) % 3u == 1u;
    }

    /// A nets ball: `roll` in [0, 1).
    public static bool NetsBall(eBowlerKind kind, float roll) => CanBeLeftArm(kind) && roll < NetsLeftShare;

    /// FNV-1a: stable on every platform (string.GetHashCode is not).
    public static uint Hash(string s)
    {
        uint h = 2166136261u;
        foreach (char c in s)
        {
            h ^= c;
            h *= 16777619u;
        }
        // Murmur3's finaliser: FNV's low bits barely mix, so "% 3" alone put every name in one bin.
        h ^= h >> 16;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        h *= 0xc2b2ae35u;
        h ^= h >> 16;
        return h;
    }

    /// The left-armer's mark: the right-armer's mirrored across the middle of the pitch.
    public static Vector3 Mirror(Vector3 p) => new Vector3(p.x, p.y, -p.z);
}
