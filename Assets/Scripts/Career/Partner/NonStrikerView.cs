using UnityEngine;

/// <summary>
/// Moves the career player between the ends as the strike changes while he is watching his partner
/// (phase 3): a short blink to black, the rig placed at the other end, his bat off or on, the
/// partner to the crease or to the non-striker's spot; and bowls the next ball on its own while the
/// partner is on strike. CONTRACT STUB.
/// </summary>
public class NonStrikerView : MonoBehaviour
{
    /// Mid-blink (the screen going to or coming back from black).
    public static bool Blinking { get; private set; }
}
