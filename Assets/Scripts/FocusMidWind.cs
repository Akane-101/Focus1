using UnityEngine;

public sealed class FocusMidWind : MonoBehaviour
{
    private PlayerMove playerMove;

    private void Start()
    {
        FocusLevelState levelState = FindObjectOfType<FocusLevelState>();

        if (levelState != null && levelState.PlayerTransform != null)
        {
            playerMove = levelState.PlayerTransform.GetComponent<PlayerMove>();
        }

        ClearWindLock();
    }

    private void OnDisable()
    {
        ClearWindLock();
    }

    private void ClearWindLock()
    {
        if (playerMove != null)
        {
            playerMove.SetMovementLocked(this, false);
            playerMove.SetDirectionBlocked(this, false, false);
        }
    }
}
