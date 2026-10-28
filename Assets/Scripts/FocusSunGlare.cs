using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusSunGlare : MonoBehaviour
{
    [SerializeField] private bool requireDoorBlown = true;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D sunCollider;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        sunCollider = GetComponent<Collider2D>();
        sunCollider.isTrigger = true;

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

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }
    }

    public bool Covers(Collider2D other)
    {
        if (other == null || sunCollider == null || !sunCollider.enabled || !IsLightActive())
        {
            return false;
        }

        return CoversPoint(other.bounds.center);
    }

    public bool CoversPoint(Vector2 point)
    {
        if (sunCollider == null || !sunCollider.enabled || !IsLightActive())
        {
            return false;
        }

        return sunCollider.OverlapPoint(point);
    }

    private void ApplyState()
    {
        if (levelState == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(FocusLayer.Mid, FocusSortOrder.Resolve(transform, -1));
        sunCollider.enabled = IsLightActive();
    }

    private bool IsLightActive()
    {
        return levelState != null && (!requireDoorBlown || levelState.CurrentStage >= FocusStage.DoorBlown);
    }
}
