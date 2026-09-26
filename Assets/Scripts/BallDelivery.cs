using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic set-up for the start of a delivery.
///
/// The previous approach derived the release from wherever the bowler's hand bone happened to be
/// on the frame the animation event fired, then solved for the vertical velocity with an ad-hoc
/// formula written in feet. That was neither consistent nor precise:
///
///   * the hand bone is an animated transform, so the release point varied by metres between
///     deliveries, and occasionally landed somewhere absurd (measured once at x=+1.56, y=4.05,
///     z=6.08 - mid-pitch and 4 m in the air);
///   * the old solver divided by a flight time it never checked, so once the release point was
///     past the target pitching point the time went negative and the vertical velocity flipped
///     sign - firing the ball vertically at ~42 m/s;
///   * it mixed feet and metres, and `height - startPos.y / time` was missing the parentheses
///     needed to mean `(height - startPos.y) / time`.
///
/// This replaces all of that with one exact projectile solve in metres, plus explicit validation
/// of every input, so the same configuration always produces the same delivery.
/// </summary>
public static class BallDelivery
{
    // ---- Pitch geometry, measured from the scene (metres, world space) ----------------------
    /// Batsman's stumps. The ball travels in +X, from the bowler's end toward here.
    public const float BatsmanStumpsX = 10.30f;
    /// Bowler's stumps.
    public const float BowlerStumpsX = -10.36f;

    // ---- Release window ----------------------------------------------------------------------
    /// Nominal release point: roughly where a bowler's hand is at the top of the action.
    /// Used directly when the hand transform cannot be trusted.
    public static readonly Vector3 NominalRelease = new Vector3(-8.95f, 2.10f, 0f);
    /// The hand may pull the release point around within this box. Anything outside is clamped,
    /// which bounds the worst case without decoupling the ball from the animation entirely.
    /// Generous on purpose: a normal delivery stride and follow-through must never be clamped,
    /// because clamping moves the ball and that is visible. This only has to reject the absurd
    /// (measured failures were x=+1.56 / y=4.05 / z=6.08, and the ball left at the keeper x=+16).
    /// The y floor is 0.50, not the 0.80 first guessed: the release animation event fires with the
    /// hand between 0.63 m and 1.7 m, so a 0.80 floor clamped legitimate deliveries and nudged the
    /// ball upward at release. Measured release heights: 0.63, 0.65, 1.17, 1.21, 1.66.
    public static readonly Vector3 ReleaseBoxMin = new Vector3(-12.50f, 0.50f, -2.50f);
    public static readonly Vector3 ReleaseBoxMax = new Vector3(-5.00f, 3.00f, 2.50f);

    // ---- Plausibility limits -------------------------------------------------------------------
    /// 54 - 162 km/h. Anything outside this is a configuration error, not a delivery.
    public const float MinSpeedMps = 15f;
    public const float MaxSpeedMps = 45f;
    /// The ball must pitch on the strip, and far enough away to be a real delivery rather than a
    /// ball dropped at the bowler's feet or a full toss at the stumps.
    public const float MinPitchX = -7.0f;
    public const float MaxPitchX = 9.5f;
    /// Shortest credible flight time from release to pitching, in seconds. Was 0.18, which clamped
    /// real bouncers: at the pace configs' top speed (43 m/s) a ball pitching 12 m from the stumps
    /// is in the air only 0.168 s from the nominal release, so it was pushed ~0.5 m fuller. 0.15 s
    /// still keeps the pitch well ahead of the hand, which is all this guard is for.
    public const float MinFlightTime = 0.15f;
    /// Height of the ball's centre when it pitches (its radius). Regulation ball, 72 mm across.
    public const float BounceHeight = 0.036f;
    /// Batsman's popping crease is 1.22 m in front of the stumps. Anything pitching past it reaches
    /// the batter on the full.
    public const float PoppingCreaseDistance = 1.22f;
    /// A bouncer that passes the stumps higher than this is over the batter's head and would be
    /// called wide (or a no-ball for height). The ball is pulled fuller until it clears the check.
    public const float MaxHeightAtStumps = 1.9f;
    /// How far fuller each retry pulls a too-high bouncer, and how many times it may.
    const float FullerStep = 0.5f;
    const int MaxFullerRetries = 8;

