using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerChangeLayer : MonoBehaviour
{
    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private PlayerMove playerMove;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerMove = GetComponent<PlayerMove>();

    }

    private void OnEnable()
    {
        if (levelState != null)
        {
            levelState.StateChanged += ApplySortingOrder;
        }
    }

    private void Start()
    {
        if (levelState == null)
        {
            levelState = FindObjectOfType<FocusLevelState>();
            if (levelState != null)
            {
                enabled = true;
                levelState.StateChanged += ApplySortingOrder;
            }
        }

        ApplySortingOrder();
    }

    private void Update()
    {
        if (levelState == null)
        {
            levelState = FindObjectOfType<FocusLevelState>();
            if (levelState == null)
            {
                return;
            }

            levelState.StateChanged += ApplySortingOrder;
        }

        if (playerMove != null && playerMove.IsStunned)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            levelState.SetLayer(FocusLayer.Near);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            levelState.SetLayer(FocusLayer.Mid);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            levelState.SetLayer(FocusLayer.Far);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            levelState.SetLayer(GetPreviousLayer(levelState.CurrentLayer));
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            levelState.SetLayer(GetNextLayer(levelState.CurrentLayer));
        }
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplySortingOrder;
        }
    }

    private void ApplySortingOrder()
    {
        if (levelState == null || spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder = levelState.GetSortingOrder(levelState.CurrentLayer, FocusLevelState.PlayerOrderOffset);
    }

    private static FocusLayer GetPreviousLayer(FocusLayer currentLayer)
    {
        switch (currentLayer)
        {
            case FocusLayer.Far:
                return FocusLayer.Mid;
            case FocusLayer.Mid:
                return FocusLayer.Near;
            default:
                return FocusLayer.Far;
        }
    }

    private static FocusLayer GetNextLayer(FocusLayer currentLayer)
    {
        switch (currentLayer)
        {
            case FocusLayer.Far:
                return FocusLayer.Near;
            case FocusLayer.Mid:
                return FocusLayer.Far;
            default:
                return FocusLayer.Mid;
        }
    }
}
