/// <summary>
/// The career player's own shots, for the record book (phase 7): the shot he struck, and the four
/// or six it went for. Only watching - nothing here changes a ball.
/// </summary>
public partial class CareerMatch
{
    /// A shot off the bat (Bat.ShotStruck): kept until the ball is settled.
    private void OnShotStruck(ShotInfo info) { }

    /// A four or six to his name; a six also goes to the records (NoteSix, RecordMoments.YourSix).
    private void YourBoundary(Batsman b, int runs) => CountBoundary(b, runs);
}
