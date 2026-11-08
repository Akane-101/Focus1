using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraHudLayout : MonoBehaviour
{
    [SerializeField] private RectTransform hudFrame;
    [SerializeField] private RectTransform handle;
    [SerializeField] private CameraFocusDrag focusDrag;

    private bool configured;

    private void Awake()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private void LateUpdate()
    {
        if (hudFrame == null)
        {
            return;
        }

        if (!configured && focusDrag != null && handle != null)
        {
            focusDrag.Configure(handle);
            configured = true;
        }

        GameObject display = GameObject.Find("CameraLCD");
        Renderer displayRenderer = display != null ? display.GetComponent<Renderer>() : null;
        Camera frameCamera = null;
        GameObject frameObject = GameObject.Find("CameraFrameCamera");
        if (frameObject != null)
        {
            frameCamera = frameObject.GetComponent<Camera>();
        }

        if (frameCamera == null && Camera.main != null && Camera.main.targetTexture == null)
        {
            frameCamera = Camera.main;
        }

        if (frameCamera != null && displayRenderer != null)
        {
            FitToOpening(frameCamera, displayRenderer.bounds);
        }
    }

    private void FitToOpening(Camera frameCamera, Bounds bounds)
    {
        Vector3 bottomLeft = frameCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
        Vector3 topRight = frameCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
        ApplyScreenRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    private void ApplyScreenRect(float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform canvasRect = hudFrame.parent as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        Vector2 localMin;
        Vector2 localMax;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(xMin, yMin), null, out localMin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(xMax, yMax), null, out localMax))
        {
            return;
        }

        hudFrame.anchorMin = new Vector2(0.5f, 0.5f);
        hudFrame.anchorMax = new Vector2(0.5f, 0.5f);
        hudFrame.pivot = Vector2.zero;
        hudFrame.anchoredPosition = localMin;
        hudFrame.sizeDelta = localMax - localMin;
    }
}
