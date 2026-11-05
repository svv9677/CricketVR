using UnityEngine;

/// <summary>
/// What changes with the ground, as pure rules (no scene, no audio), so it is tested in EditMode
/// and tuned in one place:
///
///   Resolve       a career match is played where its plan says; a Quick Match where the player set;
///   ClubCrowd     a club crowd is a murmur that never swells (cheer == ambience), louder when fuller;
///   ForSignal     the stadium roars exactly as it always has; a club ground claps, scaled by how
///                 many are there;
///   OohVolume     no "ooh" from a dozen people on benches;
///   BoardsGroup   a ball into the rope at a club hits a wooden picket fence, not advertising boards;
///   SpectatorGroups / GlowSize / names   how the ground is dressed and labelled.
/// </summary>
public static class VenueRules
{
    public const string ClubDefaultName = "Cricket Club";

    public static eVenue Resolve(bool careerActive, eVenue planVenue, eVenue quickVenue) =>
        careerActive ? planVenue : quickVenue;

    /// CrowdController's two volumes: the bed it plays at rest and the one it lerps to when excited.
    public struct CrowdAudio { public float ambience, cheer; }

    /// Equal ambience and cheer: CrowdController's excitement lerp then changes nothing, so the club's
    /// birdsong-and-chatter loop never surges like a stadium.
    public static CrowdAudio ClubCrowd(eCrowd c)
    {
        float v = c == eCrowd.Packed ? 0.35f : c == eCrowd.Half ? 0.32f : 0.30f;
        return new CrowdAudio { ambience = v, cheer = v };
    }

    /// A one-shot for the crowd: the GameSfx group and its volume. A null group is silence.
    public struct Reaction { public string group; public float volume; }

    /// The crowd's answer to the umpire's signal. The stadium is today's sounds exactly, whatever the
    /// crowd setting (it always ignored it); the club claps, quieter the emptier it is.
    public static Reaction ForSignal(eVenue v, eCrowd c, UmpireSignal s)
    {
        if (v == eVenue.Stadium)
        {
            switch (s)
            {
                case UmpireSignal.Four: return new Reaction { group = "crowd_four", volume = 0.75f };
                case UmpireSignal.Six: return new Reaction { group = "crowd_six", volume = 0.95f };
                case UmpireSignal.Out: return new Reaction { group = "crowd_wicket", volume = 0.95f };
                default: return new Reaction();
            }
        }
        float f = ClubFactor(c);
        switch (s)
        {
            case UmpireSignal.Four: return new Reaction { group = "club_clap", volume = 0.5f * f };
            case UmpireSignal.Six: return new Reaction { group = "club_cheer", volume = 0.7f * f };
            case UmpireSignal.Out: return new Reaction { group = "club_clap", volume = 0.6f * f };
            default: return new Reaction();
        }
    }

    /// How loud a club crowd is against a full one.
    private static float ClubFactor(eCrowd c) => c == eCrowd.Packed ? 1f : c == eCrowd.Half ? 0.85f : 0.6f;

    /// The "ooh" when the batter is beaten: a stadium noise. 0 means do not play it.
    public static float OohVolume(eVenue v) => v == eVenue.Stadium ? 0.6f : 0f;

    /// The sound of a ball into whatever is round the rope. "fence" only if those clips exist, so a
    /// build without them still knocks on "boards". `has` is only asked at a club ground.
    public static string BoardsGroup(eVenue v, System.Func<string, bool> has) =>
        v == eVenue.ClubGround && has != null && has("fence") ? "fence" : "boards";

    /// How many of the club's spectator groups (benches) are filled.
    public static int SpectatorGroups(eCrowd c) => c == eCrowd.Packed ? 3 : c == eCrowd.Half ? 2 : 1;

    /// The size of each night glow quad: the stadium's banks of floodlights, or a club pylon's lamp.
    public static Vector2 GlowSize(eVenue v) => v == eVenue.Stadium ? new Vector2(13f, 5.5f) : new Vector2(4f, 2.5f);

    public static string Name(eVenue v) => v == eVenue.Stadium ? "Stadium" : "Club ground";

    /// The name on the pavilion: your own club in a career, an invented one in a Quick Match.
    public static string ClubName(bool careerActive, string yourTeamName) =>
        careerActive && !string.IsNullOrWhiteSpace(yourTeamName) ? yourTeamName.Trim() : ClubDefaultName;

    /// The Quick Match "Ground" control lists Stadium first, but the enum has ClubGround = 0
    /// (Ladder.cs), so the UI maps through these rather than casting.
    public static int QuickIndex(eVenue v) => v == eVenue.ClubGround ? 1 : 0;

    public static eVenue FromQuickIndex(int i) => i == 1 ? eVenue.ClubGround : eVenue.Stadium;
}
