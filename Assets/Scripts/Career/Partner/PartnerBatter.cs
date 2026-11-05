using UnityEngine;

/// <summary>
/// The AI partner the career player watches from the non-striker's end (phase 3): a body with a
/// helmet and a bat in both hands, standing at whichever end he is batting from, playing the shot
/// PartnerDirector planned. Built only for a career match. CONTRACT STUB - the implementation
/// replaces the bodies, keeping these members.
/// </summary>
public class PartnerBatter : MonoBehaviour
{
    public static PartnerBatter Instance { get; private set; }
    public bool Ready { get; private set; }
    public Transform Head { get; private set; }
    public Transform Hips { get; private set; }
    public Transform BatTransform { get; private set; }
    public bool OnStrike { get; private set; }
    /// The top of his head in his stance (head bone in stance + 0.20 m, sampled once).
    public float StanceHeadTopY { get; private set; } = 1.75f;
    /// Where across the crease he takes guard (world z).
    public float StanceZ { get; private set; }
    public BatGrip Grip { get; private set; }

    public void TakeStrike() { OnStrike = true; }
    public void StandAtNonStriker() { OnStrike = false; }
    /// Start the swing so the bat meets the ball `contactTimeFromNow` seconds from now.
    public void Play(in ShotPlan plan, float contactTimeFromNow) { }
    public void BackToStance() { }
}
