using UnityEngine;

public sealed class FocusElevator : MonoBehaviour
{
    [SerializeField] private Transform car;
    [SerializeField] private Transform alignTarget;
    [SerializeField] private FocusLayer rideLayer = FocusLayer.Mid;
    [SerializeField] private float travelHeight = 5.7f;
    [SerializeField] private float period = 5.5f;
    [SerializeField] private float endPause = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float stickStrength = 0.22f;

    private FocusLevelState levelState;
    private Transform playerTransform;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private Collider2D carCollider;
    private Vector3 carStartPosition;
    private float bottomY;
    private float topY;
    private float motionStartTime;
    private bool isRiding;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        ResolveCar();
        carStartPosition = car.position;
        bottomY = carStartPosition.y;
        topY = bottomY + Mathf.Max(0.1f, travelHeight);

        if (levelState == null)
        {
            Debug.LogError("Missing FocusLevelState in scene.");
            enabled = false;
        }
    }

    private void Start()
    {
        AlignTravelRange();

        if (levelState == null || levelState.PlayerTransform == null)
        {
            return;
        }

        playerTransform = levelState.PlayerTransform;
        playerBody = playerTransform.GetComponent<Rigidbody2D>();
        playerCollider = playerTransform.GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (levelState != null)
        {
            levelState.StateChanged += ApplyState;
            ApplyState();
        }
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }

        StopRiding();
    }

    private void LateUpdate()
    {
        float previousY = car != null ? car.position.y : 0f;
        ApplyMotion();
        float deltaY = car != null ? car.position.y - previousY : 0f;
        UpdateRide(deltaY);
    }

    private void ResolveCar()
    {
        if (car == null)
        {
            Transform found = transform.Find("ElevatorCar");
            if (found == null)
            {
                found = transform.Find("Square (1)");
            }

            car = found != null ? found : transform;
        }

        carCollider = car.GetComponent<Collider2D>();
        if (carCollider == null)
        {
            carCollider = car.GetComponentInChildren<Collider2D>();
        }

        if (carCollider != null)
        {
            car = carCollider.transform;
        }
    }

    private void AlignTravelRange()
    {
        if (car == null)
        {
            return;
        }

        carStartPosition = car.position;
        float placedY = car.position.y;
        float topOffset = carCollider != null ? carCollider.bounds.max.y - car.position.y : 0f;

        SpriteRenderer shaft = FindShaftRenderer();
        if (shaft != null)
        {
            bottomY = shaft.bounds.min.y + topOffset;
            topY = Mathf.Max(bottomY + 0.1f, shaft.bounds.max.y - topOffset);
        }
        else
        {
            Collider2D groundCollider = FindNamedCollider("Ground");
            if (groundCollider != null && groundCollider.bounds.max.y <= placedY + 0.25f)
            {
                bottomY = groundCollider.bounds.max.y - topOffset;
            }
            else
            {
                bottomY = placedY;
            }

            if (alignTarget == null)
            {
                GameObject catchPlatform = GameObject.Find("Platform_FarCatch");
                if (catchPlatform != null)
                {
                    alignTarget = catchPlatform.transform;
                }
            }

            float targetTop = GetTopY(alignTarget);
            if (!float.IsNaN(targetTop) && targetTop > bottomY + topOffset + 0.1f)
            {
                topY = targetTop - topOffset;
            }
            else
            {
                topY = bottomY + Mathf.Max(0.1f, travelHeight);
            }
        }

        float startT = Mathf.InverseLerp(bottomY, topY, placedY);
        motionStartTime = Time.time - GetCycleTimeForT(startT);
    }

    private SpriteRenderer FindShaftRenderer()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == car || child.name != "BG")
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private static Collider2D FindNamedCollider(string objectName)
    {
        GameObject namedObject = GameObject.Find(objectName);
        return namedObject != null ? namedObject.GetComponent<Collider2D>() : null;
    }

    private float GetCycleTimeForT(float rideT)
    {
        float moveTime = Mathf.Max(0.1f, period);
        float halfMove = moveTime * 0.5f;
        float t = Mathf.Clamp01(rideT);

        if (t <= 0.001f)
        {
            return 0f;
        }

        if (t >= 0.999f)
        {
            return endPause + halfMove;
        }

        return endPause + InverseSmoothStep(t) * halfMove;
    }

    private static float InverseSmoothStep(float value)
    {
        float y = Mathf.Clamp01(value);
        return 0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * y) / 3f);
    }

    private static float GetTopY(Transform target)
    {
        if (target == null)
        {
            return float.NaN;
        }

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.max.y;
        }

        SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
        return targetRenderer != null ? targetRenderer.bounds.max.y : target.position.y;
    }

    private void ApplyState()
    {
        if (carCollider == null || levelState == null)
        {
            return;
        }

        carCollider.enabled = levelState.CurrentLayer == rideLayer;
    }

    private void ApplyMotion()
    {
        if (car == null)
        {
            return;
        }

        float y = Mathf.Lerp(bottomY, topY, GetRideT());
        car.position = new Vector3(carStartPosition.x, y, carStartPosition.z);
    }

    private float GetRideT()
    {
        float moveTime = Mathf.Max(0.1f, period);
        float halfMove = moveTime * 0.5f;
        float cycle = moveTime + endPause * 2f;
        float time = Mathf.Repeat(Mathf.Max(0f, Time.time - motionStartTime), cycle);

        if (time <= endPause)
        {
            return 0f;
        }

        time -= endPause;
        if (time <= halfMove)
        {
            return Mathf.SmoothStep(0f, 1f, time / halfMove);
        }

        time -= halfMove;
        if (time <= endPause)
        {
            return 1f;
        }

        time -= endPause;
        return 1f - Mathf.SmoothStep(0f, 1f, time / halfMove);
    }

    private void UpdateRide(float deltaY)
    {
        if (car == null || playerTransform == null || playerCollider == null || carCollider == null)
        {
            return;
        }

        if (IsPlayerOnCar(0.28f, 0.18f))
        {
            isRiding = true;
            FollowCar(deltaY);
            return;
        }

        isRiding = false;
    }

    private void FollowCar(float deltaY)
    {
        float standY = carCollider.bounds.max.y + playerCollider.bounds.extents.y - 0.02f;
        Vector2 nextPosition = playerBody != null ? playerBody.position : (Vector2)playerTransform.position;
        nextPosition.y += deltaY;
        nextPosition.y = Mathf.Lerp(nextPosition.y, standY, stickStrength);

        if (playerBody != null)
        {
            if (playerBody.velocity.y < 0f)
            {
                playerBody.velocity = new Vector2(playerBody.velocity.x, 0f);
            }

            playerBody.position = nextPosition;
        }

        playerTransform.position = new Vector3(nextPosition.x, nextPosition.y, playerTransform.position.z);
    }

    private void StopRiding()
    {
        isRiding = false;
    }

    private bool IsPlayerOnCar(float belowTop, float aboveTop)
    {
        if (levelState == null || levelState.CurrentLayer != rideLayer || carCollider == null || !carCollider.enabled)
        {
            return false;
        }

        Bounds carBounds = carCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        bool overlapX = playerBounds.max.x > carBounds.min.x && playerBounds.min.x < carBounds.max.x;
        float feetY = playerBounds.min.y;
        bool onTop = feetY >= carBounds.max.y - belowTop && feetY <= carBounds.max.y + aboveTop;
        return overlapX && onTop;
    }
}
