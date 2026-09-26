using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Six distance. Blank for every other outcome. When a hit clears the rope on the full, the flight
/// is carried on from where it crossed - same speed, same drag (BallFlight) - to where it would
/// have come down, and the display shows how far that is from where the ball was struck.
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

    /// The bat met the ball here.
    public void RecordHit(Vector3 position)
    {
        hitPoint = position;
        setText("");
    }

    /// The ball has just cleared the rope without bouncing.
    public void OnSix(Vector3 position, Vector3 velocity)
    {
        float ground = BallFlight.OutfieldY + BallFlight.Radius;
        BallFlight.Simulate(position, velocity, BallFlight.DeliveryEffects.None, 10f, samples,
            s => s.position.y <= ground && s.velocity.y <= 0f || s.bounced);
        Vector3 landing = samples.Count > 0 ? samples[samples.Count - 1].position : position;
        float distance = new Vector2(landing.x - hitPoint.x, landing.z - hitPoint.z).magnitude;
        setText($"{distance:F0} m");
    }

    public void setText(string text)
    {
        if (myText != null)
            myText.text = text;
    }
}