    public struct Solution
    {
        public Vector3 releasePosition;
        public Vector3 releaseVelocity;
        public float flightTime;
        public float pitchX;
        public float lineZ;
        /// Height of the ball's centre as it passes the batsman's stumps.
        public float heightAtStumps;
        public float speedMps;
        /// True only when the release POSITION had to be moved. The caller should reposition the
        /// ball only in that case - repositioning on a normal delivery is a visible snap.
        public bool releasePositionCorrected;
        /// Non-empty when an input had to be corrected. Worth logging - it means something upstream
        /// (usually the ball not being in the bowler's hand) is wrong.
        public string warnings;

        public float SpeedKph => speedMps * 3.6f;
        public override string ToString() =>
            $"release={releasePosition.ToString("F2")} vel={releaseVelocity.ToString("F2")} " +
            $"({SpeedKph:F0} km/h) pitchAt x={pitchX:F2} line z={lineZ:F2} h@stumps={heightAtStumps:F2} flight={flightTime:F3}s" +
            (string.IsNullOrEmpty(warnings) ? "" : $"  [corrected: {warnings}]");
    }

    /// <summary>
    /// Work out where the ball should leave the hand and how fast, for a given delivery.
    /// </summary>
    /// <param name="handPosition">Where the ball actually is (the bowler's hand) at release.</param>
    /// <param name="targetPitchX">World X the ball should bounce at (the config's `length`).</param>
    /// <param name="speedMps">Horizontal speed down the pitch at release, metres per second.</param>
    /// <param name="targetLineZ">World Z the ball should be at when it reaches the batsman's
    /// stumps, after swing, the bounce and any turn. This is what the bowler controls, and what
    /// decides whether it is a wide.</param>
    public static Solution Solve(Vector3 handPosition, float targetPitchX, float speedMps, float targetLineZ,
                                 BallFlight.DeliveryEffects effects)
    {
        string warn = "";
        Vector3 release = ValidateRelease(handPosition, ref warn, out bool positionCorrected);
        speedMps = ValidateSpeed(speedMps, ref warn);
        float pitchX = ValidatePitch(targetPitchX, release, speedMps, ref warn);
        if (!IsFinite(targetLineZ)) targetLineZ = 0f;

        Vector3 velocity = Aim(release, pitchX, speedMps, targetLineZ, effects, out Shot shot);

        // A bouncer from a high or forward release can sail over the batter's head. Pull it fuller,
        // half a metre at a time, until it passes the stumps at a legal height. The step is small
        // so the ball stays short - just not absurdly so.
        float fullest = BatsmanStumpsX - PoppingCreaseDistance;
        for (int i = 0; i < MaxFullerRetries && shot.heightAtStumps > MaxHeightAtStumps && pitchX < fullest; i++)
        {
            float fuller = Mathf.Min(pitchX + FullerStep, fullest);
            warn += $"bouncer {shot.heightAtStumps:F2} m high at the stumps; pitch x {pitchX:F2} -> {fuller:F2}. ";
            pitchX = fuller;
            velocity = Aim(release, pitchX, speedMps, targetLineZ, effects, out shot);
        }

        if (Mathf.Abs(shot.pitchX - pitchX) > 0.1f || Mathf.Abs(shot.lineZ - targetLineZ) > 0.05f)
            warn += $"aim missed by length {shot.pitchX - pitchX:F2} m, line {shot.lineZ - targetLineZ:F2} m. ";

        return new Solution
        {
            releasePosition = release,
            releaseVelocity = velocity,
            flightTime = shot.flightTime,
            pitchX = shot.pitchX,
            lineZ = shot.lineZ,
            heightAtStumps = shot.heightAtStumps,
            speedMps = speedMps,
            releasePositionCorrected = positionCorrected,
            warnings = warn.Trim()
        };
    }

