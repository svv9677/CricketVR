using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The umpire at the bowler's end. Built when the match scene loads (no scene change): the
/// "David" character, which no fielder uses, in a white coat, dark trousers and a white
/// wide-brimmed hat, standing on the fielders' idle. Before each ball he takes his place directly
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

    private static readonly Color Coat = new Color(0.95f, 0.95f, 0.92f);
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
        white.SetColor("_BaseColor", Coat);
        float top = TopOfHead(animator, head);
        Part(PrimitiveType.Cylinder, head, white, new Vector3(0.34f, 0.006f, 0.34f), top + 0.01f);     // brim
        Part(PrimitiveType.Cylinder, head, white, new Vector3(0.19f, 0.045f, 0.19f), top + 0.05f);     // crown
    }

    /// Height of the top of the head above the head bone, from the head's skinned bounds.
    private static float TopOfHead(Animator animator, Transform head)
    {
        float top = head.position.y + 0.15f;
        foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            if (skin.name.Contains("Body") || skin.name.Contains("Hair"))
                top = Mathf.Max(top, Mathf.Min(skin.bounds.max.y, head.position.y + 0.25f));
        return top - head.position.y;
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
