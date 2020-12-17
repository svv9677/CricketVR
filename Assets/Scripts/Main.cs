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

public class Main : MonoBehaviour
{
    [SerializeField]
    protected GameObject theBall;
    [SerializeField]
    protected GameObject theBat;
    [SerializeField]
    protected OVRPlayerController theController;
    [SerializeField]
    protected bool dbgToggle;
    [SerializeField]
    protected eDifficulty difficulty;
    [SerializeField]
    protected eBattingStyle battingStyle;
    [SerializeField]
    protected eStadiumMode stadiumMode;
    [SerializeField]
    protected Material NightMaterial;
    [SerializeField]
    protected Material DayMaterial;
    [SerializeField]
    protected GameObject theNightLights;
    [SerializeField]
    protected GameObject theDayLights;

    private bool initialized;
    private float tX, tY, tZ, fX, fY, fZ, zOffset;

    private const int HISTORY_MAX_LINES = 9999;
    private const int HISTORY_TEXT_MAX_LENGTH = 16000;
    private List<string> history;
    private int historyTextLength;
    private int maxViewableLines;

    private Bat theBatScript;
    private Ball theBallScript;
    private Rigidbody theBallRigidBody;

    public eDifficulty Difficulty
    {
        get { return difficulty; }
        set { difficulty = value; updateDifficulty(); }
    }
    public eBattingStyle BattingStyle
    {
        get { return battingStyle; }
        set { battingStyle = value; updateBattingStyle(); }
    }
    public eStadiumMode StadiumMode
    {
        get { return stadiumMode; }
        set { stadiumMode = value; updateStadiumMode(); }
    }

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
        tX = -45.0f;
        tY = 25.0f;
        tZ = -25.0f;

        fX = 6.0f;
        fY = -1.0f;
        fZ = 0.0f;

        dbgToggle = false;
        SetupDebugMenu();

        int diff = PlayerPrefs.GetInt("difficulty", 0);
        Difficulty = (eDifficulty)diff;

        int style = PlayerPrefs.GetInt("style", 0);
        BattingStyle = (eBattingStyle)style;

        int mode = PlayerPrefs.GetInt("mode", 0);
        StadiumMode = (eStadiumMode)mode;

        //zOffset = PlayerPrefs.GetFloat("menu_offset", 5f);
        zOffset = 3f;
        onZChange(zOffset);