    /// Release point: trust the hand only within a plausible box.
    static Vector3 ValidateRelease(Vector3 hand, ref string warn, out bool corrected)
    {
        corrected = false;
        if (!IsFinite(hand))
        {
            corrected = true;
            warn += "release not finite; used nominal. ";
            return NominalRelease;
        }
        Vector3 clamped = new Vector3(
            Mathf.Clamp(hand.x, ReleaseBoxMin.x, ReleaseBoxMax.x),
            Mathf.Clamp(hand.y, ReleaseBoxMin.y, ReleaseBoxMax.y),
            Mathf.Clamp(hand.z, ReleaseBoxMin.z, ReleaseBoxMax.z));
        if ((clamped - hand).sqrMagnitude > 0.0001f)
        {
            warn += $"release {hand.ToString("F2")} outside the plausible box -> {clamped.ToString("F2")}. ";
            corrected = true;
            return clamped;
        }
        return hand;
    }

    static float ValidateSpeed(float speedMps, ref string warn)
    {
        if (!IsFinite(speedMps) || speedMps <= 0f)
        {
            warn += $"speed {speedMps:F2} invalid; used {MinSpeedMps}. ";
            return MinSpeedMps;
        }
        if (speedMps < MinSpeedMps || speedMps > MaxSpeedMps)
        {
            float c = Mathf.Clamp(speedMps, MinSpeedMps, MaxSpeedMps);
            warn += $"speed {speedMps:F1} m/s clamped to {c:F1}. ";
            return c;
        }
        return speedMps;
    }

    static float ValidatePitch(float targetPitchX, Vector3 release, float speedMps, ref string warn)
    {
        float pitchX = IsFinite(targetPitchX) ? targetPitchX : 4f;
        float pitchClamped = Mathf.Clamp(pitchX, MinPitchX, MaxPitchX);
        if (!Mathf.Approximately(pitchClamped, pitchX))
        {
            warn += $"pitch x {pitchX:F2} clamped to {pitchClamped:F2}. ";
            pitchX = pitchClamped;
        }

        // The ball must still be behind its pitching point, with room to get there. This is the
        // check the old solver lacked, and its absence is what let the trajectory invert. Too
        // short to reach means the nearest reachable length, not a wild ball.
        float minDx = MinFlightTime * speedMps;
        if (pitchX - release.x < minDx)
        {
            float newPitchX = release.x + minDx;
            warn += $"pitch x {pitchX:F2} is only {pitchX - release.x:F2} m ahead of release; pushed to {newPitchX:F2}. ";
            pitchX = newPitchX;
        }
        return pitchX;
    }

    /// <summary>
    /// Shoot with the real flight model (drag, swing, bounce, turn). Start from the drag-free
    /// ballistic answer, then correct vy (length) and vz (line) in turn with capped Newton steps.
    /// The two barely interact - vy sets where it lands, vz the line - so alternating converges in
    /// a few rounds, and the caps keep a bad derivative from ever throwing the ball into the sky.
    /// Each shot is ~150 physics steps. Returns the best shot tried, so a length that cannot be
    /// hit exactly (a very steep bouncer, say) gives the nearest one, never the last wild probe.
    /// </summary>
    static Vector3 Aim(Vector3 release, float pitchX, float speedMps, float targetLineZ,
                       BallFlight.DeliveryEffects effects, out Shot best)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        float t = (pitchX - release.x) / speedMps;
        float vy = (BounceHeight - release.y + 0.5f * g * t * t) / t;
        float vz = (targetLineZ - release.z) * speedMps / (BatsmanStumpsX - release.x);
        if (!IsFinite(vy)) vy = 0f;
        if (!IsFinite(vz)) vz = 0f;

