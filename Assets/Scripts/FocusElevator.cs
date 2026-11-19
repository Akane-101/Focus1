using UnityEngine;

[DefaultExecutionOrder(50)]
public sealed class FocusElevator : MonoBehaviour
{
    [SerializeField] private Transform car;
    [SerializeField] private Transform alignTarget;
    [SerializeField] private FocusLayer rideLayer = FocusLayer.Mid;
    [SerializeField] private float travelHeight = 5.7f;
    [SerializeField] private float period = 5.5f;
    [SerializeField] private float endPause = 0.4f;
    [SerializeField] [Range(0f, 1f)] private float stickStrength = 0.22f;

    private const float RideWalkSpeed = 6f;

    private FocusLevelState levelState;
    private Transform playerTransform;
    private Rigidbody2D playerBody;
    private PlayerMove playerMove;
    private Collider2D playerCollider;
    private Collider2D carCollider;
    private RigidbodyType2D playerBodyType = RigidbodyType2D.Dynamic;
    private Vector3 carStartPosition;
    private float bottomY;
    private float topY;
    private float motionStartTime;
    private bool isRiding;
    private bool seatOnBoard;
    private float carriedVelocityY;

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
        ApplyState();

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

    private void FixedUpdate()
    {
        BindPlayer();
        ApplyState();
        float previousY = car != null ? car.position.y : 0f;
        ApplyMotion();
        Physics2D.SyncTransforms();
        float deltaY = car != null ? car.position.y - previousY : 0f;
        float platformVelocityY = Time.fixedDeltaTime > 0f ? deltaY / Time.fixedDeltaTime : 0f;
        UpdateRide(platformVelocityY);
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

    private void BindPlayer()
    {
        if (levelState == null)
        {
            levelState = FindObjectOfType<FocusLevelState>();
        }

        if (playerTransform == null && levelState != null)
        {
            playerTransform = levelState.PlayerTransform;
        }

        if (playerTransform == null)
        {
            PlayerMove playerMove = FindObjectOfType<PlayerMove>();
            playerTransform = playerMove != null ? playerMove.transform : null;
        }

        if (playerTransform == null)
        {
            return;
        }

        if (playerBody == null)
        {
            playerBody = playerTransform.GetComponent<Rigidbody2D>();
        }

        if (playerMove == null)
        {
            playerMove = playerTransform.GetComponent<PlayerMove>();
        }

        if (playerCollider == null)
        {
            playerCollider = playerTransform.GetComponent<Collider2D>();
        }
    }

    private void UpdateRide(float platformVelocityY)
    {
        if (car == null || playerTransform == null || playerCollider == null || playerBody == null || carCollider == null)
        {
            StopRiding();
            return;
        }

        if (!CanUseElevator())
        {
            StopRiding();
            return;
        }

        float surfaceY = carCollider.bounds.max.y;
        if (!isRiding)
        {
            if (!IsPlayerOnCar(surfaceY))
            {
                return;
            }

            StartRiding();
        }

        RideOnCar(surfaceY, platformVelocityY);
    }

    private bool CanUseElevator()
    {
        return levelState != null && levelState.CurrentLayer == rideLayer && carCollider != null && carCollider.enabled;
    }

    private void StartRiding()
    {
        isRiding = true;
        playerBodyType = playerBody.bodyType;
        playerBody.bodyType = RigidbodyType2D.Kinematic;
        if (playerMove != null)
        {
            playerMove.SetCarried(this, true);
        }

        Physics2D.IgnoreCollision(playerCollider, carCollider, true);
        seatOnBoard = true;
    }

    private void RideOnCar(float surfaceY, float platformVelocityY)
    {
        if (playerBody.bodyType != RigidbodyType2D.Kinematic)
        {
            playerBody.bodyType = RigidbodyType2D.Kinematic;
        }

        float input = playerMove != null && playerMove.IsMovementLocked ? 0f : Input.GetAxisRaw("Horizontal");
        float nextX = playerBody.position.x + input * RideWalkSpeed * Time.fixedDeltaTime;
        bool seated = false;
        if (seatOnBoard)
        {
            nextX = SeatOntoCar(nextX);
            seatOnBoard = false;
            seated = true;
        }

        float overlap = HorizontalOverlap(nextX);
        if (overlap < 0.1f)
        {
            StopRiding();
            playerBody.velocity = new Vector2(input * RideWalkSpeed, platformVelocityY);
            return;
        }

        float feetOffset = playerCollider.bounds.min.y - playerBody.position.y;
        float seatClearance = Mathf.Lerp(0.04f, 0.06f, stickStrength);
        float keptX = seated || Mathf.Abs(input) > 0.01f ? nextX : playerBody.position.x;
        Vector2 nextPosition = new Vector2(keptX, surfaceY + seatClearance - feetOffset);
        carriedVelocityY = platformVelocityY;
        playerBody.position = nextPosition;
        playerBody.velocity = new Vector2(input * RideWalkSpeed, platformVelocityY);
        playerTransform.position = new Vector3(nextPosition.x, nextPosition.y, playerTransform.position.z);
        Physics2D.SyncTransforms();
    }

    private float SeatOntoCar(float bodyX)
    {
        const float targetOverlap = 0.35f;
        if (HorizontalOverlap(bodyX) >= targetOverlap)
        {
            return bodyX;
        }

        float centerShift = playerCollider.bounds.center.x - playerBody.position.x;
        float centerX = bodyX + centerShift;
        float carMid = (carCollider.bounds.min.x + carCollider.bounds.max.x) * 0.5f;
        float direction = centerX < carMid ? 1f : -1f;
        float shifted = bodyX;
        for (int i = 0; i < 6; i++)
        {
            shifted += direction * 0.08f;
            if (HorizontalOverlap(shifted) >= targetOverlap)
            {
                return shifted;
            }
        }

        return shifted;
    }

    private float HorizontalOverlap(float bodyX)
    {
        float centerX = bodyX + playerCollider.bounds.center.x - playerBody.position.x;
        float extentsX = playerCollider.bounds.extents.x;
        Bounds carBounds = carCollider.bounds;
        return Mathf.Min(centerX + extentsX, carBounds.max.x) - Mathf.Max(centerX - extentsX, carBounds.min.x);
    }

    private void StopRiding()
    {
        if (!isRiding)
        {
            return;
        }

        isRiding = false;
        seatOnBoard = false;
        carriedVelocityY = 0f;
        if (playerCollider != null && carCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, carCollider, false);
        }

        if (playerMove != null)
        {
            playerMove.SetCarried(this, false);
        }

        if (playerBody != null)
        {
            playerBody.bodyType = playerBodyType;
        }
    }

    private bool IsPlayerOnCar(float surfaceY)
    {
        if (!CanUseElevator())
        {
            return false;
        }

        if (HorizontalOverlap(playerBody.position.x) < 0.08f)
        {
            return false;
        }

        float gap = playerCollider.bounds.min.y - surfaceY;
        return gap >= -0.55f && gap <= 0.4f;
    }
}
