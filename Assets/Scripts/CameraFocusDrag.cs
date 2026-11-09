using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CameraFocusDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private RectTransform leftBound;
    [SerializeField] private RectTransform nearStop;
    [SerializeField] private RectTransform midStop;
    [SerializeField] private RectTransform farStop;
    [SerializeField] private RectTransform rightBound;

    [Tooltip("进入该档位的距离，按 FocusTrack 宽度的比例计算。")]
    [Range(0.005f, 0.2f)]
    [SerializeField] private float snapRange = 0.045f;

    [Tooltip("吸住之后，指针还要再走出的距离，必须大于吸附范围。按 FocusTrack 宽度的比例计算。")]
    [Range(0.01f, 0.35f)]
    [SerializeField] private float escapeDistance = 0.09f;

    [SerializeField] private float authoredYOverHeight = -0.116f;

    public event System.Action PointerPressed;
    public event System.Action PointerReleased;
    public event System.Action DragBegan;
    public event System.Action<FocusLayer> FocusSnapped;

    private bool applying;
    private bool subscribed;
    private bool placed;
    private bool orderWarned;
    private bool pointerHeld;
    private bool announcedDrag;
    private int caught = -1;
    private int suppressed = -1;
    private float freeNormalized = 0.5f;
    private float lastTrackWidth = -1f;
    private FocusLayer lastLayer;

    public RectTransform LeftBound { get { return leftBound; } }
    public RectTransform NearStop { get { return nearStop; } }
    public RectTransform MidStop { get { return midStop; } }
    public RectTransform FarStop { get { return farStop; } }
    public RectTransform RightBound { get { return rightBound; } }
    public float SnapRange { get { return snapRange; } }
    public float EscapeDistance { get { return escapeDistance; } }

    public bool IsCaughtOn(FocusLayer layer)
    {
        return caught >= 0 && StopLayer(caught) == layer;
    }

    public void Configure(RectTransform handleRect)
    {
        if (handleRect != null)
        {
            handle = handleRect;
        }
    }

    public string OrderError()
    {
        if (!HasMarkers())
        {
            return "五个标记点还没有全部指定。";
        }

        float left = LocalX(leftBound);
        float near = LocalX(nearStop);
        float mid = LocalX(midStop);
        float far = LocalX(farStop);
        float right = LocalX(rightBound);
        if (left < near && near < mid && mid < far && far < right)
        {
            return null;
        }

        return "标记点顺序必须是：最左 < 近景 < 中景 < 远景 < 最右。当前 "
            + left.ToString("F1") + " / " + near.ToString("F1") + " / " + mid.ToString("F1") + " / "
            + far.ToString("F1") + " / " + right.ToString("F1");
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
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState != null)
        {
            levelState.StateChanged -= OnStateChanged;
        }

        subscribed = false;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || handle == null)
        {
            return;
        }

        Rect trackRect = Track.rect;
        if (trackRect.width < 8f || trackRect.height < 8f)
        {
            return;
        }

        if (!placed)
        {
            PlaceInitial();
            placed = true;
            lastTrackWidth = trackRect.width;
            return;
        }

        if (Mathf.Abs(trackRect.width - lastTrackWidth) > 0.5f)
        {
            ApplyPose();
            lastTrackWidth = trackRect.width;
        }
    }

    private void OnValidate()
    {
        if (escapeDistance <= snapRange)
        {
            escapeDistance = snapRange + 0.01f;
        }

        if (Application.isPlaying || handle == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
        {
            return;
        }
#endif

        Rect trackRect = Track.rect;
        if (trackRect.height < 8f)
        {
            return;
        }

        authoredYOverHeight = handle.anchoredPosition.y / trackRect.height;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!pointerHeld)
        {
            pointerHeld = true;
            announcedDrag = false;
            if (PointerPressed != null)
            {
                PointerPressed();
            }
        }

        suppressed = -1;
        FollowPointer(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!announcedDrag)
        {
            announcedDrag = true;
            if (DragBegan != null)
            {
                DragBegan();
            }
        }

        FollowPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        FollowPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        suppressed = -1;
        ApplyPose();
        if (!pointerHeld)
        {
            return;
        }

        pointerHeld = false;
        if (PointerReleased != null)
        {
            PointerReleased();
        }
    }

    private void PlaceInitial()
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        FocusLayer layer = levelState != null ? levelState.CurrentLayer : FocusLayer.Mid;
        caught = StopIndex(layer);
        lastLayer = layer;
        freeNormalized = NormalizedOnTrack(LocalX(StopMarker(caught)));
        ApplyPose();
        Subscribe();
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
        levelState.StateChanged += OnStateChanged;
        subscribed = true;
    }

    private void OnStateChanged()
    {
        if (applying)
        {
            return;
        }

        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null || levelState.CurrentLayer == lastLayer)
        {
            return;
        }

        lastLayer = levelState.CurrentLayer;
        caught = StopIndex(lastLayer);
        suppressed = -1;
        ApplyPose();
    }

    private void FollowPointer(PointerEventData eventData)
    {
        if (handle == null || !HasMarkers())
        {
            return;
        }

        string orderError = OrderError();
        if (orderError != null)
        {
            if (!orderWarned)
            {
                Debug.LogWarning(orderError, this);
                orderWarned = true;
            }

            return;
        }

        Vector2 localPoint;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Track, eventData.position, eventCamera, out localPoint))
        {
            return;
        }

        float pointerX = Mathf.Clamp(localPoint.x, LocalX(leftBound), LocalX(rightBound));
        if (caught >= 0)
        {
            float heldX = LocalX(StopMarker(caught));
            if (Mathf.Abs(pointerX - heldX) <= EscapePixels())
            {
                ApplyPose();
                return;
            }

            suppressed = caught;
            caught = -1;
        }

        if (suppressed >= 0 && Mathf.Abs(pointerX - LocalX(StopMarker(suppressed))) > SnapPixels())
        {
            suppressed = -1;
        }

        int hit = StopWithin(pointerX, suppressed);
        if (hit >= 0)
        {
            bool newlyCaught = hit != caught;
            caught = hit;
            SetFocus(StopLayer(hit));
            if (newlyCaught && FocusSnapped != null)
            {
                FocusSnapped(StopLayer(hit));
            }
        }
        else
        {
            freeNormalized = NormalizedOnTrack(pointerX);
        }

        ApplyPose();
    }

    private void ApplyPose()
    {
        if (handle == null || !HasMarkers())
        {
            return;
        }

        float targetX = caught >= 0
            ? LocalX(StopMarker(caught))
            : Mathf.Lerp(Track.rect.xMin, Track.rect.xMax, freeNormalized);
        targetX = Mathf.Clamp(targetX, LocalX(leftBound), LocalX(rightBound));
        SetCenterX(targetX);

        Vector2 anchored = handle.anchoredPosition;
        anchored.y = authoredYOverHeight * Track.rect.height;
        handle.anchoredPosition = anchored;
    }

    private void SetCenterX(float trackLocalX)
    {
        Bounds before = RectTransformUtility.CalculateRelativeRectTransformBounds(Track, handle);
        Vector2 anchored = handle.anchoredPosition;
        anchored.x += trackLocalX - before.center.x;
        handle.anchoredPosition = anchored;
    }

    private int StopWithin(float pointerX, int ignored)
    {
        int nearest = -1;
        float nearestDistance = SnapPixels();
        for (int i = 0; i < 3; i++)
        {
            if (i == ignored)
            {
                continue;
            }

            float distance = Mathf.Abs(pointerX - LocalX(StopMarker(i)));
            if (distance <= nearestDistance)
            {
                nearest = i;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private float SnapPixels()
    {
        return Mathf.Max(0.01f, snapRange) * Track.rect.width;
    }

    private float EscapePixels()
    {
        return Mathf.Max(SnapPixels() + 0.01f, escapeDistance * Track.rect.width);
    }

    private float NormalizedOnTrack(float localX)
    {
        return Mathf.InverseLerp(Track.rect.xMin, Track.rect.xMax, localX);
    }

    private float LocalX(RectTransform marker)
    {
        if (marker == null)
        {
            return 0f;
        }

        return Track.InverseTransformPoint(marker.position).x;
    }

    private RectTransform StopMarker(int index)
    {
        if (index <= 0)
        {
            return nearStop;
        }

        if (index == 1)
        {
            return midStop;
        }

        return farStop;
    }

    private static int StopIndex(FocusLayer layer)
    {
        if (layer == FocusLayer.Near)
        {
            return 0;
        }

        if (layer == FocusLayer.Far)
        {
            return 2;
        }

        return 1;
    }

    private static FocusLayer StopLayer(int index)
    {
        if (index <= 0)
        {
            return FocusLayer.Near;
        }

        if (index == 1)
        {
            return FocusLayer.Mid;
        }

        return FocusLayer.Far;
    }

    private bool HasMarkers()
    {
        return leftBound != null && nearStop != null && midStop != null && farStop != null && rightBound != null;
    }

    private RectTransform Track
    {
        get { return (RectTransform)transform; }
    }

    private void SetFocus(FocusLayer layer)
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();
        if (levelState == null)
        {
            return;
        }

        applying = true;
        levelState.SetLayer(layer);
        lastLayer = layer;
        applying = false;
    }
}
