using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class FocusIce : MonoBehaviour
{
    [SerializeField] private FocusLayer pushLayer = FocusLayer.Mid;
    [SerializeField] private GameObject keyObject;
    [SerializeField] private float meltDuration = 1.1f;
    [SerializeField] private float interactDistance = 1.6f;
    [SerializeField] private float holdOffsetX = 1.05f;
    [SerializeField] private float fallSpeed = 12f;
    [SerializeField] private bool anchorToWorldOnPickup;

    private FocusLevelState levelState;
    private SpriteRenderer spriteRenderer;
    private Collider2D iceCollider;
    private Rigidbody2D body;
    private Vector3 restScale;
    private Vector3 startPosition;
    private Color restColor;
    private bool isMelting;
    private float meltTime;
    private bool isHeld;
    private bool anchoredToWorld;
    private bool pendingFall;
    private float holdSide = 1f;
    private Collider2D playerCollider;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

    public bool IsHeld
    {
        get { return isHeld; }
    }

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        iceCollider = GetComponent<Collider2D>();
        body = GetComponent<Rigidbody2D>();
        restScale = transform.localScale;
        restColor = spriteRenderer.color;
        startPosition = transform.position;

        body.freezeRotation = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.velocity = Vector2.zero;
        iceCollider.isTrigger = false;
        EmbedKey();

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
        if (isMelting)
        {
            StepMelt();
            return;
        }

        if (Input.GetKeyDown(KeyCode.F) && !FocusLevelExit.IsAnyDoorReady(levelState))
        {
            if (isHeld)
            {
                ReleaseHold();
            }
            else if (CanInteract())
            {
                BeginHold();
            }
        }

        if (IsInSunlight())
        {
            BeginMelt();
        }
    }

    private void OnDisable()
    {
        if (levelState != null)
        {
            levelState.StateChanged -= ApplyState;
        }

        ReleaseHold();
    }

    private void ApplyState()
    {
        if (levelState == null || isMelting)
        {
            return;
        }

        spriteRenderer.enabled = true;
        iceCollider.enabled = anchoredToWorld || isHeld || levelState.CurrentLayer == HomeLayer();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.velocity = Vector2.zero;
        ApplyHeldLayer();
        KeepKeyOnIce();
    }

    private void ApplyHeldLayer()
    {
        if (anchoredToWorld)
        {
            ApplyWorldLayer();
            return;
        }

        if (isHeld)
        {
            int playerLayer = LayerMask.NameToLayer(FocusUnityLayers.Player);
            if (playerLayer >= 0)
            {
                FocusUnityLayers.Assign(transform, playerLayer);
                if (keyObject != null)
                {
                    FocusUnityLayers.Assign(keyObject.transform, playerLayer);
                }
            }

            spriteRenderer.sortingOrder = levelState.GetSortingOrder(levelState.CurrentLayer, FocusLevelState.PlayerOrderOffset);
            return;
        }

        FocusLayer homeLayer = HomeLayer();
        FocusUnityLayers.Assign(transform, homeLayer);
        if (keyObject != null)
        {
            FocusUnityLayers.Assign(keyObject.transform, homeLayer);
        }

        spriteRenderer.sortingOrder = levelState.GetSortingOrder(homeLayer, 1);
    }

    private void ApplyWorldLayer()
    {
        FocusUnityLayers.Assign(transform, 0);
        spriteRenderer.sortingOrder = 1;

        if (keyObject == null)
        {
            return;
        }

        FocusUnityLayers.Assign(keyObject.transform, 0);
    }

    private void AnchorToWorld()
    {
        if (anchoredToWorld)
        {
            return;
        }

        anchoredToWorld = true;
        DetachFromParent(transform);

        if (keyObject != null)
        {
            DetachFromParent(keyObject.transform);
        }

        ApplyWorldLayer();
    }

    private static void DetachFromParent(Transform target)
    {
        Vector3 worldPosition = target.position;
        target.SetParent(null, true);
        target.position = worldPosition;
    }

    private FocusLayer HomeLayer()
    {
        FocusLayerView view = GetComponentInParent<FocusLayerView>();
        if (view != null)
        {
            return view.RepresentedLayer;
        }

        return pushLayer;
    }

    private void LateUpdate()
    {
        if (isHeld)
        {
            FollowPlayer();
        }
        else if (!isMelting && pendingFall)
        {
            ApplyFall();
        }

        if (!isMelting)
        {
            KeepKeyOnIce();
        }
    }

    private bool CanInteract()
    {
        if (levelState == null || levelState.PlayerTransform == null)
        {
            return false;
        }

        if (!anchoredToWorld && levelState.CurrentLayer != HomeLayer())
        {
            return false;
        }

        PlayerMove playerMove = levelState.PlayerTransform.GetComponent<PlayerMove>();
        if (playerMove != null && playerMove.IsStunned)
        {
            return false;
        }

        return IsPlayerInRange(interactDistance);
    }

    private bool IsPlayerInRange(float distance)
    {
        Transform player = levelState.PlayerTransform;
        if (player == null || iceCollider == null)
        {
            return false;
        }

        Vector2 playerPos = player.position;
        bool wasEnabled = iceCollider.enabled;
        iceCollider.enabled = true;
        Vector2 closest = iceCollider.ClosestPoint(playerPos);
        iceCollider.enabled = wasEnabled;
        return Vector2.Distance(playerPos, closest) <= distance;
    }

    private void BeginHold()
    {
        Transform player = levelState.PlayerTransform;
        if (player == null)
        {
            return;
        }

        isHeld = true;
        pendingFall = false;
        if (anchorToWorldOnPickup)
        {
            AnchorToWorld();
        }
        holdSide = Mathf.Sign(transform.position.x - player.position.x);
        if (Mathf.Abs(holdSide) < 0.5f)
        {
            SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
            holdSide = playerRenderer != null && playerRenderer.flipX ? -1f : 1f;
        }

        playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null && iceCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, iceCollider, true);
        }

        iceCollider.enabled = true;
        ApplyHeldLayer();
        FollowPlayer();
    }

    public void ForceRelease()
    {
        if (isHeld)
        {
            ReleaseHold();
        }
    }

    private void ReleaseHold()
    {
        if (!isHeld)
        {
            return;
        }

        isHeld = false;
        pendingFall = true;
        if (playerCollider != null && iceCollider != null)
        {
            Physics2D.IgnoreCollision(playerCollider, iceCollider, false);
        }

        ApplyState();
    }

    private void FollowPlayer()
    {
        Transform player = levelState != null ? levelState.PlayerTransform : null;
        if (player == null)
        {
            return;
        }

        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            holdSide = playerRenderer.flipX ? -1f : 1f;
        }

        Vector3 nextPosition = player.position;
        nextPosition.x += holdSide * holdOffsetX;
        nextPosition.z = transform.position.z;
        transform.position = nextPosition;
    }

    private void ApplyFall()
    {
        if (iceCollider == null || !iceCollider.enabled)
        {
            return;
        }

        float probe = fallSpeed * Time.deltaTime + 0.06f;
        int hitCount = iceCollider.Cast(Vector2.down, castHits, probe);
        float landDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            if (!IsSolidObstacle(hit) || hit.normal.y < 0.4f)
            {
                continue;
            }

            landDistance = Mathf.Min(landDistance, hit.distance);
        }

        if (landDistance < float.MaxValue)
        {
            if (landDistance > 0.01f)
            {
                transform.position += Vector3.down * landDistance;
            }

            pendingFall = false;
            return;
        }

        transform.position += Vector3.down * fallSpeed * Time.deltaTime;
    }

    private bool IsSolidObstacle(RaycastHit2D hit)
    {
        if (hit.collider == null || hit.collider == iceCollider || hit.collider.isTrigger)
        {
            return false;
        }

        return levelState == null ||
               levelState.PlayerTransform == null ||
               hit.collider.transform != levelState.PlayerTransform;
    }

    private bool IsInSunlight()
    {
        FocusSunGlare[] lights = FindObjectsOfType<FocusSunGlare>();

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].Covers(iceCollider))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginMelt()
    {
        ReleaseHold();
        isMelting = true;
        meltTime = 0f;
        iceCollider.enabled = false;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.velocity = Vector2.zero;
        DetachFromFocusLayers();
    }

    private void DetachFromFocusLayers()
    {
        Vector3 icePosition = transform.position;
        transform.SetParent(null, true);
        transform.position = icePosition;
        FocusUnityLayers.Assign(transform, 0);

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = 40;
        }

        if (keyObject == null)
        {
            return;
        }

        FocusKey key = keyObject.GetComponent<FocusKey>();
        if (key != null)
        {
            key.DetachFromFocusLayers();
            return;
        }

        keyObject.transform.SetParent(null, true);
        FocusUnityLayers.Assign(keyObject.transform, 0);
    }

    private void StepMelt()
    {
        meltTime += Time.deltaTime;
        float t = meltDuration <= 0.01f ? 1f : Mathf.Clamp01(meltTime / meltDuration);
        transform.localScale = restScale * Mathf.Lerp(1f, 0.15f, t);

        Color color = restColor;
        color.a = Mathf.Lerp(restColor.a, 0f, t);
        spriteRenderer.color = color;

        if (t < 1f)
        {
            return;
        }

        spriteRenderer.enabled = false;
        ReleaseKey();
        gameObject.SetActive(false);
    }

    private void EmbedKey()
    {
        if (keyObject == null)
        {
            return;
        }

        keyObject.SetActive(true);
        keyObject.transform.SetParent(transform.parent, true);
        keyObject.transform.localScale = Vector3.one * 0.5f;
        KeepKeyOnIce();

        SpriteRenderer keyRenderer = keyObject.GetComponent<SpriteRenderer>();
        if (keyRenderer != null)
        {
            keyRenderer.enabled = true;
            keyRenderer.sortingOrder = 40;
        }

        FocusKey key = keyObject.GetComponent<FocusKey>();
        if (key != null)
        {
            key.SetCollectable(false);
        }
    }

    private void KeepKeyOnIce()
    {
        if (keyObject == null || isMelting)
        {
            return;
        }

        Vector3 keyPosition = transform.position;
        keyPosition.z = transform.position.z - 0.05f;
        keyObject.transform.position = keyPosition;
        FreezeKeyPhysics();
    }

    private void ReleaseKey()
    {
        if (keyObject == null)
        {
            return;
        }

        Vector3 dropPosition = FindKeyDropPosition();
        dropPosition.y = startPosition.y;
        keyObject.transform.position = dropPosition;
        keyObject.transform.localScale = Vector3.one * 0.5f;
        keyObject.SetActive(true);
        FreezeKeyPhysics();

        SpriteRenderer keyRenderer = keyObject.GetComponent<SpriteRenderer>();
        if (keyRenderer != null)
        {
            keyRenderer.enabled = true;
            keyRenderer.sortingOrder = 40;
        }

        FocusKey key = keyObject.GetComponent<FocusKey>();
        if (key != null)
        {
            key.SetCollectable(true);
        }
        else
        {
            keyObject.transform.SetParent(null, true);
            FocusUnityLayers.Assign(keyObject.transform, 0);
        }
    }

    private Vector3 FindKeyDropPosition()
    {
        Vector3 dropPosition = startPosition;
        dropPosition.y = startPosition.y;

        Vector3 away = startPosition - transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = Vector3.left;
        }

        away.Normalize();
        Vector3 probe = transform.position;
        probe.y = startPosition.y;

        for (int i = 0; i < 16; i++)
        {
            if (!IsPointInSunlight(probe))
            {
                return probe;
            }

            probe += away * 0.28f;
        }

        return dropPosition;
    }

    private void FreezeKeyPhysics()
    {
        if (keyObject == null)
        {
            return;
        }

        Rigidbody2D keyBody = keyObject.GetComponent<Rigidbody2D>();
        if (keyBody != null)
        {
            keyBody.bodyType = RigidbodyType2D.Kinematic;
            keyBody.gravityScale = 0f;
            keyBody.velocity = Vector2.zero;
            keyBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private static bool IsPointInSunlight(Vector3 point)
    {
        FocusSunGlare[] lights = FindObjectsOfType<FocusSunGlare>();

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].CoversPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        meltDuration = Mathf.Max(0.05f, meltDuration);
        interactDistance = Mathf.Max(0.2f, interactDistance);
        holdOffsetX = Mathf.Max(0.2f, holdOffsetX);
        fallSpeed = Mathf.Max(0.1f, fallSpeed);
    }
}
