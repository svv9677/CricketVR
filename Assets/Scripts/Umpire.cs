using UnityEngine;

/// <summary>
/// The umpire at the bowler's end, placed in CricketVR.unity (Prefabs/Scene/Umpire.prefab, built by
/// CricketVR > Scene Objects > Build Umpire; until 2026-11-15 he was built in code as the scene
/// loaded): the fielders' "David" character in a black coat (a white one hid the ball coming out
/// of the bowler's hand), dark trousers and a black wide-brimmed hat, standing on the fielders' idle. Before each ball he takes his place directly
/// behind the bowler's stumps, in line with them - further back for the quicks, close up to the
/// spinners. The bowler comes in beside the stumps, not through them, so he passes the umpire
/// (measured: no closer than ~0.5 m). His signals: UmpireSignals.
///
/// In the scene from the start, so the replay (ReplayActors) records him like any player.
/// </summary>
public class Umpire : MonoBehaviour
{
    /// Behind the stumps for each kind of bowler.
    /// Umpires stand back far enough to see the bowler's front foot at the crease and the ball
    /// down the pitch: a little way back to the spinners, much further back to the quicks.
    private const float BackForPace = 3.6f, BackForSwing = 3.0f, BackForSpin = 1.8f;

    [Tooltip("His coat's renderers (UmpireBuilder sets them).")]
    [SerializeField] private Renderer[] coat = new Renderer[0];
    [Tooltip("A Test's umpire in a white shirt: the ball is red there (TestLook).")]
    [SerializeField] private Material testShirt;

    private Transform stumps;

    private void Awake()
    {
        if (TestLook.Applies && testShirt != null)
            foreach (Renderer r in coat)
                if (r != null)
                    r.sharedMaterial = testShirt;
    }

    private void Start()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged += OnGameState;
    }

    private void OnDestroy()
    {
        if (Main.Instance != null)
            Main.Instance.onGameStateChanged -= OnGameState;
    }

    private void OnGameState()
    {
        // The bowler is on his mark now (AnimatedBowler moves him there in the run-up state).
        if (Main.Instance.gameState == eGameState.InGame_SelectDeliveryLoop)
            TakePlace();
    }

    /// Behind the bowler's stumps for this bowler, facing down the pitch to the batter.
    public void TakePlace()
    {
        if (stumps == null)
        {
            GameObject end = GameObject.Find("Main/Stumps_BowlingEnd");
            stumps = end != null ? end.transform : null;
        }
        Vector3 at = stumps != null ? StumpsCentre(stumps) : new Vector3(-10.4f, 0f, 0f);
        float back = BackForSwing;
        if (Main.Instance != null)
        {
            eSwingType type = Main.Instance.swingType;
            back = type == eSwingType.Pace ? BackForPace
                 : type == eSwingType.LegSpin || type == eSwingType.OffSpin ? BackForSpin : BackForSwing;
        }
        transform.SetPositionAndRotation(new Vector3(at.x - back, at.y, at.z), Quaternion.LookRotation(Vector3.right));
    }

    private static Vector3 StumpsCentre(Transform end)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Transform stump in end)
        {
            sum += stump.position;
            n++;
        }
        Vector3 centre = n > 0 ? sum / n : end.position;
        centre.y = 0f;
        return centre;
    }
}
