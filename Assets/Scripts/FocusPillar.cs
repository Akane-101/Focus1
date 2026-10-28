using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusPillar : MonoBehaviour
{
    [SerializeField] private FocusLayer blockingLayer = FocusLayer.Near;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D pillarCollider;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        pillarCollider = GetComponent<Collider2D>();
        pillarCollider.isTrigger = false;

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

    private void ApplyState()
    {
        if (levelState == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(blockingLayer, FocusSortOrder.Resolve(transform, 1));
        pillarCollider.enabled = levelState.CurrentLayer == blockingLayer;
    }
}
