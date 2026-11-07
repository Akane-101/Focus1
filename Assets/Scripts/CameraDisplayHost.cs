using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed class CameraDisplayHost : MonoBehaviour
{
    public const int LevelCount = 12;

    private static readonly Vector3 LevelWorldOffset = new Vector3(0f, -80f, 0f);
    private static readonly string[] LevelNames =
    {
        "Level1", "Level2", "Level3", "Level4", "Level5", "Level6",
        "Level7", "Level8", "Level9", "Level10", "Level11", "Level12"
    };

    public static CameraDisplayHost Instance { get; private set; }

    public static bool IsHosting
    {
        get { return Instance != null && Instance.isActiveAndEnabled; }
    }

    [SerializeField] private int displayWidth = 1280;
    [SerializeField] private int displayHeight = 720;

    private RenderTexture displayTexture;
    private Material displayMaterial;
    private GameObject displayQuad;
    private int currentLevelIndex;
    private Scene loadedLevel;
    private bool loading;
    private Camera levelCamera;
    private Camera hudCamera;
    private Canvas levelHud;
    private CameraFocusDrag pointerDrag;
    private bool pointerDragging;
    private readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    private readonly Vector2 textureScale = new Vector2(-1f, 1f);
    private readonly Vector2 textureOffset = new Vector2(1f, 0f);

    public int CurrentLevelNumber
    {
        get { return currentLevelIndex + 1; }
    }

    public static bool TryHandleLevelClear()
    {
        if (!IsHosting)
        {
            return false;
        }

        Instance.ShowNext();
        return true;
    }

    private void Awake()
    {
        Instance = this;
        CreateDisplaySurface();
        CreateRenderTarget();
        RestrictHostCamera();
        FrameHostCameraToBody();
    }

    private void Start()
    {
        StartCoroutine(LoadLevelRoutine(0, false));
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ReleaseRenderTarget();
    }

    private void Update()
    {
        if (loading)
        {
            return;
        }

        RouteScreenPointer();

        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.PageDown))
        {
            ShowNext();
        }
        else if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.PageUp))
        {
            ShowPrevious();
        }
    }

    public void ShowNext()
    {
        ShowLevel((currentLevelIndex + 1) % LevelCount);
    }

    public void ShowPrevious()
    {
        ShowLevel((currentLevelIndex + LevelCount - 1) % LevelCount);
    }

    public void ShowLevel(int index)
    {
        if (loading)
        {
            return;
        }

<<<<<<< Updated upstream
        StartCoroutine(LoadLevelRoutine(Mathf.Clamp(index, 0, LevelCount - 1), true));
=======
        index = Mathf.Clamp(index, 0, LevelCount - 1);
        if (embeddedLevel)
        {
            LoadEmbeddedLevelScene(index);
            return;
        }

        StartCoroutine(LoadLevelRoutine(index, true));
    }

    private void LoadEmbeddedLevelScene(int index)
    {
        string sceneName = EmbeddedLevelPrefix + (index + 1);
        if (sceneName == gameObject.scene.name)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Camera display cannot load " + sceneName + ". Add it to File > Build Settings.");
            return;
        }

        currentLevelIndex = index;
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator BindEmbeddedLevelRoutine()
    {
        loading = true;
        loadedLevel = gameObject.scene;
        yield return null;
        BindLevelCameraToDisplay();
        EnsureViewfinder();
        loading = false;
    }

    private void LateUpdate()
    {
        if (loading)
        {
            return;
        }

        if (embeddedLevel)
        {
            BindLevelCameraToDisplay();
            return;
        }

        SyncHudCamera();
    }

    private void BindLevelCameraToDisplay()
    {
        Camera frameCamera = FindHostCamera();
        Camera levelCamera = null;
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null &&
                camera.gameObject.scene == gameObject.scene &&
                camera.name == "Main Camera")
            {
                levelCamera = camera;
                break;
            }
        }

        int cameraModelLayer = LayerMask.NameToLayer("CameraModel");
        if (frameCamera != null)
        {
            frameCamera.enabled = true;
            frameCamera.targetTexture = null;
            frameCamera.depth = 1f;
            frameCamera.rect = new Rect(0f, 0f, 1f, 1f);
            if (cameraModelLayer >= 0)
            {
                frameCamera.cullingMask = 1 << cameraModelLayer;
            }

            AudioListener frameListener = frameCamera.GetComponent<AudioListener>();
            if (frameListener != null)
            {
                frameListener.enabled = true;
            }
        }

        if (levelCamera != null && displayTexture != null)
        {
            levelCamera.targetTexture = displayTexture;
            levelCamera.depth = -1f;
            if (cameraModelLayer >= 0)
            {
                levelCamera.cullingMask &= ~(1 << cameraModelLayer);
            }

            AudioListener levelListener = levelCamera.GetComponent<AudioListener>();
            if (levelListener != null)
            {
                levelListener.enabled = false;
            }
        }
    }

    private void DisableDedicatedFrameCamera()
    {
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null &&
                camera.gameObject.scene == gameObject.scene &&
                camera.name == "CameraFrameCamera")
            {
                camera.enabled = false;
                AudioListener listener = camera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }
    }

    private void PrepareEmbeddedFrame()
    {
        embeddedBody = FindEmbeddedBody();
        if (embeddedBody == null)
        {
            Debug.LogError("Camera frame body was not found in " + gameObject.scene.name);
            return;
        }

        embeddedBody.gameObject.SetActive(true);
        AssignCameraModelLayer(embeddedBody);
        embeddedScreen = FindLcdGlassRenderer(embeddedBody);
        if (embeddedScreen == null)
        {
            Debug.LogError("Camera frame screen was not found in " + gameObject.scene.name);
            return;
        }

        embeddedScreen.gameObject.SetActive(true);
        embeddedScreen.enabled = true;
        Renderer[] renderers = embeddedBody.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }

        CacheEmbeddedMetrics();
        BuildEmbeddedFrameMaterials(renderers);
    }

    private void CacheEmbeddedMetrics()
    {
        Vector3 previousScale = embeddedBody.localScale;
        embeddedBody.localScale = Vector3.one;
        Bounds screenBounds = embeddedScreen.bounds;
        Bounds bodyBounds = screenBounds;
        Renderer[] renderers = embeddedBody.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                bodyBounds.Encapsulate(renderers[i].bounds);
            }
        }

        unitRootToScreen = screenBounds.center - embeddedBody.position;
        unitRootToBodyCenter = bodyBounds.center - embeddedBody.position;
        unitBodySize = bodyBounds.size;
        unitBodyMinFromScreen = bodyBounds.min - bodyBounds.center;
        embeddedBody.localScale = previousScale;
        embeddedMetricsReady = unitBodySize.x > 0.01f && unitBodySize.y > 0.01f;

        MeshFilter screenFilter = embeddedScreen.GetComponent<MeshFilter>();
        if (embeddedScreenMaterial != null && screenFilter != null && screenFilter.sharedMesh != null)
        {
            Bounds local = screenFilter.sharedMesh.bounds;
            embeddedScreenMaterial.SetVector("_LcdBounds", new Vector4(local.min.x, local.min.y, local.max.x, local.max.y));
        }
    }

    private void BuildEmbeddedFrameMaterials(Renderer[] renderers)
    {
        ReleaseEmbeddedFrameMaterials();
        Shader bodyShader = Shader.Find("Hidden/CameraFrameBody");
        Shader screenShader = Shader.Find("Hidden/CameraFrameScreen");
        if (bodyShader == null || screenShader == null)
        {
            Debug.LogError("Camera frame shaders are missing.");
            return;
        }

        int partCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != embeddedScreen)
            {
                partCount++;
            }
        }

        Renderer[] parts = new Renderer[partCount];
        embeddedPartMaterials = new Material[partCount];
        int partIndex = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer sourceRenderer = renderers[i];
            if (sourceRenderer == null || sourceRenderer == embeddedScreen)
            {
                continue;
            }

            Material source = sourceRenderer.sharedMaterial;
            Material frameMaterial = source;
            if (frameMaterial == null)
            {
                frameMaterial = new Material(bodyShader);
                frameMaterial.name = "CameraFrameBody_Runtime";
                frameMaterial.SetColor("_BaseColor", Color.black);
            }

            parts[partIndex] = sourceRenderer;
            embeddedPartMaterials[partIndex] = frameMaterial;
            partIndex++;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            for (int j = i + 1; j < parts.Length; j++)
            {
                if (parts[j] != null && parts[i] != null && parts[j].bounds.center.z > parts[i].bounds.center.z)
                {
                    Renderer swapRenderer = parts[i];
                    parts[i] = parts[j];
                    parts[j] = swapRenderer;
                    Material swapMaterial = embeddedPartMaterials[i];
                    embeddedPartMaterials[i] = embeddedPartMaterials[j];
                    embeddedPartMaterials[j] = swapMaterial;
                }
            }
        }

        embeddedScreenMaterial = new Material(screenShader);
        embeddedScreenMaterial.name = "CameraFrameScreen_Runtime";
        MeshFilter screenFilter = embeddedScreen.GetComponent<MeshFilter>();
        if (screenFilter != null && screenFilter.sharedMesh != null)
        {
            Bounds local = screenFilter.sharedMesh.bounds;
            embeddedScreenMaterial.SetVector("_LcdBounds", new Vector4(local.min.x, local.min.y, local.max.x, local.max.y));
        }

        ActiveEmbeddedFrame = new EmbeddedFrame
        {
            screen = embeddedScreen,
            screenMaterial = embeddedScreenMaterial,
            parts = parts,
            partMaterials = embeddedPartMaterials
        };
    }

    private void FitEmbeddedFrame()
    {
        if (!embeddedMetricsReady || embeddedBody == null || embeddedScreen == null)
        {
            return;
        }

        Camera levelCamera = null;
        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].name == "Main Camera" && cameras[i].gameObject.scene == gameObject.scene)
            {
                levelCamera = cameras[i];
                break;
            }
        }

        if (levelCamera == null)
        {
            return;
        }

        float viewHeight = Mathf.Max(0.01f, levelCamera.orthographicSize * 2f);
        float viewWidth = viewHeight * Mathf.Max(0.01f, levelCamera.aspect);
        float scale = Mathf.Max(viewWidth / Mathf.Max(0.01f, unitBodySize.x), viewHeight / Mathf.Max(0.01f, unitBodySize.y)) * 1.08f;
        embeddedBody.rotation = Quaternion.identity;
        embeddedBody.localScale = new Vector3(scale, scale, scale);

        float bodyZ = levelCamera.transform.position.z + levelCamera.nearClipPlane + 0.08f - unitBodyMinFromScreen.z * scale;
        Vector3 bodyCenter = new Vector3(levelCamera.transform.position.x, levelCamera.transform.position.y, bodyZ);
        embeddedBody.position = bodyCenter - unitRootToBodyCenter * scale;

        if (embeddedScreenMaterial != null)
        {
            float lcdAspect = unitBodySize.y > 0.01f ? (embeddedScreen.bounds.size.x / Mathf.Max(0.01f, embeddedScreen.bounds.size.y)) : 1.63f;
            embeddedScreenMaterial.SetVector("_FrameAspects", new Vector4(lcdAspect, levelCamera.aspect, 0f, 0f));
        }
    }

    private void ReleaseEmbeddedFrameMaterials()
    {
        if (ActiveEmbeddedFrame != null && ActiveEmbeddedFrame.screenMaterial == embeddedScreenMaterial)
        {
            ActiveEmbeddedFrame = null;
        }

        if (embeddedPartMaterials != null)
        {
            for (int i = 0; i < embeddedPartMaterials.Length; i++)
            {
                if (embeddedPartMaterials[i] != null && embeddedPartMaterials[i].name == "CameraFrameBody_Runtime")
                {
                    Destroy(embeddedPartMaterials[i]);
                }
            }

            embeddedPartMaterials = null;
        }

        if (embeddedScreenMaterial != null)
        {
            Destroy(embeddedScreenMaterial);
            embeddedScreenMaterial = null;
        }
    }

    private static Transform FindEmbeddedBody()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == "UMesh_PolySphere2_6")
            {
                return transforms[i].root;
            }
        }

        return FindCameraBodyRoot();
    }

    private void HideBodyFromLevelView()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform.parent != null || transform.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            if (transform.name == "Null")
            {
                transform.gameObject.SetActive(false);
            }
        }
    }

    private void EnsureViewfinder()
    {
        if (viewfinderFrame == null || focusTrack == null || focusHandle == null)
        {
            Debug.LogError("Camera HUD sprites are missing. Import Assets/Textures/hud.psd with the PSD Importer.");
            return;
        }

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        if (GameObject.Find("CameraFrameHud") != null)
        {
            return;
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            uiLayer = 0;
        }

        GameObject canvasObject = new GameObject("CameraFrameHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = uiLayer;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        GameObject frameObject = new GameObject("HudFrame", typeof(RectTransform));
        frameObject.layer = uiLayer;
        frameObject.transform.SetParent(canvasObject.transform, false);
        RectTransform createdFrame = frameObject.GetComponent<RectTransform>();
        createdFrame.anchorMin = new Vector2(0.5f, 0.5f);
        createdFrame.anchorMax = new Vector2(0.5f, 0.5f);
        createdFrame.pivot = new Vector2(0f, 0f);

        CreateHudImage("Viewfinder", viewfinderFrame, createdFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), false);

        GameObject trackObject = CreateHudImage("FocusTrack", focusTrack, createdFrame, new Vector2(0.259f, 0.110f), new Vector2(0.753f, 0.252f), true);
        GameObject handleObject = CreateHudImage("FocusHandle", focusHandle, trackObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        trackObject.GetComponent<CameraFocusDrag>().Configure(handleRect);
    }

    private static GameObject CreateHudImage(string name, Sprite sprite, Transform parent, Vector2 anchorMin, Vector2 anchorMax, bool receivesDrag)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (receivesDrag)
        {
            imageObject.AddComponent<CameraFocusDrag>();
        }
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = receivesDrag;
        image.color = Color.white;
        return imageObject;
    }

    private void OffsetEmbeddedLevelRoots()
    {
        GameObject[] roots = loadedLevel.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (IsCameraFrameRoot(roots[i]))
            {
                continue;
            }

            roots[i].transform.position += LevelWorldOffset;
            AudioListener[] listeners = roots[i].GetComponentsInChildren<AudioListener>(true);
            for (int j = 0; j < listeners.Length; j++)
            {
                listeners[j].enabled = false;
            }
        }
    }

    private static bool IsCameraFrameRoot(GameObject root)
    {
        if (root == null)
        {
            return false;
        }

        string name = root.name;
        return name == "Null"
            || name == "CameraFrameCamera"
            || name == "CameraFrameLight"
            || name == "CameraDisplayHost"
            || name == "Global Volume";
>>>>>>> Stashed changes
    }

    private IEnumerator LoadLevelRoutine(int index, bool unloadCurrent)
    {
        loading = true;
        currentLevelIndex = index;
        string levelName = LevelNames[index];

        if (unloadCurrent && loadedLevel.IsValid() && loadedLevel.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedLevel);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        if (!Application.CanStreamedLevelBeLoaded(levelName))
        {
            Debug.LogError("Camera display cannot load " + levelName + ". Add it to File > Build Settings.");
            loading = false;
            yield break;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        while (load != null && !load.isDone)
        {
            yield return null;
        }

        loadedLevel = SceneManager.GetSceneByName(levelName);
        if (!loadedLevel.IsValid() || !loadedLevel.isLoaded)
        {
            Debug.LogError("Camera display loaded an invalid scene: " + levelName);
            loading = false;
            yield break;
        }

        SceneManager.SetActiveScene(loadedLevel);
        OffsetLoadedRoots();
        BindLevelCamera();
        HideHostedOverlayUi();
        loading = false;
    }

    private void OffsetLoadedRoots()
    {
        GameObject[] roots = loadedLevel.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == "CameraFrameHud")
            {
                continue;
            }

            roots[i].transform.position += LevelWorldOffset;
            AudioListener[] listeners = roots[i].GetComponentsInChildren<AudioListener>(true);
            for (int j = 0; j < listeners.Length; j++)
            {
                listeners[j].enabled = false;
            }
        }
    }

    private void BindLevelCamera()
    {
        Camera levelCamera = FocusLevelState.FindCameraInScene(loadedLevel);
        if (levelCamera == null)
        {
            Debug.LogError("Camera display could not find a camera in " + loadedLevel.name);
            return;
        }

        this.levelCamera = levelCamera;
        levelCamera.tag = "Untagged";
        levelCamera.targetTexture = displayTexture;
        levelCamera.depth = -50;
        levelCamera.allowMSAA = false;
        AudioListener[] listeners = FindObjectsOfType<AudioListener>();
        for (int i = 0; i < listeners.Length; i++)
        {
            Camera owner = listeners[i].GetComponent<Camera>();
            bool keep = owner != null && owner.CompareTag("MainCamera") && owner.targetTexture == null;
            listeners[i].enabled = keep;
        }

        int cameraModelLayer = LayerMask.NameToLayer("CameraModel");
        if (cameraModelLayer >= 0)
        {
            levelCamera.cullingMask &= ~(1 << cameraModelLayer);
        }

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            levelCamera.cullingMask &= ~(1 << uiLayer);
        }

        ApplyDisplayTexture();
        ComposeLevelHud();
        HideHostedOverlayUi();
    }

    private void ComposeLevelHud()
    {
        if (levelCamera == null || displayTexture == null)
        {
            return;
        }

        EnsureHudCamera();
        levelHud = FindLevelHudCanvas();
        if (levelHud == null)
        {
            Debug.LogError("Camera display could not find CameraFrameHud in " + loadedLevel.name);
            return;
        }

        levelHud.renderMode = RenderMode.WorldSpace;
        levelHud.worldCamera = hudCamera;
        levelHud.overrideSorting = true;
        levelHud.sortingOrder = 50;
        CanvasScaler scaler = levelHud.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.enabled = false;
        }

        RectTransform root = levelHud.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(displayTexture.width, displayTexture.height);
        SyncHudCamera();
    }

    private Canvas FindLevelHudCanvas()
    {
        if (!loadedLevel.IsValid() || !loadedLevel.isLoaded)
        {
            return null;
        }

        GameObject[] roots = loadedLevel.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == "CameraFrameHud")
            {
                return roots[i].GetComponent<Canvas>();
            }
        }

        return null;
    }

    private void EnsureHudCamera()
    {
        if (hudCamera != null)
        {
            return;
        }

        GameObject cameraObject = new GameObject("CameraFrameHudCamera");
        cameraObject.transform.SetParent(transform, false);
        hudCamera = cameraObject.AddComponent<Camera>();
        hudCamera.enabled = true;
        hudCamera.clearFlags = CameraClearFlags.Depth;
        int uiLayer = LayerMask.NameToLayer("UI");
        hudCamera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : 0;
        hudCamera.allowMSAA = false;
        hudCamera.allowHDR = false;
        hudCamera.useOcclusionCulling = false;
        UniversalAdditionalCameraData cameraData = hudCamera.GetUniversalAdditionalCameraData();
        cameraData.renderType = CameraRenderType.Overlay;
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.SetRenderer(0);
    }

    private void SyncHudCamera()
    {
        if (hudCamera == null || levelCamera == null || displayTexture == null)
        {
            return;
        }

        hudCamera.orthographic = levelCamera.orthographic;
        hudCamera.orthographicSize = levelCamera.orthographicSize;
        hudCamera.fieldOfView = levelCamera.fieldOfView;
        hudCamera.nearClipPlane = levelCamera.nearClipPlane;
        hudCamera.farClipPlane = levelCamera.farClipPlane;
        hudCamera.rect = levelCamera.rect;
        hudCamera.aspect = levelCamera.aspect;
        hudCamera.targetTexture = null;
        hudCamera.clearFlags = CameraClearFlags.Depth;
        hudCamera.depth = levelCamera.depth + 10f;
        hudCamera.transform.SetPositionAndRotation(levelCamera.transform.position, levelCamera.transform.rotation);

        UniversalAdditionalCameraData levelData = levelCamera.GetUniversalAdditionalCameraData();
        if (!levelData.cameraStack.Contains(hudCamera))
        {
            levelData.cameraStack.Add(hudCamera);
        }

        if (levelHud == null)
        {
            return;
        }

        levelHud.overrideSorting = true;
        levelHud.sortingOrder = 50;
        RectTransform root = levelHud.GetComponent<RectTransform>();
        float viewHeight = levelCamera.orthographic
            ? levelCamera.orthographicSize * 2f
            : 2f * 10f * Mathf.Tan(levelCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float scale = viewHeight / Mathf.Max(1f, displayTexture.height);
        root.localScale = new Vector3(scale, scale, scale);
        float distance = Mathf.Clamp(10f, levelCamera.nearClipPlane + 0.05f, levelCamera.farClipPlane - 0.05f);
        root.SetPositionAndRotation(levelCamera.transform.position + levelCamera.transform.forward * distance, levelCamera.transform.rotation);
    }

    private void RouteScreenPointer()
    {
        if (levelHud == null || hudCamera == null || displayQuad == null || displayTexture == null)
        {
            return;
        }

        if (!TryReadPointer(out Vector2 hostPosition, out bool began, out bool held, out bool ended))
        {
            return;
        }

        bool mapped = TryMapHostToHudScreen(hostPosition, out Vector2 hudPosition);
        if (began && mapped)
        {
            CameraFocusDrag drag = RaycastDrag(hudPosition);
            if (drag != null)
            {
                pointerDrag = drag;
                pointerDragging = true;
                pointerDrag.OnBeginDrag(BuildPointer(hudPosition));
            }
        }

        if (held && pointerDragging && pointerDrag != null && mapped)
        {
            pointerDrag.OnDrag(BuildPointer(hudPosition));
        }

        if (ended && pointerDragging && pointerDrag != null)
        {
            if (!mapped)
            {
                hudPosition = pointerDragLast;
            }

            pointerDrag.OnEndDrag(BuildPointer(hudPosition));
            pointerDrag = null;
            pointerDragging = false;
        }
    }

    private Vector2 pointerDragLast;

    private bool TryReadPointer(out Vector2 screenPosition, out bool began, out bool held, out bool ended)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            began = touch.phase == TouchPhase.Began;
            held = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        screenPosition = Input.mousePosition;
        began = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        ended = Input.GetMouseButtonUp(0);
        return began || held || ended;
    }

    private bool TryMapHostToHudScreen(Vector2 hostPosition, out Vector2 hudPosition)
    {
        hudPosition = Vector2.zero;
        Camera hostCamera = FindHostCamera();
        if (hostCamera == null || displayQuad == null || displayTexture == null)
        {
            return false;
        }

        Ray ray = hostCamera.ScreenPointToRay(hostPosition);
        Plane plane = new Plane(displayQuad.transform.forward, displayQuad.transform.position);
        if (!plane.Raycast(ray, out float distance))
        {
            plane = new Plane(-displayQuad.transform.forward, displayQuad.transform.position);
            if (!plane.Raycast(ray, out distance))
            {
                return false;
            }
        }

        Vector3 local = displayQuad.transform.InverseTransformPoint(ray.GetPoint(distance));
        float meshU = local.x + 0.5f;
        float meshV = local.y + 0.5f;
        if (!pointerDragging && (meshU < 0f || meshU > 1f || meshV < 0f || meshV > 1f))
        {
            return false;
        }

        float textureU = meshU * textureScale.x + textureOffset.x;
        float textureV = meshV * textureScale.y + textureOffset.y;
        if (levelHud == null || hudCamera == null)
        {
            return false;
        }

        RectTransform root = levelHud.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        root.GetWorldCorners(corners);
        Vector3 world = corners[0] + (corners[3] - corners[0]) * textureU + (corners[1] - corners[0]) * textureV;
        Vector3 screen = hudCamera.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            return false;
        }

        hudPosition = screen;
        pointerDragLast = hudPosition;
        return true;
    }

    private CameraFocusDrag RaycastDrag(Vector2 hudPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return null;
        }

        pointerHits.Clear();
        eventSystem.RaycastAll(BuildPointer(hudPosition), pointerHits);
        for (int i = 0; i < pointerHits.Count; i++)
        {
            CameraFocusDrag drag = pointerHits[i].gameObject.GetComponent<CameraFocusDrag>();
            if (drag != null)
            {
                return drag;
            }
        }

        return null;
    }

    private PointerEventData BuildPointer(Vector2 hudPosition)
    {
        return new PointerEventData(EventSystem.current)
        {
            position = hudPosition,
            pressPosition = hudPosition,
            button = PointerEventData.InputButton.Left
        };
    }

    private void HideHostedOverlayUi()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvases[i].enabled = false;
            }
        }
    }

    private void CreateDisplaySurface()
    {
        Transform bodyRoot = FindCameraBodyRoot();
        if (bodyRoot == null)
        {
            Debug.LogError("Camera display could not find the camera body (Null).");
            return;
        }

        displayQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        displayQuad.name = "CameraLCD";
        Collider quadCollider = displayQuad.GetComponent<Collider>();
        if (quadCollider != null)
        {
            Destroy(quadCollider);
        }

        displayQuad.transform.SetParent(bodyRoot, false);
        Renderer lcdGlass = FindLcdGlassRenderer(bodyRoot);
<<<<<<< Updated upstream
        if (lcdGlass != null)
        {
            Bounds lcdBounds = lcdGlass.bounds;
            displayQuad.transform.position = new Vector3(lcdBounds.center.x, lcdBounds.center.y, lcdBounds.min.z - 0.02f);
=======
        Renderer openingPlate = FindNamedRenderer(bodyRoot, "PolySphere1_1");
        Renderer opening = openingPlate != null ? openingPlate : lcdGlass;
        if (opening != null)
        {
            Bounds openingBounds = opening.bounds;
            displayAspect = openingBounds.size.x / Mathf.Max(0.01f, openingBounds.size.y);
            displayQuad.transform.SetParent(null, false);
            displayQuad.transform.position = new Vector3(openingBounds.center.x, openingBounds.center.y, openingBounds.min.z - 0.01f);
>>>>>>> Stashed changes
            displayQuad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            displayQuad.transform.localScale = new Vector3(lcdBounds.size.x * 0.86f, lcdBounds.size.y * 0.86f, 1f);
        }
        else
        {
            displayQuad.transform.localPosition = new Vector3(0.02f, -1.31f, -0.93f);
            displayQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            displayQuad.transform.localScale = new Vector3(2.9f, 1.78f, 1f);
        }

        AssignCameraModelLayer(bodyRoot);
        AssignCameraModelLayer(displayQuad.transform);

        MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
        quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        quadRenderer.receiveShadows = false;
        quadRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        quadRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private static Transform FindCameraBodyRoot()
    {
        if (Instance != null)
        {
            Scene scene = Instance.gameObject.scene;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "Null")
                {
                    return roots[i].transform;
                }
            }
        }

        GameObject fallback = GameObject.Find("UMesh_PolySphere2_6");
        return fallback != null ? fallback.transform.root : null;
    }

    private static Renderer FindLcdGlassRenderer(Transform bodyRoot)
    {
        if (bodyRoot == null)
        {
            return null;
        }

        Transform[] children = bodyRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "PolySphere")
            {
                return children[i].GetComponent<Renderer>();
            }
        }

        return null;
    }

    private static Camera FindHostCamera()
    {
        Camera hostCamera = Camera.main;
        if (hostCamera != null && hostCamera.targetTexture == null)
        {
            return hostCamera;
        }

        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].CompareTag("MainCamera") && cameras[i].targetTexture == null)
            {
                return cameras[i];
            }
        }

        return hostCamera;
    }

    private static void RestrictHostCamera()
    {
        Camera hostCamera = FindHostCamera();
        int cameraModelLayer = LayerMask.NameToLayer("CameraModel");
        if (hostCamera != null && cameraModelLayer >= 0)
        {
            hostCamera.cullingMask = 1 << cameraModelLayer;
        }
    }

    private static void FrameHostCameraToBody()
    {
        Camera hostCamera = FindHostCamera();
        Transform bodyRoot = FindCameraBodyRoot();
        if (hostCamera == null || bodyRoot == null)
        {
            return;
        }

        Renderer bodyRenderer = null;
        Renderer[] renderers = bodyRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == "UMesh_PolySphere2_6")
            {
                bodyRenderer = renderers[i];
                break;
            }
        }

        if (bodyRenderer == null && renderers.Length > 0)
        {
            bodyRenderer = renderers[0];
        }

        if (bodyRenderer == null)
        {
            return;
        }

        Bounds bounds = bodyRenderer.bounds;

        hostCamera.orthographic = true;
        hostCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 4f);
        hostCamera.transform.rotation = Quaternion.identity;

        float padding = 1.06f;
        float sizeForHeight = bounds.extents.y * padding;
        float sizeForWidth = (bounds.extents.x * padding) / Mathf.Max(0.01f, hostCamera.aspect);
        hostCamera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }

    private static void AssignCameraModelLayer(Transform root)
    {
        int layer = LayerMask.NameToLayer("CameraModel");
        if (root == null || layer < 0)
        {
            return;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private void CreateRenderTarget()
    {
        displayTexture = new RenderTexture(Mathf.Max(16, displayWidth), Mathf.Max(16, displayHeight), 16, RenderTextureFormat.ARGB32)
        {
            name = "CameraLCD_RT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        displayTexture.Create();

        Shader shader = Shader.Find("Hidden/CameraLCD");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (shader == null)
        {
            Debug.LogError("Camera display is missing an Unlit shader.");
            return;
        }

        displayMaterial = new Material(shader);
        displayMaterial.name = "CameraLCD_Runtime";
        displayMaterial.SetColor("_BaseColor", Color.white);
        displayMaterial.SetColor("_Color", Color.white);
        ApplyDisplayTexture();

        if (displayQuad != null)
        {
            MeshRenderer quadRenderer = displayQuad.GetComponent<MeshRenderer>();
            if (quadRenderer != null)
            {
                quadRenderer.sharedMaterial = displayMaterial;
            }
        }
    }

    private void ApplyDisplayTexture()
    {
        if (displayMaterial == null || displayTexture == null)
        {
            return;
        }

        displayMaterial.SetTexture("_BaseMap", displayTexture);
        displayMaterial.SetTexture("_MainTex", displayTexture);
        displayMaterial.SetTextureScale("_BaseMap", textureScale);
        displayMaterial.SetTextureOffset("_BaseMap", textureOffset);
        displayMaterial.SetTextureScale("_MainTex", textureScale);
        displayMaterial.SetTextureOffset("_MainTex", textureOffset);
        displayMaterial.mainTexture = displayTexture;
        displayMaterial.mainTextureScale = textureScale;
        displayMaterial.mainTextureOffset = textureOffset;
    }

    private void ReleaseRenderTarget()
    {
        if (displayTexture != null)
        {
            displayTexture.Release();
            Destroy(displayTexture);
            displayTexture = null;
        }

        if (displayMaterial != null)
        {
            Destroy(displayMaterial);
            displayMaterial = null;
        }
    }
}
