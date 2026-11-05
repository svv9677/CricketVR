using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The conditions picked in Customise, put on the ground when the match scene loads.
///
/// The pitch (also read by the ball and the bowling, through PitchConditions):
///   Flat   the hard, true strip the game was tuned on (e 0.65, grip 0.88);
///   Green  a tinge of grass: bounces more and comes on quicker (e 0.70, grip 0.90), seam
///          movement x1.7 for the quicks, a little less turn;
///   Dusty  dry and worn: keeps lower and slows up (e 0.58, grip 0.82), turn x1.5 for the
///          spinners, less seam.
/// The restitution is PhysX's (the pitch collider's material, averaged with the ball's 0.5) and
/// BallFlight's prediction of it, set together so the fielders and the aim stay right.
///
/// The light is never touched: the scene's own sun, sky and Rao's clouds. The crowd: Packed as
/// is; Half the upper tier empty and the noise down; Empty no one, and only a murmur.
///
/// Materials are copied before they are changed: in the editor, changing the shared ones would
/// write into the assets.
/// </summary>
public static class StadiumConditions
{
    private const string MatchScene = "CricketVR";

    public struct Pitch
    {
        public float restitution, grip, seamScale, turnScale;
        public Color tint;
    }

    public static Pitch For(ePitch p)
    {
        switch (p)
        {
            case ePitch.Green: return new Pitch { restitution = 0.70f, grip = 0.90f, seamScale = 1.7f, turnScale = 0.75f, tint = new Color(0.84f, 1f, 0.72f) };
            case ePitch.Dusty: return new Pitch { restitution = 0.58f, grip = 0.82f, seamScale = 0.7f, turnScale = 1.5f, tint = new Color(1f, 0.9f, 0.74f) };
            default: return new Pitch { restitution = BallFlight.FlatPitchRestitution, grip = BallFlight.FlatPitchGrip, seamScale = 1f, turnScale = 1f, tint = Color.white };
        }
    }

    /// The pitch in play: the match's in the match scene, the flat one anywhere else.
    public static Pitch Current { get; private set; } = For(ePitch.Flat);

    /// The ball's own bounciness, which PhysX averages with the pitch's.
    private const float BallBounciness = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Apply(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

    private static void Apply(Scene scene)
    {
        bool match = scene.name == MatchScene;
        Current = For(match ? MatchSetup.Pitch : ePitch.Flat);
        BallFlight.SetPitch(Current.restitution, Current.grip);
        if (!match)
            return;
        ApplyPitch(Current);
        ApplyCrowd(MatchSetup.Crowd);
        StadiumLighting.Apply(MatchSetup.TimeOfDay);
    }

    private static void ApplyPitch(Pitch p)
    {
        GameObject pitch = GameObject.Find("Main/Pitch/Pitch Base");
        if (pitch == null)
            return;
        if (pitch.TryGetComponent(out Collider c) && c.sharedMaterial != null)
        {
            var m = new PhysicsMaterial(c.sharedMaterial.name + " (match)")
            {
                bounciness = Mathf.Clamp01(2f * p.restitution - BallBounciness),
                dynamicFriction = c.sharedMaterial.dynamicFriction,
                staticFriction = c.sharedMaterial.staticFriction,
                bounceCombine = c.sharedMaterial.bounceCombine,
                frictionCombine = c.sharedMaterial.frictionCombine,
            };
            c.sharedMaterial = m;
        }
        if (p.tint != Color.white && pitch.TryGetComponent(out Renderer r))
        {
            Material mat = r.material;   // an instance
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", mat.GetColor("_BaseColor") * p.tint);
        }
    }

    private static void ApplyCrowd(eCrowd crowd)
    {
        if (crowd == eCrowd.Packed)
            return;
        GameObject stands = GameObject.Find("Crowd");
        if (stands == null)
            return;
        foreach (Renderer r in stands.GetComponentsInChildren<Renderer>(true))
            if (crowd == eCrowd.Empty || r.name.Contains("Upper"))
                r.enabled = false;
        if (stands.TryGetComponent(out CrowdController noise))
        {
            float level = crowd == eCrowd.Empty ? 0.15f : 0.55f;
            noise.ambienceVolume *= level;
            noise.cheerVolume *= level;
        }
    }
}
