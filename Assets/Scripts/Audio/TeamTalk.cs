using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The players talking (2026-11-08: "various people should encourage the bowler or encourage
/// fielders for good catches or good bowling... even if it gets hit for a six, the bowler's
/// teammate should be like 'it's all good, you'll get it next ball'. Or they should clap"):
///
///   beaten outside off   the keeper and a close fielder: "Ooh, so close!", "Unlucky, bowler!"; claps
///   a good dot ball      now and then a close fielder: "Good ball!", "Keep it there!"; a clap or two
///   four or six          a fielder near the bowler lifts him: "It's all good, you'll get him next
///                        ball"; a couple of claps. The batting side by the rope (BattingSide) claps
///                        and calls "Shot!" ("Out of here!" for a six), and the partner at the other
///                        end too
///   a wicket             two or three fielders: "Yes! Got him!", "Well bowled!" ("Great catch!" for
///                        a catch, from the catcher's neighbours), and half the side claps
///   a hard hit stopped   "Good stop!", "Well fielded!" from the stopper's neighbour
///   between balls        now and then the keeper or a close fielder: "Come on, boys!"
///
/// Every line is a real voice (Resources/Sounds/Voices, Tools/voices/build_voices.py) from the
/// speaker's head, 3D; each player keeps one voice all match. The claps are each player's own
/// hands (PlayerClap). Played on the Fielders channel at shouting distance (heard at full within
/// ShoutWithin m), so a shout from slip or from the boundary is heard as from there.
/// </summary>
public class TeamTalk : MonoBehaviour
{
    /// The voices rendered (build_voices.py VOICES).
    public const int Voices = 5;
    /// A shout is at full loudness within this; the batting side's from the boundary within more.
    private const float ShoutWithin = 7f, BoundaryShoutWithin = 16f, ClapWithin = 5f, BoundaryClapWithin = 14f;
    /// Close in: the keeper's and slips' ring, and how near the ball a stop is judged hard.
    private const float CloseIn = 22f, HardHit = 22f, StopSeconds = 2.5f;
    /// No chatter within this long of the last line (wickets and boundaries are always answered).
    private const float Quiet = 7f;

    private struct Line { public float at; public string group; public Transform head; public int voice; public float within, pitch; }

    private readonly List<Line> lines = new List<Line>();
    private readonly List<Speaker> fielders = new List<Speaker>();
    private Speaker keeper, bowler;
    private Main main;
    private Rigidbody ball;
    private float lastLineAt = -100f;

    // This ball.
    private bool struck, bowled, beatenClose, gathered, passedBat;
    private float hitAt, exitSpeed;
    private Vector3 gatherAt;

    private class Speaker
    {
        public Transform root, head;
        public int voice;
        public float pitch;
        public PlayerClap hands;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        MakeFor(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => MakeFor(s);

    private static void MakeFor(Scene s)
    {
        if (s.name != CareerMatchSession.MatchScene || FindFirstObjectByType<TeamTalk>() != null)
            return;
        var go = new GameObject("Team Talk");
        SceneManager.MoveGameObjectToScene(go, s);
        go.AddComponent<TeamTalk>();
    }

    private void Start()
    {
        main = Main.Instance != null ? Main.Instance : FindFirstObjectByType<Main>();
        ball = main != null ? main.theBallRigidBody : null;
        int n = 0;
        if (main != null && main.theKeeper != null)
            keeper = MakeSpeaker(main.theKeeper.transform, n++, false);
        if (AnimatedBowler.Instance != null)
            bowler = MakeSpeaker(AnimatedBowler.Instance.transform, n++, false);
        foreach (AnimatedFielder f in FindObjectsByType<AnimatedFielder>(FindObjectsSortMode.InstanceID))
            fielders.Add(MakeSpeaker(f.transform, n++, true));
        HUD.BallScored += OnScored;
        Bat.ShotStruck += OnShot;
        if (main != null) main.onGameStateChanged += OnState;
    }

    private void OnDestroy()
    {
        HUD.BallScored -= OnScored;
        Bat.ShotStruck -= OnShot;
        if (main != null) main.onGameStateChanged -= OnState;
    }

    private static Speaker MakeSpeaker(Transform root, int n, bool claps)
    {
        Animator a = root.GetComponentInChildren<Animator>();
        Transform head = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
        var s = new Speaker
        {
            root = root, head = head != null ? head : root, voice = n % Voices,
            // Two players on one voice are told apart by pitch.
            pitch = (n / Voices) % 2 == 0 ? 1f : 0.94f,
        };
        if (claps && a != null && a.isHuman)
        {
            s.hands = a.GetComponent<PlayerClap>();
            if (s.hands == null)
                s.hands = a.gameObject.AddComponent<PlayerClap>();
        }
        return s;
    }

    // ---- What happened ------------------------------------------------------------------------

    private void OnState()
    {
        eGameState st = main.gameState;
        if (st == eGameState.InGame_DeliverBall)
        {
            struck = bowled = beatenClose = gathered = passedBat = false;
            exitSpeed = 0f;
        }
        else if (st == eGameState.InGame_Bowled)
            bowled = true;
        else if (st == eGameState.InGame_SelectDelivery)
            Chatter();
    }

    private void OnShot(ShotInfo info)
    {
        struck = true;
        hitAt = Time.time;
        exitSpeed = info.exitVelocity.magnitude;
    }

    private void Update()
    {
        if (main == null || ball == null)
            return;
        Watch();
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (Time.time < lines[i].at)
                continue;
            Line l = lines[i];
            lines.RemoveAt(i);
            if (l.head != null)
                GameSfx.Play($"{l.group}_v{l.voice}", l.head.position, GameAudio.Channel.Fielders, 1f, l.pitch, l.within, 150f, 40);
        }
    }

    /// Past the bat close to the stumps (beaten), and where a struck ball was first stopped.
    private void Watch()
    {
        eGameState st = main.gameState;
        if (!struck && !passedBat && (st == eGameState.InGame_DeliverBallLoop || st == eGameState.InGame_BallMissed) &&
            ball.position.x > BallDelivery.BatsmanStumpsX - 0.5f)
        {
            passedBat = true;
            beatenClose = Mathf.Abs(ball.position.z) < 0.45f && ball.position.y < 1.3f && !main.theBallScript.wide;
        }
        if (struck && !gathered && ball.isKinematic && Time.time - hitAt < 8f)
        {
            gathered = true;
            gatherAt = ball.position;
            gatherSeconds = Time.time - hitAt;
        }
    }

    private float gatherSeconds;

    private void OnScored(BallOutcome o)
    {
        switch (o.Signal)
        {
            case UmpireSignal.Out:
                Wicket();
                return;
            case UmpireSignal.Four:
            case UmpireSignal.Six:
                Boundary(o.Signal == UmpireSignal.Six);
                return;
            case UmpireSignal.None:
                Runs(o.Label);
                return;
        }
    }

    private void Wicket()
    {
        bool caught = struck && !bowled;
        Vector3 at = caught ? ball.position : Bowler();
        List<Speaker> near = Nearest(at, 3, null);
        for (int i = 0; i < near.Count && i < 3; i++)
            Say(near[i], caught && i < 2 ? "catch" : "wicket", 0.15f + 0.45f * i + Random.Range(0f, 0.2f));
        foreach (Speaker s in Nearest(at, 6, null))
            Clap(s, Random.Range(4, 8), Random.Range(0.3f, 0.9f));
    }

    private void Boundary(bool six)
    {
        // The fielding side: someone near the bowler lifts him, once the ball has gone.
        if (Random.value < 0.75f)
        {
            List<Speaker> near = Nearest(Bowler(), 2, bowler);
            if (near.Count > 0)
            {
                Speaker s = near[Random.Range(0, near.Count)];
                Say(s, "console", Random.Range(1.2f, 2f));
                Clap(s, Random.Range(2, 4), 2.2f);
            }
        }
        BattingSideCheers(six);
    }

    /// Runs off the bat or a dot.
    private void Runs(string label)
    {
        int runs = int.TryParse(label, out int r) ? r : 0;
        if (runs == 0 && !struck && beatenClose)
        {
            Say(keeper, "beaten", 0.25f);
            List<Speaker> close = Nearest(Striker(), 1, null);
            if (close.Count > 0 && Random.value < 0.7f)
                Say(close[0], Random.value < 0.5f ? "beaten" : "goodball", 0.9f + Random.Range(0f, 0.3f));
            foreach (Speaker s in Nearest(Striker(), 2, null))
                Clap(s, Random.Range(2, 4), 0.5f);
        }
        else if (runs <= 1 && struck && exitSpeed > HardHit && gathered && gatherSeconds < StopSeconds &&
                 (gatherAt - Striker()).sqrMagnitude > CloseIn * CloseIn * 0.25f)
        {
            // Hit hard, and stopped: the stopper's nearest team-mate.
            List<Speaker> near = Nearest(gatherAt, 2, null);
            if (near.Count > 1)
                Say(near[1], "stop", 0.6f + Random.Range(0f, 0.3f));
        }
        else if (runs == 0 && Time.time - lastLineAt > Quiet && Random.value < 0.35f)
        {
            List<Speaker> close = Nearest(Striker(), 3, null);
            if (close.Count > 0)
            {
                Speaker s = close[Random.Range(0, close.Count)];
                Say(s, "goodball", 0.5f + Random.Range(0f, 0.4f));
                Clap(s, Random.Range(2, 4), 0.6f);
            }
        }
        else if (runs >= 2 && struck && exitSpeed > HardHit && Random.value < 0.5f)
            BattingSideCheers(false, quiet: true);
    }

    /// Now and then, as the next ball is set up.
    private void Chatter()
    {
        if (Time.time - lastLineAt < Quiet || Random.value > 0.18f)
            return;
        List<Speaker> close = Nearest(Striker(), 3, null);
        Speaker s = Random.value < 0.5f || close.Count == 0 ? keeper : close[Random.Range(0, close.Count)];
        Say(s, "chatter", Random.Range(0.4f, 1.2f));
    }

    /// The batting side by the rope, and the partner at the other end.
    private void BattingSideCheers(bool six, bool quiet = false)
    {
        BattingSide side = BattingSide.Instance;
        if (side != null)
        {
            for (int i = 0; i < side.Hands.Count; i++)
                if (!quiet || i % 2 == 0)
                    side.Hands[i].Clap(Random.Range(quiet ? 3 : 6, quiet ? 5 : 10), Random.Range(0.15f, 0.6f), 1f, BoundaryClapWithin);
            int callers = quiet ? 1 : 2;
            for (int i = 0; i < callers && i < side.Members.Count; i++)
            {
                int who = (i * 2 + Random.Range(0, 2)) % side.Members.Count;
                Transform head = HeadOf(side.Members[who]);
                Queue(six && i == 0 ? "six" : "shot", head, (who + 2) % Voices, BoundaryShoutWithin, 1f, 0.3f + 0.5f * i);
            }
        }
        // The partner backing up, for the player's shot (not for his own: then the player is there).
        PartnerBatter partner = PartnerBatter.Instance;
        if (partner != null && partner.Ready && partner.Head != null && !PlayerEnd.NonStriker && !quiet && Random.value < 0.6f)
            Queue("shot", partner.Head, 3, ShoutWithin, 1.03f, 0.5f);
    }

    // ---- Who ------------------------------------------------------------------------------------

    private static void Clap(Speaker s, int count, float delay)
    {
        if (s != null && s.hands != null)
            s.hands.Clap(count, delay, 1f, ClapWithin);
    }

    private void Say(Speaker s, string group, float delay)
    {
        if (s == null)
            return;
        Queue(group, s.head, s.voice, ShoutWithin, s.pitch, delay);
    }

    private void Queue(string group, Transform head, int voice, float within, float pitch, float delay)
    {
        // One line at a time from one mouth.
        foreach (Line l in lines)
            if (l.head == head)
                return;
        lines.Add(new Line { at = Time.time + delay, group = group, head = head, voice = voice, within = within, pitch = pitch });
        lastLineAt = Time.time + delay;
    }

    /// The `count` fielders (and keeper) nearest `at`, nearest first, leaving out `except`.
    private List<Speaker> Nearest(Vector3 at, int count, Speaker except)
    {
        var all = new List<Speaker>(fielders);
        if (keeper != null) all.Add(keeper);
        all.RemoveAll(s => s == null || s == except || s.root == null || !s.root.gameObject.activeInHierarchy);
        all.Sort((a, b) => (a.root.position - at).sqrMagnitude.CompareTo((b.root.position - at).sqrMagnitude));
        if (all.Count > count)
            all.RemoveRange(count, all.Count - count);
        return all;
    }

    private Vector3 Bowler() => bowler != null && bowler.root != null ? bowler.root.position : new Vector3(BallDelivery.BowlerStumpsX, 0f, 0f);
    private static Vector3 Striker() => new Vector3(BallDelivery.BatsmanStumpsX, 0f, 0f);

    private static Transform HeadOf(Transform t)
    {
        Animator a = t.GetComponent<Animator>();
        Transform head = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Head) : null;
        return head != null ? head : t;
    }
}
