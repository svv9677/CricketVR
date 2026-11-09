using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Main : MonoBehaviour
{
    public static Main Instance;

    [HideInInspector]
    public eGameState gameState {
                                    get { return _gameState; }
                                    set { _gameState = value; if (onGameStateChanged != null) onGameStateChanged(); }
                                }
    private eGameState _gameState;
    [HideInInspector]
    public event System.Action onGameStateChanged;
    [HideInInspector]
    public Bat theBatScript;
    [HideInInspector]
    public Ball theBallScript;
    [HideInInspector]
    public Stumps theStumpsScript;
    [HideInInspector]
    public Rigidbody theBallRigidBody;
    [HideInInspector]
    public HUD theHUD;

    [HideInInspector]
    public BowlingProfileManager bowlingProfileManager;
    [HideInInspector]
    public BowlingParams currentBowlingConfig;
    [HideInInspector]
    public string currentFielderName;

    [Header("Connections")]
    [SerializeField]
    public GameObject theBall;
    [SerializeField]
    public GameObject theBat;
    [SerializeField]
    public GameObject theStumps;
    [SerializeField]
    public GameObject theKeeper;
    [SerializeField]
    public GameObject theStadium;
    [SerializeField]
    public GameObject theBoundaryCollider;
    [SerializeField]
    protected Material NightMaterial;
    [SerializeField]
    protected Material DayMaterial;
    [SerializeField]
    protected GameObject theNightLights;
    [SerializeField]
    protected GameObject theDayLights;
    [SerializeField]
    protected Text debugText;
    [SerializeField]
    protected Text consoleText;

    [SerializeField]
    protected GameObject dbgOverlayParent;
    [SerializeField]
    protected GameObject hudParent;
    

    [Header("Settings")]
    [SerializeField]
    protected eDifficulty difficulty;
    private eDifficulty _difficulty = eDifficulty.None;

    [SerializeField]
    protected eBattingStyle battingStyle;
    private eBattingStyle _shownBattingStyle = eBattingStyle.None;

    [SerializeField]
    protected eStadiumMode stadiumMode;

    [SerializeField]
    [Range(3f, 10f)]
    protected float zOffset;
    private float _zOffset;
    private TMPro.TMP_Text _zOffsetText;
    private Slider _zOffsetSlider;

    [Header("Debug Tweaks")]
    [Tooltip("Log game-state transitions made via WaitAndSetGameState. Mirrored into the in-world console by HandleLog.")]
    [SerializeField]
    private bool verboseStateLogging = false;

    // The settings panel mirrors these (SettingsPanel.Refresh); each `_x` is the value last applied.
    [Tooltip("The debug overlay (game state + log console). Off by default; Settings > Advanced.")]
    public bool overlayVisible = false;
    private bool _overlayVisible = false;

    [Range(0f, 5f)]
    public float resetDelay = -100f;
    private float _resetDelay = NextBallDelay;
    /// Between balls. The ball is dead (fielded, bowled, missed, over the rope) -> DeadBallPause ->
    /// reset -> resetDelay -> ready for the next ball. It was 2 s + 2 s (4 s after a boundary):
    /// four to six seconds of nothing before Bowl could even be pressed.
    public const float DeadBallPause = 1f, NextBallDelay = 0.75f, BoundaryDelay = 1.5f;

    [Range(0.5f, 2.5f)]
    public float fielderSpeed = -100f;
    private float _fielderSpeed = 1.5f;

    public eSwingType swingType;
    private eSwingType _swingType;

    // Not used by the bat any more (only logged by BallDiagnostics); kept for their saved values.
    [Range(0f, 15f)]
    public float ampMin = -100f;
    private float _ampMin = 10f;

    [Range(0f, 25f)]
    public float ampMax = -100f;
    private float _ampMax = 15f;

    public float MinX = Constants.paceCfg[0];
    private float _MinX = -100f;

    public float MaxX = Constants.paceCfg[1];
    private float _MaxX = -100f;

    public float MinY = Constants.paceCfg[2];

    public float MaxY = Constants.paceCfg[3];

    public float MinZ = Constants.paceCfg[4];
    private float _MinZ = -100f;

    public float MaxZ = Constants.paceCfg[5];
    private float _MaxZ = -100f;

    public float MinSwing = Constants.paceCfg[6];
    private float _MinSwing = -100f;

    public float MaxSwing = Constants.paceCfg[7];
    private float _MaxSwing = -100f;

    public float MinPitchTurn = Constants.paceCfg[8];
    private float _MinPitchTurn = -100f;

    public float MaxPitchTurn = Constants.paceCfg[9];
    private float _MaxPitchTurn = -100f;

    [Tooltip("Bat power: scales the bat's swing-speed contribution. 75 = a real bat (\"Realistic\").")]
    public float BatAmplifier = Constants.BatPowerRealistic;
    private float _BatAmplifier = -100f;


    public eBattingStyle BattingStyle => battingStyle;
    public eDifficulty Difficulty => difficulty;
    public BatGripCalibration GripCalibration => gripCalibration;
    [Tooltip("The B-menu settings panel in the scene (prefab SettingsPanel).")]
    [SerializeField] private SettingsPanel settingsPanel;
    /// The most recent delivery's aim, for checking it against where the ball really went.
    [HideInInspector] public BallDelivery.Solution lastDelivery;

    // Internal variables
    private bool initialized;
    private bool menuToggle;
    private BatGripCalibration gripCalibration;
    private eGameState debugShownState = (eGameState)(-1);
    private BowlingParams debugShownConfig;

    // Console log parameters
    private const int HISTORY_MAX_LINES = 100;
    private const int HISTORY_TEXT_MAX_LENGTH = 16000;
    private List<string> history;
    private int historyTextLength;
    private const int MAX_VIEWABLE_LINES = 17;

    


    private void Awake()
    {
        if (theBall != null)
        {
            theBallRigidBody = theBall.GetComponent<Rigidbody>();
            theBallScript = theBall.GetComponent<Ball>();
        }
        if(theBat != null)
        {
            theBatScript = theBat.GetComponent<Bat>();
        }
        if(theStumps != null)
        {
            theStumpsScript = theStumps.GetComponent<Stumps>();
        }
        if(hudParent != null)
        {
            theHUD = hudParent.GetComponent<HUD>();
        }

        initialized = false;

        this.history = new List<string>(HISTORY_MAX_LINES);
        this.historyTextLength = 0;

        Application.logMessageReceived += this.HandleLog;


    }

    public void OnDestroy()
    {
        Application.logMessageReceived -= this.HandleLog;
        // GameSettings.Changed is static and Main is per-scene: without this a destroyed Main
        // from the previous scene would stay alive on the event and apply settings into a
        // scene that no longer exists.
        GameSettings.Changed -= ApplyGameSettings;
    }

    // Start is called before the first frame update
    void Start()
    {
        if (Main.Instance == null)
            Main.Instance = this;

        // On the Bat prefab - nothing is added at runtime.
        if (theBat != null)
            gripCalibration = theBat.GetComponent<BatGripCalibration>();

        // Initialize menus
        menuToggle = false;
        SetupMenus();

        // Read Settings - GameSettings owns these now, so the menus can change them from
        // Splash where there is no Main. Its defaults are the ones that used to be inline here.
        difficulty = GameSettings.Difficulty;
        battingStyle = GameSettings.BattingStyle;
        stadiumMode = (eStadiumMode)PlayerPrefs.GetInt(Constants.PP_StadiumMode, (int)eStadiumMode.Day);
        zOffset = PlayerPrefs.GetFloat(Constants.PP_ZOffset, 3f);

        // Read Tweakables
        // Set default using private vars, and set private var to -1000 so that we
        // let auto-update update this along with the UI
        overlayVisible = GameSettings.OverlayVisible;
        _overlayVisible = !overlayVisible;   // so the first update shows or hides the overlay
        BatAmplifier = GameSettings.BatPower;
        // Not from PlayerPrefs any more: the slider that saved it is gone, and an old saved 2 s
        // would have kept the slow gap between balls alive.
        resetDelay = NextBallDelay;
        _resetDelay = -1000;
        fielderSpeed = GameSettings.FielderSpeed;
        _fielderSpeed = -1000;
        ampMin = PlayerPrefs.GetFloat(Constants.PP_AmpMin, _ampMin);
        _ampMin = -1000;
        ampMax = PlayerPrefs.GetFloat(Constants.PP_AmpMax, _ampMax);
        _ampMax = -1000;
        swingType = eSwingType.Pace;
        _swingType = eSwingType.None;

        updateDifficulty(true);
        updateBatColliderSize();
        updateBattingStyle(true);
        updateStadiumMode(true);
        updateZOffset(true);
        updateTweakables(true);

        currentBowlingConfig = new BowlingParams();
        bowlingProfileManager = new BowlingProfileManager();
        bowlingProfileManager.InitProfilesFromParams();

        SetupXRControllers();

        GameSettings.Changed += ApplyGameSettings;
        GameAudio.ApplyAll();

        initialized = true;
        gameState = eGameState.None;
    }

    /// Pull everything GameSettings owns back into Main. Called whenever the settings screen
    /// changes something - in this scene, or on the next load after a change made in Splash.
    private void ApplyGameSettings()
    {
        SetDifficulty(GameSettings.Difficulty);
        SetBattingStyle(GameSettings.BattingStyle);
        if (theBatScript != null && theBatScript.attachParent != (GameSettings.BatInLeftHand(battingStyle) ? theBatScript.leftHandParent : theBatScript.rightHandParent))
            updateBattingStyle();   // the bat's controller changed, the stance did not
        BatAmplifier = GameSettings.BatPower;
        onFielderSpeed(GameSettings.FielderSpeed);
        overlayVisible = GameSettings.OverlayVisible;
        ApplyTweaks();
        RefreshSettingsPanel();
    }

    private void SetupXRControllers()
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb != null && mb.GetType().Name.StartsWith("OVR"))
                mb.enabled = false;
        }

        // The controller trackers, hand models and keyboard movement are placed in the scene by
        // Tools > CricketVR > Build UI Prefabs; nothing is added here at runtime.
        foreach (Transform hand in new[] { theBatScript?.leftHandParent, theBatScript?.rightHandParent })
        {
            if (hand != null && hand.parent != null && hand.parent.GetComponent<XRControllerTracker>() == null)
                Debug.LogError($"{hand.parent.name} has no XRControllerTracker - run Tools > CricketVR > Build UI Prefabs.", hand.parent);
        }
    }

    /// <summary>
    /// The settings panel is a prefab (SettingsPanel) with its controls wired in the editor. Main
    /// does not touch its controls: it calls SettingsPanel.Refresh when a setting changes.
    /// </summary>
    public void SetupMenus()
    {
        if (settingsPanel == null)
            Debug.LogError("Main has no SettingsPanel - run Tools > CricketVR > Build Player UI.", this);
    }

    private void RefreshSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.Refresh();
    }



    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Update is called once per frame
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    void Update()
    {
        if (!initialized)
            return;

        if (Main.Instance == null)
        {
            Main.Instance = this;
        }

        // Update values from inspector, when in editor, otherwise, toggle UI will drive this
        if (Application.isEditor)
        {
            updateBattingStyle(true);
            updateDifficulty(true);
            updateStadiumMode(true);
            updateZOffset(true);
            updateTweakables(true);
        }

        // Only when the state or delivery changes: rebuilding this string and the canvas every
        // frame allocated garbage and cost a UI rebuild per frame on the headset.
        if (gameState != debugShownState || currentBowlingConfig != debugShownConfig)
        {
            debugShownState = gameState;
            debugShownConfig = currentBowlingConfig;
            if (debugText != null)
                debugText.text = "GameState: " + gameState + (currentBowlingConfig != null ? "\n" + currentBowlingConfig : "");
        }

        // Grip calibration owns A and B while it runs, so the A that locks the grip cannot also
        // start a delivery and B cancels it rather than opening the menu.
        if (gripCalibration != null && gripCalibration.IsActive)
        {
            gripCalibration.Tick(GetButton(XRButton.A), GetButton(XRButton.B),
                                 GetButton(XRButton.X) || GetButton(XRButton.Y));
            return;
        }

        // Temporary stuff
        if (GetButton(XRButton.X))
        {
            gameState = eGameState.InGame_ResetToReady;
        }

        // if debug menu is not active!
        if (!menuToggle)
            StepGameState();


        // Pause: B brings up Resume / Settings / Main Menu. Settings is a screen inside it, so
        // there is one way in from every scene. Closing with B resumes, like the Resume button.
        if (GetButton(XRButton.B))
        {
            MenuRoot menu = MenuRoot.Instance;
            if (menu != null)
            {
                if (menu.IsOpen)
                {
                    Time.timeScale = 1f;
                    menu.Close();
                }
                else
                {
                    menu.Open();
                }
            }
            else
            {
                ToggleUI(!menuToggle);   // no menu in this scene yet: old behaviour
            }
        }

    }

    /*private float GetYVel(float length, float z)
    {
        float h = ballStartingPos.y - 0.2f;
        float g = Physics.gravity.magnitude;
        float d = length - ballStartingPos.z;
        float y = ((2 * z * z * h) - (d * d * g)) / (2 * d * z);
        return -y;
    }*/

    private void ToggleUI(bool flag)
    {
        menuToggle = flag;

        if (menuToggle)
        {
            settingsPanel.Show();
            theBat.SetActive(false);
        }
        else
        {
            // Save changes from settings
            PlayerPrefs.Save();
            settingsPanel.Hide();
            theBat.SetActive(true);
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Helper functions section
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private Key GetKeyForButton(XRButton button)
    {
        switch(button)
        {
            case XRButton.A: return Key.RightShift;
            case XRButton.B: return Key.Slash;
            case XRButton.X: return Key.LeftShift;
            case XRButton.Y: return Key.Z;
        }
        return Key.None;
    }

    public bool GetButton(XRButton button, bool justDown = true)
    {
        bool keyboard = false;
        if (Keyboard.current != null)
        {
            var key = Keyboard.current[GetKeyForButton(button)];
            keyboard = justDown ? key.wasPressedThisFrame : key.isPressed;
        }
        return keyboard || (justDown ? XRInput.GetDown(button) : XRInput.Get(button));
    }

    void LateUpdate()
    {
        XRInput.LateUpdate();
    }

}
