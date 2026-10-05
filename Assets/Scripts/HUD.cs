using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Batsman
{
    public string Name;
    public int Runs;
    public int Balls;   // faced (HUDStats)

    public Batsman(string name, int runs=0)
    {
        Name = name;
        Runs = runs;
    }
}

public class Bowler
{
    public string Name;
    public eSwingType Type;
    public int Balls, Conceded, Wickets;   // HUDStats

    public Bowler(string name, eSwingType typ = eSwingType.None)
    {
        Name = name;
        Type = typ;
    }
}

public partial class HUD : MonoBehaviour
{
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtScore;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtRunRate;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBatsman1;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBatsman2;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBatsman1Score;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBatsman2Score;

    [SerializeField]
    protected TMPro.TextMeshProUGUI txtOvers;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBowler;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBowlerStyle;
    [SerializeField]
    protected TMPro.TextMeshProUGUI txtBalls;
    [SerializeField]
    public TMPro.TextMeshProUGUI txtVersion;


    public static HUD Instance = null;

    public int Runs;
    public int Wickets;
    public int Overs;
    public int Balls;

    public int TotalOvers;

    public List<string> BowledBalls;

    public List<Batsman> Batsmen;
    public Batsman CurrentBatsman;
    public Batsman CurrentRunner;
    public int NextBatsmanIndex;

    public List<Bowler> Bowlers;
    public Bowler CurrentBowler;
    public int NextBowlerIndex;

    public float RunRate { get
                            {
                                if (Overs == 0 && Balls == 0) return 0f;
                                else return (float)Runs / ((float)Overs + ((float)Balls / 6.0f));
                            }
                           set {}
                         }

    /// When the shot (or the byes) started, in game time: pausing mid-chase must not add runs.
    private float StartOfShot;


    // Start is called before the first frame update
    void Start()
    {
        if (HUD.Instance == null)
            HUD.Instance = this;

        Main.Instance.onGameStateChanged += HandleGameState;
        Main.Instance.onGameStateChanged += AnnounceBall;   // after HandleGameState has scored it
        Bat.ShotStruck += ShowShot;

        Reset();
    }

    /// Overs come from GameSettings, which the Splash main menu sets on the Quick Match row.
    /// The explicit parameter is kept for callers that want to override it.
    public void Reset(int totalOvers = -1)
    {
        if (totalOvers < 0)
            totalOvers = GameSettings.Overs;

        Runs = 0;
        Wickets = 0;
        Overs = 0;
        Balls = 0;

        TotalOvers = totalOvers;
        BowledBalls = new List<string>();

        Batsmen = new List<Batsman>();
        string[] batNames = { "Ishan", "Shreyas", "Samson", "Hardik", "Deepak", "Karthik", "Axar", "Kuldeep", "Avesh", "Bishnoi", "Arshdeep" };
        for(int i=0; i<11; i++)
        {
            Batsman bat = new Batsman(batNames[i]);
            Batsmen.Add(bat);
        }
        CurrentBatsman = Batsmen[0];
        CurrentRunner = Batsmen[1];
        NextBatsmanIndex = 2;

        Bowlers = new List<Bowler>();
        string[] boNames = { "Bumrah", "Shami", "Bhuvneshwar", "Siraj", "Ashwin", "Chahal", "Hooda", "Bishnoi" };
        eSwingType[] boTypes = { eSwingType.InSwing, eSwingType.Pace, eSwingType.OutSwing, eSwingType.Pace, eSwingType.OffSpin, eSwingType.LegSpin, eSwingType.OffSpin, eSwingType.LegSpin };
        for(int i=0; i<boNames.Length; i++)
        {
            Bowler bo = new Bowler(boNames[i], boTypes[i]);
            Bowlers.Add(bo);
            AnimatedBowler.Instance.myBowlers.data.Add(AnimatedBowler.Instance.configs.data[((int)boTypes[i]) - 1]);
        }
        CurrentBowler = Bowlers[0];
        //CurrentBowler = Bowlers[2];               // QWERTYUIOP
        Main.Instance.swingType = CurrentBowler.Type;
        UpdateKeeperPosition();
        AnimatedBowler.Instance.UpdateInfo(Bowlers.IndexOf(CurrentBowler));
        NextBowlerIndex = 0;

        
        UpdateUI();
    }

    public void OnDestroy()
    {
        Bat.ShotStruck -= ShowShot;
        if (Main.Instance != null)
        {
            Main.Instance.onGameStateChanged -= HandleGameState;
            Main.Instance.onGameStateChanged -= AnnounceBall;
        }
    }

    // Update is called once per frame
    private bool keeperMovePending;

