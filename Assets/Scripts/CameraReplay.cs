using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraReplay : MonoBehaviour
{
    public static CameraReplay Instance = null;

    [SerializeField]
    private Material targetMat;

    private Camera myCamera;
    private List<Texture2D> frames;

    [HideInInspector]
    public bool record;
    [HideInInspector]
    public bool display;

    private Texture2D myTexture2D;
    private Texture2D originalTex;

    [HideInInspector]
    public Coroutine startCapturing;
    [HideInInspector]
    public Coroutine startDisplaying;

    private Vector3 startRot;
    private Vector3 startPos;

    private int viewSetting;

    // 5 seems to be ideal for smooth gameplay
    private int captureRate;
    private float replaySpeed;

    // Start is called before the first frame update
    void Start()
    {
        if (CameraReplay.Instance == null)
            CameraReplay.Instance = this;

        viewSetting = 0;
        startPos = transform.position;
        startRot = transform.rotation.eulerAngles;
        myCamera = GetComponent<Camera>();
        frames = new List<Texture2D>();
        record = false;
        display = false;
        //originalTex = new Texture2D(myCamera.activeTexture.width, myCamera.activeTexture.height, TextureFormat.ARGB32, 1, true);
        originalTex = Texture2D.blackTexture;
        captureRate = 7; // Frames per capture
        replaySpeed = 1f;
    }

    private void Update()
    {
        if (viewSetting == 0)
        {
            transform.position = startPos;
            transform.rotation = Quaternion.Euler(startRot);
            myCamera.fieldOfView = 7f;
        }
        else if (viewSetting == 1)
        {
            transform.position = new Vector3(-14f, 1.78f, 0f);
            transform.LookAt(Main.Instance.theBall.transform);
            transform.rotation = Quaternion.Euler(new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0f));
            myCamera.fieldOfView = 60f;
        }
    }

    public void StartRecording(float delay)
    {
        if (startCapturing == null)
        {
            startCapturing = StartCoroutine(StartCapturing(delay));
        }
    }

    private IEnumerator StartCapturing(float delay)
    {
        yield return new WaitForSeconds(delay);

        for(int i = 0; i < frames.Count; i++)
        {
            Destroy(frames[i]);
            frames[i] = null;
        }

        frames.Clear();

        record = true;
        display = false;

        while (record)
        {
            myTexture2D = new Texture2D(myCamera.activeTexture.width, myCamera.activeTexture.height, TextureFormat.ARGB32, 1, true);
            Graphics.CopyTexture(myCamera.activeTexture, myTexture2D);
            frames.Add(myTexture2D);
            for (int i = 0; i < captureRate; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }

    public void StopRecording(float delay)
    {
        if (startDisplaying == null)
        {
            viewSetting = 0;
            startDisplaying = StartCoroutine(StartDisplaying(delay));

        }
    }

    private IEnumerator StartDisplaying(float delay)
    {
        yield return new WaitForSeconds(delay);

        record = false;
        display = true;

        while (display)
        {
            foreach (Texture2D tex in frames)
            {
                targetMat.SetTexture("_MainTex", tex);
                for (int i = 0; i < (int)(captureRate / replaySpeed); i++)
                {
                    if (!display)
                        break;
                    yield return new WaitForFixedUpdate();
                }
                if (!display)
                    break;
            }
            yield return new WaitForSeconds(1f);
        }

        targetMat.SetTexture("_MainTex", originalTex);
    }

    public void StopDisplaying()
    {
        display = false;
    }

    public void setViewSetting(int setting, float delay = 0f)
    {
        if (delay == 0)
        {
            viewSetting = setting;
        }
        else
        {
            StartCoroutine(setViewSettingDelay(setting, delay));
        }
    }

    private IEnumerator setViewSettingDelay(int setting, float delay = 0f)
    {
        yield return new WaitForSeconds(delay);
        viewSetting = setting;
    }
}
