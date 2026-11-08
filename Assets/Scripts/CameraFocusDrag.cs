using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraFocusDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    [SerializeField] private RectTransform handle;

    [Tooltip("Smallest Pos X the handle can reach. Same unit as FocusHandle RectTransform Pos X.")]
    [SerializeField] private float leftMax = -218.1f;

    [Tooltip("Largest Pos X the handle can reach. Same unit as FocusHandle RectTransform Pos X.")]
    [SerializeField] private float rightMax = 196f;

    private bool applying;
    private bool subscribed;
    private bool capturedPosition;
    private bool syncedLayer;
    private FocusLayer lastLayer;
    private float lockedAnchoredY;

    public void Configure(RectTransform handleRect)
    {
        if (handleRect != null)
        {
            handle = handleRect;
        }

        CaptureAuthoredPosition();
        Subscribe();
    }

    private void Awake()
    {
        if (handle == null)
        {
            Transform child = transform.Find("FocusHandle");
            if (child != null)
            {
                handle = child as RectTransform;
            }
        }

        CaptureAuthoredPosition();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDestroy()
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState != null)
        {
            levelState.StateChanged -= OnStateChanged;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyPointer(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ApplyPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        ApplyPointer(eventData);
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null)
        {
            return;
        }

        lastLayer = levelState.CurrentLayer;
        syncedLayer = true;
        levelState.StateChanged += OnStateChanged;
        subscribed = true;
    }

    private void CaptureAuthoredPosition()
    {
        if (capturedPosition || handle == null)
        {
            return;
        }

        lockedAnchoredY = handle.anchoredPosition.y;
        capturedPosition = true;
    }

    private void OnStateChanged()
    {
        if (applying)
        {
            return;
        }

        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null || (syncedLayer && levelState.CurrentLayer == lastLayer))
        {
            return;
        }

        lastLayer = levelState.CurrentLayer;
        syncedLayer = true;
        ApplyLayer(levelState.CurrentLayer);
    }

    private void ApplyPointer(PointerEventData eventData)
    {
        if (handle == null)
        {
            return;
        }

        RectTransform rect = (RectTransform)transform;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventCamera, out localPoint))
        {
            return;
        }

        SetHandleCenterX(localPoint.x);
        UpdateFocus(CurrentNormalized());
    }

    public void ApplyNormalized(float normalized, bool updateFocus)
    {
        normalized = Mathf.Clamp01(normalized);
        if (handle != null)
        {
            Rect trackRect = ((RectTransform)transform).rect;
            float centerX = Mathf.Lerp(trackRect.xMin, trackRect.xMax, normalized);
            SetHandleCenterX(centerX);
        }

        if (updateFocus)
        {
            UpdateFocus(normalized);
        }
    }

    private void ApplyLayer(FocusLayer layer)
    {
        float normalized = 0.5f;
        if (layer == FocusLayer.Near)
        {
            normalized = 0.16f;
        }
        else if (layer == FocusLayer.Far)
        {
            normalized = 0.84f;
        }

        ApplyNormalized(normalized, false);
    }

    private void SetHandleCenterX(float parentLocalX)
    {
        CaptureAuthoredPosition();
        Bounds before = RectTransformUtility.CalculateRelativeRectTransformBounds((RectTransform)transform, handle);
        Vector2 anchored = handle.anchoredPosition;
        anchored.x += parentLocalX - before.center.x;
        anchored.y = lockedAnchoredY;
        anchored.x = Mathf.Clamp(anchored.x, Mathf.Min(leftMax, rightMax), Mathf.Max(leftMax, rightMax));
        handle.anchoredPosition = anchored;
        KeepEdgesInsideTrack();
    }

    private void KeepEdgesInsideTrack()
    {
        Rect trackRect = ((RectTransform)transform).rect;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds((RectTransform)transform, handle);
        float shift = 0f;
        if (bounds.min.x < trackRect.xMin)
        {
            shift = trackRect.xMin - bounds.min.x;
        }
        else if (bounds.max.x > trackRect.xMax)
        {
            shift = trackRect.xMax - bounds.max.x;
        }

        if (Mathf.Abs(shift) <= 0.01f)
        {
            return;
        }

        Vector2 anchored = handle.anchoredPosition;
        anchored.x += shift;
        anchored.y = lockedAnchoredY;
        handle.anchoredPosition = anchored;
    }

    private float CurrentNormalized()
    {
        Rect trackRect = ((RectTransform)transform).rect;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds((RectTransform)transform, handle);
        return Mathf.Clamp01(Mathf.InverseLerp(trackRect.xMin, trackRect.xMax, bounds.center.x));
    }

    private void UpdateFocus(float normalized)
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null)
        {
            return;
        }

        applying = true;
        if (normalized < 0.34f)
        {
            levelState.SetLayer(FocusLayer.Near);
            lastLayer = FocusLayer.Near;
        }
        else if (normalized < 0.67f)
        {
            levelState.SetLayer(FocusLayer.Mid);
            lastLayer = FocusLayer.Mid;
        }
        else
        {
            levelState.SetLayer(FocusLayer.Far);
            lastLayer = FocusLayer.Far;
        }

        syncedLayer = true;
        applying = false;
    }
}
