using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusGate : MonoBehaviour
{
    [SerializeField] private float passThreshold = 0.15f;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D gateCollider;
    private bool hasAdvancedStage;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        gateCollider = GetComponent<Collider2D>();

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
        TryAdvanceStage();
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }
    }

    private void TryAdvanceStage()
    {
        if (levelState == null || hasAdvancedStage || levelState.CurrentStage != FocusStage.OpenGate || !IsPassable())
        {
            return;
        }

        Transform playerTransform = levelState.PlayerTransform;

        if (playerTransform != null && playerTransform.position.x <= transform.position.x - passThreshold)
        {
            hasAdvancedStage = true;
            levelState.AdvanceStage(FocusStage.BuildBridge);
        }
    }

    private void ApplyState()
    {
        if (levelState == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(FocusLayer.Near);
        gateCollider.enabled = !IsPassable();
    }

    private bool IsPassable()
    {
        return levelState.CurrentLayer != FocusLayer.Mid;
    }
}