    void HandleGameState()
    {
        eGameState st = Main.Instance.gameState;
        if (st == eGameState.InGame_SelectDelivery && keeperMovePending)
        {
            keeperMovePending = false;
            UpdateKeeperPosition();
        }
        // A ball that beat the keeper, played on (Extras): byes, or runs added to a wide.
        if (Extras.InPlay && (st == eGameState.InGame_BallFielded || st == eGameState.InGame_BallPastBoundary))
        {
            AddExtras(st == eGameState.InGame_BallPastBoundary ? 4 : RunsForTime());
            UpdateUI();
            return;
        }
        if(st == eGameState.InGame_BallFielded)
        {
            // first check, if it bounced or not
            if(!Main.Instance.theBallScript.bounced)
            {
                // It if didnt bounce, it is a catch
                AddRuns(-1);
            }
            else
            {
                float seconds = Time.time - StartOfShot;
                //Debug.Log("SHOT TIMER: " + seconds.ToString());
                if (seconds < 2f)
                    AddRuns(0);
                else if (seconds < 4.5f)
                    AddRuns(1);
                else if (seconds < 7.5f)
                    AddRuns(2);
                else if (seconds < 11f)
                    AddRuns(3);
                else
                    AddRuns(4);
            }

            UpdateUI();
        }
        if(st == eGameState.InGame_BallMissed)
        {
            // Ball was missed: possible outcomes - dot ball, wide(s) or bye(s)
            //  For now, not using extras in the game
            if (Main.Instance.theBallScript.wide)
            {
                AddRuns(10);
            }
            else
            {
                AddRuns(0);
            }
            

            UpdateUI();
        }
        if(st == eGameState.InGame_BallPastBoundary)
        {
            // first check, if it bounced or not
            if (!Main.Instance.theBallScript.bounced)
            {
                // it if didn't bounce, it is a six
                AddRuns(6);
            }
            else
            {
                AddRuns(4);
            }

            UpdateUI();
        }
        if(st == eGameState.InGame_Bowled)
        {
            AddRuns(-1);

            UpdateUI();
        }
        if(st == eGameState.InGame_BallHit)
        {
            //start track of time till ball gets fielded
            StartOfShot = Time.time;
        }
    }

    /// Runs the batters take while the ball is fielded, by how long that took (as for a shot).
    int RunsForTime()
    {
        float seconds = Time.time - StartOfShot;
        return seconds < 2f ? 0 : seconds < 4.5f ? 1 : seconds < 7.5f ? 2 : seconds < 11f ? 3 : 4;
    }

    /// Byes (a legal ball: it counts in the over, "2b" in the recent balls) or, off a wide, runs
    /// added to that wide ("3wd"). Extras go to the team, never to the batter.
    void AddExtras(int runs)
    {
        CreditExtras(runs);
        Runs += runs;
        if (runs % 2 == 1)
            SwitchBatsmen();
        if (Extras.OnWide)
        {
            int last = BowledBalls.Count - 1;
            if (last >= 0 && BowledBalls[last] == "wd" && runs > 0)
                BowledBalls[last] = $"{1 + runs}wd";
            return;
        }
        IncrementBalls();
        BowledBalls.Add(runs == 0 ? "*" : $"{runs}b");
    }

    // Note: -1 runs is a wicket
    void AddRuns(int runs)
    {
        Credit(runs);
        switch(runs)
        {
            // Current batsman is out
            case -1:
                {
                    IncrementRuns(0);
                    IncrementWickets();
                    IncrementBalls();

                    BowledBalls.Add("W");
                }
                break;
            // Dot ball 
            case 0:
                {
                    IncrementRuns(0);
                    IncrementBalls();

                    BowledBalls.Add("*");
                }
                break;
            // Runs scored, and batsmen switch
            case 1:
            case 3:
                {
                    IncrementRuns(runs);
                    SwitchBatsmen();
                    IncrementBalls();

                    BowledBalls.Add(runs.ToString());
                }
                break;
            // Runs scored, but no switch
            case 2:
            case 4:
            case 6:
                {
                    IncrementRuns(runs);
                    IncrementBalls();

                    BowledBalls.Add(runs.ToString());
                }
                break;
            case 10:
                {
                    // A wide is an extra: the team's run, not the batter's (it was credited to him).
                    Runs += 1;
                    BowledBalls.Add("wd");
                }
                break;
        }
    }

    void SwitchBatsmen()
    {
        Batsman tmp = CurrentBatsman;
        CurrentBatsman = CurrentRunner;
        CurrentRunner = tmp;
    }

