using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The replay on the big screen. Records the last few seconds of a delivery from the replay
/// camera and loops them on the screen after the ball is dead.
///
/// Built for the Quest's GPU, with nothing created at runtime:
///   * the camera is disabled and renders only while recording, once per CaptureInterval, into
///     the pre-made RenderTexture `view` (it used to render the whole stadium - with its own shadow
///     pass - every frame, into a texture nothing showed);
///   * each captured frame is copied into a slice of the pre-made Texture2DArray asset `frames`
///     (it used to allocate a new Texture2D for every captured frame);
///   * the screen's material (CricketVR/ReplayScreen) shows one slice via _Slice, -1 = off.
/// </summary>
public class CameraReplay : MonoBehaviour
{
    public static CameraReplay Instance = null;

    [SerializeField]
    private Material targetMat;
    [Tooltip("What the replay camera renders into (a RenderTexture asset).")]
    [SerializeField]
    private RenderTexture view;
    [Tooltip("Recorded frames: a Texture2DArray RenderTexture asset, same size as `view`.")]
    [SerializeField]
    private RenderTexture frames;
    [SerializeField]
    private float captureInterval = 0.06f;

    private Camera myCamera;
    private int frameCount;

    [HideInInspector]
    public bool record;
    [HideInInspector]
    public bool display;

    [HideInInspector]
    public Coroutine startCapturing;
    [HideInInspector]
    public Coroutine startDisplaying;

    private Vector3 startRot;
    private Vector3 startPos;
    private int viewSetting;

    private static readonly int SliceId = Shader.PropertyToID("_Slice");
    private static readonly int FramesId = Shader.PropertyToID("_Frames");

    void Start()
    {
        if (CameraReplay.Instance == null)
            CameraReplay.Instance = this;

        viewSetting = 0;
        startPos = transform.position;
        startRot = transform.rotation.eulerAngles;
        myCamera = GetComponent<Camera>();
        myCamera.enabled = false;               // renders only when a frame is captured
        myCamera.targetTexture = view;
        record = false;
        display = false;
        targetMat.SetTexture(FramesId, frames);
        targetMat.SetFloat(SliceId, -1f);
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
            startCapturing = StartCoroutine(StartCapturing(delay));
    }

    private IEnumerator StartCapturing(float delay)
    {
        yield return new WaitForSeconds(delay);

        frameCount = 0;
        record = true;
        display = false;
        var wait = new WaitForSeconds(captureInterval);
        while (record)
        {
            Capture(frameCount % frames.volumeDepth);
            frameCount++;
            yield return wait;
        }
    }

    private void Capture(int slice)
    {
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = view };
        if (RenderPipeline.SupportsRenderRequest(myCamera, request))
            RenderPipeline.SubmitRenderRequest(myCamera, request);
        else
            myCamera.Render();
        Graphics.CopyTexture(view, 0, 0, frames, slice, 0);
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
        // Only the most recent `volumeDepth` frames survive the ring; play them oldest first.
        int count = Mathf.Min(frameCount, frames.volumeDepth);
        int first = frameCount - count;
        var wait = new WaitForSeconds(captureInterval);
        while (display && count > 0)
        {
            for (int i = 0; i < count && display; i++)
            {
                targetMat.SetFloat(SliceId, (first + i) % frames.volumeDepth);
                yield return wait;
            }
            if (display)
                yield return new WaitForSeconds(1f);
        }
        targetMat.SetFloat(SliceId, -1f);
    }

    public void StopDisplaying()
    {
        display = false;
        targetMat.SetFloat(SliceId, -1f);
    }

    public void setViewSetting(int setting, float delay = 0f)
    {
        if (delay == 0)
            viewSetting = setting;
        else
            StartCoroutine(setViewSettingDelay(setting, delay));
    }

    private IEnumerator setViewSettingDelay(int setting, float delay = 0f)
    {
        yield return new WaitForSeconds(delay);
        viewSetting = setting;
    }
}
