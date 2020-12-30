using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum eBattingStyle
{
    RightHanded,
    LeftHanded
}

public enum eDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum eStadiumMode
{
    Night,
    Day
}

public enum eSwingType
{
    None,
    InSwing,
    OutSwing,
    Random
}

public class Main : MonoBehaviour
{
    public const string PP_difficulty = "difficulty";
    public const string PP_battingStyle = "style";
    public const string PP_stadiumMode = "mode";
    public const string PP_ampMin = "amp_min";
    public const string PP_ampMax = "amp_max";
    public const string PP_pitchTurn = "pitch_turn";
    public const string PP_fX = "fX";
    public const string PP_fY = "fY";
    public const string PP_fZ = "fZ";
    public const string PP_minSwing = "min_swing";
    public const string PP_maxSwing = "max_swing";
    public const string PP_swingType = "swing";
    public const string PP_zOffset = "menu_offset";

    [SerializeField]
    protected GameObject theBall;
    [SerializeField]
    protected GameObject theBat;
    [SerializeField]
    protected GameObject theStumps;
    [SerializeField]
    protected OVRPlayerController theController;
    [SerializeField]
    protected Material NightMaterial;
    [SerializeField]
    protected Material DayMaterial;
    [SerializeField]
    protected GameObject theNightLights;
    [SerializeField]
    protected GameObject theDayLights;
    [SerializeField]
    protected bool dbgToggle;
    [SerializeField]
    protected Text debugText;


    [Header("Settings")]
    [SerializeField]
    protected eDifficulty difficulty;
    [SerializeField]
    protected eBattingStyle battingStyle;
    [SerializeField]
    protected eStadiumMode stadiumMode;
    [SerializeField]
    [Range(3, 10)]
    protected float zOffset;
    [SerializeField]
    [Range(0, 15)]
    protected float ampMin;
    [SerializeField]
    [Range(0, 25)]
    protected float ampMax;
    [SerializeField]
    [Range(0, 1)]
    protected float pitchTurn;
    [SerializeField]
    [Range(0, 10)]
    protected float fX;
    [SerializeField]
    [Range(-2, 1)]
    protected float fY;
    [SerializeField]
    [Range(-0.75f, 0.75f)]
    protected float fZ;
    [SerializeField]
    [Range(0, 1)]
    protected float minSwing;
    [SerializeField]
    [Range(0, 1)]
    protected float maxSwing;
    private float _zOffset, _ampMin, _ampMax, _pitchTurn, _fX, _fY, _fZ, _minSwing, _maxSwing;
    [SerializeField]
    protected eSwingType swingType;
    private eSwingType _swingType;

    private bool initialized;

    private const int HISTORY_MAX_LINES = 9999;
    private const int HISTORY_TEXT_MAX_LENGTH = 16000;
    private List<string> history;
    private int historyTextLength;
    private int maxViewableLines;

    private Bat theBatScript;
    private Ball theBallScript;
    private Stumps theStumpsScript;
    private Rigidbody theBallRigidBody;

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

        initialized = false;

        this.history = new List<string>(HISTORY_MAX_LINES);
        this.historyTextLength = 0;
        this.maxViewableLines = 100;