    /// Bring the next bowler on now (the between-balls "Change Bowler" option).
    public void ChangeBowler()
    {
        SelectNextBowler();
        UpdateUI();
    }

    /// Bring on the first bowler of this type (the settings panel's bowler selector). The usual
    /// rotation carries on from them at the end of the over.
    public void SelectBowlerOfType(eSwingType type)
    {
        int index = Bowlers.FindIndex(b => b.Type == type);
        if (index < 0 || Bowlers[index] == CurrentBowler)
            return;
        NextBowlerIndex = index;
        SetBowler(index);
        UpdateUI();
    }

    /// A struck ball, for the last-shot card. Subscribe to Bat.ShotStruck with this.
    public void ShowShot(ShotInfo info)
    {
        if (ShotCard.Instance != null)
            ShotCard.Instance.ShowShot(info);
    }

    void SelectNextBowler()
    {
        NextBowlerIndex++;
        if (NextBowlerIndex >= Bowlers.Count)
            NextBowlerIndex = 0;
        SetBowler(NextBowlerIndex);
    }

    void SetBowler(int index)
    {
        CurrentBowler = Bowlers[index];
        Main.Instance.swingType = CurrentBowler.Type;
        // The keeper moves up or back for the new bowler at the start of his first ball - not now,
        // when (end of an over) he may be holding the ball or throwing it on: moved now, he
        // teleported 5-12 m with it and a throw in his wind-up never came (review).
        keeperMovePending = true;
        AnimatedBowler.Instance.UpdateInfo(index);
    }

    void UpdateKeeperPosition()
    {
        switch (Main.Instance.swingType)
        {
            case eSwingType.Pace:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionFast;
                }
                break;
            case eSwingType.InSwing:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionMedium;
                }
                break;
            case eSwingType.OutSwing:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionMedium;
                }
                break;
            case eSwingType.OffSpin:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionSlow;
                }
                break;
            case eSwingType.LegSpin:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionSlow;
                }
                break;
            default:
                {
                    Main.Instance.theKeeper.transform.position = Constants.KeeperPositionMedium;
                }
                break;
        }
    }

    void IncrementRuns(int runs)
    {
        CurrentBatsman.Runs += runs;
        Runs += runs;
    }

    void IncrementBalls()
    {
        Balls++;
        if (Balls >= 6)
        {
            Balls = 0;
            Overs++;

            // switch batsmen
            SwitchBatsmen();

            // switch bowlers
            SelectNextBowler();

            // If overs are done!
            if (Overs == TotalOvers)
            {
                Debug.LogWarning("MATCH DONE!!!!");
            }
        }
    }

    void IncrementWickets()
    {
        // Select next batsman
        CurrentBatsman = Batsmen[NextBatsmanIndex];

        // Update wickets count
        Wickets++;

        // Now set the index for future use
        NextBatsmanIndex++;

        if (NextBatsmanIndex > 10)
        {
            NextBatsmanIndex = 0;
            // All Out!!
            Debug.LogWarning("GAME OVER!! ALL OUT");
        }
    }

    public void UpdateUI()
    {
        //txtVersion.text = "Version: " + PlayerSettings.bundleVersion;
        int count = BowledBalls.Count;
        if (count > 0)
        {
            string str = "";
            int legal = 0;

            for (int i = 0; i < count; i++)
            {
                str = BowledBalls[i] + " " + str;

                if (!BowledBalls[i].EndsWith("wd"))
                {
                    legal++;
                    if (legal == 6)
                    {
                        str = " | " + str;
                        legal = 0;
                    }
                }
                
                

            }
            txtBalls.text = str;
        }
        else
            txtBalls.text = "";

        txtScore.text = Runs.ToString() + "/" + Wickets.ToString();
        // Two decimals. RunRate is runs/overs, so the raw ToString() gives things like
        // "6.8571429" - far too long for the scoreboard's bracketed field.
        txtRunRate.text = RunRate.ToString("F2");
        txtBatsman1.text = CurrentBatsman.Name;
        txtBatsman1Score.text = RunsAndBalls(CurrentBatsman);
        txtBatsman2.text = CurrentRunner.Name;
        txtBatsman2Score.text = RunsAndBalls(CurrentRunner);

        txtOvers.text = Overs.ToString() + "." + Balls.ToString() + " (" + TotalOvers.ToString() + ")";
        txtBowler.text = CurrentBowler.Name;
        txtBowlerStyle.text = Constants.CT_SwingPrefixes[(int)CurrentBowler.Type];

        if (ShotCard.Instance != null)
            ShotCard.Instance.SetScore(Runs, Wickets, Overs, Balls, BowledBalls);
    }
}
