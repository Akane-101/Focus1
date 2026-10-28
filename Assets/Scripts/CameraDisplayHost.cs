using System.Collections;
using UnityEngine;
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

        StartCoroutine(LoadLevelRoutine(Mathf.Clamp(index, 0, LevelCount - 1), true));
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
        if (lcdGlass != null)
        {
            Bounds lcdBounds = lcdGlass.bounds;
            displayQuad.transform.position = new Vector3(lcdBounds.center.x, lcdBounds.center.y, lcdBounds.min.z - 0.02f);
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