        const float h = 0.05f, maxStepVy = 2f, maxStepVz = 1f;
        Shot shot = Fire(release, new Vector3(speedMps, vy, vz), effects);
        best = shot;
        Vector3 bestVelocity = new Vector3(speedMps, vy, vz);
        for (int round = 0; round < 8; round++)
        {
            float errLen = shot.pitchX - pitchX;
            if (Mathf.Abs(errLen) > 0.02f)
            {
                Shot probe = Fire(release, new Vector3(speedMps, vy + h, vz), effects);
                float slope = (probe.pitchX - shot.pitchX) / h;   // metres of length per m/s of vy
                if (slope > 1e-3f)
                    vy -= Mathf.Clamp(errLen / slope, -maxStepVy, maxStepVy);
                shot = Fire(release, new Vector3(speedMps, vy, vz), effects);
            }
            float errLine = shot.lineZ - targetLineZ;
            if (Mathf.Abs(errLine) > 0.01f)
            {
                Shot probe = Fire(release, new Vector3(speedMps, vy, vz + h), effects);
                float slope = (probe.lineZ - shot.lineZ) / h;
                if (Mathf.Abs(slope) > 1e-3f)
                    vz -= Mathf.Clamp(errLine / slope, -maxStepVz, maxStepVz);
                shot = Fire(release, new Vector3(speedMps, vy, vz), effects);
            }
            if (AimError(shot, pitchX, targetLineZ) < AimError(best, pitchX, targetLineZ))
            {
                best = shot;
                bestVelocity = new Vector3(speedMps, vy, vz);
            }
            if (Mathf.Abs(shot.pitchX - pitchX) < 0.02f && Mathf.Abs(shot.lineZ - targetLineZ) < 0.01f)
                break;
        }
        return bestVelocity;
    }

    /// Line counts double: a length error changes the ball, a line error can make it a wide.
    static float AimError(Shot s, float pitchX, float lineZ) =>
        Mathf.Abs(s.pitchX - pitchX) + 2f * Mathf.Abs(s.lineZ - lineZ);

    /// Where one candidate release pitches, and where it is when it reaches the stumps.
    public struct Shot
    {
        public float pitchX;
        public float lineZ;
        /// Height of the ball's centre as it passes the stumps (0 if it never got there).
        public float heightAtStumps;
        public float flightTime;
    }

    private static readonly List<BallFlight.Sample> samples = new List<BallFlight.Sample>(512);

    public static Shot Fire(Vector3 release, Vector3 velocity, BallFlight.DeliveryEffects effects)
    {
        // Run until it has both pitched and passed the stumps, so a trial that would be a full toss
        // still reports where it would land - the length always has a slope to follow.
        BallFlight.Simulate(release, velocity, effects, 4f, samples,
            s => (s.bounced && s.position.x >= BatsmanStumpsX) || s.position.x > BatsmanStumpsX + 30f || s.position.y < -1f);
        var shot = new Shot { pitchX = BatsmanStumpsX + 30f, lineZ = release.z, heightAtStumps = 0f, flightTime = 0f };
        bool pitched = false, crossed = false;
        for (int i = 0; i < samples.Count; i++)
        {
            if (!pitched && samples[i].bounced)
            {
                pitched = true;
                shot.pitchX = samples[i].position.x;
                shot.flightTime = samples[i].time;
            }
            if (!crossed && samples[i].position.x >= BatsmanStumpsX)
            {
                crossed = true;
                var prev = i > 0 ? samples[i - 1] : samples[i];
                float span = samples[i].position.x - prev.position.x;
                float f = span > 1e-5f ? Mathf.Clamp01((BatsmanStumpsX - prev.position.x) / span) : 1f;
                shot.lineZ = Mathf.Lerp(prev.position.z, samples[i].position.z, f);
                shot.heightAtStumps = Mathf.Lerp(prev.position.y, samples[i].position.y, f);
            }
        }
        return shot;
    }

    /// <summary>
    /// Where the ball will actually bounce, integrating the same way PhysX will. Useful for
    /// verifying the solve without running the game.
    /// </summary>
    public static float PredictPitchX(Vector3 release, Vector3 velocity)
    {
        float g = Mathf.Abs(Physics.gravity.y);
        // y0 + vy t - 0.5 g t^2 = BounceHeight  ->  solve the quadratic for the positive root
        float a = -0.5f * g, b = velocity.y, c = release.y - BounceHeight;
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return float.NaN;
        float sq = Mathf.Sqrt(disc);
        float t1 = (-b + sq) / (2f * a);
        float t2 = (-b - sq) / (2f * a);
        float t = Mathf.Max(t1, t2);
        if (t <= 0f) return float.NaN;
        return release.x + velocity.x * t;
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
}
