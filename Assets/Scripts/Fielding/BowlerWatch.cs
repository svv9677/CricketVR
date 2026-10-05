using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Diagnostics for a bowler who stops bowling on the headset (reported: after the first ball he
/// stands in an odd pose and the next delivery never comes; not reproduced in the Editor). Once
/// per stall it logs what the bowler is doing - animator state, controller, Action, whether he is
/// still fielding or down in his ragdoll, where he is - so `adb logcat -s Unity` shows why.
/// Logs only when a run-up has gone on far longer than any real one (a run-up is 3.5-5 s).
/// </summary>
public class BowlerWatch : MonoBehaviour
{
    private const string MatchScene = "CricketVR";
    private const float StallAfter = 10f;

    private float runUpSince = -1f;
    private bool reported;

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
        if (scene.name == MatchScene && FindFirstObjectByType<BowlerWatch>() == null)
            SceneManager.MoveGameObjectToScene(new GameObject("Bowler Watch", typeof(BowlerWatch)), scene);
    }

    private void Update()
    {
        Main main = Main.Instance;
        AnimatedBowler bowler = AnimatedBowler.Instance;
        if (main == null || bowler == null)
            return;
        if (main.gameState != eGameState.InGame_SelectDeliveryLoop)
        {
            runUpSince = -1f;
            reported = false;
            return;
        }
        if (runUpSince < 0f)
            runUpSince = Time.time;
        if (reported || Time.time - runUpSince < StallAfter)
            return;
        reported = true;
        Debug.LogWarning("[BowlerWatch] " + Describe(bowler));
    }

    private static string Describe(AnimatedBowler bowler)
    {
        Animator a = bowler.GetComponent<Animator>();
        FielderRagdoll ragdoll = bowler.GetComponent<FielderRagdoll>();
        BowlerFielding fielding = bowler.GetComponent<BowlerFielding>();
        AnimatorStateInfo s = a.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clips = a.GetCurrentAnimatorClipInfo(0);
        return $"run-up stalled: clip={(clips.Length > 0 ? clips[0].clip.name : "-")} t={s.normalizedTime:F2} " +
               $"inTransition={a.IsInTransition(0)} ctrl={(a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "none")} " +
               $"Action={a.GetInteger("Action")} enabled={a.enabled} speed={a.speed:F2} rootMotion={a.applyRootMotion} " +
               $"fielding={bowler.Fielding}/{(fielding != null && fielding.Fielding)} ragdollBusy={(ragdoll != null && ragdoll.Busy)} " +
               $"fielderComp={(bowler.GetComponent<AnimatedFielder>() != null)} pos={bowler.transform.position.ToString("F2")}";
    }
}
