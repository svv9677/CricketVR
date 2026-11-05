using UnityEngine;

/// <summary>
/// What a contact would have been on the real-width blade. The bat shield (eDifficulty) widens the
/// blade for finding a hit (BatContact.Sweep) and BatContact.Respond reads quality off that wider
/// blade, so on Easy a ball met a full blade's width outside the real edge can still come off the
/// "middle". The career measures how the player really bats and must never be flattered by the
/// shield, so every contact is also judged here against the blade as it really is. Nothing here
/// changes the shot: the ball still does what the widened bat says.
///
/// Pure maths on the sweep's Hit, in the bat's local units (BatContact's frames): hit.localBall is
/// the ball's centre at the touch, hit.localBat the touched point on the (widened) blade box. The
/// widening is across the face only (x), so:
///   * centre within the real half-width: the real blade is met at the same point with the same
///     normal - the contact is re-judged with the real width (lateral share, rounded edge);
///   * centre outside it but within a ball's radius: the real blade is met on its side - an edge;
///   * centre further out than that: the real blade never touches it - WouldHaveMissed, quality 0.
/// The quality and edge rules are BatContact.Respond's own, run on the real blade, so at width
/// multiplier 1 this agrees with the game exactly (BallLedgerTests).
///
/// It judges from where the ball's centre was, not where it was going: right for the contacts a
/// swing makes (the ball comes at the face), but a ball sliding sideways across the blade onto
/// the widened side would have gone on to meet the real side, and is called WouldHaveMissed here
/// (rare: the bat would have to slide edge-first into the ball).
/// </summary>
public static class RealBlade
{
    public struct Contact
    {
        public eRealContact contact;
        /// BatContact quality 0..1 on the real blade; 0 when it would have missed.
        public float quality;
        public bool edge;
        /// Where on the real blade, as a batter would describe it (meaningless when missed).
        public BatContact.ContactKind kind;
    }

    /// <param name="hit">The sweep's contact, found with widthMultiplier.</param>
    /// <param name="scale">The bat's uniform scale (local units to metres), as passed to Sweep.</param>
    /// <param name="widthMultiplier">The shield's widening, as passed to Sweep (1 = the real blade).</param>
    public static Contact Evaluate(BatContact.Hit hit, BatContact.Blade blade, float scale, float widthMultiplier)
    {
        if (!OnRealBlade(hit, blade, scale, widthMultiplier, out BatContact.Hit real))
            return new Contact { contact = eRealContact.WouldHaveMissed, quality = 0f, edge = false, kind = BatContact.ContactKind.ThinEdge };
        // Respond's quality, edge and kind depend only on where the blade was met, not on the
        // velocities, so they are read off a still bat and a still ball.
        // A centre outside the real half-width always reads as a (thin) edge there: OnRealBlade
        // turns the normal toward it from the side, so Respond's centre lands outside too.
        BatContact.Result r = BatContact.Respond(real, blade, scale, Vector3.zero, Vector3.zero, BallFlight.Mass, 1f);
        return new Contact
        {
            contact = r.edge ? eRealContact.Edge : eRealContact.Clean,
            quality = r.quality,
            edge = r.edge,
            kind = r.kind,
        };
    }

    /// <summary>
    /// The same contact moved onto the real blade, or false when the real blade could not have
    /// touched the ball (its centre further outside the real half-width than a ball's radius).
    /// </summary>
    public static bool OnRealBlade(BatContact.Hit hit, BatContact.Blade blade, float scale, float widthMultiplier,
                                   out BatContact.Hit real)
    {
        real = hit;
        float widen = Mathf.Max(1f, widthMultiplier);
        float halfWidth = blade.halfWidth;
        float ballRadius = BallFlight.Radius / Mathf.Max(scale, 1e-5f);
        Vector3 centre = hit.localBall;
        float across = Mathf.Abs(centre.x);
        // Not widened, or met within the real width: the widened box and the real one are the same
        // box at that x, so the sweep's own touch and normal already are the real blade's.
        if (widen <= 1f + 1e-4f || across <= halfWidth)
            return true;
        if (across > halfWidth + ballRadius)
            return false;
        // Outside the real edge but within a ball's radius: the real blade is met on its side (or
        // the rounding into it). The nearest point of the real blade box, and the normal toward
        // the ball from there - as Sweep reads a first touch.
        Vector3 min = new Vector3(-halfWidth, blade.backY, blade.toeZ);
        Vector3 max = new Vector3(halfWidth, blade.faceY, blade.shoulderZ);
        Vector3 closest = Vector3.Max(min, Vector3.Min(max, centre));
        // Keep the place along the blade the sweep found (the handle lies beyond shoulderZ).
        closest.z = hit.localBat.z;
        Vector3 offset = centre - closest;
        real.localBat = closest;
        if (offset.sqrMagnitude > 1e-10f)
            real.localNormal = offset.normalized;
        return true;
    }
}
