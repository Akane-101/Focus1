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
    }
}
