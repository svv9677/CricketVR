using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Six distance. Blank for every other outcome. When a hit clears the rope on the full, the flight
/// is carried on from where it crossed - same speed, same drag (BallFlight) - to where it would
/// have come down, and the display shows how far that is from where the ball was struck.
/// The shot card (ShotCard) also gets every hit's projected carry, and the six distance.
/// </summary>
public class ShotDistance : MonoBehaviour
{
    public static ShotDistance Instance;

    private TMP_Text myText;
    private Vector3 hitPoint;
    private readonly List<BallFlight.Sample> samples = new List<BallFlight.Sample>(1024);

    void Start()
    {
        if (ShotDistance.Instance == null)
            ShotDistance.Instance = this;

        myText = GetComponent<TMP_Text>();
        setText("");
    }

    /// The bat met the ball here (called once the ball has its new velocity). The shot card gets
    /// the projected carry straight away: where the ball will first land, ignoring fielders.
    public void RecordHit(Vector3 position)
    {
        hitPoint = position;
        setText("");
        Rigidbody ball = Main.Instance != null ? Main.Instance.theBallRigidBody : null;
        if (ShotCard.Instance == null || ball == null)
            return;
        BallFlight.Simulate(position, ball.linearVelocity, BallFlight.DeliveryEffects.None, 8f, samples, s => s.bounced);
        ShotCard.Instance.SetDistance(HorizontalFromHit(LastSample(position)), false);
    }

    /// The ball has just cleared the rope without bouncing.
    public void OnSix(Vector3 position, Vector3 velocity)
    {
        float ground = BallFlight.OutfieldY + BallFlight.Radius;
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, 10f, samples,
            s => s.position.y <= ground && s.velocity.y <= 0f || s.bounced);
        float distance = HorizontalFromHit(LastSample(position));
        setText($"{distance:F0} m");
        if (ShotCard.Instance != null)
            ShotCard.Instance.SetDistance(distance, true);
    }

    private Vector3 LastSample(Vector3 fallback) => samples.Count > 0 ? samples[samples.Count - 1].position : fallback;

    private float HorizontalFromHit(Vector3 point) => new Vector2(point.x - hitPoint.x, point.z - hitPoint.z).magnitude;

    public void setText(string text)
    {
        if (myText != null)
            myText.text = text;
    }
}
