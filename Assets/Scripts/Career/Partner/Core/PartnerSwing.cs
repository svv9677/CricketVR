using UnityEngine;

/// <summary>
/// The partner's swing as poses over time, relative to contact (t = 0): the bat exactly, the body
/// as IK targets (PartnerBatterBody plays them). Procedural - no animation clips - and a pure
/// function of the plan and the time, so any frame can be posed and the result is continuous in t.
///
/// The phases, for a shot that makes contact:
///   * Stance (until BackliftStart): side-on, knees bent, feet either side of the hips along the
///     pitch, the bat's toe grounded behind the back foot with the handle resting toward the front
///     thigh and the face to the bowler; head over the off-stump line, eyes level on the bowler.
///   * Backlift (to DownswingTime before contact): the bat rises behind him, hands to the back hip.
///     The front foot strides toward the pitch of the ball for a drive, defence, loft, glance or
///     sweep (to the leg side of its line, just behind the contact); for a cut or a pull the back
///     foot goes back and across.
///   * Downswing (DownswingTime): the bat turns about one axis into the contact pose - the sweet
///     spot on the contact point with the face along the exit velocity - while the hands come
///     through, so at contact the sweet spot moves along the plan's bat velocity.
///   * Follow-through (FollowThroughTime): the same turn carries on and dies away (checked short
///     for a defence) as the hands go to the shot's finish; then he holds it.
/// A leave lifts the bat high out of the way (or ducks under a high one); a play-and-miss or an
/// edge is the drive he meant, inside the line of the ball (MissInside, EdgeInside).
///
/// The bat pose comes from the hands: both are on the handle at the grip's points, and the hands
/// are never further than HandReach from the shoulders (the body is moved to them if they would
/// be). Frame: the pitch along X with the bowler at -X, off side +Z for a right-hander (offSign).
/// </summary>
public static partial class PartnerSwing
{
    /// Hips to the shoulder line (ReachMath.Body), and how far the hands may get from its middle.
    public const float TorsoLength = 0.52f, HandReach = 0.70f;
    /// Ankle height for the foot targets, and the bat's toe on the ground.
    public const float FootY = 0.08f, ToeY = 0.02f;
    /// Eyes above the hips in the stance.
    public const float EyesAboveHips = 0.72f;
    public const float FollowThroughTime = 0.45f;
    /// The sweet spot's speed at contact is the plan's bat speed within these (m/s).
    public const float MinBatSpeed = 4f, MaxBatSpeed = 34f;
    /// How far inside the ball's line the sweet spot passes for a play-and-miss, and for an edge
    /// (the outside edge on the ball).
    public const float MissInside = 0.12f, EdgeInside = 0.05f;

    public static float DownswingTime(PartnerShot s)
    {
        switch (s)
        {
            case PartnerShot.Defend: case PartnerShot.Leave: return 0.24f;
            case PartnerShot.Loft: case PartnerShot.Sweep: return 0.22f;
            case PartnerShot.Cut: case PartnerShot.Pull: return 0.18f;
            default: return 0.20f;
        }
    }

    /// When the pick-up starts (seconds, negative: before contact): about 0.3 s of backlift before
    /// the downswing, never before release, and never less than 0.12 s of it.
    public static float BackliftStart(in ShotPlan p)
    {
        float d = DownswingTime(p.shot);
        float start = -(d + 0.30f);
        if (p.contactTime > 0f) start = Mathf.Max(start, -p.contactTime);
        return Mathf.Min(start, -(d + 0.12f));
    }

    /// The middle of the shoulders: TorsoLength up the line from the hips to the head.
    public static Vector3 ShoulderCentre(in SwingPose s) => Shoulders(s.hips, s.head);

    private static Vector3 Shoulders(Vector3 hips, Vector3 head)
    {
        Vector3 up = head - hips;
        return hips + (up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up) * TorsoLength;
    }

    public static SwingPose Pose(in ShotPlan p, in BatGrip g, float offSign, Vector3 stanceHips, float tFromContact)
    {
        Keys k = Build(p, g, offSign, stanceHips);
        return k.At(tFromContact);
    }

    // ---- Per-shot shape ------------------------------------------------------------------------

    /// Degrees the bat turns back from contact to the top of the backlift.
    private static float Backlift(PartnerShot s)
    {
        switch (s)
        {
            case PartnerShot.Defend: return 70f;
            case PartnerShot.Loft: return 165f;
            case PartnerShot.Sweep: return 110f;
            case PartnerShot.Glance: return 100f;
            case PartnerShot.Edge: return 120f;
            case PartnerShot.Cut: case PartnerShot.Pull: case PartnerShot.PlayAndMiss: return 140f;
            default: return 150f;
        }
    }

    /// Degrees the bat carries on turning after contact.
    private static float Carry(PartnerShot s)
    {
        switch (s)
        {
            case PartnerShot.Defend: return 6f;
            case PartnerShot.Loft: return 230f;
            case PartnerShot.Cut: return 110f;
            case PartnerShot.Pull: return 170f;
            case PartnerShot.Sweep: return 140f;
            case PartnerShot.PlayAndMiss: return 150f;
            case PartnerShot.Glance: return 90f;
            case PartnerShot.Edge: return 80f;
            default: return 200f;
        }
    }

