using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public sealed class FocusLockedDoor : MonoBehaviour
{
    [SerializeField] private float enterDistance = 3.5f;
    [SerializeField] private bool requireCurrentLayer;
    [SerializeField] private FocusLayer requiredLayer = FocusLayer.Far;
    [SerializeField] private float minEnterY = -999f;
    [SerializeField] private bool requireKey;
    [SerializeField] private FocusKey requiredKey;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D doorCollider;
    private bool stateBound;
    private bool gatesOnKey;

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
        PairWithKeys();
        ApplyState();
    }

    private void Update()
    {
        BindLevelState();
        ApplyPassage();
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
        ApplyPassage();
    }

    private void ApplyPassage()
    {
        if (doorCollider == null)
        {
            return;
        }

        doorCollider.enabled = true;
        doorCollider.isTrigger = true;
    }

    private static void PairWithKeys()
    {
        FocusKey[] keys = FindObjectsOfType<FocusKey>(true);
        FocusLockedDoor[] doors = FindObjectsOfType<FocusLockedDoor>();
        bool anyKey = false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] != null)
            {
                anyKey = true;
                break;
            }
        }

        bool[] used = new bool[keys.Length];
        for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
        {
            FocusLockedDoor door = doors[doorIndex];
            if (door == null || door.requiredKey == null)
            {
                continue;
            }

            door.gatesOnKey = true;
            MarkKeyUsed(keys, used, door.requiredKey);
        }

        while (true)
        {
            int bestDoor = -1;
            int bestKey = -1;
            float bestDistance = float.MaxValue;
            for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
            {
                FocusLockedDoor door = doors[doorIndex];
                if (door == null || door.requiredKey != null)
                {
                    continue;
                }

                if (!door.requireKey && !anyKey)
                {
                    continue;
                }

                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    if (keys[keyIndex] == null || used[keyIndex])
                    {
                        continue;
                    }

                    float distance = (door.transform.position - keys[keyIndex].transform.position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestDoor = doorIndex;
                        bestKey = keyIndex;
                    }
                }
            }

            if (bestDoor < 0)
            {
                break;
            }

            doors[bestDoor].requiredKey = keys[bestKey];
            doors[bestDoor].gatesOnKey = true;
            used[bestKey] = true;
        }

        for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
        {
            if (doors[doorIndex] != null && doors[doorIndex].requireKey)
            {
                doors[doorIndex].gatesOnKey = true;
            }
        }
    }

    private static void MarkKeyUsed(FocusKey[] keys, bool[] used, FocusKey key)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == key)
            {
                used[i] = true;
                return;
            }
        }
    }

    private bool HasRequiredKey()
    {
        return requiredKey != null && requiredKey.IsCollected;
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

        if (gatesOnKey && !HasRequiredKey())
        {
            return false;
        }

        Collider2D playerCollider = levelState.PlayerTransform.GetComponent<Collider2D>();
        float playerY = playerCollider != null ? playerCollider.bounds.center.y : levelState.PlayerTransform.position.y;
        return playerY >= minEnterY;
    }
}
