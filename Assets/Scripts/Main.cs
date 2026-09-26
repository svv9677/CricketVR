using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Main : MonoBehaviour
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
    protected GameObject BowlingMachineFace;
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
    private Toggle _easyToggle;
    private Toggle _mediumToggle;
    private Toggle _hardToggle;

    [SerializeField]
    protected eBattingStyle battingStyle;
    private Toggle _lBatToggle;
    private Toggle _rBatToggle;

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

    public bool overlayVisible = true;
    private bool _overlayVisible = false;
    private Toggle _overlayToggle;

    [Range(0f, 5f)]
    public float resetDelay = -100f;
    private float _resetDelay = 2f;
    private TMPro.TMP_Text _resetDelayText;
    private Slider _resetDelaySlider;

    [Range(0.5f, 2.5f)]
    public float fielderSpeed = -100f;
    private float _fielderSpeed = 1.5f;
    private TMPro.TMP_Text _fielderSpeedText;
    private Slider _fielderSpeedSlider;

    public eSwingType swingType;
    private eSwingType _swingType;
    private TMPro.TMP_Text _swingTypeText;

    [Range(0f, 15f)]
    public float ampMin = -100f;
    private float _ampMin = 10f;
    private TMPro.TMP_Text _ampMinText;
    private Slider _ampMinSlider;

    [Range(0f, 25f)]
    public float ampMax = -100f;
    private float _ampMax = 15f;
    private TMPro.TMP_Text _ampMaxText;
    private Slider _ampMaxSlider;

    public float MinX = Constants.paceCfg[0];
    private float _MinX = -100f;
    private TMPro.TMP_Text _MinXText;
    private Slider _MinXSlider;

    public float MaxX = Constants.paceCfg[1];
    private float _MaxX = -100f;
    private TMPro.TMP_Text _MaxXText;
    private Slider _MaxXSlider;

    public float MinY = Constants.paceCfg[2];
    private float _MinY = -100f;
    private TMPro.TMP_Text _MinYText;
    private Slider _MinYSlider;

    public float MaxY = Constants.paceCfg[3];
    private float _MaxY = -100f;
    private TMPro.TMP_Text _MaxYText;
    private Slider _MaxYSlider;

    public float MinZ = Constants.paceCfg[4];
    private float _MinZ = -100f;
    private TMPro.TMP_Text _MinZText;
    private Slider _MinZSlider;

    public float MaxZ = Constants.paceCfg[5];
    private float _MaxZ = -100f;
    private TMPro.TMP_Text _MaxZText;
    private Slider _MaxZSlider;

    public float MinSwing = Constants.paceCfg[6];
    private float _MinSwing = -100f;
    private TMPro.TMP_Text _MinSwingText;
    private Slider _MinSwingSlider;

    public float MaxSwing = Constants.paceCfg[7];
    private float _MaxSwing = -100f;
    private TMPro.TMP_Text _MaxSwingText;
    private Slider _MaxSwingSlider;

    public float MinPitchTurn = Constants.paceCfg[8];
    private float _MinPitchTurn = -100f;
    private TMPro.TMP_Text _MinPitchTurnText;
    private Slider _MinPitchTurnSlider;

    public float MaxPitchTurn = Constants.paceCfg[9];
    private float _MaxPitchTurn = -100f;
    private TMPro.TMP_Text _MaxPitchTurnText;
    private Slider _MaxPitchTurnSlider;

    public float BatAmplifier = 75f;
    private float _BatAmplifier = -100f;
    private TMPro.TMP_Text _BatAmplifierText;
    private Slider _BatAmplifierSlider;


    public eBattingStyle BattingStyle => battingStyle;
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
    private Material SignalMaterial;

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
        if(BowlingMachineFace != null)
        {
            SignalMaterial = BowlingMachineFace.GetComponent<Renderer>().material;
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

        // Read Settings
        difficulty = (eDifficulty)PlayerPrefs.GetInt(Constants.PP_Difficulty, 1);
        battingStyle = (eBattingStyle)PlayerPrefs.GetInt(Constants.PP_BattingStyle, 1);
        stadiumMode = (eStadiumMode)PlayerPrefs.GetInt(Constants.PP_StadiumMode, 1);
        zOffset = PlayerPrefs.GetFloat(Constants.PP_ZOffset, 3f);

        // Read Tweakables
        // Set default using private vars, and set private var to -1000 so that we
        // let auto-update update this along with the UI
        overlayVisible = PlayerPrefs.GetInt(Constants.PP_Overlay, 1) == 1;
        resetDelay = PlayerPrefs.GetFloat(Constants.PP_ResetDelay, _resetDelay);
        _resetDelay = -1000;
        fielderSpeed = PlayerPrefs.GetFloat(Constants.PP_FielderSpeed, _fielderSpeed);
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

        initialized = true;
        gameState = eGameState.None;
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
    /// The settings panel is a prefab (SettingsPanel) with its controls wired in the editor; here
    /// Main just takes hold of the controls it keeps in step with the settings.
    /// </summary>
    public void SetupMenus()
    {
        SettingsPanel s = settingsPanel;
        _lBatToggle = s.leftHanded;
        _rBatToggle = s.rightHanded;
        _easyToggle = s.easy;
        _mediumToggle = s.medium;
        _hardToggle = s.hard;
        _overlayToggle = s.overlay;
        (_resetDelaySlider, _resetDelayText) = (s.resetDelay.slider, s.resetDelay.value);
        (_fielderSpeedSlider, _fielderSpeedText) = (s.fielderSpeed.slider, s.fielderSpeed.value);
        (_ampMinSlider, _ampMinText) = (s.ampMin.slider, s.ampMin.value);
        (_ampMaxSlider, _ampMaxText) = (s.ampMax.slider, s.ampMax.value);
        (_BatAmplifierSlider, _BatAmplifierText) = (s.batPower.slider, s.batPower.value);
        _swingTypeText = s.bowlingType;
        (_MinXSlider, _MinXText) = (s.minSpeed.slider, s.minSpeed.value);
        (_MaxXSlider, _MaxXText) = (s.maxSpeed.slider, s.maxSpeed.value);
        (_MinZSlider, _MinZText) = (s.minLine.slider, s.minLine.value);
        (_MaxZSlider, _MaxZText) = (s.maxLine.slider, s.maxLine.value);
        (_MinSwingSlider, _MinSwingText) = (s.minSwing.slider, s.minSwing.value);
        (_MaxSwingSlider, _MaxSwingText) = (s.maxSwing.slider, s.maxSwing.value);
        (_MinPitchTurnSlider, _MinPitchTurnText) = (s.minTurn.slider, s.minTurn.value);
        (_MaxPitchTurnSlider, _MaxPitchTurnText) = (s.maxTurn.slider, s.maxTurn.value);
    }

    /// Apply tuning changes made on the settings panel.
    public void ApplyTweaks() => updateTweakables();

    public void CloseSettings()
    {
        if (menuToggle)
            ToggleUI(false);
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Difficulty settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateDifficulty(bool updateUI = false)
    {
        bool changed = false;
        // TODO: Take action for appropriate difficulty, and set 'changed'

        PlayerPrefs.SetInt(Constants.PP_Difficulty, (int)difficulty);

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (difficulty == eDifficulty.Easy && !_easyToggle.isOn)
        {
            _easyToggle.isOn = true;
            updateBatColliderSize();
        }
        else if (difficulty == eDifficulty.Medium && !_mediumToggle.isOn)
        {
            _mediumToggle.isOn = true;
            updateBatColliderSize();
        }
        else if (difficulty == eDifficulty.Hard && !_hardToggle.isOn)
        {
            _hardToggle.isOn = true;
            updateBatColliderSize();
        }

        if (changed)
        {
            PlayerPrefs.Save();
        }

            
    }
    public void onRadioEasy(Toggle t)
    {
        if (!t.isOn)
            return;
        if (difficulty != eDifficulty.Easy)
        {
            difficulty = eDifficulty.Easy;
            updateDifficulty();
        }
    }
    public void onRadioMedium(Toggle t)
    {
        if (!t.isOn)
            return;
        if (difficulty != eDifficulty.Medium)
        {
            difficulty = eDifficulty.Medium;
            updateDifficulty();
        }
    }
    public void onRadioHard(Toggle t)
    {
        if (!t.isOn)
            return;
        if (difficulty != eDifficulty.Hard)
        {
            difficulty = eDifficulty.Hard;
            updateDifficulty();
        }
    }

    void updateBatColliderSize()
    {
        switch (difficulty)
        {
            case eDifficulty.Easy:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierEasy;
                    theBatScript.batCollider.size = theSize;
                }
                break;
            case eDifficulty.Medium:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierMedium;
                    theBatScript.batCollider.size = theSize;
                }
                break;
            case eDifficulty.Hard:
                {
                    Vector3 theSize = theBatScript.originalSize;
                    theSize.x *= Constants.BatColliderMultiplierHard;
                    theBatScript.batCollider.size = theSize;
                }
                break;
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Batting Style settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateBattingStyle(bool updateUI = false)
    {
        bool changed = false;
        if (theBatScript.leftHandParent != null && battingStyle == eBattingStyle.LeftHanded)
        {
            changed = true;
            theBatScript.attachParent = theBatScript.leftHandParent;
            theBatScript.CheckAndGrab();
            PlayerPrefs.SetInt(Constants.PP_BattingStyle, (int)battingStyle);
        }
        if (theBatScript.rightHandParent != null && battingStyle == eBattingStyle.RightHanded)
        {
            changed = true;
            theBatScript.attachParent = theBatScript.rightHandParent;
            theBatScript.CheckAndGrab();
            PlayerPrefs.SetInt(Constants.PP_BattingStyle, (int)battingStyle);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (battingStyle == eBattingStyle.RightHanded && !_rBatToggle.isOn)
            _rBatToggle.isOn = true;
        else if (battingStyle == eBattingStyle.LeftHanded && !_lBatToggle.isOn)
            _lBatToggle.isOn = true;

        if (changed)
            PlayerPrefs.Save();
    }
    public void onRadioRightHanded(Toggle t)
    {
        if (!t.isOn)
            return;
        if (battingStyle != eBattingStyle.RightHanded)
        {
            battingStyle = eBattingStyle.RightHanded;
            updateBattingStyle();
        }
    }
    public void onRadioLeftHanded(Toggle t)
    {
        if (!t.isOn)
            return;
        if (battingStyle != eBattingStyle.LeftHanded)
        {
            battingStyle = eBattingStyle.LeftHanded;
            updateBattingStyle();
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Stadium settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateStadiumMode(bool updateUI = false)
    {
        bool changed = false;
        if (stadiumMode == eStadiumMode.Night && !theNightLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = NightMaterial;
            theNightLights.SetActive(true);
            theDayLights.SetActive(false);
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }
        else if(stadiumMode == eStadiumMode.Day && !theDayLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = DayMaterial;
            theNightLights.SetActive(false);
            theDayLights.SetActive(true);
            PlayerPrefs.SetInt(Constants.PP_StadiumMode, (int)stadiumMode);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        if(changed)
            PlayerPrefs.Save();
    }
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Z Offset
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateZOffset(bool savePrefs = false)
    {
        // The settings panel now opens in front of the player, wherever they face, so the old
        // "Menu Position" slider is gone; the value is only kept in PlayerPrefs.
        if (_zOffset != zOffset)
        {
            _zOffset = zOffset;
            PlayerPrefs.SetFloat(Constants.PP_ZOffset, zOffset);
            if (savePrefs)
                PlayerPrefs.Save();
        }
    }
    public void onZChange(float val)
    {
        zOffset = val;
        updateZOffset();
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Debug Tweakables
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateTweakables(bool savePrefs = false)
    {
        bool changed = false;
        if (ampMin != _ampMin)
        {
            changed = true;
            _ampMin = ampMin;
            PlayerPrefs.SetFloat(Constants.PP_AmpMin, ampMin);

            if (_ampMinText != null)
                _ampMinText.text = ampMin.ToString();
            if (_ampMinSlider != null)
                _ampMinSlider.value = ampMin;
        }
        if (ampMax != _ampMax)
        {
            changed = true;
            _ampMax = ampMax;
            PlayerPrefs.SetFloat(Constants.PP_AmpMax, ampMax);

            if (_ampMaxText != null)
                _ampMaxText.text = ampMax.ToString();
            if (_ampMaxSlider != null)
                _ampMaxSlider.value = ampMax;
        }
        if (resetDelay != _resetDelay)
        {
            changed = true;
            _resetDelay = resetDelay;
            PlayerPrefs.SetFloat(Constants.PP_ResetDelay, resetDelay);

            if (_resetDelayText != null)
                _resetDelayText.text = resetDelay.ToString();
            if (_resetDelaySlider != null)
                _resetDelaySlider.value = resetDelay;
        }
        if (fielderSpeed != _fielderSpeed)
        {
            changed = true;
            _fielderSpeed = fielderSpeed;
            PlayerPrefs.SetFloat(Constants.PP_FielderSpeed, fielderSpeed);

            if (_fielderSpeedText != null)
                _fielderSpeedText.text = fielderSpeed.ToString();
            if (_fielderSpeedSlider != null)
                _fielderSpeedSlider.value = fielderSpeed;
        }
        if (overlayVisible != _overlayVisible)
        {
            changed = true;
            _overlayVisible = overlayVisible;

            dbgOverlayParent.SetActive(overlayVisible);
            if (_overlayToggle != null)
                _overlayToggle.isOn = overlayVisible;

            PlayerPrefs.SetInt(Constants.PP_Overlay, overlayVisible ? 1 : 0);
        }
        if(swingType != _swingType)
        {
            _swingType = swingType;
            reloadTweakables();
        }

        if (_BatAmplifier != BatAmplifier)
        {
            _BatAmplifier = BatAmplifier;
            _BatAmplifierText.text = _BatAmplifier.ToString();
            _BatAmplifierSlider.value = _BatAmplifier;
        }

        if (bowlingProfileManager != null)
        {
            BowlingProfile profile = bowlingProfileManager.GetProfile(swingType);
            if (_MinX != MinX)
            {
                _MinX = MinX;
                _MinXText.text = _MinX.ToString();
                _MinXSlider.value = _MinX;
                profile.minX = MinX;
            }
            if (_MaxX != MaxX)
            {
                _MaxX = MaxX;
                _MaxXText.text = _MaxX.ToString();
                _MaxXSlider.value = _MaxX;
                profile.maxX = MaxX;
            }
            //if (_MinY != MinY)
            //{
            //    _MinY = MinY;
            //    _MinYText.text = _MinY.ToString();
            //    _MinYSlider.value = _MinY;
            //    profile.minY = MinY;
            //}
            //if (_MaxY != MaxY)
            //{
            //    _MaxY = MaxY;
            //    _MaxYText.text = _MaxY.ToString();
            //    _MaxYSlider.value = _MaxY;
            //    profile.maxY = MaxY;
            //}
            if (_MinZ != MinZ)
            {
                _MinZ = MinZ;
                _MinZText.text = _MinZ.ToString();
                _MinZSlider.value = _MinZ;
                profile.minZ = MinZ;
            }
            if (_MaxZ != MaxZ)
            {
                _MaxZ = MaxZ;
                _MaxZText.text = _MaxZ.ToString();
                _MaxZSlider.value = _MaxZ;
                profile.maxZ = MaxZ;
            }
            if (_MinSwing != MinSwing)
            {
                _MinSwing = MinSwing;
                _MinSwingText.text = _MinSwing.ToString();
                _MinSwingSlider.value = _MinSwing;
                profile.minSwing = MinSwing;
            }
            if (_MaxSwing != MaxSwing)
            {
                _MaxSwing = MaxSwing;
                _MaxSwingText.text = _MaxSwing.ToString();
                _MaxSwingSlider.value = _MaxSwing;
                profile.maxSwing = MaxSwing;
            }
            if (_MinPitchTurn != MinPitchTurn)
            {
                _MinPitchTurn = MinPitchTurn;
                _MinPitchTurnText.text = _MinPitchTurn.ToString();
                _MinPitchTurnSlider.value = _MinPitchTurn;
                profile.minPitchTurn = MinPitchTurn;
            }
            if (_MaxPitchTurn != MaxPitchTurn)
            {
                _MaxPitchTurn = MaxPitchTurn;
                _MaxPitchTurnText.text = _MaxPitchTurn.ToString();
                _MaxPitchTurnSlider.value = _MaxPitchTurn;
                profile.maxPitchTurn = MaxPitchTurn;
            }
        }

        if (savePrefs && changed)
            PlayerPrefs.Save();
    }
    public void onVoid(float val) { }
    /// Bowl the next ball (the A button, or "Next Ball" on the between-balls panel).
    public void StartNextBall()
    {
        if (gameState != eGameState.InGame_Ready)
            return;
        currentFielderName = "";

        // Reset stumps - both ends
        foreach (Stumps stumps in FindObjectsByType<Stumps>(FindObjectsSortMode.None))
            stumps.Reset();

        StopTheBall();

        // Switch to the process of selecting delivery type, stride & other params before delivery loop
        gameState = eGameState.InGame_SelectDelivery;
    }

    public void OpenSettings()
    {
        if (!menuToggle)
            ToggleUI(true);
    }

    public bool SettingsOpen => menuToggle;
    public bool CalibratingGrip => gripCalibration != null && gripCalibration.IsActive;

    public void onCalibrateGrip() { StartGripCalibration(false); }
    public void onCalibrateGripFlat() { StartGripCalibration(true); }
    private void StartGripCalibration(bool flat)
    {
        if (gripCalibration == null)
            return;
        // Close the menu first: showing it hides the bat.
        if (menuToggle)
            ToggleUI(false);
        gripCalibration.Begin(flat);
    }
    public void onResetGrip()
    {
        if (gripCalibration != null)
            gripCalibration.ResetToDefault();
    }
    public void reloadTweakables()
    {
        _swingTypeText.text = "Bowling Type - " + swingType.ToString();
        float[] config;
        switch(swingType)
        {
            case eSwingType.Pace:
                config = Constants.paceCfg;
                break;
            case eSwingType.InSwing:
                config = Constants.inSwingCfg;
                break;
            case eSwingType.OutSwing:
                config = Constants.outSwingCfg;
                break;
            case eSwingType.LegSpin:
                config = Constants.legSpinCfg;
                break;
            case eSwingType.OffSpin:
                config = Constants.offSpinCfg;
                break;
            default:
                config = Constants.paceCfg;
                break;
        }
        MinX = config[0];
        MaxX = config[1];
        MinY = config[2];
        MaxY = config[3];
        MinZ = config[4];
        MaxZ = config[5];
        MinSwing = config[6];
        MaxSwing = config[7];
        MinPitchTurn = config[8];
        MaxPitchTurn = config[9];
    }
    public void onOverlayToggle(Toggle t)
    {
        overlayVisible = t.isOn;
        updateTweakables();
    }
    public void onResetDelay(float val)
    {
        resetDelay = val;
        updateTweakables();
    }
    public void onFielderSpeed(float val)
    {
        fielderSpeed = val;
        updateTweakables();
    }
    public void onAmpMinChange(float val)
    {
        ampMin = val;
        updateTweakables();
    }
    public void onAmpMaxChange(float val)
    {
        ampMax = val;
        updateTweakables();
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
            if (SignalMaterial != null)
                SignalMaterial.color = gameState == eGameState.InGame_Ready ? Color.green : Color.red;
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
        {
            switch(gameState)
            {
                case eGameState.None:
                    {
                        // For now, skip the menus
                        gameState = eGameState.InGame_ResetToReady;
                    }
                    break;
                case eGameState.InMenu:
                    {

                    }
                    break;
                case eGameState.InGame_Ready:
                    {
                        if (GetButton(XRButton.A))
                            StartNextBall();
                    }
                    break;
                case eGameState.InGame_SelectDelivery:
                    {
                        // Get current bowler from Score keeper
                        currentBowlingConfig = bowlingProfileManager.GetProfile(theHUD.CurrentBowler.Type).GetRandomDelivery();
                        CameraReplay.Instance.startCapturing = null;
                        CameraReplay.Instance.startDisplaying = null;
                        CameraReplay.Instance.StopDisplaying();
                        CameraReplay.Instance.setViewSetting(0);
                        ShotDistance.Instance.setText("");
                        BallSpeed.Instance.setText("");
                        // TODO: Move bowling machine

                        // For now, switch directly to loop state
                        gameState = eGameState.InGame_SelectDeliveryLoop;
                    }
                    break;
                case eGameState.InGame_SelectDeliveryLoop:
                    {
                        // TODO: Add the coroutines & tweening waits
                        // For now, switch to deliver ball state
                        // Uncomment the below line to release the ball straight away without waiting for the animation.
                        //gameState = eGameState.InGame_DeliverBall;
                    }
                    break;
                case eGameState.InGame_DeliverBall:
                    {
                        // Remove hand parent
                        theBall.transform.SetParent(null);
                        Vector3 torque = new Vector3(currentBowlingConfig.torqueX, 0f, 0f);

                        // Take the release point from the bowler's hand, captured by the release
                        // animation event itself (AnimatedBowler.ReleaseBall), not from the ball's
                        // own transform. The ball is supposed to be sitting on the hand, so in the
                        // healthy case these are the same point and nothing moves. They are not the
                        // same when carrying the ball has failed - on device the ball was orbiting
                        // the hand on a 12 m arm and its transform read (-1.36, 7.11, 8.10), eight
                        // metres off the pitch and seven in the air. The hand is the thing the
                        // player watches, so it is the thing the delivery should start from.
                        Vector3 releaseFrom = theBall.transform.position;
                        AnimatedBowler bowler = AnimatedBowler.Instance;
                        if (bowler != null && bowler.HasReleasePosition)
                            releaseFrom = bowler.ReleasePosition;

                        // Work out the whole release from one validated solve (see BallDelivery).
                        // speedX is an impulse against the configs' 0.2 kg ball; `length` is the
                        // world X the ball pitches at; speedZ is the line at the batsman's stumps,
                        // off side positive, so mirror it for a left-hander.
                        float offSign = battingStyle == eBattingStyle.LeftHanded ? -1f : 1f;
                        float speedMps = currentBowlingConfig.speedX / Constants.ConfigImpulseMass;
                        float targetLine = offSign * currentBowlingConfig.speedZ;
                        BallFlight.DeliveryEffects effects = currentBowlingConfig.Effects(offSign);
                        theBallScript.deliveryEffects = effects;
                        BallDelivery.Solution delivery = BallDelivery.Solve(
                            releaseFrom, currentBowlingConfig.length, speedMps, targetLine, effects);
                        if (!string.IsNullOrEmpty(delivery.warnings))
                            Debug.LogWarning($"[Delivery] {delivery}");
                        else if (verboseStateLogging)
                            Debug.Log($"[Delivery] {delivery}");

                        // The collider is switched off while the bowler carries the ball, so that
                        // teleporting it onto his hand cannot shove the stumps (see
                        // AnimatedBowler.HoldBall). Switch it back on now the ball is live.
                        Collider ballCollider = theBall.GetComponent<Collider>();
                        if (ballCollider != null)
                            ballCollider.enabled = true;

                        // enable physics
                        theBallRigidBody.isKinematic = false;
                        //Set collision type to continuous dynamic
                        theBallRigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        // Set interpolation mode to interpolate
                        theBallRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
                        // Put the ball exactly on the solved release point. This is safe to do
                        // unconditionally now that the point comes from the hand rather than from
                        // the ball: on a healthy delivery the ball is already there and this moves
                        // it by nothing, so there is no snap. When carrying the ball has gone
                        // wrong it is the correction that keeps the delivery sane.
                        theBall.transform.position = delivery.releasePosition;
                        // And the body itself: autoSyncTransforms is off, so the transform write
                        // alone left PhysX starting the ball from where the hand was on the last
                        // physics step - measured up to 0.24 m behind, pitching that much short.
                        theBallRigidBody.position = delivery.releasePosition;

                        // ASSIGN both velocities rather than AddForce onto whatever the body was
                        // carrying. While parented to the bowler's hand the ball is kinematic, and
                        // a kinematic body carries its hand motion (~14-16 m/s, mostly downward)
                        // across the switch to dynamic. Assignment overwrites; AddForce would add.
                        // Zero first, then set. The assignment alone is already sufficient (it
                        // overwrites rather than accumulates), but zeroing explicitly makes the
                        // intent obvious and costs nothing.
                        theBallRigidBody.linearVelocity = Vector3.zero;
                        theBallRigidBody.angularVelocity = Vector3.zero;
                        // delivery.releaseVelocity is the old `speed / theBallRigidBody.mass`,
                        // but solved against a validated release point (see BallDelivery).
                        theBallRigidBody.linearVelocity = delivery.releaseVelocity;
                        theBallRigidBody.AddTorque(torque, ForceMode.Impulse);
                        
                        // save it
                        //theBallScript.lastVelocity = speed;   (commented out because the lastVelocity for the bat collision equations should be updated after the ball hits the pitch.
                        // mark as fresh delivery!
                        theBallScript.fresh = true;
                        theBallScript.lastPitchPoint = new Vector3(float.NaN, 0f, 0f);
                        theBallScript.lastLineAtStumps = float.NaN;
                        lastDelivery = delivery;
                        theBallScript.bounced = false;
                        theBallScript.wide = false;
                        theBatScript.hasHitBall = false;

                        CameraReplay.Instance.StartRecording(0f);
                        StartCoroutine(BallSpeed.Instance.updateBallSpeed());
                        gameState = eGameState.InGame_DeliverBallLoop;
                    }
                    break;
                case eGameState.InGame_DeliverBallLoop:
                    {
                        // Nothing to do here for now!
                        // Bat collision with the ball or
                        // Ball collision with the stumps or
                        // Ball collision with the keeper (invisible collider)
                        // will trigger next state change
                    }
                    break;
                case eGameState.InGame_BallHit:
                    {
                        // TODO: Add any special processing needed here
                        // For now, switch to next state
                        CameraReplay.Instance.StopRecording(2f);
                        gameState = eGameState.InGame_BallHitLoop;
                    }
                    break;
                case eGameState.InGame_BallHitLoop:
                    {
                        // Nothing to do here for now!
                        // Fielder collision with the ball or
                        // Boundary collision with the ball
                        // will trigger next state change
                    }
                    break;
                case eGameState.InGame_BallFielded:
                    {
                        // TODO: Add any special processing needed here
                        // For now, switch to next state
                        gameState = eGameState.InGame_BallFielded_Loop;

                        // Wait for '2' seconds and reset to Ready
                        StartCoroutine(WaitAndSetGameState(2f, eGameState.InGame_ResetToReady));
                    }
                    break;
                case eGameState.InGame_BallFielded_Loop:
                    {
                        // Nothing to do here for now!
                    }
                    break;
                case eGameState.InGame_Bowled:
                    {
                        // TODO: Add any special processing needed here
                        // For now, switch to next state
                        CameraReplay.Instance.StopRecording(1f);
                        gameState = eGameState.InGame_BowledLoop;

                        // Wait for '2' seconds and reset to Ready
                        StartCoroutine(WaitAndSetGameState(2f, eGameState.InGame_ResetToReady));
                    }
                    break;
                case eGameState.InGame_BowledLoop:
                    {
                        // Nothing to do here for now!
                    }
                    break;
                case eGameState.InGame_BallMissed:
                    {
                        // TODO: Add any special processing needed here
                        // For now, switch to next state
                        CameraReplay.Instance.StopRecording(0f);
                        gameState = eGameState.InGame_BallMissedLoop;

                        // Wait for '2' seconds and reset to Ready
                        StartCoroutine(WaitAndSetGameState(2f, eGameState.InGame_ResetToReady));
                    }
                    break;
                case eGameState.InGame_BallMissedLoop:
                    {
                        // Nothing to do here for now!
                    }
                    break;
                case eGameState.InGame_BallPastBoundary:
                    {
                        // TODO: Add any special processing needed here
                        // For now, switch to next state
                        gameState = eGameState.InGame_BallPastBoundaryLoop;

                        // Wait for '2' seconds and reset to Ready
                        StartCoroutine(WaitAndSetGameState(2f, eGameState.InGame_ResetToReady));
                    }
                    break;
                case eGameState.InGame_BallPastBoundaryLoop:
                    {
                        // Nothing to do here for now!
                    }
                    break;
                case eGameState.InGame_ResetToReady:
                    {
                        gameState = eGameState.InGame_ResetToReadyLoop;

                        // Wait for 'ResetDelay' seconds and reset to Ready
                        StartCoroutine(WaitAndSetGameState(resetDelay, eGameState.InGame_Ready));
                    }
                    break;
                case eGameState.InGame_ResetToReadyLoop:
                    {
                        // Nothing to do here for now!
                    }
                    break;

                default:
                    break;
            }
        }

        // Debug UI toggle!
        if (GetButton(XRButton.B))
        {
            ToggleUI(!menuToggle);
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

    private void StopTheBall()
    {
        // pause the particles
        //theBallScript.myParticles.Stop();
        //theBallScript.myParticles.Clear();  PARTICLE
        theBallScript.myParticles.Clear();
        theBallScript.myParticles.enabled = false;
        // Stop the ball BEFORE making it kinematic. Setting velocity on a kinematic body is a
        // no-op (Unity even warns about it), which is what let the ball carry its motion into
        // the next delivery.
        //
        // The ball has usually already been parked as kinematic by the time we get here - by the
        // boundary, a fielder or the keeper - so it has to be made dynamic again first or the
        // zeroing below is silently discarded. That was still happening: every delivery logged
        // "Setting angular velocity of a kinematic body is not supported" from this line.
        theBallRigidBody.isKinematic = false;
        theBallRigidBody.linearVelocity = Vector3.zero;
        theBallRigidBody.angularVelocity = Vector3.zero;
        // disable physics
        theBallRigidBody.isKinematic = true;
        // Take the collider out of the world before teleporting. This can be a long jump - from
        // the boundary rope back to the bowler's mark - and PhysX treats a kinematic body's
        // teleport as motion, so an enabled collider sweeps everything on the way and knocks the
        // stumps over. Re-enabled when the ball is delivered.
        Collider parkedCollider = theBall.GetComponent<Collider>();
        if (parkedCollider != null)
            parkedCollider.enabled = false;
        // reset ball position to inside machine
        theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);
        theBallRigidBody.position = theBall.transform.position;
    }

    public IEnumerator WaitAndSetGameState(float delay, eGameState state)
    {
        yield return new WaitForSeconds(delay);

        if (verboseStateLogging)
            Debug.Log("Setting GameState from: " + gameState.ToString() + " to: " + state.ToString());
        gameState = state;
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

    private void HandleLog(string message, string stackTrace, LogType logType)
    {
        // BallDiagnostics emits several lines per physics step. Mirroring that onto the in-world
        // console buries everything else and covers the view in a headset capture. It is still in
        // the device log, which is the useful place to read it from:
        //     adb logcat -c && adb logcat -v time -s Unity:I
        if (message != null && message.StartsWith("[DIAG]"))
            return;

        string color;

        // Check if we are ignoring a certain type of message.
        if (logType == LogType.Log)
            color = "white";
        else if (logType == LogType.Warning)
            color = "yellow";
        else
            color = "red";

        string txt = "<color=" + color + ">" + System.DateTime.Now.ToLongTimeString() + " - " + message + "</color>";
        history.Add(txt);
        historyTextLength += txt.Length;

        // Enforce history and system limits
        while (history.Count > 0 && (history.Count > HISTORY_MAX_LINES || historyTextLength > HISTORY_TEXT_MAX_LENGTH))
        {
            // Remove oldest entry.
            historyTextLength -= history[0].Length;
            history.RemoveAt(0);
        }

        // Harden the history text length in case the history text length "drifts" from the history contents...
        historyTextLength = history.Count > 0 ? Mathf.Max(0, historyTextLength) : 0;

        onConsoleTextChange();
    }

    public void onConsoleTextChange()
    {
        string viewText = string.Empty;

        int start = history.Count - MAX_VIEWABLE_LINES;
        if (start < 0)
            start = 0;

        for (int i = start; i < history.Count; i++)
        {
            viewText += history[i];

            if (i < history.Count - 1)
            {
                viewText += "\n";
            }
        }

        consoleText.text = viewText;
    }

    //float GetYVel(Vector3 ballPos, float length, float x)
    //{
    //    float h = ballPos.y - 0.2f;
    //    float g = Physics.gravity.magnitude;
    //    float d = length - ballPos.x;
    //    float y = ((2 * x * x * h) - (d * d * g)) / (2 * d * x);
    //    print(y + " || " + -y);
    //    return -y;
    //}

    
    float GetYVel(Vector3 startPos, float startVelX, float length)
    {
        //Main inst = Main.Instance;
        float height = 0.1f;
        float time = (length - startPos.x) / startVelX;

        // Guard: if the ball is released from at/behind the target pitching point, `time` goes
        // zero or negative and the expression below flips sign, returning a huge POSITIVE vy -
        // the ball is fired vertically at ~40 m/s instead of being bowled. That is exactly what
        // happens when the ball is not sitting in the bowler's hand at release. Clamp to a
        // plausible flight time so a bad release position degrades to a sane delivery instead of
        // a rocket.
        const float minFlightTime = 0.15f;
        if (!(time > minFlightTime))
        {
            Debug.LogWarning($"[GetYVel] implausible flight time {time:F3}s from startPos {startPos / 3.28f} " +
                             $"to length {length / 3.28f} - the ball is probably not in the bowler's hand. " +
                             $"Clamping to {minFlightTime}s.");
            time = minFlightTime;
        }

        float startVelY = (16f * time) + (height - startPos.y / time);
        //// Air Resistance Formula
        //var p = 0.25f; // 1.225f;
        //var cd = 0.25f; // 0.47f;
        //var a = Mathf.PI * 0.0575f * 0.0575f;
        //var v = new Vector3(currentBowlingConfig.speedX, startVelY, currentBowlingConfig.speedZ).magnitude;
        //var direction = -new Vector3(currentBowlingConfig.speedX, startVelY, currentBowlingConfig.speedZ).normalized;
        //var forceAmount = (p * v * v * cd * a) / 2;

        //Vector3 right = Vector3.zero;
        //bool inSwing = UnityEngine.Random.Range(0f, 1f) > 0.5f;
        //if (inst.currentBowlingConfig.swingType == eSwingType.InSwing || inst.currentBowlingConfig.swingType == eSwingType.LegSpin ||
        //    (inst.currentBowlingConfig.swingType == eSwingType.Random && inSwing))
        //    right = Vector3.Cross(direction, Vector3.up).normalized; // direction is negative already
        //if (inst.currentBowlingConfig.swingType == eSwingType.OutSwing || inst.currentBowlingConfig.swingType == eSwingType.OffSpin ||
        //    (inst.currentBowlingConfig.swingType == eSwingType.Random && !inSwing))
        //    right = Vector3.Cross(-direction, Vector3.up).normalized;

        //forceAmount *= 10f;
        //right.x = 0f;
        //right.y = 0f;

        //float finalZ = GetFinalZ(theBall.transform.position.z, currentBowlingConfig.speedZ * Time.deltaTime / theBallRigidBody.mass, ((direction.z * forceAmount / 10f) + (right * forceAmount * inst.currentBowlingConfig.swing).z) * Time.deltaTime / theBallRigidBody.mass, time / Time.deltaTime);
        //print(finalZ);
        return startVelY;
    }

    float GetFinalZ(float startZ, float startVelZ, float velChange, float frames)
    {
        //print(startZ);
        //print(startVelZ);
        // startZ    :  meters
        // startVelZ :  meters/frame
        // velChange :  meters/frame
        // frames    :  frame
        //print("%%% " + frames + " %%% " + Factorial(frames));
        return startZ + ((startVelZ + velChange) * frames);
    }

    float Factorial(float n)
    {
        if (n <= 1)
        {
            return 1;
        }

        return n + Factorial(n - 1);
    }

    //float GetYVel(Vector3 ballStartingPos, float x, float length)
    //{
    //    float h = ballStartingPos.y - 0.2f;
    //    float g = Physics.gravity.magnitude;
    //    float d = length - ballStartingPos.x;
    //    float y = ((2 * x * x * h) - (d * d * g)) / (2 * d * x);
    //    return -y;
    //}
}
