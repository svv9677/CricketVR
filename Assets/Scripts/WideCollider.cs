using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Positions one of the two wide trigger boxes at the batsman's end. The ball leaving the box is a
/// wide (KeeperCollider), so the box's inner face is the wide line.
///
/// Measurements are from the middle stump, in metres, per the Laws / ICC limited-overs playing
/// conditions:
///   * off side: the wide guideline, 0.889 m (35 in, 17 in inside the 1.32 m return crease).
///   * leg side: outside the leg stump - stumps are 0.2286 m across the outside, so its outer
///     edge is 0.1143 m out.
/// A ball is only wide when it passes entirely outside the line, so the box's inner face sits a
/// full ball diameter beyond it: the trigger fires on any overlap, and a ball whose inner edge
/// clears the line has its outer edge a diameter further out.
///
/// The off side is +Z for a right-hander and -Z for a left-hander. The line moves with the
/// batter once they step across (the umpire judges from where the striker stands).
/// </summary>
public class WideCollider : MonoBehaviour
{

    [SerializeField] private Transform player;
    [SerializeField] private bool leg;
    private BoxCollider boxCollider;

    private const float OffSideWideLine = 0.889f;
    private const float LegSideWideLine = 0.1143f;
    /// How far the player can drift from the centre line before the wide line follows them.
    private const float StanceTolerance = 0.12f;
    /// Fallback if the ball cannot be measured (regulation diameter).
    private const float DefaultBallDiameter = 0.072f;

    // Start is called before the first frame update
    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    // Update is called once per frame
    void Update()
    {
        bool leftHanded = Main.Instance != null && Main.Instance.BattingStyle == eBattingStyle.LeftHanded;
        float offSign = leftHanded ? -1f : 1f;
        float side = leg ? -offSign : offSign;

        float line = (leg ? LegSideWideLine : OffSideWideLine) + BallDiameter();
        // Distance the batter has stepped toward this side, beyond normal stance drift.
        float step = Mathf.Max(0f, player.position.z * side - StanceTolerance);
        float innerFace = line + step;

        float centreZ = side * (innerFace + boxCollider.size.z * transform.lossyScale.z / 2f);
        transform.position = new Vector3(transform.position.x, transform.position.y, centreZ);
    }

    private static SphereCollider ballSphere;

    private static float BallDiameter()
    {
        if (Main.Instance == null || Main.Instance.theBall == null)
            return DefaultBallDiameter;
        if (ballSphere == null)
            ballSphere = Main.Instance.theBall.GetComponent<SphereCollider>();
        var sphere = ballSphere;
        if (sphere == null)
            return DefaultBallDiameter;
        Vector3 s = sphere.transform.lossyScale;
        return 2f * sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }
}
