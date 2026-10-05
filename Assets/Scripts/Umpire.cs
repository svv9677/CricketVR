using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The umpire at the bowler's end. Built when the match scene loads (no scene change): the
/// "David" character, which no fielder uses, in a black coat (a white one hid the ball coming out
/// of the bowler's hand), dark trousers and a white wide-brimmed hat, standing on the fielders' idle. Before each ball he takes his place directly
/// behind the bowler's stumps, in line with them - further back for the quicks, close up to the
/// spinners. The bowler comes in beside the stumps, not through them, so he passes the umpire
/// (measured: no closer than ~0.5 m). His signals: UmpireSignals.
///
/// Built before any Start runs, so the replay (ReplayActors) records him like any player.
/// </summary>
public class Umpire : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    /// Behind the stumps for each kind of bowler.
    private const float BackForPace = 2.4f, BackForSwing = 2.0f, BackForSpin = 1.3f;

    private static readonly Color Coat = new Color(0.06f, 0.06f, 0.07f);
    private static readonly Color Hat = new Color(0.95f, 0.95f, 0.92f);
    private static readonly Color Trousers = new Color(0.1f, 0.11f, 0.16f);
    private static readonly Color Shoes = new Color(0.05f, 0.05f, 0.05f);

    private Transform stumps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnLoaded;
        SceneManager.sceneLoaded += OnLoaded;
        Build(SceneManager.GetActiveScene());
    }

    private static void OnLoaded(Scene scene, LoadSceneMode mode) => Build(scene);

    private static void Build(Scene scene)
    {
        if (scene.name != MatchScene || FindFirstObjectByType<Umpire>() != null)
            return;
        GameObject model = Resources.Load<GameObject>("Models/David");
        var controller = Resources.Load<RuntimeAnimatorController>("Animations/Fielder Controller");
        if (model == null || controller == null)
            return;
        GameObject umpire = Instantiate(model);
        umpire.name = "Umpire";
        SceneManager.MoveGameObjectToScene(umpire, scene);
        Animator animator = umpire.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;   // Action 0: the idle, and nothing else
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;   // the replay records him
        Dress(umpire);
        PutOnHat(animator);
        umpire.AddComponent<UmpireSignals>();
        umpire.AddComponent<Umpire>().TakePlace();
    }

    private static void Dress(GameObject umpire)
    {
        foreach (SkinnedMeshRenderer skin in umpire.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string n = skin.name;
            Color? plain = n.Contains("Hoody") ? Coat : n.Contains("Pants") ? Trousers : n.Contains("Sneakers") ? Shoes : (Color?)null;
            if (plain == null)
                continue;
            var material = new Material(skin.sharedMaterial) { name = "Umpire " + n };
            material.mainTexture = null;        // one flat colour: the clothes' part of the atlas would show through
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", plain.Value);
            if (material.HasProperty("_Color")) material.SetColor("_Color", plain.Value);
            skin.sharedMaterial = material;
        }
    }

    private static void PutOnHat(Animator animator)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (head == null || lit == null)
            return;
        var white = new Material(lit) { name = "Umpire Hat" };
        white.SetColor("_BaseColor", Hat);
        float top = TopOfHead(animator, head);
        // Worn, not perched: the brim at the brow, a low rounded crown over the top of the head.
        Part(PrimitiveType.Cylinder, head, white, new Vector3(0.36f, 0.005f, 0.36f), top - 0.085f);    // brim
        Part(PrimitiveType.Sphere, head, white, new Vector3(0.21f, 0.15f, 0.23f), top - 0.045f);       // crown
    }

    /// Height of the top of the head (hair included) above the head bone, from the skinned mesh
    /// itself: the renderers' bounds are padded, and a hat set on them floated above the head.
    private static float TopOfHead(Animator animator, Transform head)
    {
        float top = float.NegativeInfinity;
        var baked = new Mesh();
        foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!skin.name.Contains("Body") && !skin.name.Contains("Hair"))
                continue;
            skin.BakeMesh(baked, true);     // scale applied: only the position and turn to add
            Vector3 at = skin.transform.position;
            Quaternion turn = skin.transform.rotation;
            foreach (Vector3 v in baked.vertices)
            {
                Vector3 w = at + turn * v;
                if (Vector2.Distance(new Vector2(w.x, w.z), new Vector2(head.position.x, head.position.z)) < 0.2f)
                    top = Mathf.Max(top, w.y);
            }
        }
        Destroy(baked);
        return float.IsNegativeInfinity(top) ? 0.17f : top - head.position.y;
    }

    private static void Part(PrimitiveType shape, Transform head, Material material, Vector3 size, float up)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Destroy(part.GetComponent<Collider>());
        part.name = "Hat";
        part.transform.SetPositionAndRotation(head.position + Vector3.up * up, Quaternion.identity);
        part.transform.localScale = size;
        part.transform.SetParent(head, true);
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
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
