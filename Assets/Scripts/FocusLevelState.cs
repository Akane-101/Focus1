using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FocusLayer
{
    Far,
    Mid,
    Near
}

public enum FocusStage
{
    OpenGate,
    BuildBridge,
    ReachExit,
    WaitFirstWind,
    DoorBlown,
    LeafReady,
    LeafCollected,
    ReachedDoor,
    ExitOpen,
    WaitKey,
    HasKey
}

public sealed class FocusLevelState : MonoBehaviour
{
    public const int PlayerOrderOffset = 5;

    private const int FarSortingOrder = 10;
    private const int MidSortingOrder = 20;
    private const int NearSortingOrder = 30;

    [SerializeField] private FocusLayer initialLayer = FocusLayer.Mid;
    [SerializeField] private FocusStage initialStage = FocusStage.OpenGate;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private bool autoLoadNextScene = true;
    [SerializeField] private float focusTransitionDuration = 0.7f;
    [SerializeField] private float unfocusedBlur = 2.2f;
    [SerializeField] private bool followPlayerCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.8f, -10f);
    [SerializeField] private float cameraMinX = 0f;
    [SerializeField] private float cameraMaxX = 58f;
    [SerializeField] private float cameraMinY = 0f;
    [SerializeField] private float cameraMaxY = 3.5f;
    [SerializeField] private float cameraSmooth = 8f;

    public event Action StateChanged;

    public FocusLayer CurrentLayer { get; private set; }

    public FocusStage CurrentStage { get; private set; } = FocusStage.OpenGate;

    public int MidFocusCount { get; private set; }

    public Transform PlayerTransform { get; private set; }

    public float FocusTransitionDuration
    {
        get { return focusTransitionDuration; }
    }

    public float UnfocusedBlur
    {
        get { return unfocusedBlur; }
    }

    private void Awake()
    {
        CurrentLayer = initialLayer;
        CurrentStage = initialStage;

        if (CurrentLayer == FocusLayer.Mid)
        {
            MidFocusCount = 1;
        }

        EnsureCompositor();
        RestartLevel.EnsureHud();
        EnsurePlayerTransform();

        if (PlayerTransform == null)
        {
            Debug.LogError($"Missing player object: {playerObjectName}");
        }
    }

    private void LateUpdate()
    {
        if (PlayerTransform == null)
        {
            EnsurePlayerTransform();
        }

        if (!followPlayerCamera || PlayerTransform == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 next = PlayerTransform.position + cameraOffset;
        next.x = Mathf.Clamp(next.x, cameraMinX, cameraMaxX);
        next.y = Mathf.Clamp(next.y, cameraMinY, cameraMaxY);
        next.z = cameraOffset.z;
        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            next,
            1f - Mathf.Exp(-cameraSmooth * Time.deltaTime));
    }

    public void SetLayer(FocusLayer nextLayer)
    {
        if (CurrentLayer == nextLayer)
        {
            return;
        }

        if (nextLayer == FocusLayer.Mid)
        {
            MidFocusCount++;
        }

        CurrentLayer = nextLayer;
        NotifyStateChanged();
    }

    public void AdvanceStage(FocusStage nextStage)
    {
        if (nextStage <= CurrentStage)
        {
            return;
        }

        CurrentStage = nextStage;
        NotifyStateChanged();
    }

    public void TryEnterDoor(Transform door, float enterDistance, bool hardConditionMet)
    {
        EnsurePlayerTransform();
        FocusLevelExit.TryEnter(this, door, enterDistance, hardConditionMet);
    }

    public void CompleteLevel()
    {
        ReleaseHeldIce();

        if (CameraDisplayHost.TryHandleLevelClear())
        {
            return;
        }

        if (!autoLoadNextScene)
        {
            Debug.Log("Level clear.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        string nextSceneName = GetNextLevelName(activeScene.name);

        if (!string.IsNullOrEmpty(nextSceneName) && Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        int nextBuildIndex = activeScene.buildIndex + 1;

        if (activeScene.buildIndex >= 0 && nextBuildIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextBuildIndex);
            return;
        }

        Debug.Log("Level clear.");
    }

    private void EnsurePlayerTransform()
    {
        if (PlayerTransform != null)
        {
            return;
        }

        GameObject playerObject = GameObject.Find(playerObjectName);
        if (playerObject == null)
        {
            PlayerMove playerMove = FindObjectOfType<PlayerMove>();
            if (playerMove != null)
            {
                playerObject = playerMove.gameObject;
            }
        }

        if (playerObject != null)
        {
            PlayerTransform = playerObject.transform;
        }
    }

    private static void ReleaseHeldIce()
    {
        FocusIce[] iceBlocks = FindObjectsOfType<FocusIce>();
        for (int i = 0; i < iceBlocks.Length; i++)
        {
            if (iceBlocks[i] != null)
            {
                iceBlocks[i].ForceRelease();
            }
        }
    }

    private static string GetNextLevelName(string currentName)
    {
        if (currentName == "Level1" || currentName == "1")
        {
            return "Level2";
        }

        if (currentName == "Level2" || currentName == "2")
        {
            return "Level3";
        }

        if (currentName == "Level3" || currentName == "3")
        {
            return "Level4";
        }

        if (currentName == "Level4" || currentName == "4")
        {
            return "Level5";
        }

        if (currentName == "Level5" || currentName == "5")
        {
            return "Level6";
        }

        if (currentName == "Level6" || currentName == "6")
        {
            return "Level7";
        }

        if (currentName == "Level7" || currentName == "7")
        {
            return "Level8";
        }

        if (currentName == "Level8" || currentName == "8")
        {
            return "Level9";
        }

        if (currentName == "Level9" || currentName == "9")
        {
            return "Level10";
        }

        if (currentName == "Level10" || currentName == "10")
        {
            return "Level11";
        }

        if (currentName == "Level11" || currentName == "11")
        {
            return "Level12";
        }

        return null;
    }

    public int GetSortingOrder(FocusLayer layer)
    {
        return GetSortingOrder(layer, 0);
    }

    public int GetSortingOrder(FocusLayer layer, int orderOffset)
    {
        switch (layer)
        {
            case FocusLayer.Near:
                return NearSortingOrder + orderOffset;
            case FocusLayer.Mid:
                return MidSortingOrder + orderOffset;
            default:
                return FarSortingOrder + orderOffset;
        }
    }

    private void NotifyStateChanged()
    {
        Action handler = StateChanged;

        if (handler != null)
        {
            handler();
        }
    }

    public static Camera FindCameraInScene(Scene scene)
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        Camera fallback = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }

            if (fallback == null)
            {
                fallback = camera;
            }
        }

        return fallback;
    }

    private void EnsureCompositor()
    {
        Camera levelCamera = FindCameraInScene(gameObject.scene);
        if (levelCamera == null)
        {
            levelCamera = Camera.main;
        }

        if (levelCamera != null && levelCamera.GetComponent<FocusLayerCompositor>() == null)
        {
            levelCamera.gameObject.AddComponent<FocusLayerCompositor>();
        }
    }

    private void OnValidate()
    {
        focusTransitionDuration = Mathf.Max(0.01f, focusTransitionDuration);
        unfocusedBlur = Mathf.Max(0f, unfocusedBlur);
    }
}
