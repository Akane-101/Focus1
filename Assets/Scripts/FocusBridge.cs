using UnityEngine;

public sealed class FocusBridge : MonoBehaviour
{
    [SerializeField] private float clearThresholdX = 3f;

    private FocusLevelState levelState;
    private bool hasAdvancedStage;

    private void Awake()
    {
        levelState = FindObjectOfType<FocusLevelState>();

        if (levelState == null)
        {
            Debug.LogError("Missing FocusLevelState in scene.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (levelState == null || hasAdvancedStage || levelState.CurrentStage != FocusStage.BuildBridge || levelState.CurrentLayer != FocusLayer.Near)
        {
            return;
        }

        Transform playerTransform = levelState.PlayerTransform;

        if (playerTransform != null && playerTransform.position.x >= clearThresholdX)
        {
            hasAdvancedStage = true;
            levelState.AdvanceStage(FocusStage.ReachExit);
        }
    }
}
