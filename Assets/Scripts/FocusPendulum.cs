using UnityEngine;

public sealed class FocusPendulum : MonoBehaviour
{
    [SerializeField] private Transform bob;
    [SerializeField] private FocusLayer rideLayer = FocusLayer.Far;
    [SerializeField] private bool ignoreFocusLayer;
    [SerializeField] private float maxAngle = 32f;
    [SerializeField] private float period = 4.2f;
    [SerializeField] private float rideWalkSpeed = 4.5f;

    private FocusLevelState levelState;
    private Transform playerTransform;
    private Rigidbody2D playerBody;
    private Collider2D playerCollider;
    private PlayerMove playerMove;
    private Transform seat;
    private Collider2D seatCollider;
    private Vector3 bobRestPosition;
    private Vector3 bobPivot;
    private bool isRiding;
    private float rideLocalX;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();

        if (bob == null)
        {
            Transform found = transform.Find("Bob");
            bob = found != null ? found : transform;
        }

        bobRestPosition = bob.position;
        SpriteRenderer bobRenderer = bob.GetComponent<SpriteRenderer>();
        float topY = bobRenderer != null ? bobRenderer.bounds.max.y : bob.position.y;
        bobPivot = new Vector3(bob.position.x, topY, bob.position.z);
        seatCollider = bob.GetComponentInChildren<Collider2D>();
        seat = seatCollider != null ? seatCollider.transform : bob;

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
            ApplyState();
        }
    }

    private void Start()
    {
        if (levelState == null)
        {
            return;
        }

        playerTransform = levelState.PlayerTransform;
        if (playerTransform == null)
        {
            return;
        }

        playerBody = playerTransform.GetComponent<Rigidbody2D>();
        playerCollider = playerTransform.GetComponent<Collider2D>();
        playerMove = playerTransform.GetComponent<PlayerMove>();
        ApplyState();
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }

        StopRiding();
    }

    private void ApplyState()
    {
        if (seatCollider == null || levelState == null)
        {
            return;
        }

        bool canRide = ignoreFocusLayer || levelState.CurrentLayer == rideLayer;
        seatCollider.enabled = canRide;
        if (!canRide)
        {
            StopRiding();
        }
    }

    private void LateUpdate()
    {
        ApplySwing();
        UpdateRide();
    }

    private void ApplySwing()
    {
        transform.localRotation = Quaternion.identity;

        if (bob == null)
        {
            return;
        }

        float wave = period <= 0.01f ? 0f : (Time.time * (Mathf.PI * 2f / period));
        float angle = maxAngle * Mathf.Sin(wave);
        Quaternion swing = Quaternion.Euler(0f, 0f, angle);
        bob.position = bobPivot + swing * (bobRestPosition - bobPivot);
        bob.rotation = swing;
    }

    private void UpdateRide()
    {
        if (seat == null || playerTransform == null || playerCollider == null)
        {
            return;
        }

        if (isRiding)
        {
            if (!CanKeepRiding())
            {
                StopRiding();
                return;
            }

            float input = playerMove != null && playerMove.IsStunned ? 0f : Input.GetAxisRaw("Horizontal");
            float nextLocalX = rideLocalX + input * rideWalkSpeed * Time.deltaTime;
            float halfWidth = seatCollider.bounds.extents.x * 0.85f;

            if (Mathf.Abs(nextLocalX) > halfWidth && Mathf.Abs(input) > 0.01f && Mathf.Sign(nextLocalX) == Mathf.Sign(input))
            {
                StopRiding();
                if (playerBody != null)
                {
                    playerBody.velocity = new Vector2(input * rideWalkSpeed, playerBody.velocity.y);
                }

                return;
            }

            rideLocalX = Mathf.Clamp(nextLocalX, -halfWidth, halfWidth);
            SnapPlayerToSeat();
            return;
        }

        if (CanStartRiding())
        {
            StartRiding();
        }
    }

    private void StartRiding()
    {
        isRiding = true;
        rideLocalX = playerTransform.position.x - seat.position.x;
        if (playerMove != null)
        {
            playerMove.SetCarried(this, true);
        }

        if (playerBody != null && playerCollider != null && seatCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, seatCollider, true);
        }

        SnapPlayerToSeat();
    }

    private void StopRiding()
    {
        if (!isRiding)
        {
            return;
        }

        isRiding = false;
        if (playerMove != null)
        {
            playerMove.SetCarried(this, false);
        }

        if (playerCollider != null && seatCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, seatCollider, false);
        }
    }

    private void SnapPlayerToSeat()
    {
        float playerHalfHeight = playerCollider.bounds.extents.y;
        Vector2 nextPosition = new Vector2(
            seat.position.x + rideLocalX,
            seatCollider.bounds.max.y + playerHalfHeight - 0.02f);

        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.position = nextPosition;
        }

        playerTransform.position = new Vector3(nextPosition.x, nextPosition.y, playerTransform.position.z);
    }

    private bool CanStartRiding()
    {
        return IsPlayerOverSeat(0.35f, 0.2f);
    }

    private bool CanKeepRiding()
    {
        return IsPlayerOverSeat(0.7f, 0.45f);
    }

    private bool IsPlayerOverSeat(float belowTop, float aboveTop)
    {
        if (levelState == null || seatCollider == null || !seatCollider.enabled)
        {
            return false;
        }

        if (!ignoreFocusLayer && levelState.CurrentLayer != rideLayer)
        {
            return false;
        }

        Bounds seatBounds = seatCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        bool overlapX = playerBounds.max.x > seatBounds.min.x && playerBounds.min.x < seatBounds.max.x;
        float feetY = playerBounds.min.y;
        bool onTop = feetY >= seatBounds.max.y - belowTop && feetY <= seatBounds.max.y + aboveTop;
        return overlapX && onTop;
    }
}
