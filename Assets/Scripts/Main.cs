using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
    protected OVRPlayerController theController;
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
    private Toggle _nightToggle;
    private Toggle _dayToggle;

    [SerializeField]
    [Range(3f, 10f)]
    protected float zOffset;
    private float _zOffset;
    private Text _zOffsetText;
    private Slider _zOffsetSlider;

    [SerializeField]
    [Range(3f, 5f)]
    protected float hudOffset;
    private float _hudOffset;
    private Text _hudOffsetText;
    private Slider _hudOffsetSlider;

    [Header("Debug Tweaks")]
    public bool overlayVisible = true;
    private bool _overlayVisible = false;
    private Toggle _overlayToggle;

    [Range(0f, 5f)]
    public float resetDelay = -100f;
    private float _resetDelay = 2f;
    private Text _resetDelayText;
    private Slider _resetDelaySlider;

    [Range(0.5f, 2.5f)]
    public float fielderSpeed = -100f;
    private float _fielderSpeed = 1.5f;
    private Text _fielderSpeedText;
    private Slider _fielderSpeedSlider;

    public eSwingType swingType;
    private eSwingType _swingType;
    private Text _swingTypeText;

    [Range(0f, 15f)]
    public float ampMin = -100f;
    private float _ampMin = 10f;
    private Text _ampMinText;
    private Slider _ampMinSlider;

    [Range(0f, 25f)]
    public float ampMax = -100f;
    private float _ampMax = 15f;
    private Text _ampMaxText;
    private Slider _ampMaxSlider;

    public float MinX = Constants.paceCfg[0];
    private float _MinX = -100f;
    private Text _MinXText;
    private Slider _MinXSlider;

    public float MaxX = Constants.paceCfg[1];
    private float _MaxX = -100f;
    private Text _MaxXText;
    private Slider _MaxXSlider;

    public float MinY = Constants.paceCfg[2];
    private float _MinY = -100f;
    private Text _MinYText;
    private Slider _MinYSlider;

    public float MaxY = Constants.paceCfg[3];
    private float _MaxY = -100f;
    private Text _MaxYText;
    private Slider _MaxYSlider;

    public float MinZ = Constants.paceCfg[4];
    private float _MinZ = -100f;
    private Text _MinZText;
    private Slider _MinZSlider;

    public float MaxZ = Constants.paceCfg[5];
    private float _MaxZ = -100f;
    private Text _MaxZText;
    private Slider _MaxZSlider;

    public float MinSwing = Constants.paceCfg[6];
    private float _MinSwing = -100f;
    private Text _MinSwingText;
    private Slider _MinSwingSlider;

    public float MaxSwing = Constants.paceCfg[7];
    private float _MaxSwing = -100f;
    private Text _MaxSwingText;
    private Slider _MaxSwingSlider;

    public float MinPitchTurn = Constants.paceCfg[8];
    private float _MinPitchTurn = -100f;
    private Text _MinPitchTurnText;
    private Slider _MinPitchTurnSlider;

    public float MaxPitchTurn = Constants.paceCfg[9];
    private float _MaxPitchTurn = -100f;
    private Text _MaxPitchTurnText;
    private Slider _MaxPitchTurnSlider;

    // Internal variables
    private bool initialized;
    private bool menuToggle;
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

        // Initialize menus
        menuToggle = false;
        SetupMenus();

        // Read Settings
        difficulty = (eDifficulty)PlayerPrefs.GetInt(Constants.PP_Difficulty, 1);
        battingStyle = (eBattingStyle)PlayerPrefs.GetInt(Constants.PP_BattingStyle, 1);
        stadiumMode = (eStadiumMode)PlayerPrefs.GetInt(Constants.PP_StadiumMode, 1);
        zOffset = PlayerPrefs.GetFloat(Constants.PP_ZOffset, 3f);
        hudOffset = PlayerPrefs.GetFloat(Constants.PP_HudOffset, 4f);

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
        updateBattingStyle(true);
        updateStadiumMode(true);
        updateZOffset(true);
        updateHUDOffset(true);

        updateTweakables(true);

        currentBowlingConfig = new BowlingParams();
        bowlingProfileManager = new BowlingProfileManager();
        bowlingProfileManager.InitProfilesFromParams();

        initialized = true;
        gameState = eGameState.None;

        
    }

    public void SetupMenus()
    {
        DebugUIBuilder.instance.AddLabel("Settings");
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Settings Menu
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Batting Style");
        var radio1 = DebugUIBuilder.instance.AddRadio("Left Handed", "batting", onRadioLeftHanded);
        _lBatToggle = radio1.GetComponentInChildren<Toggle>();
        var radio2 = DebugUIBuilder.instance.AddRadio("Right Handed", "batting", onRadioRightHanded);
        _rBatToggle = radio2.GetComponentInChildren<Toggle>();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Difficulty");
        var radio3 = DebugUIBuilder.instance.AddRadio("Easy", "difficulty", onRadioEasy);
        _easyToggle = radio3.GetComponentInChildren<Toggle>();
        var radio4 = DebugUIBuilder.instance.AddRadio("Medium", "difficulty", onRadioMedium);
        _mediumToggle = radio4.GetComponentInChildren<Toggle>();
        var radio5 = DebugUIBuilder.instance.AddRadio("Hard", "difficulty", onRadioHard);
        _hardToggle = radio5.GetComponentInChildren<Toggle>();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Stadium");
        var radio6 = DebugUIBuilder.instance.AddRadio("Night", "stadium", onNightMode);
        _nightToggle = radio6.GetComponentInChildren<Toggle>();
        var radio7 = DebugUIBuilder.instance.AddRadio("Day", "stadium", onDayMode);
        _dayToggle = radio7.GetComponentInChildren<Toggle>();

        DebugUIBuilder.instance.AddDivider();
        // offsetZ
        var pr = DebugUIBuilder.instance.AddSlider("Menu Position", 3.0f, 10.0f, onZChange, true);
        _zOffsetText = pr.GetComponentsInChildren<Text>()[1];
        _zOffsetSlider = pr.GetComponentInChildren<Slider>();
        // offsetHUD
        var pr2 = DebugUIBuilder.instance.AddSlider("HUD Position", 3.0f, 5.0f, onHUDChange, false);
        _hudOffsetText = pr2.GetComponentsInChildren<Text>()[1];
        _hudOffsetSlider = pr2.GetComponentInChildren<Slider>();

        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Debug Tweakables
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        DebugUIBuilder.instance.AddLabel("Tweakable Parameters", 1);
        DebugUIBuilder.instance.AddDivider(1);
        // overlayToggle
        var p = DebugUIBuilder.instance.AddToggle("Show Overlay", onOverlayToggle, true, 1);
        _overlayToggle = p.GetComponentInChildren<Toggle>();
        // resetDelay
        var p1 = DebugUIBuilder.instance.AddSlider("Reset Delay", 0f, 5.0f, onResetDelay, false, 1);
        _resetDelayText = p1.GetComponentsInChildren<Text>()[1];
        _resetDelaySlider = p1.GetComponentInChildren<Slider>();
        // fielderSpeed
        var p2 = DebugUIBuilder.instance.AddSlider("Fielder Speed", 0.5f, 2.5f, onFielderSpeed, false, 1);
        _fielderSpeedText = p2.GetComponentsInChildren<Text>()[1];
        _fielderSpeedSlider = p2.GetComponentInChildren<Slider>();
        // ampMin
        var p3 = DebugUIBuilder.instance.AddSlider("Min Amplifier", 0.0f, 15.0f, onAmpMinChange, false, 1);
        _ampMinText = p3.GetComponentsInChildren<Text>()[1];
        _ampMinSlider = p3.GetComponentInChildren<Slider>();
        // ampMax
        var p4 = DebugUIBuilder.instance.AddSlider("Max Amplifier", 0.0f, 25.0f, onAmpMaxChange, false, 1);
        _ampMaxText = p4.GetComponentsInChildren<Text>()[1];
        _ampMaxSlider = p4.GetComponentInChildren<Slider>();
        // Bowling type
        var spw = DebugUIBuilder.instance.AddLabel("Bowling Type - ", 1);
        _swingTypeText = spw.GetComponent<Text>();
        // MinX
        var pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MinX, 0f, 10f, onVoid, false, 1);
        _MinXText = pr1.GetComponentsInChildren<Text>()[1];
        _MinXSlider = pr1.GetComponentInChildren<Slider>();
        _MinXSlider.onValueChanged.AddListener(delegate (float f) { MinX = f; updateTweakables(); });
        // MaxX
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MaxX, 0f, 10f, onVoid, false, 1);
        _MaxXText = pr1.GetComponentsInChildren<Text>()[1];
        _MaxXSlider = pr1.GetComponentInChildren<Slider>();
        _MaxXSlider.onValueChanged.AddListener(delegate (float f) { MaxX = f; updateTweakables(); });
        // MinY
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MinY, -3f, 2f, onVoid, false, 1);
        _MinYText = pr1.GetComponentsInChildren<Text>()[1];
        _MinYSlider = pr1.GetComponentInChildren<Slider>();
        _MinYSlider.onValueChanged.AddListener(delegate (float f) { MinY = f; updateTweakables(); });
        // MaxY
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MaxY, -3f, 2f, onVoid, false, 1);
        _MaxYText = pr1.GetComponentsInChildren<Text>()[1];
        _MaxYSlider = pr1.GetComponentInChildren<Slider>();
        _MaxYSlider.onValueChanged.AddListener(delegate (float f) { MaxY = f; updateTweakables(); });
        // MinZ
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MinZ, -0.75f, 0.75f, onVoid, false, 1);
        _MinZText = pr1.GetComponentsInChildren<Text>()[1];
        _MinZSlider = pr1.GetComponentInChildren<Slider>();
        _MinZSlider.onValueChanged.AddListener(delegate (float f) { MinZ = f; updateTweakables(); });
        // MaxZ
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MaxZ, -0.75f, 0.75f, onVoid, false, 1);
        _MaxZText = pr1.GetComponentsInChildren<Text>()[1];
        _MaxZSlider = pr1.GetComponentInChildren<Slider>();
        _MaxZSlider.onValueChanged.AddListener(delegate (float f) { MaxZ = f; updateTweakables(); });
        // MinSwing
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MinSwing, 0f, 1f, onVoid, false, 1);
        _MinSwingText = pr1.GetComponentsInChildren<Text>()[1];
        _MinSwingSlider = pr1.GetComponentInChildren<Slider>();
        _MinSwingSlider.onValueChanged.AddListener(delegate (float f) { MinSwing = f; updateTweakables(); });
        // MaxSwing
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MaxSwing, 0f, 1f, onVoid, false, 1);
        _MaxSwingText = pr1.GetComponentsInChildren<Text>()[1];
        _MaxSwingSlider = pr1.GetComponentInChildren<Slider>();
        _MaxSwingSlider.onValueChanged.AddListener(delegate (float f) { MaxSwing = f; updateTweakables(); });
        // MinPitchTurn
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MinPitchTurn, 0f, 1f, onVoid, false, 1);
        _MinPitchTurnText = pr1.GetComponentsInChildren<Text>()[1];
        _MinPitchTurnSlider = pr1.GetComponentInChildren<Slider>();
        _MinPitchTurnSlider.onValueChanged.AddListener(delegate (float f) { MinPitchTurn = f; updateTweakables(); });
        // MaxPitchTurn
        pr1 = DebugUIBuilder.instance.AddSlider(Constants.CT_MaxPitchTurn, 0f, 1f, onVoid, false, 1);
        _MaxPitchTurnText = pr1.GetComponentsInChildren<Text>()[1];
        _MaxPitchTurnSlider = pr1.GetComponentInChildren<Slider>();
        _MaxPitchTurnSlider.onValueChanged.AddListener(delegate (float f) { MaxPitchTurn = f; updateTweakables(); });
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
            _easyToggle.isOn = true;
        else if (difficulty == eDifficulty.Medium && !_mediumToggle.isOn)
            _mediumToggle.isOn = true;
        else if (difficulty == eDifficulty.Hard && !_hardToggle.isOn)
            _hardToggle.isOn = true;

        if (changed)
            PlayerPrefs.Save();
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

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (stadiumMode == eStadiumMode.Night && !_nightToggle.isOn)
            _nightToggle.isOn = true;
        else if (stadiumMode == eStadiumMode.Day && !_dayToggle.isOn)
            _dayToggle.isOn = true;

        if(changed)
            PlayerPrefs.Save();
    }
    public void onNightMode(Toggle t)
    {
        if (!t.isOn)
            return;
        if (stadiumMode != eStadiumMode.Night)
        {
            stadiumMode = eStadiumMode.Night;
            updateStadiumMode();
        }
    }
    public void onDayMode(Toggle t)
    {
        if (!t.isOn)
            return;
        if (stadiumMode != eStadiumMode.Day)
        {
            stadiumMode = eStadiumMode.Day;
            updateStadiumMode();
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Z Offset
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateZOffset(bool savePrefs = false)
    {
        if (_zOffset != zOffset)
        {
            _zOffset = zOffset;
            _zOffsetText.text = zOffset.ToString();
            _zOffsetSlider.value = zOffset;

            DebugUIBuilder.instance.menuOffset.z = zOffset;
            DebugUIBuilder.instance.UpdatePosition();

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
    /// HUD Offset
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateHUDOffset(bool savePrefs = false)
    {
        if (_hudOffset != hudOffset)
        {
            _hudOffset = hudOffset;
            _hudOffsetText.text = hudOffset.ToString();
            _hudOffsetSlider.value = hudOffset;

            RectTransform trans = hudParent.GetComponent<RectTransform>();
            Vector3 pos = trans.anchoredPosition3D;
            pos.z = hudOffset;
            trans.anchoredPosition3D = pos;

            PlayerPrefs.SetFloat(Constants.PP_HudOffset, hudOffset);
            if (savePrefs)
                PlayerPrefs.Save();
        }
    }
    public void onHUDChange(float val)
    {
        hudOffset = val;
        updateHUDOffset();
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

        if(bowlingProfileManager != null)
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
            updateHUDOffset(true);

            updateTweakables(true);
        }

        if (debugText != null)
        {
            debugText.text = "GameState: " + gameState.ToString();
            if (currentBowlingConfig != null)
                debugText.text += "\n" + currentBowlingConfig.ToString();
        }

        if(SignalMaterial != null)
        {
            if (gameState == eGameState.InGame_Ready)
                SignalMaterial.color = Color.green;
            else
                SignalMaterial.color = Color.red;
        }

        // Temporary stuff
        if (GetButton(OVRInput.Button.Three))
        {
            gameState = eGameState.InGame_ResetToReady;
        }

        // if debug menu is not active!
        if (!DebugUIBuilder.instance.isActiveAndEnabled)
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
                        if (GetButton(OVRInput.Button.One))
                        {
                            currentFielderName = "";

                            // Reset stumps
                            theStumpsScript.Reset();

                            StopTheBall();

                            // Switch to the process of selecting delivery type, stride & other params before delivery loop
                            gameState = eGameState.InGame_SelectDelivery;
                        }
                    }
                    break;
                case eGameState.InGame_SelectDelivery:
                    {
                        // Get current bowler from Score keeper
                        currentBowlingConfig = bowlingProfileManager.GetProfile(theHUD.CurrentBowler.Type).GetRandomDelivery();

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
                        theBallRigidBody.velocity = Vector3.zero;
                        Vector3 torque = new Vector3(currentBowlingConfig.torqueX, 0f, 0f);
                        //Vector3 speed = new Vector3(currentBowlingConfig.speedX, currentBowlingConfig.speedY, currentBowlingConfig.speedZ);
                        float myY = GetYVel(theBall.transform.position * 3.28f, currentBowlingConfig.speedX / theBallRigidBody.mass * 3.28f, currentBowlingConfig.length * 3.28f);
                        Vector3 speed = new Vector3(currentBowlingConfig.speedX, myY * theBallRigidBody.mass / 3.28f, currentBowlingConfig.speedZ);
                        // enable physics
                        theBallRigidBody.isKinematic = false;
                        //Set collision type to continuous dynamic
                        theBallRigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        // Set interpolation mode to interpolate
                        theBallRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
                        // Add the required force & rotation
                        theBallRigidBody.AddTorque(torque, ForceMode.Impulse);
                        theBallRigidBody.AddForce(speed, ForceMode.Impulse);
                        //theBallRigidBody.velocity = speed / theBallRigidBody.mass;
                        
                        // save it
                        //theBallScript.lastVelocity = speed;   (commented out because the lastVelocity for the bat collision equations should be updated after the ball hits the pitch.
                        // mark as fresh delivery!
                        theBallScript.fresh = true;
                        theBallScript.bounced = false;
                        theBallScript.wide = false;

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
        if (GetButton(OVRInput.Button.Two))
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
            DebugUIBuilder.instance.Show();
            theBat.SetActive(false);
        }
        else
        {
            // Save changes from settings
            PlayerPrefs.Save();
            DebugUIBuilder.instance.Hide();
            theBat.SetActive(true);
        }
    }

    private void StopTheBall()
    {
        // pause the particles
        theBallScript.myParticles.Stop();
        theBallScript.myParticles.Clear();
        // disable physics
        theBallRigidBody.isKinematic = true;
        // reset ball position to inside machine
        theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);
        // make the ball static
        theBallRigidBody.velocity = Vector3.zero;
    }

    public IEnumerator WaitAndSetGameState(float delay, eGameState state)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log("Setting GameState from: " + gameState.ToString() + " to: " + state.ToString());
        gameState = state;
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Helper functions section
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private KeyCode GetKeyCodeForButton(OVRInput.Button button)
    {
        switch(button)
        {
            case OVRInput.Button.One:
                return KeyCode.RightShift;  // A -> Right Shift
            case OVRInput.Button.Two:
                return KeyCode.Slash;       // B -> /
            case OVRInput.Button.Three:
                return KeyCode.LeftShift;   // X -> Left Shift
            case OVRInput.Button.Four:
                return KeyCode.Z;           // Y -> Z
        }

        return KeyCode.None;
    }

    public bool GetButton(OVRInput.Button button, bool justDown = true)
    {
        if(justDown)
            return (Application.isEditor && Input.GetKeyDown(GetKeyCodeForButton(button)) ||
                !Application.isEditor && OVRInput.GetDown(button));
        else
            return (Application.isEditor && Input.GetKey(GetKeyCodeForButton(button)) ||
                !Application.isEditor && OVRInput.Get(button));
    }

    private void HandleLog(string message, string stackTrace, LogType logType)
    {
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
