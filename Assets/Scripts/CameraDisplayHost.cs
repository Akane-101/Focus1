using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(50)]
public sealed class CameraDisplayHost : MonoBehaviour
{
    public const int LevelCount = 12;

    private static readonly Vector3 LevelWorldOffset = new Vector3(0f, -80f, 0f);
    private static readonly string[] LevelNames =
    {
        "ColorLevel1", "ColorLevel2", "ColorLevel3", "ColorLevel4", "ColorLevel5", "ColorLevel6",
        "ColorLevel7", "ColorLevel8", "ColorLevel9", "ColorLevel10", "ColorLevel11", "ColorLevel12"
    };

    public static CameraDisplayHost Instance { get; private set; }

    public static bool IsHosting
    {
        get { return Instance != null && Instance.isActiveAndEnabled; }
    }

    public sealed class EmbeddedFrame
    {
        public Renderer screen;
        public Material screenMaterial;
        public Renderer[] parts;
        public Material[] partMaterials;
    }

    public static EmbeddedFrame ActiveEmbeddedFrame { get; private set; }

    public const string EmbeddedLevelPrefix = "CameraFrameLevel";

    [SerializeField] private int displayWidth = 1280;
    [SerializeField] private int displayHeight = 720;
    [SerializeField] private Vector2 screenFill = new Vector2(0.86f, 0.84f);
    [SerializeField] private float screenLift = 0.01f;
    [SerializeField] private bool embeddedLevel;
    [SerializeField] private int embeddedLevelIndex;
    [SerializeField] private Sprite viewfinderSprite;
    [SerializeField] private Sprite viewfinderFrame;
    [SerializeField] private Sprite focusTrack;
    [SerializeField] private Sprite focusHandle;

    private RenderTexture displayTexture;
    private Material displayMaterial;
    private GameObject displayQuad;
    private float displayAspect = 16f / 9f;
    private int currentLevelIndex;
    private Transform embeddedBody;
    private Renderer embeddedScreen;
    private Vector3 unitRootToScreen;
    private Vector3 unitRootToBodyCenter;
    private Vector3 unitBodySize;
    private Vector3 unitBodyMinFromScreen;
    private bool embeddedMetricsReady;
    private Material[] embeddedPartMaterials;
    private Material embeddedScreenMaterial;
    private Scene loadedLevel;
    private bool loading;

    public int CurrentLevelNumber
    {
        get { return currentLevelIndex + 1; }
    }

    public static bool TryHandleLevelClear()
    {
        if (!IsHosting || Instance.embeddedLevel)
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
        if (embeddedLevel)
        {
            BindLevelCameraToDisplay();
        }
        else
        {
            FrameHostCameraToBody();
        }
    }

    private void Start()
    {
        if (embeddedLevel)
        {
            currentLevelIndex = Mathf.Clamp(embeddedLevelIndex, 0, LevelCount - 1);
            StartCoroutine(BindEmbeddedLevelRoutine());
            return;
        }

        StartCoroutine(LoadLevelRoutine(0, false));
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            ActiveEmbeddedFrame = null;
        }

