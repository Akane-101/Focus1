using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class FocusKey : MonoBehaviour
{
    [SerializeField] private FocusLayer requiredLayer = FocusLayer.Far;
    [SerializeField] private float collectDistance = 1.1f;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private bool isCollected;
    private bool canCollect;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        Collider2D keyCollider = GetComponent<Collider2D>();
        if (keyCollider != null)
        {
            Destroy(keyCollider);
        }

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.velocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

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
        TryCollect();
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }
    }

    private void TryCollect()
    {
        if (!canCollect || isCollected || levelState == null || levelState.PlayerTransform == null)
        {
            return;
        }

        if (Vector2.Distance(transform.position, levelState.PlayerTransform.position) > collectDistance)
        {
            return;
        }

        isCollected = true;
        spriteRenderer.enabled = false;
        levelState.AdvanceStage(FocusStage.HasKey);
    }

    private void ApplyState()
    {
        if (levelState == null || isCollected)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = 40;
    }

    public void SetCollectable(bool collectable)
    {
        canCollect = collectable;
        if (collectable)
        {
            DetachFromFocusLayers();
        }
    }

    public void DetachFromFocusLayers()
    {
        transform.SetParent(null, true);
        FocusUnityLayers.Assign(transform, 0);

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = !isCollected;
            spriteRenderer.sortingOrder = 40;
        }
    }

    private void OnValidate()
    {
        collectDistance = Mathf.Max(0.1f, collectDistance);
    }
}
