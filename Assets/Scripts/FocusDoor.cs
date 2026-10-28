using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusDoor : MonoBehaviour
{
    [SerializeField] private float enterDistance = 3.5f;
    [SerializeField] private bool requireCurrentLayer;
    [SerializeField] private FocusLayer requiredLayer = FocusLayer.Far;
    [SerializeField] private float minEnterY = -999f;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D doorCollider;
    private bool stateBound;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        doorCollider = GetComponent<Collider2D>();
        doorCollider.isTrigger = true;
        BindLevelState();
    }

    private void OnEnable()
    {
        BindLevelState();
    }

    private void Start()
    {
        BindLevelState();
        ApplyState();
    }

    private void Update()
    {
        BindLevelState();
        if (levelState != null)
        {
            levelState.TryEnterDoor(transform, enterDistance, CanEnter());
        }
    }

    public bool IsEnterReady()
    {
        BindLevelState();
        return CanEnter() && FocusLevelExit.IsPlayerAt(transform, levelState != null ? levelState.PlayerTransform : null, enterDistance);
    }

    private void OnDisable()
    {
        UnbindLevelState();
    }

    private void ApplyState()
    {
        if (levelState == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(FocusLayer.Far);
        doorCollider.enabled = true;
    }

    private void BindLevelState()
    {
        if (levelState == null)
        {
            levelState = FindObjectOfType<FocusLevelState>();
        }

        if (levelState != null && !stateBound)
        {
            levelState.StateChanged += ApplyState;
            stateBound = true;
        }
    }

    private void UnbindLevelState()
    {
        if (levelState != null && stateBound)
        {
            levelState.StateChanged -= ApplyState;
            stateBound = false;
        }
    }

    private bool CanEnter()
    {
        if (levelState == null || levelState.PlayerTransform == null)
        {
            return false;
        }

        // requireCurrentLayer is ignored: unfocused doors stay visible, so walking there is the gate.
        return PlayerEnterY() >= minEnterY;
    }

    private float PlayerEnterY()
    {
        Collider2D playerCollider = levelState.PlayerTransform.GetComponent<Collider2D>();
        return playerCollider != null ? playerCollider.bounds.center.y : levelState.PlayerTransform.position.y;
    }
}
