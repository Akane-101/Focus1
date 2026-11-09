using UnityEngine;

[ExecuteAlways]
public sealed class FocusTrackMarker : MonoBehaviour
{
    public enum Kind
    {
        LeftBound,
        Near,
        Mid,
        Far,
        RightBound
    }

    [SerializeField] private Kind kind;

    public Kind MarkerKind
    {
        get { return kind; }
    }

    private void OnDrawGizmos()
    {
        DrawMarker(false);
    }

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (UnityEditorSelectionIsThis())
        {
            return;
        }

        BakeIntoAnchor();
    }

    public void BakeIntoAnchor()
    {
        RectTransform marker = transform as RectTransform;
        RectTransform track = transform.parent as RectTransform;
        if (marker == null || track == null || track.rect.width < 1f)
        {
            return;
        }

        if (marker.anchoredPosition.sqrMagnitude <= 0.25f &&
            Mathf.Abs(marker.anchorMin.y - 0.5f) <= 0.001f &&
            Mathf.Abs(marker.anchorMax.x - marker.anchorMin.x) <= 0.001f)
        {
            return;
        }

        Vector3 local = track.InverseTransformPoint(marker.position);
        float normalized = Mathf.InverseLerp(track.rect.xMin, track.rect.xMax, local.x);
        marker.anchorMin = new Vector2(Mathf.Clamp01(normalized), 0.5f);
        marker.anchorMax = new Vector2(Mathf.Clamp01(normalized), 0.5f);
        marker.pivot = new Vector2(0.5f, 0.5f);
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = new Vector2(18f, 18f);
    }

    private void DrawMarker(bool selected)
    {
        RectTransform marker = transform as RectTransform;
        if (marker == null)
        {
            return;
        }

        Gizmos.color = ColorFor(kind);
        Vector3 center = marker.position;
        float scale = selected ? 14f : 10f;
        Gizmos.DrawSphere(center, scale);
        RectTransform track = transform.parent as RectTransform;
        if (track == null)
        {
            return;
        }

        Vector3 left = track.TransformPoint(new Vector3(track.rect.xMin, 0f, 0f));
        Vector3 right = track.TransformPoint(new Vector3(track.rect.xMax, 0f, 0f));
        Gizmos.DrawLine(left, right);
    }

    public static Color ColorFor(Kind markerKind)
    {
        if (markerKind == Kind.LeftBound || markerKind == Kind.RightBound)
        {
            return new Color(1f, 1f, 1f, 0.95f);
        }

        if (markerKind == Kind.Near)
        {
            return new Color(1f, 0.82f, 0.2f, 0.95f);
        }

        if (markerKind == Kind.Mid)
        {
            return new Color(0.35f, 0.9f, 0.45f, 0.95f);
        }

        return new Color(0.35f, 0.7f, 1f, 0.95f);
    }

    private bool UnityEditorSelectionIsThis()
    {
#if UNITY_EDITOR
        return UnityEditor.Selection.activeTransform == transform;
#else
        return false;
#endif
    }
}
