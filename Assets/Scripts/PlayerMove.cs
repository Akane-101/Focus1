using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float deceleration = 30f;
    [SerializeField] private float gravityScale = 4f;

    private Rigidbody2D body;
    private CapsuleCollider2D capsule;
    private SpriteRenderer spriteRenderer;
    private readonly List<object> movementLocks = new List<object>();
    private readonly List<object> carryLocks = new List<object>();
    private readonly List<object> rightLocks = new List<object>();
    private readonly List<object> leftLocks = new List<object>();
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];

    private float horizontalInput;
    private float stunUntilTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        body.gravityScale = gravityScale;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        capsule.direction = CapsuleDirection2D.Vertical;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        gravityScale = Mathf.Max(0.01f, gravityScale);
    }

    private void Update()
    {
        horizontalInput = IsMovementLocked ? 0f : Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            spriteRenderer.flipX = horizontalInput < 0f;
        }
    }

    public bool IsStunned
    {
        get { return Time.time < stunUntilTime; }
    }

    public void ApplyStun(float duration)
    {
        stunUntilTime = Mathf.Max(stunUntilTime, Time.time + Mathf.Max(0f, duration));
        StopHorizontal();
    }

    public void StopHorizontal()
    {
        horizontalInput = 0f;
        if (body != null && carryLocks.Count == 0)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }

    public bool IsMovementLocked
    {
        get { return movementLocks.Count > 0 || IsStunned; }
    }

    public void SetMovementLocked(object source, bool locked)
    {
        if (source == null)
        {
            return;
        }

        if (locked)
        {
            if (!movementLocks.Contains(source))
            {
                movementLocks.Add(source);
            }

            return;
        }

        movementLocks.Remove(source);
    }

    public void SetCarried(object source, bool carried)
    {
        if (source == null)
        {
            return;
        }

        if (carried)
        {
            if (!carryLocks.Contains(source))
            {
                carryLocks.Add(source);
            }
        }
        else
        {
            carryLocks.Remove(source);
        }

        body.gravityScale = carryLocks.Count > 0 ? 0f : gravityScale;
        if (carryLocks.Count > 0)
        {
            body.velocity = Vector2.zero;
        }
    }

    public void SetDirectionBlocked(object source, bool blockRight, bool blockLeft)
    {
        if (source == null)
        {
            return;
        }

        SetDirectionLock(rightLocks, source, blockRight);
        SetDirectionLock(leftLocks, source, blockLeft);
    }

    private static void SetDirectionLock(List<object> locks, object source, bool locked)
    {
        if (locked)
        {
            if (!locks.Contains(source))
            {
                locks.Add(source);
            }

            return;
        }

        locks.Remove(source);
    }

    private void FixedUpdate()
    {
        if (carryLocks.Count > 0)
        {
            return;
        }

        float input = horizontalInput;

        if (IsMovementLocked || (input > 0f && rightLocks.Count > 0) || (input < 0f && leftLocks.Count > 0))
        {
            input = 0f;
        }

        float targetSpeed = input * moveSpeed;
        float accelerationRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        Vector2 groundNormal;
        bool onSlope = TryGetGroundNormal(out groundNormal) && Mathf.Abs(groundNormal.x) > 0.12f;

        if (onSlope)
        {
            Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x);
            if (tangent.x < 0f)
            {
                tangent = -tangent;
            }

            Vector2 desiredVelocity = tangent * targetSpeed;
            body.velocity = Vector2.MoveTowards(body.velocity, desiredVelocity, accelerationRate * Time.fixedDeltaTime);
            return;
        }

        float nextHorizontalVelocity = Mathf.MoveTowards(body.velocity.x, targetSpeed, accelerationRate * Time.fixedDeltaTime);
        body.velocity = new Vector2(nextHorizontalVelocity, body.velocity.y);
    }

    private bool TryGetGroundNormal(out Vector2 normal)
    {
        normal = Vector2.up;
        Bounds bounds = capsule.bounds;
        Vector2 origin = bounds.center;
        float radius = Mathf.Max(0.05f, bounds.extents.x * 0.85f);
        float distance = bounds.extents.y + 0.12f;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
        filter.useTriggers = false;

        int hitCount = Physics2D.CircleCast(origin, radius, Vector2.down, filter, groundHits, distance);
        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null || hit.collider == capsule || hit.normal.y < 0.2f)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                normal = hit.normal;
                found = true;
            }
        }

        return found;
    }
}