    /// How far the hips have opened toward the bowler at contact and at the finish (degrees).
    private static void Open(PartnerShot s, out float atContact, out float atFinish)
    {
        switch (s)
        {
            case PartnerShot.Defend: atContact = 8f; atFinish = 12f; break;
            case PartnerShot.Loft: atContact = 32f; atFinish = 70f; break;
            case PartnerShot.Cut: atContact = 18f; atFinish = 40f; break;
            case PartnerShot.Pull: atContact = 60f; atFinish = 100f; break;
            case PartnerShot.Sweep: atContact = 25f; atFinish = 45f; break;
            case PartnerShot.Glance: atContact = 30f; atFinish = 55f; break;
            case PartnerShot.Edge: atContact = 15f; atFinish = 30f; break;
            case PartnerShot.PlayAndMiss: atContact = 15f; atFinish = 35f; break;
            case PartnerShot.Leave: atContact = 0f; atFinish = 0f; break;
            default: atContact = 22f; atFinish = 50f; break;
        }
    }

    /// Where the hands want to be at contact, from the hips then: in front of the body below the
    /// eyes for the straight-bat shots, at the chest for the cut and the pull, low for the sweep.
    private static Vector3 HandsAtContact(PartnerShot s, Vector3 hips, in Frame fr)
    {
        switch (s)
        {
            case PartnerShot.Cut: return hips + fr.U * 0.45f + fr.S * 0.30f + fr.F * 0.15f;
            case PartnerShot.Pull: return hips + fr.U * 0.42f + fr.S * 0.15f + fr.F * 0.25f;
            case PartnerShot.Sweep: return hips + fr.U * 0.05f + fr.S * 0.30f + fr.F * 0.45f;
            default: return hips + fr.S * 0.28f + fr.F * 0.50f;
        }
    }

    /// Where the hands finish, from the hips then.
    private static Vector3 HandsAtFinish(PartnerShot s, Vector3 hips, in Frame fr)
    {
        switch (s)
        {
            case PartnerShot.Loft: return hips + fr.U * 0.85f + fr.F * 0.10f;
            case PartnerShot.Cut: return hips + fr.U * 0.25f + fr.S * 0.45f - fr.F * 0.10f;
            case PartnerShot.Pull: return hips + fr.U * 0.55f - fr.S * 0.15f - fr.F * 0.15f;
            case PartnerShot.Sweep: return hips + fr.U * 0.15f - fr.S * 0.20f + fr.F * 0.10f;
            case PartnerShot.Glance: return hips + fr.U * 0.35f + fr.S * 0.10f - fr.F * 0.05f;
            case PartnerShot.Edge: case PartnerShot.PlayAndMiss: return hips + fr.U * 0.50f + fr.S * 0.10f + fr.F * 0.15f;
            default: return hips + fr.U * 0.70f + fr.S * 0.05f + fr.F * 0.15f;
        }
    }

    /// Where the hands are at the top of the backlift, from the stance hips.
    private static Vector3 HandsAtTop(PartnerShot s, in Keys k, in Frame fr)
    {
        switch (s)
        {
            case PartnerShot.Defend: return Vector3.Lerp(k.handsS, k.handsC, 0.25f) + fr.U * 0.18f - fr.F * 0.05f;
            case PartnerShot.Cut: case PartnerShot.Pull: return k.hipsS + fr.U * 0.55f + fr.S * 0.22f - fr.F * 0.12f;
            case PartnerShot.Sweep: return k.hipsS + fr.U * 0.35f + fr.S * 0.22f - fr.F * 0.12f;
            default: return k.hipsS + fr.U * 0.42f + fr.S * 0.20f - fr.F * 0.16f;
        }
    }

    /// The batter's own axes: F toward the bowler, S toward the off side (where his chest faces
    /// side-on), U up; o is +1 for a right-hander.
    private struct Frame
    {
        public Vector3 F, S, U;
        public float o;
        public Frame(float offSign)
        {
            o = offSign < 0f ? -1f : 1f;
            F = Vector3.left;
            S = new Vector3(0f, 0f, o);
            U = Vector3.up;
        }
    }

    private static Vector3 SafeNormal(Vector3 v, Vector3 fallback) => v.sqrMagnitude > 1e-8f ? v.normalized : fallback;

    /// `v` made perpendicular to `axis` (unit), or `fallback` made so.
    private static Vector3 Perpendicular(Vector3 v, Vector3 axis, Vector3 fallback)
    {
        Vector3 p = Vector3.ProjectOnPlane(v, axis);
        if (p.sqrMagnitude < 1e-6f) p = Vector3.ProjectOnPlane(fallback, axis);
        return SafeNormal(p, Vector3.up);
    }

    private static float Smooth(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }

    /// Cubic Hermite from p0 (tangent m0) to p1 (tangent m1), tangents per unit u.
    private static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float u)
    {
        float u2 = u * u, u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * p0 + (u3 - 2f * u2 + u) * m0 + (-2f * u3 + 3f * u2) * p1 + (u3 - u2) * m1;
    }
}
