using UnityEngine;
using UnityEngine.UI;

public sealed class CameraViewfinderPresence : MonoBehaviour
{
    private enum Motion
    {
        Idle,
        Drag,
        Release
    }

    [SerializeField] private CameraFocusDrag focusDrag;
    [SerializeField] private Graphic viewfinder;

    [Header("待机")]
    [Tooltip("进入关卡或一次操作收尾后，隔多久才做第一次待机呼吸，秒。")]
    [SerializeField] private float idleDelay = 12f;

    [Tooltip("第一次之后，两次待机呼吸之间的最短间隔，秒。")]
    [SerializeField] private float idleIntervalMin = 15f;

    [Tooltip("第一次之后，两次待机呼吸之间的最长间隔，秒。")]
    [SerializeField] private float idleIntervalMax = 25f;

    [Tooltip("待机呼吸的最高透明度。当前是整张取景框，所以用较浅的 4%。")]
    [Range(0f, 1f)]
    [SerializeField] private float idlePeak = 0.04f;

    [Tooltip("一次待机起伏的时长，秒。")]
    [SerializeField] private float idleBreathDuration = 2f;

    [Header("拖动呼吸")]
    [Tooltip("拖动时呼吸的最低透明度。")]
    [Range(0f, 1f)]
    [SerializeField] private float dragMin = 0.08f;

    [Tooltip("拖动时呼吸的最高透明度。")]
    [Range(0f, 1f)]
    [SerializeField] private float dragMax = 0.25f;

    [Tooltip("拖动呼吸走完一次起伏的时间，秒。")]
    [SerializeField] private float dragPeriod = 1.4f;

    [Tooltip("开始拖动时，从当前亮度接到呼吸的时间，秒。")]
    [SerializeField] private float engageDuration = 0.12f;

    [Header("到档")]
    [Tooltip("吸到新档位后，还要保持多久才闪，秒。")]
    [SerializeField] private float settleDuration = 0.1f;

    [Tooltip("到档轻闪的最高透明度。闪完回到当时的呼吸亮度。")]
    [Range(0f, 1f)]
    [SerializeField] private float flashAlpha = 0.45f;

    [Tooltip("从当前亮度闪到峰值的时间，秒。设为 0 则直接到达峰值。")]
    [SerializeField] private float flashAttack = 0.04f;

    [Tooltip("从峰值回到当时呼吸亮度的时间，秒。")]
    [SerializeField] private float flashDuration = 0.15f;

    [Header("松手")]
    [Tooltip("这次起伏回到低点之后，再淡到透明的时间，秒。")]
    [SerializeField] private float releaseFadeDuration = 0.4f;

    private const float TroughEpsilon = 0.0001f;

    private Motion motion = Motion.Idle;
    private bool subscribed;
    private bool pointerDown;
    private bool engaging;
    private bool seenPeak;
    private bool fading;
    private bool idleBreathing;
    private bool hadIdleBreath;
    private bool pending;
    private bool flashing;
    private float idleWait;
    private float idleTime;
    private float scheduledGap;
    private float phase;
    private float engageFrom;
    private float engageTime;
    private float fadeFrom;
    private float fadeTime;
    private float pendingTime;
    private float flashTime;
    private float flashEnvelope;
    private float baseAlpha;
    private float alpha;
    private FocusLayer pendingLayer;
    private Color baseRgb = Color.white;

    public float CurrentAlpha { get { return alpha; } }
    public int FlashCount { get; private set; }
    public int IdleBreathCount { get; private set; }
    public float ScheduledIdleGap { get { return scheduledGap; } }
    public FocusLayer LastFlashedLayer { get; private set; }
    public bool IsIdleMotion { get { return motion == Motion.Idle; } }
    public bool IsDragMotion { get { return motion == Motion.Drag; } }
    public bool IsReleaseMotion { get { return motion == Motion.Release; } }

    private void Awake()
    {
        Resolve();
        if (viewfinder != null)
        {
            viewfinder.raycastTarget = false;
            baseRgb = viewfinder.color;
        }

        alpha = 0f;
    }

