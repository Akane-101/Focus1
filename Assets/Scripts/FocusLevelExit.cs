using UnityEngine;

public static class FocusLevelExit
{
    public const KeyCode EnterKey = KeyCode.F;

    public static bool IsEnterPressed()
    {
        return Input.GetKeyDown(EnterKey);
    }

    public static bool IsPlayerAt(Transform door, Transform player, float enterDistance)
    {
        if (door == null || player == null)
        {
            return false;
        }

        float reach = Mathf.Max(0.5f, enterDistance);
        Collider2D doorCollider = door.GetComponent<Collider2D>();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Vector2 playerPos = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.position;

        if (doorCollider != null)
        {
            bool doorWasEnabled = doorCollider.enabled;
            doorCollider.enabled = true;

            Vector2 closestDoor = doorCollider.ClosestPoint(playerPos);
            Vector2 closestPlayer = playerCollider != null ? playerCollider.ClosestPoint(closestDoor) : playerPos;
            float surfaceDistance = Vector2.Distance(closestDoor, closestPlayer);

            Bounds doorBounds = doorCollider.bounds;
            doorCollider.enabled = doorWasEnabled;

            if (surfaceDistance <= reach)
            {
                return true;
            }

            float dx = Mathf.Abs(playerPos.x - doorBounds.center.x) - doorBounds.extents.x;
            float dy = Mathf.Abs(playerPos.y - doorBounds.center.y) - doorBounds.extents.y;
            if (dx <= reach && dy <= reach * 2f)
            {
                return true;
            }
        }

        return Vector2.Distance(playerPos, (Vector2)door.position) <= reach + 1f;
    }

    public static bool IsAnyDoorReady(FocusLevelState levelState)
    {
        if (levelState == null)
        {
            return false;
        }

        FocusDoor[] doors = Object.FindObjectsOfType<FocusDoor>();
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].IsEnterReady())
            {
                return true;
            }
        }

        FocusLockedDoor[] lockedDoors = Object.FindObjectsOfType<FocusLockedDoor>();
        for (int i = 0; i < lockedDoors.Length; i++)
        {
            if (lockedDoors[i] != null && lockedDoors[i].IsEnterReady())
            {
                return true;
            }
        }

        FocusWindDoor[] windDoors = Object.FindObjectsOfType<FocusWindDoor>();
        for (int i = 0; i < windDoors.Length; i++)
        {
            if (windDoors[i] != null && windDoors[i].IsEnterReady())
            {
                return true;
            }
        }

        return false;
    }

    public static void TryEnter(FocusLevelState levelState, Transform door, float enterDistance, bool hardConditionMet)
    {
        if (levelState == null || door == null || !hardConditionMet || !IsEnterPressed())
        {
            return;
        }

        if (!IsPlayerAt(door, levelState.PlayerTransform, enterDistance))
        {
            return;
        }

        levelState.CompleteLevel();
    }
}