        Application.logMessageReceived += this.HandleLog;
    }

    public void OnDestroy()
    {
        Application.logMessageReceived -= this.HandleLog;
    }

    // Start is called before the first frame update
    void Start()
    {
        _ampMin = _ampMax = _pitchTurn = _fX = _fY = _fZ = _zOffset = 0f;
        _swingType = eSwingType.None;

        dbgToggle = false;
        SetupDebugMenu();

        difficulty = (eDifficulty)PlayerPrefs.GetInt(PP_difficulty, 0);
        battingStyle = (eBattingStyle)PlayerPrefs.GetInt(PP_battingStyle, 0);
        stadiumMode = (eStadiumMode)PlayerPrefs.GetInt(PP_stadiumMode, 0);
        zOffset = PlayerPrefs.GetFloat(PP_zOffset, 3f);
        ampMin = PlayerPrefs.GetFloat(PP_ampMin, 10.0f);
        ampMax = PlayerPrefs.GetFloat(PP_ampMax, 15.0f);
        pitchTurn = PlayerPrefs.GetFloat(PP_pitchTurn, 0.2f);
        fX = PlayerPrefs.GetFloat(PP_fX, 7.0f);
        fY = PlayerPrefs.GetFloat(PP_fY, -1.0f);
        fZ = PlayerPrefs.GetFloat(PP_fZ, 0.0f);
        minSwing = PlayerPrefs.GetFloat(PP_minSwing, 0.1f);
        maxSwing = PlayerPrefs.GetFloat(PP_maxSwing, 0.9f);
        swingType = (eSwingType)PlayerPrefs.GetInt(PP_swingType, 3);
        // Clamp zOffset
        if (zOffset < 3f || zOffset > 15f)
            zOffset = 3f;

        updateDifficulty(true);
        updateBattingStyle(true);
        updateStadiumMode(true);
        updateSwingParams(true);
        updateZOffset();
        updateSpeedParams();

        initialized = true;
    }

    private Text fXText;
    private Text fYText;
    private Text fZText;
    private Text minSwingText;
    private Text maxSwingText;
    private Text ampMinText;
    private Text ampMaxText;
    private Text pitchTurnText;
    private Text consoleText;
    private Text offsetZText;
    private Slider fXSlider;
    private Slider fYSlider;
    private Slider fZSlider;
    private Slider ampMinSlider;
    private Slider ampMaxSlider;
    private Slider pitchTurnSlider;
    private Slider offsetZSlider;
    private Slider minSwingSlider;
    private Slider maxSwingSlider;
    private Toggle inSwingToggle;
    private Toggle outSwingToggle;
    private Toggle noSwingToggle;
    private Toggle randSwingToggle;
    private Toggle lBatToggle;
    private Toggle rBatToggle;
    private Toggle easyToggle;
    private Toggle mediumToggle;
    private Toggle hardToggle;
    private Toggle nightToggle;
    private Toggle dayToggle;

    public void SetupDebugMenu()
    {
        DebugUIBuilder.instance.AddLabel("Settings");
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Settings Menu
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Batting Style");
        var radio1 = DebugUIBuilder.instance.AddRadio("Left Handed", "batting", onRadioLeftHanded);
        lBatToggle = radio1.GetComponentInChildren<Toggle>();
        var radio2 = DebugUIBuilder.instance.AddRadio("Right Handed", "batting", onRadioRightHanded);
        rBatToggle = radio2.GetComponentInChildren<Toggle>();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Difficulty");
        var radio3 = DebugUIBuilder.instance.AddRadio("Easy", "difficulty", onRadioEasy);
        easyToggle = radio3.GetComponentInChildren<Toggle>();
        var radio4 = DebugUIBuilder.instance.AddRadio("Medium", "difficulty", onRadioMedium);
        mediumToggle = radio4.GetComponentInChildren<Toggle>();
        var radio5 = DebugUIBuilder.instance.AddRadio("Hard", "difficulty", onRadioHard);
        hardToggle = radio5.GetComponentInChildren<Toggle>();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Stadium");
        var radio6 = DebugUIBuilder.instance.AddRadio("Night", "stadium", onNightMode);
        nightToggle = radio6.GetComponentInChildren<Toggle>();
        var radio7 = DebugUIBuilder.instance.AddRadio("Day", "stadium", onDayMode);
        dayToggle = radio7.GetComponentInChildren<Toggle>();

        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Debug Console & z-offset
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        DebugUIBuilder.instance.AddLabel("Console Log", 2);
        // Debug log output
        DebugUIBuilder.instance.AddDivider(2);
        var prefab = DebugUIBuilder.instance.AddBigLabel("", 2);
        consoleText = prefab.GetComponent<Text>();
        // offsetZ
        var pr = DebugUIBuilder.instance.AddSlider("Menu Position", 3.0f, 10.0f, onZChange, true, 2);
        offsetZText = pr.GetComponentsInChildren<Text>()[1];
        offsetZSlider = pr.GetComponentInChildren<Slider>();

        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// Debug Tweakables
        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        DebugUIBuilder.instance.AddLabel("Ball Physics", 1);
        DebugUIBuilder.instance.AddDivider(1);
        // fX
        var prefab1 = DebugUIBuilder.instance.AddSlider("Speed X", 0.0f, 10.0f, onfXChange, false, 1);
        fXText = prefab1.GetComponentsInChildren<Text>()[1];
        fXSlider = prefab1.GetComponentInChildren<Slider>();
        // fY
        var prefab2 = DebugUIBuilder.instance.AddSlider("Speed Y", -2.0f, 1.0f, onfYChange, false, 1);
        fYText = prefab2.GetComponentsInChildren<Text>()[1];
        fYSlider = prefab2.GetComponentInChildren<Slider>();
        // fZ
        var prefab3 = DebugUIBuilder.instance.AddSlider("Speed Z", -0.75f, 0.75f, onfZChange, false, 1);
        fZText = prefab3.GetComponentsInChildren<Text>()[1];
        fZSlider = prefab3.GetComponentInChildren<Slider>();
        // minSwing
        var prefb1 = DebugUIBuilder.instance.AddSlider("Min Swing", 0.0f, 1.0f, onMinSwingChange, false, 1);
        minSwingText = prefb1.GetComponentsInChildren<Text>()[1];
        minSwingSlider = prefb1.GetComponentInChildren<Slider>();
        // maxSwing
        var prefb2 = DebugUIBuilder.instance.AddSlider("Max Swing", 0.0f, 1.0f, onMaxSwingChange, false, 1);
        maxSwingText = prefb2.GetComponentsInChildren<Text>()[1];
        maxSwingSlider = prefb2.GetComponentInChildren<Slider>();
        // in-swing
        var radio11 = DebugUIBuilder.instance.AddRadio("In Swing", "swing", onInSwing, 1);
        inSwingToggle = radio11.GetComponentInChildren<Toggle>();
        // out-swing
        var radio12 = DebugUIBuilder.instance.AddRadio("Out Swing", "swing", onOutSwing, 1);
        outSwingToggle = radio12.GetComponentInChildren<Toggle>();
        // no-swing
        var radio13 = DebugUIBuilder.instance.AddRadio("No Swing", "swing", onNoSwing, 1);
        noSwingToggle = radio13.GetComponentInChildren<Toggle>();
        // random-swing
        var radio14 = DebugUIBuilder.instance.AddRadio("Random", "swing", onRandSwing, 1);
        randSwingToggle = radio14.GetComponentInChildren<Toggle>();
        // ampMin
        var prefab4 = DebugUIBuilder.instance.AddSlider("Min Amplifier", 0.0f, 15.0f, onAmpMinChange, false, 1);
        ampMinText = prefab4.GetComponentsInChildren<Text>()[1];
        ampMinSlider = prefab4.GetComponentInChildren<Slider>();
        // ampMax
        var prefab5 = DebugUIBuilder.instance.AddSlider("Max Amplifier", 0.0f, 25.0f, onAmpMaxChange, false, 1);
        ampMaxText = prefab5.GetComponentsInChildren<Text>()[1];
        ampMaxSlider = prefab5.GetComponentInChildren<Slider>();
        // tZ
        var prefab6 = DebugUIBuilder.instance.AddSlider("Pitch Turn", 0.0f, 1.0f, onPitchTurnChange, false, 1);
        pitchTurnText = prefab6.GetComponentsInChildren<Text>()[1];
        pitchTurnSlider = prefab6.GetComponentInChildren<Slider>();
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Difficulty settings
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateDifficulty(bool updateUI = false)
    {
        bool changed = false;
        // TODO: Take action for appropriate difficulty

        PlayerPrefs.SetInt(PP_difficulty, (int)difficulty);

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (difficulty == eDifficulty.Easy && !easyToggle.isOn)
            easyToggle.isOn = true;
        else if (difficulty == eDifficulty.Medium && !mediumToggle.isOn)
            mediumToggle.isOn = true;
        else if (difficulty == eDifficulty.Hard && !hardToggle.isOn)
            hardToggle.isOn = true;

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
            PlayerPrefs.SetInt(PP_battingStyle, (int)battingStyle);
        }
        if (theBatScript.rightHandParent != null && battingStyle == eBattingStyle.RightHanded)
        {
            changed = true;
            theBatScript.attachParent = theBatScript.rightHandParent;
            theBatScript.CheckAndGrab();
            PlayerPrefs.SetInt(PP_battingStyle, (int)battingStyle);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (battingStyle == eBattingStyle.RightHanded && !rBatToggle.isOn)
            rBatToggle.isOn = true;
        else if (battingStyle == eBattingStyle.LeftHanded && !lBatToggle.isOn)
            lBatToggle.isOn = true;

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
            PlayerPrefs.SetInt(PP_stadiumMode, (int)stadiumMode);
        }
        else if(stadiumMode == eStadiumMode.Day && !theDayLights.activeSelf)
        {
            changed = true;
            RenderSettings.skybox = DayMaterial;
            theNightLights.SetActive(false);
            theDayLights.SetActive(true);
            PlayerPrefs.SetInt(PP_stadiumMode, (int)stadiumMode);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (stadiumMode == eStadiumMode.Night && !nightToggle.isOn)
            nightToggle.isOn = true;
        else if (stadiumMode == eStadiumMode.Day && !dayToggle.isOn)
            dayToggle.isOn = true;

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
    /// Debug Tweakables
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void updateSpeedParams(bool savePrefs = false)
    {
        if(fX != _fX || fY != _fY || fZ != _fZ ||
            ampMin != _ampMin || ampMax != _ampMax || pitchTurn != _pitchTurn)
        {
            _fX = fX;
            _fY = fY;
            _fZ = fZ;
            _ampMin = ampMin;
            _ampMax = ampMax;
            _pitchTurn = pitchTurn;
            if(fXText != null)
                fXText.text = fX.ToString();
            if (fYText != null)
                fYText.text = fY.ToString();
            if (fZText != null)
                fZText.text = fZ.ToString();
            if (ampMinText != null)
            {
                ampMinText.text = ampMin.ToString();
                theBatScript.ampMin = ampMin;
            }
            if (ampMaxText != null)
            {
                ampMaxText.text = ampMax.ToString();
                theBatScript.ampMax = ampMax;
            }
            if (pitchTurnText != null)
            {
                pitchTurnText.text = pitchTurn.ToString();
                theBallScript.pitchTurn = pitchTurn;
            }

            if (fXSlider != null)
                fXSlider.value = fX;
            if (fYSlider != null)
                fYSlider.value = fY;
            if (fZSlider != null)
                fZSlider.value = fZ;
            if (ampMinSlider != null)
                ampMinSlider.value = ampMin;
            if (ampMaxSlider != null)
                ampMaxSlider.value = ampMax;
            if (pitchTurnSlider != null)
                pitchTurnSlider.value = pitchTurn;

            PlayerPrefs.SetFloat(PP_fX, fX);
            PlayerPrefs.SetFloat(PP_fY, fY);
            PlayerPrefs.SetFloat(PP_fZ, fZ);
            PlayerPrefs.SetFloat(PP_ampMin, ampMin);
            PlayerPrefs.SetFloat(PP_ampMax, ampMax);
            PlayerPrefs.SetFloat(PP_pitchTurn, pitchTurn);
            if (savePrefs)
                PlayerPrefs.Save();
        }
    }
    private void updateZOffset(bool savePrefs = false)
    {
        if(_zOffset != zOffset)
        {
            _zOffset = zOffset;
            offsetZText.text = zOffset.ToString();
            offsetZSlider.value = zOffset;

            DebugUIBuilder.instance.menuOffset.z = zOffset;
            DebugUIBuilder.instance.UpdatePosition();

            PlayerPrefs.SetFloat(PP_zOffset, zOffset);
            if (savePrefs)
                PlayerPrefs.Save();
        }
    }
    public void updateSwingParams(bool updateUI = false)
    {
        bool changed = false;
        if(_minSwing != minSwing || _maxSwing != maxSwing || _swingType != swingType)
        {
            changed = true;
            _minSwing = theBallScript.minSwing = minSwing;
            _maxSwing = theBallScript.maxSwing = maxSwing;
            _swingType = theBallScript.swingType = swingType;

            if(minSwingSlider != null)
                minSwingSlider.value = minSwing;
            if (maxSwingSlider != null)
                maxSwingSlider.value = maxSwing;

            if (minSwingText != null)
                minSwingText.text = minSwing.ToString();
            if (maxSwingText != null)
                maxSwingText.text = maxSwing.ToString();

            PlayerPrefs.SetFloat(PP_minSwing, minSwing);
            PlayerPrefs.SetFloat(PP_maxSwing, maxSwing);
            PlayerPrefs.SetInt(PP_swingType, (int)swingType);
        }

        // If we are not being called from Update(), just return, as player prefs are saved on UI Hide()
        if (!updateUI)
            return;

        // If we are being called from Update(), we need to update UI & also call PlayerPrefs.Save()
        if (swingType == eSwingType.None && noSwingToggle!= null && !noSwingToggle.isOn)
            noSwingToggle.isOn = true;
        else if (swingType == eSwingType.InSwing && inSwingToggle != null && !inSwingToggle.isOn)
            inSwingToggle.isOn = true;
        else if (swingType == eSwingType.OutSwing && outSwingToggle != null && !outSwingToggle.isOn)
            outSwingToggle.isOn = true;
        else if (swingType == eSwingType.Random && randSwingToggle != null && !randSwingToggle.isOn)
            randSwingToggle.isOn = true;

        if (changed)
            PlayerPrefs.Save();
    }
    public void onZChange(float val)
    {
        zOffset = val;
        updateZOffset();
    }
    public void onfXChange(float val)
    {
        fX = val;
        updateSpeedParams();
    }
    public void onfYChange(float val)
    {
        fY = val;
        updateSpeedParams();
    }
    public void onfZChange(float val)
    {
        fZ = val;
        updateSpeedParams();
    }
    public void onAmpMinChange(float val)
    {
        ampMin = val;
        updateSpeedParams();
    }
    public void onAmpMaxChange(float val)
    {
        ampMax = val;
        updateSpeedParams();
    }
    public void onPitchTurnChange(float val)
    {
        pitchTurn = val;
        updateSpeedParams();
    }
    public void onMinSwingChange(float val)
    {
        minSwing = val;
        updateSwingParams();
    }
    public void onMaxSwingChange(float val)
    {
        maxSwing = val;
        updateSwingParams();
    }
    public void onInSwing(Toggle t)
    {
        if (!t.isOn)
            return;
        if (swingType != eSwingType.InSwing)
        {
            swingType = eSwingType.InSwing;
            updateSwingParams();
        }
    }
    public void onOutSwing(Toggle t)
    {
        if (!t.isOn)
            return;
        if (swingType != eSwingType.OutSwing)
        {
            swingType = eSwingType.OutSwing;
            updateSwingParams();
        }
    }
    public void onNoSwing(Toggle t)
    {
        if (!t.isOn)
            return;
        if (swingType != eSwingType.None)
        {
            swingType = eSwingType.None;
            updateSwingParams();
        }
    }
    public void onRandSwing(Toggle t)
    {
        if (!t.isOn)
            return;
        if (swingType != eSwingType.Random)
        {
            swingType = eSwingType.Random;
            updateSwingParams();
        }
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// Update is called once per frame
    /////////////////////////////////////////////////////////////////////////////////////////////////////////
    void Update()
    {
        if (!initialized)
            return;

        // Update values from inspector, when in editor, otherwise, toggle UI will drive this
        if (Application.isEditor)
        {
            updateBattingStyle(true);
            updateDifficulty(true);
            updateStadiumMode(true);
            updateSwingParams(true);
            updateSpeedParams(true);
            updateZOffset(true);
        }

        if (debugText != null)
            debugText.text = "Ball last velocity: " + theBallScript.lastVelocity.ToString();

        // if debug menu is not active!
        if (!DebugUIBuilder.instance.isActiveAndEnabled)
        {
            if (GetButton(OVRInput.Button.One))
            {
                // Reset stumps
                theStumpsScript.Reset();

                // disable physics
                theBallRigidBody.isKinematic = true;
                // reset ball position to inside machine
                theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);
                theBallScript.fresh = true;

                // make the ball static
                theBallRigidBody.velocity = Vector3.zero;
                // enable physics
                theBallRigidBody.isKinematic = false;
                // Add the required force & rotation
                theBallRigidBody.AddTorque(new Vector3(50f, 50f, 50f), ForceMode.Impulse);
                theBallRigidBody.AddForce(new Vector3(fX, fY, fZ), ForceMode.Impulse);
                // save it
                theBallScript.lastVelocity = new Vector3(fX, fY, fZ);
            }
        }

        if (GetButton(OVRInput.Button.Two))
        {
            dbgToggle = !dbgToggle;

            if (dbgToggle)
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
        {
            color = "white";
        }
        else if (logType == LogType.Warning)
        {
            color = "yellow";
        }
        else
        {
            color = "red";
        }

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

        // If we are not open, no extra processing
        if (!dbgToggle)
            return;

        onConsoleTextChange();
    }

    public void onConsoleTextChange()
    {
        string viewText = string.Empty;

        for (int i = 0, imax = history.Count; i < maxViewableLines && i < imax; i++)
        {
            viewText += history[i];

            if (i < maxViewableLines - 1 && i < imax - 1)
            {
                viewText += "\n";
            }
        }

        consoleText.text = viewText;
    }

}
