using UnityEngine;

/// <summary>
/// A player clapping (TeamTalk): his hands brought up in front of his chest by his HumanoidReach,
/// the palms facing, apart and together a few times a second - and a clap (GameSfx "clap") each
/// time they meet, from between his hands, so what you hear is what his arms do.
///
/// Only when his hands are free: it runs after his owner's Update (AnimatedFielder sets the reach
/// every frame), and only takes the hands when the owner has asked nothing of them this frame
/// (weight 0) - never while he is going for the ball, holding it or throwing it. The palms' gap is
/// applied as set (HumanoidReach does not smooth it), so the claps are crisp; the arms rise and fall
/// on the reach's own springs.
/// </summary>
[DefaultExecutionOrder(200)]
public class PlayerClap : MonoBehaviour
{
    /// Claps a second (a person's applause runs 3-5), and how far the palms open between claps.
    private const float Rate = 3.4f, OpenGap = 0.2f, ClosedGap = 0.012f;
    /// The hands this far up from his feet and out in front of him.
    private const float Height = 1.2f, Ahead = 0.3f;
    /// The palms meet once the arms are this far up (the reach's smoothed weight).
    private const float UpEnough = 0.75f;
    /// ...or this long after he started to raise them.
    private const float RaiseSeconds = 0.45f;

    private HumanoidReach reach;
    private int clapsLeft;
    private float phase;
    private float startAt = -1f;
    private bool holding;
    private float heldSince;
    private float loudness = 1f, minDistance = 4f;

    /// Clapping now (or about to start).
    public bool Busy => clapsLeft > 0;

    private void Awake()
    {
        reach = GetComponent<HumanoidReach>();
        if (reach == null)
            reach = gameObject.AddComponent<HumanoidReach>();
    }

    /// Clap `count` times, starting `delay` seconds from now. `minDistance`: the claps are at full
    /// loudness within it (a long way off, a group by the boundary is still heard).
    public void Clap(int count, float delay, float volume = 1f, float heardWithin = 4f)
    {
        clapsLeft = Mathf.Max(clapsLeft, count);
        startAt = Time.time + Mathf.Max(0f, delay);
        phase = 0.5f;           // starts with the hands apart
        loudness = volume;
        minDistance = heardWithin;
    }

    private void Update()
    {
        if (reach == null || !reach.Ready)
            return;
        // The owner (AnimatedFielder) writes the reach every Update before this; LateUpdate below
        // puts our weight back to 0, so a weight here is always the owner's own ask.
        bool free = reach.weight <= 0.001f;
        if (clapsLeft <= 0 || Time.time < startAt || !free)
        {
            Release();
            return;
        }
        if (!holding)
            heldSince = Time.time;
        holding = true;
        reach.handTarget = transform.position + transform.up * Height + transform.forward * Ahead;
        reach.weight = 1f;
        reach.palmFacing = Vector3.zero;     // palms to each other
        reach.leftHandShare = 1f;
        reach.quickness = 1f;
        // Up once the arms are up - or after the time they take to come up: a player out of sight is
        // not animated (culled), so his arms never rise, but his claps should still be heard.
        if (reach.SmoothedWeight < UpEnough && Time.time - heldSince < RaiseSeconds)
        {
            reach.handGap = OpenGap;
            return;
        }
        float before = phase;
        phase += Rate * Time.deltaTime;
        if (Mathf.Floor(phase) > Mathf.Floor(before))
            Meet();
        // Apart quickly, together quickly: |sin| shape, closed at whole phases.
        float open = Mathf.Abs(Mathf.Sin(phase * Mathf.PI));
        reach.handGap = Mathf.Lerp(ClosedGap, OpenGap, Mathf.Sqrt(open));
    }

    private void Meet()
    {
        clapsLeft--;
        Vector3 at = reach.LeftHandBone != null && reach.RightHandBone != null
            ? 0.5f * (reach.LeftHandBone.position + reach.RightHandBone.position)
            : reach.handTarget;
        GameSfx.Play("clap", at, GameAudio.Channel.Fielders, loudness * Random.Range(0.8f, 1f),
                     Random.Range(0.94f, 1.06f), minDistance, 140f, 150);
    }

    /// Our weight off again once the IK pass has used it, so next frame's check sees only the owner's.
    private void LateUpdate()
    {
        if (holding && reach != null)
            reach.weight = 0f;
    }

    /// The hands back to the owner once, as the clapping ends.
    private void Release()
    {
        if (!holding)
            return;
        holding = false;
        reach.handGap = 0.1f;
    }
}
