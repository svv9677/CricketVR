using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one model of how the ball moves, shared by everything that needs to know:
/// <see cref="Ball"/> applies it to the live Rigidbody, <see cref="BallDelivery"/> aims deliveries
/// with it, and the fielders predict where a hit ball will go with it. Keeping it in one place is
/// what lets the prediction and the aiming agree with what the player actually sees.
///
/// Real-world numbers (metres, kilograms, seconds):
///   * ball 0.16 kg, 72 mm across.
///   * drag  a = -k |v| v,  k = 0.5 rho Cd A / m, with Cd 0.4 (a new ball).
///   * swing a = s * SwingCoefficientMax * (0.5 rho A / m) v^2 sideways, until the first bounce.
///     s is the config's 0..1 swing amount; at the maximum a 37 m/s delivery moves about 0.5 m
///     in the air, the top end of real conventional swing.
///   * turn  the horizontal velocity is rotated by up to MaxTurnDegrees at the first bounce.
///   * bounce comes from the physics materials (see Bounce below), matched to PhysX as measured:
///     normal speed reversed and scaled by e, along-the-ground speed all but kept.
///   * rolling resistance on the ground, RollingDecel.
/// </summary>
public static class BallFlight
{
    public const float Mass = 0.16f;
    public const float Radius = 0.036f;
    public const float AirDensity = 1.2f;
    public const float DragCoefficient = 0.4f;
    public const float SwingCoefficientMax = 0.25f;
    public const float MaxTurnDegrees = 6f;
    /// Deceleration of a ball rolling on grass or the pitch, m/s^2.
    public const float RollingDecel = 2.0f;
    public const float BounceThreshold = 2f;

    static float Area => Mathf.PI * Radius * Radius;
    /// Drag per unit v^2, per unit mass: a = -DragK |v| v.
    public static float DragK => 0.5f * AirDensity * DragCoefficient * Area / Mass;
    /// Aerodynamic force per unit v^2 and unit coefficient, per unit mass.
    public static float AeroK => 0.5f * AirDensity * Area / Mass;

    // ---- Surfaces ------------------------------------------------------------------------------
    // Must match the physics materials: ball (0.5 bounce, 0.3 friction), pitch (0.55, 0.45),
    // outfield (0.3, 0.5), all combined by Average.
    public const float PitchRestitution = 0.525f;
    public const float OutfieldRestitution = 0.4f;
    /// The prepared strip. Everything else is outfield.
    public static readonly Vector2 PitchHalfExtent = new Vector2(12f, 3f);
    public const float PitchCentreX = -0.05f;
    /// Surface heights, measured from the colliders: the pitch base, and the stadium ground.
    public const float PitchY = -0.004f;
    public const float OutfieldY = -0.047f;

    public static bool OnPitch(Vector3 p) =>
        Mathf.Abs(p.x - PitchCentreX) < PitchHalfExtent.x && Mathf.Abs(p.z) < PitchHalfExtent.y;

    public static float GroundY(Vector3 p) => OnPitch(p) ? PitchY : OutfieldY;