        initialized = true;
    }

    private Text fXText;
    private Text fYText;
    private Text fZText;
    private Text tXText;
    private Text tYText;
    private Text tZText;
    private Text consoleText;
    private Text offsetZText;
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
        // Settings menu
        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Batting Style");
        var radio1 = DebugUIBuilder.instance.AddRadio("Left Handed", "batting", onRadioLeftHanded);
        lBatToggle = radio1.GetComponentInChildren<Toggle>();
        var radio2 = DebugUIBuilder.instance.AddRadio("Right Handed", "batting", onRadioRightHanded);
        rBatToggle = radio2.GetComponentInChildren<Toggle>();
        updateBattingStyle();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Difficulty");
        var radio3 = DebugUIBuilder.instance.AddRadio("Easy", "difficulty", onRadioEasy);
        easyToggle = radio3.GetComponentInChildren<Toggle>();
        var radio4 = DebugUIBuilder.instance.AddRadio("Medium", "difficulty", onRadioMedium);
        mediumToggle = radio4.GetComponentInChildren<Toggle>();
        var radio5 = DebugUIBuilder.instance.AddRadio("Hard", "difficulty", onRadioHard);
        hardToggle = radio5.GetComponentInChildren<Toggle>();
        updateDifficulty();

        DebugUIBuilder.instance.AddDivider();
        DebugUIBuilder.instance.AddLabel("Stadium");
        var radio6 = DebugUIBuilder.instance.AddRadio("Night", "stadium", onNightMode);
        nightToggle = radio6.GetComponentInChildren<Toggle>();
        var radio7 = DebugUIBuilder.instance.AddRadio("Day", "stadium", onDayMode);
        dayToggle = radio7.GetComponentInChildren<Toggle>();
        updateStadiumMode();

        DebugUIBuilder.instance.AddLabel("Console Log", 2);
        // Debug log output
        DebugUIBuilder.instance.AddDivider(2);
        var prefab = DebugUIBuilder.instance.AddBigLabel("", 2);
        consoleText = prefab.GetComponent<Text>();
        onConsoleTextChange();
        // offsetZ
        var pr = DebugUIBuilder.instance.AddSlider("Menu Position", 3.0f, 15.0f, onZChange, false, 2);
        offsetZText = pr.GetComponentsInChildren<Text>()[1];
        onZChange(zOffset);

        DebugUIBuilder.instance.AddLabel("Ball Physics", 1);
        DebugUIBuilder.instance.AddDivider(1);
        // fX
        var prefab1 = DebugUIBuilder.instance.AddSlider("fX", 0.0f, 50.0f, onfXChange, false, 1);
        fXText = prefab1.GetComponentsInChildren<Text>()[1];
        onfXChange(fX);
        // fY
        var prefab2 = DebugUIBuilder.instance.AddSlider("fY", -1.1f, 1.1f, onfYChange, false, 1);
        fYText = prefab2.GetComponentsInChildren<Text>()[1];
        onfYChange(fY);
        // fZ
        var prefab3 = DebugUIBuilder.instance.AddSlider("fZ", -1.1f, 1.1f, onfZChange, false, 1);
        fZText = prefab3.GetComponentsInChildren<Text>()[1];
        onfZChange(fZ);
        // tX
        var prefab4 = DebugUIBuilder.instance.AddSlider("tX", -50.0f, 50.0f, ontXChange, false, 1);
        tXText = prefab4.GetComponentsInChildren<Text>()[1];
        ontXChange(tX);
        // tY
        var prefab5 = DebugUIBuilder.instance.AddSlider("tY", -50.0f, 50.0f, ontYChange, false, 1);
        tYText = prefab5.GetComponentsInChildren<Text>()[1];
        ontYChange(tY);
        // tZ
        var prefab6 = DebugUIBuilder.instance.AddSlider("tZ", -50.0f, 50.0f, ontZChange, false, 1);
        tZText = prefab6.GetComponentsInChildren<Text>()[1];
        ontZChange(tZ);
    }

    private void updateDifficulty()
    {
        if (Difficulty == eDifficulty.Easy)
            easyToggle.isOn = true;
        else if (Difficulty == eDifficulty.Medium)
            mediumToggle.isOn = true;
        else if (Difficulty == eDifficulty.Hard)
            hardToggle.isOn = true;

        PlayerPrefs.SetInt("difficulty", (int)difficulty);
    }
    public void onRadioEasy(Toggle t)
    {
        if (!t.isOn)
            return;
        if (Difficulty != eDifficulty.Easy)
            Difficulty = eDifficulty.Easy;
    }
    public void onRadioMedium(Toggle t)
    {
        if (!t.isOn)
            return;
        if (Difficulty != eDifficulty.Medium)
            Difficulty = eDifficulty.Medium;
    }
    public void onRadioHard(Toggle t)
    {
        if (!t.isOn)
            return;
        if (Difficulty != eDifficulty.Hard)
            Difficulty = eDifficulty.Hard;
    }

    private void updateBattingStyle()
    {
        if (theBatScript.leftHandParent != null && BattingStyle == eBattingStyle.LeftHanded)
            theBatScript.attachParent = theBatScript.leftHandParent;
        if (theBatScript.rightHandParent != null && BattingStyle == eBattingStyle.RightHanded)
            theBatScript.attachParent = theBatScript.rightHandParent;

        theBatScript.CheckAndGrab();

        if (BattingStyle == eBattingStyle.RightHanded)
            rBatToggle.isOn = true;
        else
            lBatToggle.isOn = true;

        PlayerPrefs.SetInt("style", (int)battingStyle);
    }
    public void onRadioRightHanded(Toggle t)
    {
        if (!t.isOn)
            return;
        if (BattingStyle != eBattingStyle.RightHanded)
            BattingStyle = eBattingStyle.RightHanded;
    }
    public void onRadioLeftHanded(Toggle t)
    {
        if (!t.isOn)
            return;
        if (BattingStyle != eBattingStyle.LeftHanded)
            BattingStyle = eBattingStyle.LeftHanded;
    }

    private void updateStadiumMode()
    {
        if (StadiumMode == eStadiumMode.Night)
        {
            nightToggle.isOn = true;
            RenderSettings.skybox = NightMaterial;
            theNightLights.SetActive(true);
            theDayLights.SetActive(false);
        }
        else
        {
            dayToggle.isOn = true;
            RenderSettings.skybox = DayMaterial;
            theNightLights.SetActive(false);
            theDayLights.SetActive(true);
        }

        PlayerPrefs.SetInt("mode", (int)stadiumMode);
    }
    public void onNightMode(Toggle t)
    {
        if (!t.isOn)
            return;
        if (StadiumMode != eStadiumMode.Night)
            StadiumMode = eStadiumMode.Night;
    }
    public void onDayMode(Toggle t)
    {
        if (!t.isOn)
            return;
        if (StadiumMode != eStadiumMode.Day)
            StadiumMode = eStadiumMode.Day;
    }

    public void onZChange(float val)
    {
        zOffset = val;
        offsetZText.text = zOffset.ToString();

        DebugUIBuilder.instance.menuOffset.z = zOffset;

        PlayerPrefs.SetFloat("menu_offset", zOffset);
    }
    public void onfXChange(float val)
    {
        fX = val;
        fXText.text = fX.ToString();
    }
    public void onfYChange(float val)
    {
        fY = val;
        fYText.text = fY.ToString();
    }
    public void onfZChange(float val)
    {
        fZ = val;
        fZText.text = fZ.ToString();
    }
    public void ontXChange(float val)
    {
        Debug.Log("tX changed to : " + val.ToString());
        tX = val;
        tXText.text = tX.ToString();
    }
    public void ontYChange(float val)
    {
        tY = val;
        tYText.text = tY.ToString();
    }
    public void ontZChange(float val)
    {
        tZ = val;
        tZText.text = tZ.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (!initialized)
            return;

        // Update values from inspector, when in editor, otherwise, toggle UI will drive this
        if (Application.isEditor)
        {
            updateBattingStyle();
            updateDifficulty();
        }

        // if debug menu is not active!
        if(!DebugUIBuilder.instance.isActiveAndEnabled)
        {
            if (GetButton(OVRInput.Button.One))
            {
                // disable physics
                theBallRigidBody.isKinematic = true;
                // reset ball position to inside machine
                theBall.transform.position = new Vector3(-8.95f, 2.95f, 0f);

                // enable physics
                theBallRigidBody.isKinematic = false;
                // Add the required force & rotation
                theBallRigidBody.AddTorque(new Vector3(tX, tY, tZ), ForceMode.Impulse);
                theBallRigidBody.AddForce(new Vector3(fX, fY, fZ), ForceMode.Impulse);
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

        //float delta = 0f;
        //if (GetButton(OVRInput.Button.Three))
        //    delta = 0.25f;
        //if (GetButton(OVRInput.Button.Four))
        //    delta = -0.25f;

        //if(delta != 0f)
        //{
        //    switch(dbgCurrentToggle)
        //    {
        //        case 0:
        //            fX += delta;
        //            break;
        //        case 1:
        //            fY += delta;
        //            break;
        //        case 2:
        //            fZ += delta;
        //            break;
        //        case 3:
        //            tX += delta;
        //            break;
        //        case 4:
        //            tY += delta;
        //            break;
        //        case 5:
        //            tZ += delta;
        //            break;
        //    }
        //}
    }

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
