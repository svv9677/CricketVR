using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// One track, two handles: a min..max range (bowling speed, swing, turn, line). Replaces the old
/// pair of separate Min / Max sliders. Press anywhere on the row and the nearer handle jumps there
/// and follows the laser; the handles cannot cross (dragging one past the other hands over to it).
///
/// Built by Tools > CricketVR > Build Player UI: the row's own transparent Image is the hit area,
/// `track` is the bar, and the fill and handles are children of the track, placed by anchors.
/// </summary>
public class RangeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    [System.Serializable]
    public class RangeEvent : UnityEvent<float, float> { }

    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform fill;
    [SerializeField] private RectTransform lowHandle;
    [SerializeField] private RectTransform highHandle;
    [SerializeField] private float minLimit = 0f;
    [SerializeField] private float maxLimit = 1f;
    [Tooltip("Values snap to multiples of this; 0 = continuous.")]
    [SerializeField] private float step = 0f;
    [SerializeField] private float low = 0f;
    [SerializeField] private float high = 1f;

    public RangeEvent onValueChanged = new RangeEvent();

    private const float ActiveHandleScale = 1.2f;
    private int dragging = -1;   // 0 = low handle, 1 = high handle

    public float Low => low;
    public float High => high;

    public void Configure(float min, float max, float snap)
    {
        minLimit = min;
        maxLimit = max;
        step = snap;
        SetValuesWithoutNotify(low, high);
    }

    public void SetValuesWithoutNotify(float lowValue, float highValue)
    {
        low = Mathf.Clamp(Snap(lowValue), minLimit, maxLimit);
        high = Mathf.Clamp(Snap(highValue), low, maxLimit);
        UpdateVisuals();
    }

    private void OnEnable() => UpdateVisuals();

    private void OnDisable()
    {
        dragging = -1;
        UpdateVisuals();
    }

    // ---- Pointer ---------------------------------------------------------------------------------

    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !TryValueAt(eventData, out float v))
            return;
        float toLow = Mathf.Abs(v - low), toHigh = Mathf.Abs(v - high);
        // On a tie (handles stacked) take the side that was pressed.
        dragging = toLow < toHigh ? 0 : toHigh < toLow ? 1 : (v < low ? 0 : 1);
        MoveTo(v);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging < 0 || eventData.button != PointerEventData.InputButton.Left || !TryValueAt(eventData, out float v))
            return;
        MoveTo(v);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragging = -1;
        UpdateVisuals();
    }

    private bool TryValueAt(PointerEventData eventData, out float value)
    {
        value = low;
        if (track == null)
            return false;
        Camera cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, cam, out Vector2 local))
            return false;
        Rect r = track.rect;
        if (r.width <= 0f)
            return false;
        value = Mathf.Lerp(minLimit, maxLimit, Mathf.Clamp01((local.x - r.xMin) / r.width));
        return true;
    }

    private void MoveTo(float v)
    {
        v = Mathf.Clamp(Snap(v), minLimit, maxLimit);
        // Dragging a handle past the other one hands the drag over rather than pushing it.
        if (dragging == 1 && v < low) dragging = 0;
        else if (dragging == 0 && v > high) dragging = 1;
        float newLow = dragging == 0 ? v : low;
        float newHigh = dragging == 1 ? v : high;
        bool changed = newLow != low || newHigh != high;
        low = newLow;
        high = newHigh;
        UpdateVisuals();
        if (changed)
            onValueChanged.Invoke(low, high);
    }

    private float Snap(float v) => step > 0f ? Mathf.Round(v / step) * step : v;

    // ---- Visuals ---------------------------------------------------------------------------------

    private float Normalized(float v) => maxLimit > minLimit ? Mathf.Clamp01((v - minLimit) / (maxLimit - minLimit)) : 0f;

    private void UpdateVisuals()
    {
        float a = Normalized(low), b = Normalized(high);
        Place(lowHandle, a, dragging == 0);
        Place(highHandle, b, dragging == 1);
        if (fill != null)
        {
            fill.anchorMin = new Vector2(a, 0f);
            fill.anchorMax = new Vector2(b, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
    }

    private static void Place(RectTransform handle, float t, bool active)
    {
        if (handle == null)
            return;
        handle.anchorMin = handle.anchorMax = new Vector2(t, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.localScale = Vector3.one * (active ? ActiveHandleScale : 1f);
    }

#if UNITY_EDITOR
    /// Editor builder only.
    public void SetParts(RectTransform trackRect, RectTransform fillRect, RectTransform lowRect, RectTransform highRect)
    {
        track = trackRect;
        fill = fillRect;
        lowHandle = lowRect;
        highHandle = highRect;
    }
#endif
}