    /// Air forces (drag, plus swing when swingAccelPerV2 is non-zero), as an acceleration.
    public static Vector3 AirAcceleration(Vector3 velocity, float swingAccelPerV2, float swingSign)
    {
        float speed = velocity.magnitude;
        Vector3 accel = -DragK * speed * velocity;
        if (swingAccelPerV2 != 0f)
        {
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontal.sqrMagnitude > 1e-6f)
            {
                // Sideways, perpendicular to the direction of travel in the horizontal plane.
                Vector3 side = Vector3.Cross(Vector3.up, horizontal.normalized);
                accel += side * (swingSign * swingAccelPerV2 * speed * speed);
            }
        }
        return accel;
    }

    /// Swing acceleration per v^2 for a config swing amount (0..1).
    public static float SwingAccelPerV2(float swingAmount) =>
        Mathf.Clamp01(swingAmount) * SwingCoefficientMax * AeroK;

    /// Along-the-ground speed kept through a bounce. Measured in play, not derived: PhysX takes
    /// almost nothing off a quick bounce (pitch, 35.54 -> 35.48 m/s; outfield, unchanged) even with
    /// friction set, so Coulomb friction here would put the fielders and the aim in the wrong place.
    public const float BounceTangentialRetention = 0.998f;

    /// Velocity just after a bounce, the way PhysX resolves it (e measured 0.53 on the pitch, 0.41
    /// on the outfield, matching the averaged materials).
    public static Vector3 Bounce(Vector3 velocity, bool onPitch)
    {
        float e = onPitch ? PitchRestitution : OutfieldRestitution;
        float vn = velocity.y;
        Vector3 vt = new Vector3(velocity.x, 0f, velocity.z) * BounceTangentialRetention;
        float newVn = -vn < BounceThreshold ? 0f : -e * vn;
        return new Vector3(vt.x, newVn, vt.z);
    }

    /// Rotate the horizontal velocity about the vertical: the turn off the pitch.
    public static Vector3 Turn(Vector3 velocity, float degrees)
    {
        return Quaternion.AngleAxis(degrees, Vector3.up) * velocity;
    }

    public struct Sample
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;
        public bool bounced;
    }

    /// Delivery-only effects: swing before the first bounce, turn at it.
    public struct DeliveryEffects
    {
        public float swingAccelPerV2;
        public float swingSign;
        public float turnDegrees;
        public static readonly DeliveryEffects None = default;
    }

    /// <summary>
    /// Step the ball forward from a state, recording every step. Stops after maxTime, when the ball
    /// has stopped, or when stop(sample) returns true. The step matches the physics step.
    /// </summary>
    public static void Simulate(Vector3 position, Vector3 velocity, DeliveryEffects effects,
                                float maxTime, List<Sample> output, System.Func<Sample, bool> stop = null,
                                float dt = 0f)
    {
        output.Clear();
        if (dt <= 0f) dt = Time.fixedDeltaTime;
        bool bounced = false;
        bool rolling = false;
        float t = 0f;
        while (t < maxTime)
        {
            if (!rolling)
            {
                Vector3 accel = Physics.gravity + AirAcceleration(velocity, bounced ? 0f : effects.swingAccelPerV2, effects.swingSign);
                velocity += accel * dt;
                Vector3 before = position;
                position += velocity * dt;
                float ground = GroundY(position);
                if (position.y <= ground + Radius && velocity.y < 0f)
                {
                    // Bounce where the ball actually met the ground inside the step, as the ball's
                    // continuous collision detection does - not at the end of the step, which
                    // would quantise the pitching point to ~0.3 m at pace.
                    float drop = before.y - position.y;
                    float f = drop > 1e-6f ? Mathf.Clamp01((before.y - (ground + Radius)) / drop) : 1f;
                    position = Vector3.Lerp(before, position, f);
                    position.y = ground + Radius;
                    bool first = !bounced;
                    velocity = Bounce(velocity, OnPitch(position));
                    if (first && effects.turnDegrees != 0f)
                        velocity = Turn(velocity, effects.turnDegrees);
                    bounced = true;
                    if (velocity.y == 0f)
                        rolling = true;
                }
            }
            else
            {
                velocity += AirAcceleration(velocity, 0f, 0f) * dt;
                float speed = velocity.magnitude;
                float slowed = Mathf.Max(0f, speed - RollingDecel * dt);
                velocity = speed > 1e-5f ? velocity * (slowed / speed) : Vector3.zero;
                position += velocity * dt;
                position.y = GroundY(position) + Radius;
            }
            t += dt;
            var sample = new Sample { time = t, position = position, velocity = velocity, bounced = bounced };
            output.Add(sample);
            if (rolling && velocity.sqrMagnitude < 0.01f)
                break;
            if (stop != null && stop(sample))
                break;
        }
    }
}
