using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-50)]
[ExecuteAlways]
public sealed class CameraHudLayout : MonoBehaviour
{
    [SerializeField] private RectTransform hudFrame;
    [SerializeField] private RectTransform handle;
    [SerializeField] private CameraFocusDrag focusDrag;

    private bool handleConfigured;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private void OnEnable()
    {
        RestoreCanvasScale();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            RestoreCanvasScale();
            return;
        }

        if (hudFrame == null)
        {
            return;
        }

        if (!handleConfigured && focusDrag != null)
        {
            focusDrag.Configure(handle);
            handleConfigured = true;
        }

        GameObject display = GameObject.Find("CameraLCD");
        Renderer displayRenderer = display != null ? display.GetComponent<Renderer>() : null;
        Camera frameCamera = FindFrameCamera();

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

    private static Camera FindFrameCamera()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        int i;
        for (i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.name == "CameraFrameCamera")
            {
                return candidate;
            }
        }

        for (i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null &&
                candidate.targetTexture == null &&
                candidate.CompareTag("MainCamera"))
            {
                return candidate;
            }
        }

        return Camera.main;
    }

    private void RestoreCanvasScale()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect != null && canvasRect.localScale.sqrMagnitude < 0.001f)
        {
            canvasRect.localScale = Vector3.one;
        }
    }
}
