using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusWindDoor : MonoBehaviour
{
    [SerializeField] private float reachThreshold = 1.35f;
    [SerializeField] private float enterDistance = 3.5f;

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
        TryAdvanceFromWind();
        TryMarkReached();
        TryOpenForExit();
        if (levelState != null)
        {
            levelState.TryEnterDoor(transform, enterDistance, true);
        }
    }

    public bool IsEnterReady()
    {
        BindLevelState();
        return levelState != null && FocusLevelExit.IsPlayerAt(transform, levelState.PlayerTransform, enterDistance);
    }

    private void OnDisable()
    {
        UnbindLevelState();
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

    private void ApplyState()
    {
        if (levelState == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = levelState.GetSortingOrder(FocusLayer.Mid, FocusSortOrder.Resolve(transform, 0));
        doorCollider.enabled = true;
    }

    private void TryAdvanceFromWind()
    {
        if (levelState == null || levelState.CurrentStage != FocusStage.WaitFirstWind || levelState.CurrentLayer != FocusLayer.Mid || levelState.MidFocusCount < 1)
        {
            return;
        }

        levelState.AdvanceStage(FocusStage.DoorBlown);
    }

    private void TryMarkReached()
    {
        if (levelState == null || levelState.CurrentStage != FocusStage.LeafCollected || levelState.CurrentLayer != FocusLayer.Far)
        {
            return;
        }

        Transform playerTransform = levelState.PlayerTransform;

        if (playerTransform != null && playerTransform.position.x >= transform.position.x - reachThreshold)
        {
            levelState.AdvanceStage(FocusStage.ReachedDoor);
        }
    }

    private void TryOpenForExit()
    {
        if (levelState == null || levelState.CurrentStage != FocusStage.ReachedDoor || levelState.CurrentLayer != FocusLayer.Mid)
        {
            return;
        }

        levelState.AdvanceStage(FocusStage.ExitOpen);
    }
}
