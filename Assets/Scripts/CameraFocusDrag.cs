using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraFocusDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform handle;
    private Vector2 sceneAnchoredPosition;
    private Vector2 dragOriginAnchoredPosition;
    private float dragOriginPointerX;
    private bool dragging;

    private bool savedScenePosition;

    private void OnEnable()
    {
        EnsureHandle();
    }

    public void Configure(RectTransform handleRect)
    {
        if (handleRect == null)
        {
            return;
        }

        handle = handleRect;
        SaveScenePosition();
    }

    private void EnsureHandle()
    {
        if (handle == null)
        {
            Transform child = transform.Find("FocusHandle");
            if (child != null)
            {
                handle = child.GetComponent<RectTransform>();
            }
        }

        SaveScenePosition();
    }

    private void SaveScenePosition()
    {
        if (savedScenePosition || handle == null)
        {
            return;
        }

        sceneAnchoredPosition = handle.anchoredPosition;
        savedScenePosition = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        EnsureHandle();
        if (handle == null || !TryGetTrackLocalX(eventData, out dragOriginPointerX))
        {
            return;
        }

        dragOriginAnchoredPosition = handle.anchoredPosition;
        dragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        EnsureHandle();
        if (!dragging || handle == null || !TryGetTrackLocalX(eventData, out float pointerX))
        {
            return;
        }

        Vector2 next = dragOriginAnchoredPosition;
        next.x += pointerX - dragOriginPointerX;
        next.x = ClampAnchoredX(next.x);
        handle.anchoredPosition = next;
        UpdateFocus();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    private bool TryGetTrackLocalX(PointerEventData eventData, out float localX)
    {
        localX = 0f;
        RectTransform track = (RectTransform)transform;
        Canvas canvas = track.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = canvas.worldCamera != null ? canvas.worldCamera : eventData.pressEventCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventCamera, out Vector2 localPoint))
        {
            return false;
        }

        localX = localPoint.x;
        return true;
    }

    private float ClampAnchoredX(float anchoredX)
    {
        RectTransform track = (RectTransform)transform;
        float currentX = handle.anchoredPosition.x;
        float pivotX = track.InverseTransformPoint(handle.position).x + (anchoredX - currentX);
        float minPivotX = track.rect.xMin - handle.rect.xMin;
        float maxPivotX = track.rect.xMax - handle.rect.xMax;
        if (maxPivotX < minPivotX)
        {
            pivotX = (track.rect.xMin + track.rect.xMax) * 0.5f - (handle.rect.xMin + handle.rect.xMax) * 0.5f;
        }
        else
        {
            pivotX = Mathf.Clamp(pivotX, minPivotX, maxPivotX);
        }

        return currentX + (pivotX - track.InverseTransformPoint(handle.position).x);
    }

    private void UpdateFocus()
    {
        RectTransform track = (RectTransform)transform;
        float centerX = track.InverseTransformPoint(handle.position).x + (handle.rect.xMin + handle.rect.xMax) * 0.5f;
        float normalized = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, centerX);
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null)
        {
            return;
        }

        if (normalized < 0.34f)
        {
            levelState.SetLayer(FocusLayer.Near);
        }
        else if (normalized < 0.67f)
        {
            levelState.SetLayer(FocusLayer.Mid);
        }
        else
        {
            levelState.SetLayer(FocusLayer.Far);
        }
    }
}
