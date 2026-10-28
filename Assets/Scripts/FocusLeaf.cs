using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusLeaf : MonoBehaviour
{
    public static int HiddenCollectedCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCollectedCount()
    {
        HiddenCollectedCount = 0;
    }

    [SerializeField] private Vector3 firstBlowLocalPosition = new Vector3(2.2f, -0.4f, -0.03f);
    [SerializeField] private Vector3 secondBlowLocalPosition = new Vector3(-3.2f, -2.2f, -0.03f);
    [SerializeField] private float blowDuration = 0.65f;
    [SerializeField] private float collectDistance = 1.6f;
    [SerializeField] private KeyCode collectKey = KeyCode.F;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D leafCollider;
    private Vector3 startLocalPosition;
    private Vector3 fromLocalPosition;
    private Vector3 toLocalPosition;
    private float blowTime = 1f;
    private bool isCollected;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        leafCollider = GetComponent<Collider2D>();
        startLocalPosition = transform.localPosition;
        fromLocalPosition = startLocalPosition;
        toLocalPosition = startLocalPosition;
        leafCollider.isTrigger = true;
        leafCollider.enabled = true;

        if (levelState == null)
        {
            Debug.LogError("Missing FocusLevelState in scene.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (levelState != null)
        {
            levelState.StateChanged += ApplyState;
        }
    }

    private void Start()
    {
        ApplyState();
    }

    private void Update()
    {
        TryAdvanceToPlayer();
        AnimateBlow();
        TryCollect();
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }
    }

    private void ApplyState()
    {
        if (levelState == null || isCollected)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(FocusLayer.Mid, FocusSortOrder.Resolve(transform, 1));
        leafCollider.enabled = true;
        BeginBlow(GetTargetLocalPosition());
    }

    private void TryAdvanceToPlayer()
    {
        if (isCollected || levelState == null || levelState.CurrentStage != FocusStage.DoorBlown || levelState.CurrentLayer != FocusLayer.Mid || levelState.MidFocusCount < 2)
        {
            return;
        }

        levelState.AdvanceStage(FocusStage.LeafReady);
    }

    private void AnimateBlow()
    {
        if (isCollected || blowTime >= 1f)
        {
            return;
        }

        blowTime = blowDuration <= 0.01f ? 1f : Mathf.Clamp01(blowTime + Time.deltaTime / blowDuration);
        float t = blowTime * blowTime * (3f - 2f * blowTime);
        transform.localPosition = Vector3.Lerp(fromLocalPosition, toLocalPosition, t);
    }

    private void TryCollect()
    {
        if (isCollected || levelState == null || levelState.CurrentStage < FocusStage.LeafReady || !Input.GetKeyDown(collectKey) || FocusLevelExit.IsAnyDoorReady(levelState))
        {
            return;
        }

        Transform playerTransform = levelState.PlayerTransform;

        if (playerTransform == null || Vector2.Distance(playerTransform.position, transform.position) > collectDistance)
        {
            return;
        }

        isCollected = true;
        HiddenCollectedCount++;
        spriteRenderer.enabled = false;
        leafCollider.enabled = false;
        levelState.AdvanceStage(FocusStage.LeafCollected);
    }

    private void BeginBlow(Vector3 nextLocalPosition)
    {
        if ((toLocalPosition - nextLocalPosition).sqrMagnitude < 0.0001f)
        {
            return;
        }

        fromLocalPosition = transform.localPosition;
        toLocalPosition = nextLocalPosition;
        blowTime = 0f;
    }

    private Vector3 GetTargetLocalPosition()
    {
        if (levelState.CurrentStage >= FocusStage.LeafReady)
        {
            return secondBlowLocalPosition;
        }

        if (levelState.CurrentStage >= FocusStage.DoorBlown)
        {
            return firstBlowLocalPosition;
        }

        return startLocalPosition;
    }
}