        ReleaseEmbeddedFrameMaterials();
        ReleaseRenderTarget();
    }

    private void Update()
    {
        if (loading)
        {
            return;
        }

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
        if (embeddedLevel && !loading)
        {
            BindLevelCameraToDisplay();
            FitHudToOpening();
        }
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

    private RectTransform hudFrame;

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
        hudFrame = frameObject.GetComponent<RectTransform>();
        hudFrame.anchorMin = new Vector2(0.5f, 0.5f);
        hudFrame.anchorMax = new Vector2(0.5f, 0.5f);
        hudFrame.pivot = new Vector2(0f, 0f);

        CreateHudImage("Viewfinder", viewfinderFrame, hudFrame, new Vector2(0f, 0f), new Vector2(1f, 1f), false);

        GameObject trackObject = CreateHudImage("FocusTrack", focusTrack, hudFrame, new Vector2(0.259f, 0.110f), new Vector2(0.753f, 0.252f), true);
        GameObject handleObject = CreateHudImage("FocusHandle", focusHandle, trackObject.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), false);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(focusHandle.rect.width, focusHandle.rect.height);
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

    private void FitHudToOpening()
    {
        if (hudFrame == null)
        {
            GameObject existing = GameObject.Find("HudFrame");
            if (existing != null)
            {
                hudFrame = existing.GetComponent<RectTransform>();
            }
        }

        if (hudFrame == null || displayQuad == null)
        {
            return;
        }

        Camera frameCamera = FindHostCamera();
        if (frameCamera == null)
        {
            return;
        }

        Bounds bounds = displayQuad.GetComponent<Renderer>().bounds;
        Vector3 bottomLeft = frameCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
        Vector3 topRight = frameCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
        RectTransform canvasRect = hudFrame.parent as RectTransform;
        Vector2 localMin;
        Vector2 localMax;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, bottomLeft, null, out localMin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, topRight, null, out localMax))
        {
            return;
        }

        hudFrame.anchoredPosition = localMin;
        hudFrame.sizeDelta = localMax - localMin;
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

        if (embeddedLevel)
        {
            Camera hostCamera = FindHostCamera();
            if (hostCamera != null)
            {
                AudioListener hostListener = hostCamera.GetComponent<AudioListener>();
                if (hostListener != null)
                {
                    hostListener.enabled = true;
                }
            }
        }

        int cameraModelLayer = LayerMask.NameToLayer("CameraModel");
        if (cameraModelLayer >= 0)
        {
            levelCamera.cullingMask &= ~(1 << cameraModelLayer);
        }

        ApplyDisplayTexture();
        HideHostedOverlayUi();
    }

    private void HideHostedOverlayUi()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay && canvases[i].name != "CameraFrameHud")
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

        Renderer lcdGlass = FindLcdGlassRenderer(bodyRoot);
        Renderer openingPlate = FindNamedRenderer(bodyRoot, "PolySphere1_1");
        Renderer opening = lcdGlass != null ? lcdGlass : openingPlate;
        if (opening != null)
        {
            Bounds openingBounds = opening.bounds;
            float inset = Mathf.Clamp(Mathf.Min(screenFill.x, screenFill.y), 0.5f, 1f);
            float width = openingBounds.size.x * inset;
            float height = openingBounds.size.y * inset;
            if (lcdGlass != null && openingPlate != null)
            {
                float plateInset = Mathf.Clamp(Mathf.Min(screenFill.x, screenFill.y), 0.5f, 1f);
                float fittedWidth = openingPlate.bounds.size.x * plateInset;
                float fittedHeight = openingPlate.bounds.size.y * plateInset;
                float contain = Mathf.Min(
                    1f,
                    openingBounds.size.x / Mathf.Max(0.01f, fittedWidth),
                    openingBounds.size.y / Mathf.Max(0.01f, fittedHeight));
                width = fittedWidth * contain;
                height = fittedHeight * contain;
            }

            displayAspect = width / Mathf.Max(0.01f, height);
            displayQuad.transform.SetParent(null, false);
            displayQuad.transform.position = new Vector3(
                openingBounds.center.x,
                openingBounds.center.y,
                openingBounds.min.z - Mathf.Max(0.001f, screenLift));
            displayQuad.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            displayQuad.transform.localScale = new Vector3(width, height, 1f);
            displayQuad.transform.SetParent(bodyRoot, true);
        }
        else
        {
            displayQuad.transform.SetParent(bodyRoot, false);
            displayQuad.transform.localPosition = new Vector3(0.02f, -1.31f, -0.93f);
            displayQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            displayQuad.transform.localScale = new Vector3(2.9f, 1.78f, 1f);
        }

        if (lcdGlass != null)
        {
            lcdGlass.enabled = false;
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

    private static Renderer FindNamedRenderer(Transform root, string rendererName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == rendererName)
            {
                return children[i].GetComponent<Renderer>();
            }
        }

        return null;
    }

    private static Camera FindHostCamera()
    {
        if (Instance != null)
        {
            Camera[] sceneCameras = FindObjectsOfType<Camera>(true);
            for (int i = 0; i < sceneCameras.Length; i++)
            {
                Camera candidate = sceneCameras[i];
                if (candidate != null &&
                    candidate.gameObject.scene == Instance.gameObject.scene &&
                    candidate.name == "CameraFrameCamera")
                {
                    return candidate;
                }
            }
        }

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
        int textureHeight = Mathf.Max(16, displayHeight);
        int textureWidth = Mathf.Max(16, Mathf.RoundToInt(textureHeight * Mathf.Max(0.1f, displayAspect)));
        displayTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
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

        Vector2 scale = new Vector2(-1f, 1f);
        Vector2 offset = new Vector2(1f, 0f);
        displayMaterial.SetTexture("_BaseMap", displayTexture);
        displayMaterial.SetTexture("_MainTex", displayTexture);
        displayMaterial.SetTextureScale("_BaseMap", scale);
        displayMaterial.SetTextureOffset("_BaseMap", offset);
        displayMaterial.SetTextureScale("_MainTex", scale);
        displayMaterial.SetTextureOffset("_MainTex", offset);
        displayMaterial.mainTexture = displayTexture;
        displayMaterial.mainTextureScale = scale;
        displayMaterial.mainTextureOffset = offset;
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