    private void OnEnable()
    {
        Resolve();
        if (!Application.isPlaying)
        {
            return;
        }

        motion = Motion.Idle;
        pointerDown = false;
        engaging = false;
        seenPeak = false;
        fading = false;
        idleBreathing = false;
        hadIdleBreath = false;
        pending = false;
        flashing = false;
        idleWait = 0f;
        idleTime = 0f;
        scheduledGap = 0f;
        phase = 0f;
        flashEnvelope = 0f;
        baseAlpha = 0f;
        alpha = 0f;
        FlashCount = 0;
        IdleBreathCount = 0;
        ApplyAlpha();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!Application.isPlaying || viewfinder == null)
        {
            return;
        }

        if (!subscribed)
        {
            Subscribe();
        }

        float delta = Time.unscaledDeltaTime;
        TickPending(delta);
        TickFlash(delta);
        if (motion == Motion.Drag)
        {
            TickDrag(delta);
        }
        else if (motion == Motion.Release)
        {
            TickRelease(delta);
        }
        else
        {
            TickIdle(delta);
        }

        alpha = Mathf.Lerp(baseAlpha, flashAlpha, flashEnvelope);
        ApplyAlpha();
    }

    private void OnValidate()
    {
        idleDelay = Mathf.Max(0f, idleDelay);
        idleIntervalMin = Mathf.Max(0f, idleIntervalMin);
        idleIntervalMax = Mathf.Max(idleIntervalMin, idleIntervalMax);
        idlePeak = Mathf.Clamp01(idlePeak);
        idleBreathDuration = Mathf.Max(0.05f, idleBreathDuration);
        dragMin = Mathf.Clamp01(dragMin);
        dragMax = Mathf.Clamp01(dragMax);
        if (dragMax < dragMin)
        {
            dragMax = dragMin;
        }

        dragPeriod = Mathf.Max(0.05f, dragPeriod);
        engageDuration = Mathf.Max(0.01f, engageDuration);
        settleDuration = Mathf.Max(0f, settleDuration);
        flashAlpha = Mathf.Clamp01(flashAlpha);
        flashAttack = Mathf.Max(0f, flashAttack);
        flashDuration = Mathf.Max(0.01f, flashDuration);
        releaseFadeDuration = Mathf.Max(0.01f, releaseFadeDuration);
    }

    private void TickIdle(float delta)
    {
        if (idleBreathing)
        {
            idleTime += delta;
            float u = Mathf.Clamp01(idleTime / idleBreathDuration);
            baseAlpha = idlePeak * Mathf.Sin(u * Mathf.PI);
            if (u >= 1f)
            {
                idleBreathing = false;
                baseAlpha = 0f;
                idleWait = 0f;
                hadIdleBreath = true;
                scheduledGap = Random.Range(idleIntervalMin, idleIntervalMax);
                IdleBreathCount++;
            }

            return;
        }

        if (pointerDown)
        {
            return;
        }

        idleWait += delta;
        float needed = hadIdleBreath ? scheduledGap : idleDelay;
        if (idleWait < needed)
        {
            return;
        }

        idleBreathing = true;
        idleTime = 0f;
        baseAlpha = 0f;
    }

    private void TickDrag(float delta)
    {
        AdvancePhase(delta);
        BlendTowardBreath(delta);
    }

    private void TickRelease(float delta)
    {
        if (fading)
        {
            fadeTime += delta;
            float u = fadeTime / releaseFadeDuration;
            baseAlpha = Smooth(fadeFrom, 0f, u);
            if (u >= 1f)
            {
                baseAlpha = 0f;
                EnterIdle();
            }

            return;
        }

        float previous = phase;
        AdvancePhase(delta);
        BlendTowardBreath(delta);
        if (seenPeak && Mathf.Floor(phase) > Mathf.Floor(previous))
        {
            phase = Mathf.Floor(phase);
            baseAlpha = BreathValue();
            BeginFade();
        }
    }

    private void TickPending(float delta)
    {
        if (!pending)
        {
            return;
        }

        pendingTime += delta;
        if (focusDrag == null || !focusDrag.IsCaughtOn(pendingLayer))
        {
            pending = false;
            return;
        }

        if (pendingTime < settleDuration)
        {
            return;
        }

        pending = false;
        BeginFlash();
    }

    private void TickFlash(float delta)
    {
        if (!flashing)
        {
            flashEnvelope = 0f;
            return;
        }

        if (flashTime < flashAttack)
        {
            flashEnvelope = flashAttack <= TroughEpsilon ? 1f : Smooth01(flashTime / flashAttack);
        }
        else
        {
            float u = (flashTime - flashAttack) / flashDuration;
            flashEnvelope = 1f - Smooth01(u);
            if (u >= 1f)
            {
                flashing = false;
                flashEnvelope = 0f;
            }
        }

        flashTime += delta;
    }

    private void OnPointerPressed()
    {
        pointerDown = true;
    }

    private void OnPointerReleased()
    {
        pointerDown = false;
        if (motion != Motion.Drag)
        {
            return;
        }

        motion = Motion.Release;
        float fraction = phase - Mathf.Floor(phase);
        if (seenPeak && fraction <= 0.03f)
        {
            BeginFade();
        }
    }

    private void OnDragBegan()
    {
        if (motion == Motion.Release)
        {
            float fraction = phase - Mathf.Floor(phase);
            seenPeak = fraction >= 0.5f;
        }
        else
        {
            phase = 0f;
            seenPeak = false;
        }

        engageFrom = baseAlpha;
        engageTime = 0f;
        engaging = true;
        fading = false;
        motion = Motion.Drag;
    }

    private void OnFocusSnapped(FocusLayer layer)
    {
        pending = true;
        pendingLayer = layer;
        pendingTime = 0f;
    }

    private void BeginFlash()
    {
        flashing = true;
        flashTime = flashEnvelope * flashAttack;
        FlashCount++;
        LastFlashedLayer = pendingLayer;
    }

    private void BeginFade()
    {
        fading = true;
        fadeFrom = baseAlpha;
        fadeTime = 0f;
    }

    private void EnterIdle()
    {
        motion = Motion.Idle;
        engaging = false;
        fading = false;
        seenPeak = false;
        idleBreathing = false;
        hadIdleBreath = false;
        idleWait = 0f;
        idleTime = 0f;
        baseAlpha = 0f;
    }

    private void AdvancePhase(float delta)
    {
        float previous = phase;
        float step = delta > 0.05f ? 0.05f : delta;
        phase += step / dragPeriod;
        if (Mathf.Floor(previous + 0.5f) != Mathf.Floor(phase + 0.5f))
        {
            seenPeak = true;
        }
    }

    private float BreathValue()
    {
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        return Mathf.Lerp(dragMin, dragMax, wave);
    }

    private void BlendTowardBreath(float delta)
    {
        float target = BreathValue();
        if (!engaging)
        {
            baseAlpha = target;
            return;
        }

        engageTime += delta;
        float u = Smooth01(engageTime / engageDuration);
        baseAlpha = Mathf.Lerp(engageFrom, target, u);
        if (engageTime >= engageDuration)
        {
            engaging = false;
            baseAlpha = target;
        }
    }

    private void Resolve()
    {
        if (viewfinder == null)
        {
            viewfinder = GetComponent<Graphic>();
        }

        if (focusDrag == null && transform.parent != null)
        {
            focusDrag = transform.parent.GetComponentInChildren<CameraFocusDrag>(true);
        }
    }

    private void Subscribe()
    {
        if (subscribed || focusDrag == null)
        {
            return;
        }

        focusDrag.PointerPressed += OnPointerPressed;
        focusDrag.PointerReleased += OnPointerReleased;
        focusDrag.DragBegan += OnDragBegan;
        focusDrag.FocusSnapped += OnFocusSnapped;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || focusDrag == null)
        {
            subscribed = false;
            return;
        }

        focusDrag.PointerPressed -= OnPointerPressed;
        focusDrag.PointerReleased -= OnPointerReleased;
        focusDrag.DragBegan -= OnDragBegan;
        focusDrag.FocusSnapped -= OnFocusSnapped;
        subscribed = false;
    }

    private void ApplyAlpha()
    {
        Color color = baseRgb;
        color.a = alpha;
        viewfinder.color = color;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Smooth(float from, float to, float t)
    {
        return Mathf.Lerp(from, to, Smooth01(t));
    }
}
