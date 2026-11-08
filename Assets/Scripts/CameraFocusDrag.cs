using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraFocusDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    private RectTransform handle;
    private float handleWidthRatio = 0.03f;
    private float handleHeightRatio = 0.38f;
    private bool applying;

    public void Configure(RectTransform handleRect, float widthRatio, float heightRatio)
    {
        handle = handleRect;
        handleWidthRatio = Mathf.Max(0.01f, widthRatio);
        handleHeightRatio = Mathf.Max(0.01f, heightRatio);
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState != null)
        {
            ApplyLayer(levelState.CurrentLayer);
            levelState.StateChanged += OnStateChanged;
        }
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

    private void OnStateChanged()
    {
        if (applying)
        {
            return;
        }

        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState != null)
        {
            ApplyLayer(levelState.CurrentLayer);
        }
    }

    private void ApplyPointer(PointerEventData eventData)
    {
        RectTransform rect = (RectTransform)transform;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventCamera, out localPoint))
        {
            return;
        }

        float normalized = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, localPoint.x);
        ApplyNormalized(Mathf.Clamp01(normalized), true);
    }

    public void ApplyNormalized(float normalized, bool updateFocus)
    {
        normalized = Mathf.Clamp01(normalized);
        if (handle != null)
        {
            float halfWidth = handleWidthRatio * 0.5f;
            float halfHeight = handleHeightRatio * 0.5f;
            float position = Mathf.Lerp(halfWidth, 1f - halfWidth, normalized);
            handle.anchorMin = new Vector2(position - halfWidth, 0.5f - halfHeight);
            handle.anchorMax = new Vector2(position + halfWidth, 0.5f + halfHeight);
            handle.sizeDelta = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
        }

        if (!updateFocus)
        {
            return;
        }

        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null)
        {
            return;
        }

        applying = true;
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

        applying = false;
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
}
