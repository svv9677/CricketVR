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
    /// Shortest credible flight time from release to pitching, in seconds.
    public const float MinFlightTime = 0.18f;
    /// Height of the ball's centre when it pitches (its radius).
    public const float BounceHeight = 0.05f;

    public struct Solution
    {
        public Vector3 releasePosition;
        public Vector3 releaseVelocity;
        public float flightTime;
        public float pitchX;
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
            $"({SpeedKph:F0} km/h) pitchAt x={pitchX:F2} flight={flightTime:F3}s" +
            (string.IsNullOrEmpty(warnings) ? "" : $"  [corrected: {warnings}]");
    }

    /// <summary>
    /// Work out where the ball should leave the hand and how fast, for a given delivery.
    /// </summary>
    /// <param name="handPosition">Where the ball actually is (the bowler's hand) at release.</param>
    /// <param name="targetPitchX">World X the ball should bounce at (the config's `length`).</param>
    /// <param name="speedMps">Horizontal speed down the pitch, metres per second.</param>
    /// <param name="lateralMps">Sideways drift, metres per second.</param>
    public static Solution Solve(Vector3 handPosition, float targetPitchX, float speedMps, float lateralMps)
    {
        string warn = "";

        // --- 1. release point: trust the hand only within a plausible box -----------------------
        Vector3 release = handPosition;
        bool positionCorrected = false;
        if (!IsFinite(release))
        {
            release = NominalRelease;
            positionCorrected = true;
            warn += "release not finite; used nominal. ";
        }
        else
        {
            Vector3 clamped = new Vector3(
                Mathf.Clamp(release.x, ReleaseBoxMin.x, ReleaseBoxMax.x),
                Mathf.Clamp(release.y, ReleaseBoxMin.y, ReleaseBoxMax.y),
                Mathf.Clamp(release.z, ReleaseBoxMin.z, ReleaseBoxMax.z));
            if ((clamped - release).sqrMagnitude > 0.0001f)
            {
                warn += $"release {release.ToString("F2")} outside the plausible box -> {clamped.ToString("F2")}. ";
                release = clamped;
                positionCorrected = true;
            }
        }

        // --- 2. speed ---------------------------------------------------------------------------
        if (!IsFinite(speedMps) || speedMps <= 0f)
        {
            warn += $"speed {speedMps:F2} invalid; used {MinSpeedMps}. ";
            speedMps = MinSpeedMps;
        }
        else if (speedMps < MinSpeedMps || speedMps > MaxSpeedMps)
        {
            float c = Mathf.Clamp(speedMps, MinSpeedMps, MaxSpeedMps);
            warn += $"speed {speedMps:F1} m/s clamped to {c:F1}. ";
            speedMps = c;
        }

        // --- 3. pitching point ------------------------------------------------------------------
        float pitchX = IsFinite(targetPitchX) ? targetPitchX : 4f;
        float pitchClamped = Mathf.Clamp(pitchX, MinPitchX, MaxPitchX);
        if (!Mathf.Approximately(pitchClamped, pitchX))
        {
            warn += $"pitch x {pitchX:F2} clamped to {pitchClamped:F2}. ";
            pitchX = pitchClamped;
        }

        // The ball must still be behind its pitching point, with room to get there. This is the
        // check the old solver lacked, and its absence is what let the trajectory invert.
        float dx = pitchX - release.x;
        float minDx = MinFlightTime * speedMps;
        if (dx < minDx)
        {
            float newPitchX = release.x + minDx;
            warn += $"pitch x {pitchX:F2} is only {dx:F2} m ahead of release; pushed to {newPitchX:F2}. ";
            pitchX = newPitchX;
            dx = minDx;
        }

        // --- 4. exact projectile solve, in metres ------------------------------------------------
        //     x(t) = x0 + vx*t                       -> t = dx / vx
        //     y(t) = y0 + vy*t - 0.5*g*t^2 = bounce  -> vy = (bounce - y0 + 0.5*g*t^2) / t
        float g = Mathf.Abs(Physics.gravity.y);
        float t = dx / speedMps;
        float vy = (BounceHeight - release.y + 0.5f * g * t * t) / t;

        if (!IsFinite(vy))
        {
            warn += "vy not finite; used 0. ";
            vy = 0f;
        }

        if (!IsFinite(lateralMps)) lateralMps = 0f;

        var sol = new Solution
        {
            releasePosition = release,
            releaseVelocity = new Vector3(speedMps, vy, lateralMps),
            flightTime = t,
            pitchX = pitchX,
            speedMps = speedMps,
            releasePositionCorrected = positionCorrected,
            warnings = warn.Trim()
        };
        return sol;
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
