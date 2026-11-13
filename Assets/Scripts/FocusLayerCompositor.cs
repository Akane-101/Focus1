using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public sealed class FocusLayerCompositor : MonoBehaviour
{
    private const int BlurPasses = 2;
    private const int FillPass = 2;
    private const int BlendPass = 1;
    private const int KawasePass = 0;
    private const int PremulPass = 3;
    private const string FarObjectName = "Depth_Far";
    private const string MidObjectName = "Depth_Mid";
    private const string NearObjectName = "Depth_Near";

    private static readonly FocusLayer[] CompositeOrder =
    {
        FocusLayer.Far,
        FocusLayer.Mid,
        FocusLayer.Near
    };

    [Header("Bloom")]
    [Tooltip("Soft halo around existing highlights on the composited game image. 0 turns bloom off. Does not follow focus.")]
    [SerializeField] [Range(0f, 8f)] private float bloomIntensity;
    [Tooltip("Pixels brighter than this contribute to bloom. Lower values let more of the bright areas glow.")]
    [SerializeField] [Range(0f, 2f)] private float bloomThreshold = 0.9f;

    private Camera mainCamera;
    private FocusLevelState levelState;
    private Volume bloomVolume;
    private Bloom bloom;
    private Material fxMaterial;
    private Color backgroundColor;

    private readonly Camera[] layerCameras = new Camera[3];
    private readonly UniversalAdditionalCameraData[] layerCameraData = new UniversalAdditionalCameraData[3];
    private readonly RenderTexture[] layerTextures = new RenderTexture[3];
    private readonly RTHandle[] layerHandles = new RTHandle[3];
    private readonly float[] sharpness = new float[3];
    private UniversalAdditionalCameraData playerCameraData;
    private UniversalAdditionalCameraData mainCameraData;
    private FocusLayer incomingLayer;
    private FocusLayer outgoingLayer;

    private Camera playerCamera;
    private RenderTexture playerTexture;
    private RTHandle playerHandle;
    private int textureWidth;
    private int textureHeight;
    private int originalCullingMask;
    private bool hasCapturedBackground;
    private bool urpLayerMode;

    public bool IsUrpRenderingReady
    {
        get
        {
            return urpLayerMode && enabled &&
                   layerTextures[0] != null &&
                   layerTextures[1] != null &&
                   layerTextures[2] != null &&
                   playerTexture != null &&
                   playerHandle != null;
        }
    }

    public Color BackgroundColor
    {
        get { return backgroundColor; }
    }

    public RenderTexture PlayerLayerTexture
    {
        get { return playerTexture; }
    }

    public RTHandle GetPlayerHandle()
    {
        return playerHandle;
    }

    public bool DrawsPlayerInCurrentLayer
    {
        get { return levelState != null && DrawsPlayerInLayer(levelState.CurrentLayer); }
    }

    public FocusLayer CurrentFocusLayer
    {
        get { return levelState != null ? levelState.CurrentLayer : FocusLayer.Mid; }
    }

    public RenderTexture GetLayerTexture(FocusLayer layer)
    {
        return layerTextures[(int)layer];
    }

    public RTHandle GetLayerHandle(FocusLayer layer)
    {
        return layerHandles[(int)layer];
    }

    public float GetFocusBlend(FocusLayer layer)
    {
        return Mathf.SmoothStep(0f, 1f, sharpness[(int)layer]);
    }

    public bool IsLayerInFocus(FocusLayer layer)
    {
        return levelState != null && levelState.CurrentLayer == layer && sharpness[(int)layer] >= 1f;
    }

    public float GetBlurAmount(FocusLayer layer)
    {
        FocusLayerView view = ViewFor(layer);
        if (view != null)
        {
            return view.BlurStrength;
        }

        return levelState != null ? levelState.UnfocusedBlur : 0f;
    }

    public float BloomIntensity
    {
        get { return bloomIntensity; }
    }

    public float BloomThreshold
    {
        get { return bloomThreshold; }
    }

    private FocusLayerView[] layerViews;

    private FocusLayerView ViewFor(FocusLayer layer)
    {
        if (layerViews == null)
        {
            layerViews = FindObjectsOfType<FocusLayerView>(true);
        }

        for (int i = 0; i < layerViews.Length; i++)
        {
            FocusLayerView view = layerViews[i];
            if (view != null && view.RepresentedLayer == layer)
            {
                return view;
            }
        }

        return null;
    }

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        originalCullingMask = mainCamera.cullingMask;
        mainCamera.allowMSAA = false;
    }

    private void Start()
    {
        levelState = FindObjectOfType<FocusLevelState>();

        if (levelState == null)
        {
            Debug.LogError("Focus layer compositor is missing FocusLevelState.");
            enabled = false;
            return;
        }

        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
        {
            urpLayerMode = true;
            AssignWorldLayers();
            AssignPlayerLayer();
            SnapSharpness();
            CaptureBackground();
            mainCamera.backgroundColor = backgroundColor;
            ApplyMainCulling();
            EnsureResources();
            SyncLayerCameras();
            return;
        }

        Shader fxShader = Shader.Find("Hidden/FocusLayerFX");

        if (fxShader == null)
        {
            Debug.LogError("Focus layer compositor is missing Hidden/FocusLayerFX.");
            enabled = false;
            return;
        }

        fxMaterial = new Material(fxShader);
        fxMaterial.hideFlags = HideFlags.HideAndDontSave;
        AssignWorldLayers();
        AssignPlayerLayer();
        SnapSharpness();
        CaptureBackground();
        ApplyMainCulling();
        EnsureResources();
    }

    private void LateUpdate()
    {
        if (levelState == null)
        {
            return;
        }

        if (!urpLayerMode && fxMaterial == null)
        {
            return;
        }

        AnimateSharpness();
        ApplyMainCulling();
        EnsureResources();
        SyncLayerCameras();
        ApplyBloom();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (fxMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        RenderTexture composite = RenderTexture.GetTemporary(source.descriptor);
        fxMaterial.SetColor("_Color", backgroundColor);
        Graphics.Blit(source, composite, fxMaterial, FillPass);
        Graphics.Blit(source, composite, fxMaterial, BlendPass);

        for (int i = 0; i < CompositeOrder.Length; i++)
        {
            FocusLayer layer = CompositeOrder[i];
            RenderTexture layerTexture = layerTextures[(int)layer];

            if (layerTexture == null)
            {
                continue;
            }

            float blend = GetFocusBlend(layer);
            RenderTextureDescriptor premulDescriptor = source.descriptor;
            premulDescriptor.depthBufferBits = 0;
            premulDescriptor.msaaSamples = 1;
            RenderTexture sharp = RenderTexture.GetTemporary(premulDescriptor);
            Graphics.Blit(layerTexture, sharp, fxMaterial, PremulPass);

            if (blend >= 0.995f)
            {
                Graphics.Blit(sharp, composite, fxMaterial, BlendPass);
            }
            else
            {
                RenderTexture blurred = BlurLayer(sharp, source.descriptor, GetBlurAmount(layer));
                fxMaterial.SetTexture("_FocusSharpTex", sharp);
                fxMaterial.SetFloat("_FocusBlend", blend);
                Graphics.Blit(blurred, composite, fxMaterial, 4);
                if (blurred != sharp)
                {
                    RenderTexture.ReleaseTemporary(blurred);
                }
            }

            RenderTexture.ReleaseTemporary(sharp);

            if (layer == levelState.CurrentLayer && playerTexture != null)
            {
                Graphics.Blit(playerTexture, composite, fxMaterial, BlendPass);
            }
        }

        Graphics.Blit(composite, destination);
        RenderTexture.ReleaseTemporary(composite);
    }

    private void OnDestroy()
    {
        if (mainCamera != null)
        {
            mainCamera.cullingMask = originalCullingMask;
        }

        for (int i = 0; i < layerCameras.Length; i++)
        {
            if (layerCameras[i] != null)
            {
                Destroy(layerCameras[i].gameObject);
            }
        }

        if (playerCamera != null)
        {
            Destroy(playerCamera.gameObject);
        }

        ReleaseLayerTextures();

        if (fxMaterial != null)
        {
            Destroy(fxMaterial);
        }

        ReleaseBloomVolume();
    }

    private void ApplyBloom()
    {
        if (!urpLayerMode || mainCamera == null)
        {
            return;
        }

        if (bloom == null)
        {
            CreateBloomVolume();
        }

        if (bloom == null)
        {
            return;
        }

        bool enabled = bloomIntensity > 0.0001f;
        bloom.active = enabled;
        bloom.intensity.value = bloomIntensity;
        bloom.threshold.value = bloomThreshold;

        if (mainCameraData == null)
        {
            mainCameraData = mainCamera.GetUniversalAdditionalCameraData();
        }

        if (mainCameraData != null && mainCameraData.renderPostProcessing != enabled)
        {
            mainCameraData.renderPostProcessing = enabled;
        }

        if (enabled)
        {
            mainCamera.allowHDR = true;
        }
    }

    private void CreateBloomVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.HideAndDontSave;
        bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.7f);
        bloom.highQualityFiltering.Override(true);
        bloom.active = bloomIntensity > 0.0001f;

        GameObject volumeObject = new GameObject("Focus Bloom Volume");
        volumeObject.hideFlags = HideFlags.DontSave;
        volumeObject.layer = 0;
        volumeObject.transform.SetParent(transform, false);
        bloomVolume = volumeObject.AddComponent<Volume>();
        bloomVolume.isGlobal = true;
        bloomVolume.priority = 50f;
        bloomVolume.weight = 1f;
        bloomVolume.sharedProfile = profile;
    }

    private void ReleaseBloomVolume()
    {
        if (mainCameraData != null)
        {
            mainCameraData.renderPostProcessing = false;
        }

        if (bloomVolume == null)
        {
            bloom = null;
            return;
        }

        VolumeProfile profile = bloomVolume.sharedProfile;
        Destroy(bloomVolume.gameObject);
        if (profile != null)
        {
            Destroy(profile);
        }

        bloomVolume = null;
        bloom = null;
    }

    private void AssignWorldLayers()
    {
        AssignNamedLayer(FarObjectName, FocusLayer.Far);
        AssignNamedLayer(MidObjectName, FocusLayer.Mid);
        AssignNamedLayer(NearObjectName, FocusLayer.Near);
    }

    private static void AssignNamedLayer(string objectName, FocusLayer layer)
    {
        GameObject layerObject = GameObject.Find(objectName);

        if (layerObject != null)
        {
            FocusUnityLayers.Assign(layerObject.transform, layer);
        }
    }

    private void AssignPlayerLayer()
    {
        if (levelState != null && levelState.PlayerTransform != null)
        {
            int playerLayer = LayerMask.NameToLayer(FocusUnityLayers.Player);

            if (playerLayer >= 0)
            {
                FocusUnityLayers.Assign(levelState.PlayerTransform, playerLayer);
            }
        }
    }

    private void SnapSharpness()
    {
        incomingLayer = levelState.CurrentLayer;
        outgoingLayer = levelState.CurrentLayer;
        for (int i = 0; i < sharpness.Length; i++)
        {
            sharpness[i] = levelState.CurrentLayer == (FocusLayer)i ? 1f : 0f;
        }
    }

    private void AnimateSharpness()
    {
        FocusLayer current = levelState.CurrentLayer;
        if (current != incomingLayer)
        {
            outgoingLayer = incomingLayer;
            incomingLayer = current;
        }

        float duration = Mathf.Max(0.05f, levelState.FocusTransitionDuration);
        float factor = 1f - Mathf.Exp(-Time.deltaTime * 4.5f / duration);

        for (int i = 0; i < sharpness.Length; i++)
        {
            FocusLayer layer = (FocusLayer)i;
            if (layer == incomingLayer)
            {
                sharpness[i] = Mathf.Lerp(sharpness[i], 1f, factor);
                if (sharpness[i] > 0.9985f)
                {
                    sharpness[i] = 1f;
                }

                continue;
            }

            if (layer == outgoingLayer)
            {
                sharpness[i] = Mathf.Lerp(sharpness[i], 0f, factor);
                if (sharpness[i] < 0.0015f)
                {
                    sharpness[i] = 0f;
                }

                continue;
            }

            sharpness[i] = 0f;
        }
    }

    private void CaptureBackground()
    {
        if (hasCapturedBackground)
        {
            return;
        }

        backgroundColor = mainCamera.backgroundColor;
        backgroundColor.a = 1f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        // The legacy OnRenderImage compositor needs a transparent camera target so
        // it can rebuild the frame. URP composites its layer textures in a renderer
        // feature, so the main camera must keep an opaque background; otherwise
        // transparent black is cleared into the camera color target and appears as
        // a black Game view.
        mainCamera.backgroundColor = urpLayerMode
            ? backgroundColor
            : new Color(0f, 0f, 0f, 0f);
        hasCapturedBackground = true;
    }

    private void ApplyMainCulling()
    {
        // World layers and the player are composited in depth order (Far behind the
        // player, Near in front). Keep only non-focus objects on the level camera.
        // The camera body is CameraModel and is drawn only by the frame camera.
        int mask = originalCullingMask & ~FocusUnityLayers.GetCullingMask();
        int cameraModelLayer = LayerMask.NameToLayer("CameraModel");
        if (cameraModelLayer >= 0)
        {
            mask &= ~(1 << cameraModelLayer);
        }

        mainCamera.cullingMask = mask;
    }

    private void EnsureResources()
    {
        int width = Mathf.Max(2, mainCamera.pixelWidth);
        int height = Mathf.Max(2, mainCamera.pixelHeight);

        if (textureWidth == width && textureHeight == height && layerTextures[0] != null && playerTexture != null && playerHandle != null)
        {
            return;
        }

        textureWidth = width;
        textureHeight = height;
        ReleaseLayerTextures();

        for (int i = 0; i < CompositeOrder.Length; i++)
        {
            FocusLayer layer = CompositeOrder[i];
            int layerIndex = (int)layer;

            RenderTexture layerTexture = CreateLayerTexture("FocusLayerRT_" + layer);
            layerTextures[layerIndex] = layerTexture;
            layerHandles[layerIndex] = RTHandles.Alloc(layerTexture);

            if (layerCameras[layerIndex] == null)
            {
                layerCameras[layerIndex] = CreateLayerCamera("FocusLayerCamera_" + layer);
            }
        }

        if (playerTexture == null)
        {
            playerTexture = CreateLayerTexture("FocusLayerRT_Player");
            playerHandle = RTHandles.Alloc(playerTexture);
        }

        if (playerCamera == null)
        {
            playerCamera = CreateLayerCamera("FocusLayerCamera_Player");
        }
    }

    private RenderTexture CreateLayerTexture(string textureName)
    {
        RenderTexture layerTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        layerTexture.Create();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = layerTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = previous;
        return layerTexture;
    }

    private Camera CreateLayerCamera(string cameraName)
    {
        GameObject cameraObject = new GameObject(cameraName);
        cameraObject.hideFlags = HideFlags.HideInHierarchy;
        if (mainCamera != null && mainCamera.gameObject.scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(cameraObject, mainCamera.gameObject.scene);
        }

        Camera layerCamera = cameraObject.AddComponent<Camera>();
        layerCamera.enabled = false;
        layerCamera.allowMSAA = false;
        layerCamera.allowHDR = false;
        layerCamera.depth = -100;
        return layerCamera;
    }

    private void SyncLayerCameras()
    {
        AssignWorldLayers();
        AssignPlayerLayer();
        for (int i = 0; i < CompositeOrder.Length; i++)
        {
            FocusLayer layer = CompositeOrder[i];
            int layerIndex = (int)layer;
            Camera layerCamera = layerCameras[layerIndex];
            int unityLayer = FocusUnityLayers.GetIndex(layer);

            if (layerCamera == null || unityLayer < 0 || layerTextures[layerIndex] == null)
            {
                continue;
            }

            ConfigureOverlayCamera(
                layerCamera,
                ref layerCameraData[layerIndex],
                layerTextures[layerIndex],
                1 << unityLayer,
                -100 + layerIndex,
                true);
        }

        int overlayPlayerLayer = LayerMask.NameToLayer(FocusUnityLayers.Player);
        if (playerCamera != null && playerTexture != null && overlayPlayerLayer >= 0)
        {
            ConfigureOverlayCamera(playerCamera, ref playerCameraData, playerTexture, 1 << overlayPlayerLayer, -97, true);
        }
    }

    private bool DrawsPlayerInLayer(FocusLayer layer)
    {
        return false;
    }

    private void ConfigureOverlayCamera(
        Camera overlayCamera,
        ref UniversalAdditionalCameraData cameraData,
        RenderTexture targetTexture,
        int cullingMask,
        float depth,
        bool enableCamera)
    {
        overlayCamera.enabled = false;
        overlayCamera.targetTexture = targetTexture;
        overlayCamera.orthographic = mainCamera.orthographic;
        overlayCamera.orthographicSize = mainCamera.orthographicSize;
        overlayCamera.nearClipPlane = mainCamera.nearClipPlane;
        overlayCamera.farClipPlane = mainCamera.farClipPlane;
        overlayCamera.rect = mainCamera.rect;
        overlayCamera.fieldOfView = mainCamera.fieldOfView;
        overlayCamera.allowMSAA = false;
        overlayCamera.allowHDR = false;
        overlayCamera.forceIntoRenderTexture = true;
        overlayCamera.clearFlags = CameraClearFlags.SolidColor;
        overlayCamera.backgroundColor = Color.clear;
        overlayCamera.cullingMask = cullingMask;
        overlayCamera.depth = depth;
        overlayCamera.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        if (mainCamera != null && overlayCamera.gameObject.scene != mainCamera.gameObject.scene && mainCamera.gameObject.scene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(overlayCamera.gameObject, mainCamera.gameObject.scene);
        }

        if (cameraData == null)
        {
            cameraData = overlayCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.dithering = false;
            cameraData.stopNaN = false;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.volumeLayerMask = 0;
            cameraData.renderShadows = false;
            cameraData.SetRenderer(0);
            if (cameraData.cameraStack != null)
            {
                cameraData.cameraStack.Clear();
            }
        }

        cameraData.renderType = CameraRenderType.Base;
        overlayCamera.targetTexture = targetTexture;
        DetachFromMainStack(overlayCamera);
        overlayCamera.enabled = enableCamera && targetTexture != null;
    }

    private void DetachFromMainStack(Camera overlayCamera)
    {
        if (mainCameraData == null)
        {
            mainCameraData = mainCamera.GetUniversalAdditionalCameraData();
        }

        if (mainCameraData == null || mainCameraData.cameraStack == null)
        {
            return;
        }

        for (int i = mainCameraData.cameraStack.Count - 1; i >= 0; i--)
        {
            if (mainCameraData.cameraStack[i] == overlayCamera)
            {
                mainCameraData.cameraStack.RemoveAt(i);
            }
        }
    }

    private RenderTexture BlurLayer(RenderTexture source, RenderTextureDescriptor descriptor, float blurAmount)
    {
        if (blurAmount <= 0.01f)
        {
            return source;
        }

        RenderTextureDescriptor blurDescriptor = descriptor;
        blurDescriptor.depthBufferBits = 0;
        blurDescriptor.msaaSamples = 1;
        blurDescriptor.width = Mathf.Max(2, descriptor.width / 2);
        blurDescriptor.height = Mathf.Max(2, descriptor.height / 2);

        RenderTexture bufferA = RenderTexture.GetTemporary(blurDescriptor);
        RenderTexture bufferB = RenderTexture.GetTemporary(blurDescriptor);
        Graphics.Blit(source, bufferA);

        for (int i = 0; i < BlurPasses; i++)
        {
            fxMaterial.SetFloat("_Offset", (0.5f + i) * blurAmount);
            Graphics.Blit(bufferA, bufferB, fxMaterial, KawasePass);

            RenderTexture swap = bufferA;
            bufferA = bufferB;
            bufferB = swap;
        }

        RenderTexture.ReleaseTemporary(bufferB);
        return bufferA;
    }

    private void ReleaseLayerTextures()
    {
        for (int i = 0; i < layerTextures.Length; i++)
        {
            layerHandles[i] = null;

            if (layerTextures[i] != null)
            {
                layerTextures[i].Release();
                Destroy(layerTextures[i]);
                layerTextures[i] = null;
            }
        }

        playerHandle = null;
        if (playerTexture != null)
        {
            playerTexture.Release();
            Destroy(playerTexture);
            playerTexture = null;
        }
    }
}
