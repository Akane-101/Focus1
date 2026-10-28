using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class FocusSlope : MonoBehaviour
{
    [SerializeField] private FocusLayer walkLayer = FocusLayer.Far;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D slopeCollider;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        slopeCollider = GetComponent<PolygonCollider2D>();
        slopeCollider.isTrigger = false;

        if (slopeCollider.sharedMaterial == null)
        {
            PhysicsMaterial2D material = new PhysicsMaterial2D("FocusSlopeNoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
            slopeCollider.sharedMaterial = material;
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

        bool isCurrentLayer = levelState.CurrentLayer == walkLayer;
        int unityLayer = FocusUnityLayers.GetIndex(walkLayer);
        if (unityLayer >= 0)
        {
            gameObject.layer = unityLayer;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(walkLayer);
        slopeCollider.enabled = isCurrentLayer;
    }
}
