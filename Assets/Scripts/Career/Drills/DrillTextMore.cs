/// <summary>
/// The coach's one thing to work on for the second set of drills (DrillDefsMore), by what got the
/// better of him most (DrillText.Verdict). Pure (DrillCoachTests).
/// </summary>
public static class DrillTextMore
{
    public static string Work(eDrill d, eDrillTag bad)
    {
        switch (d)
        {
            case eDrill.StraightHitting:
                return bad == eDrillTag.Edged ? "Head still and the full face: the edge comes from a bat that turns."
                     : bad == eDrillTag.Bowled ? "Play down the line of the ball, not across it."
                     : bad == eDrillTag.LeftStraight ? "A full ball on off stump is there to be driven."
                     : "Let it come, then swing through the line, back past the bowler.";
            case eDrill.Swing:
                return bad == eDrillTag.Edged || bad == eDrillTag.Fished ? "Play the swinging ball late, under your eyes - or leave it."
                     : bad == eDrillTag.Bowled ? "The inswinger needs bat and pad together."
                     : bad == eDrillTag.Mistimed ? "Keep it down: soft hands and a full face against the moving ball."
                     : bad == eDrillTag.LeftStraight ? "The one swinging in at the stumps must be played."
                     : "Watch the shine: it tells you which way it will go.";
            case eDrill.PowerHitting:
                return bad == eDrillTag.Mistimed ? "Full swing, through the line: you are getting under it but not through it."
                     : bad == eDrillTag.Edged || bad == eDrillTag.Beaten ? "Base set and still before you swing. Power comes from timing."
                     : bad == eDrillTag.Wasted ? "Every ball in the slot is one to hit."
                     : "Stay balanced and hit through the ball, not at it.";
            case eDrill.DeathOvers:
                return bad == eDrillTag.Beaten || bad == eDrillTag.Bowled ? "Read the length early: the yorker and the slower ball come out of the same hand."
                     : bad == eDrillTag.Edged ? "Make room and hit with a straight bat - the edge flies to third man."
                     : bad == eDrillTag.LeftStraight ? "At the death every ball on the stumps has to be scored off."
                     : "A dot ball at the death is a small win for the bowler. Find a run.";
            case eDrill.SoftHands:
                return bad == eDrillTag.Edged ? "Loosen the bottom hand: a soft edge dies, a hard one carries."
                     : bad == eDrillTag.Bowled || bad == eDrillTag.LeftStraight ? "On the stumps you must play: bat and pad close together."
                     : bad == eDrillTag.Beaten ? "Get forward to the pitch of it and smother it."
                     : "Defend under your eyes and let the ball come to the bat.";
            default:
                return "Keep at it.";
        }
    }
}
