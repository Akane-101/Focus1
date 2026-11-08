using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP replacement for FocusLayerCompositor.OnRenderImage.
/// Uses Blitter + a URP blit shader so Kawase actually samples the layer textures.
/// </summary>
public sealed class FocusLayerBlurRendererFeature : ScriptableRendererFeature
{
    private const string ShaderName = "Hidden/FocusLayerFX";
    public Shader shader;
    private Material material;
    private Material[] blurMaterials;
    private FocusLayerCompositePass pass;
    private CameraFrameOverlayPass framePass;

    public override void Create()
    {
        if (material != null)
        {
            CoreUtils.Destroy(material);
        }
        if (blurMaterials != null)
        {
            for (int i = 0; i < blurMaterials.Length; i++)
            {
                CoreUtils.Destroy(blurMaterials[i]);
            }
        }

        Shader resolvedShader = shader != null ? shader : Shader.Find(ShaderName);
        if (resolvedShader == null)
        {
            Debug.LogError("Missing shader: " + ShaderName);
            return;
        }

        material = CoreUtils.CreateEngineMaterial(resolvedShader);
        blurMaterials = new Material[2];
        for (int i = 0; i < blurMaterials.Length; i++)
        {
            blurMaterials[i] = CoreUtils.CreateEngineMaterial(resolvedShader);
        }

        pass = new FocusLayerCompositePass(material, blurMaterials);
        framePass = new CameraFrameOverlayPass();
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (camera == null)
        {
            return;
        }

        FocusLayerCompositor compositor = camera.GetComponent<FocusLayerCompositor>();
        if (compositor != null && pass != null)
        {
            pass.Setup(renderer.cameraColorTargetHandle, compositor);
        }

        if (compositor != null && framePass != null)
        {
            framePass.Setup(renderer.cameraColorTargetHandle);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (camera == null || pass == null || material == null)
        {
            return;
        }

        if (camera.GetComponent<FocusLayerCompositor>() != null)
        {
            renderer.EnqueuePass(pass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (pass != null)
        {
            pass.DisposeHandles();
        }

        if (framePass != null)
        {
            framePass.DisposeHandles();
        }

        if (material != null)
        {
            CoreUtils.Destroy(material);
            material = null;
        }
        if (blurMaterials != null)
        {
            for (int i = 0; i < blurMaterials.Length; i++)
            {
                CoreUtils.Destroy(blurMaterials[i]);
            }
            blurMaterials = null;
        }
    }

    private sealed class FocusLayerCompositePass : ScriptableRenderPass
    {
        private const int BlurPass = 0;
        private const int BlendPass = 1;
        private const int PremulPass = 3;
        private const int MixPass = 4;
        private const int BlurIterations = 2;

        private static readonly int FocusSharpTexId = Shader.PropertyToID("_FocusSharpTex");
        private static readonly int FocusBlendId = Shader.PropertyToID("_FocusBlend");
        private static readonly int FocusTexelSizeId = Shader.PropertyToID("_FocusTexelSize");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");

        private readonly Material material;
        private readonly Material[] blurMaterials;
        private RTHandle cameraColor;
        private RTHandle compositeHandle;
        private RTHandle sharpHandle;
        private RTHandle blurAHandle;
        private RTHandle blurBHandle;
        private FocusLayerCompositor compositor;
        private int blurWidth;
        private int blurHeight;

        public FocusLayerCompositePass(Material material, Material[] blurMaterials)
        {
            this.material = material;
            this.blurMaterials = blurMaterials;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void Setup(RTHandle cameraColor, FocusLayerCompositor compositor)
        {
            this.cameraColor = cameraColor;
            this.compositor = compositor;
        }

        public void DisposeHandles()
        {
            compositeHandle?.Release();
            sharpHandle?.Release();
            blurAHandle?.Release();
            blurBHandle?.Release();
            compositeHandle = null;
            sharpHandle = null;
            blurAHandle = null;
            blurBHandle = null;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

            RenderingUtils.ReAllocateIfNeeded(ref compositeHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FocusLayerCompositeRT");
            RenderingUtils.ReAllocateIfNeeded(ref sharpHandle, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FocusLayerSharpRT");

            RenderTextureDescriptor blurDescriptor = descriptor;
            blurDescriptor.width = Mathf.Max(2, descriptor.width / 2);
            blurDescriptor.height = Mathf.Max(2, descriptor.height / 2);
            blurWidth = blurDescriptor.width;
            blurHeight = blurDescriptor.height;
            RenderingUtils.ReAllocateIfNeeded(ref blurAHandle, blurDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FocusLayerBlurA");
            RenderingUtils.ReAllocateIfNeeded(ref blurBHandle, blurDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FocusLayerBlurB");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || compositor == null || !compositor.IsUrpRenderingReady)
            {
                return;
            }

            if (cameraColor == null)
            {
                cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

            if (cameraColor == null || compositeHandle == null || sharpHandle == null || blurAHandle == null || blurBHandle == null)
            {
                return;
            }

            CommandBuffer command = CommandBufferPool.Get("Focus Layer Blur Composite");
            Blitter.BlitCameraTexture(command, cameraColor, compositeHandle);

            CompositeLayer(command, FocusLayer.Far);
            CompositePlayerIfCurrent(command, FocusLayer.Far);
            CompositeLayer(command, FocusLayer.Mid);
            CompositePlayerIfCurrent(command, FocusLayer.Mid);
            CompositeLayer(command, FocusLayer.Near);
            CompositePlayerIfCurrent(command, FocusLayer.Near);

            Blitter.BlitCameraTexture(command, compositeHandle, cameraColor);
            context.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command);
        }

        private void CompositePlayerIfCurrent(CommandBuffer command, FocusLayer layer)
        {
            if (layer != compositor.CurrentFocusLayer)
            {
                return;
            }

            RTHandle playerHandle = compositor.GetPlayerHandle();
            if (playerHandle == null)
            {
                return;
            }

            Blitter.BlitCameraTexture(command, playerHandle, sharpHandle, material, PremulPass);
            BlitOnto(command, sharpHandle, compositeHandle, material, BlendPass);
        }

        private void CompositeLayer(CommandBuffer command, FocusLayer layer)
        {
            RTHandle layerHandle = compositor.GetLayerHandle(layer);
            if (layerHandle == null)
            {
                return;
            }

            float blend = compositor.GetFocusBlend(layer);
            Blitter.BlitCameraTexture(command, layerHandle, sharpHandle, material, PremulPass);
            if (blend >= 0.995f)
            {
                BlitOnto(command, sharpHandle, compositeHandle, material, BlendPass);
                return;
            }

            float blurAmount = compositor.GetBlurAmount(layer);
            Blitter.BlitCameraTexture(command, sharpHandle, blurAHandle);
            RTHandle source = blurAHandle;
            RTHandle destination = blurBHandle;
            Vector4 blurTexel = new Vector4(1f / blurWidth, 1f / blurHeight, blurWidth, blurHeight);
            for (int i = 0; i < BlurIterations; i++)
            {
                command.SetGlobalVector(FocusTexelSizeId, blurTexel);
                command.SetGlobalFloat(OffsetId, (0.5f + i) * blurAmount);
                Blitter.BlitCameraTexture(command, source, destination, blurMaterials[i], BlurPass);

                RTHandle swap = source;
                source = destination;
                destination = swap;
            }

            if (blend <= 0.01f)
            {
                BlitOnto(command, source, compositeHandle, material, BlendPass);
                return;
            }

            command.SetGlobalTexture(FocusSharpTexId, sharpHandle.nameID);
            command.SetGlobalFloat(FocusBlendId, blend);
            BlitOnto(command, source, compositeHandle, material, MixPass);
        }

        private static void BlitOnto(CommandBuffer command, RTHandle source, RTHandle destination, Material blitMaterial, int pass)
        {
            CoreUtils.SetRenderTarget(command, destination, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
            Blitter.BlitTexture(command, source, new Vector4(1f, 1f, 0f, 0f), blitMaterial, pass);
        }
    }

    private sealed class CameraFrameOverlayPass : ScriptableRenderPass
    {
        private RTHandle cameraColor;
        private RTHandle gameCopy;

        public CameraFrameOverlayPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void Setup(RTHandle color)
        {
            cameraColor = color;
        }

        public void DisposeHandles()
        {
            gameCopy?.Release();
            gameCopy = null;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref gameCopy, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_CameraFrameGameCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CameraDisplayHost.EmbeddedFrame frame = CameraDisplayHost.ActiveEmbeddedFrame;
            if (frame == null || frame.screen == null || frame.screenMaterial == null || gameCopy == null)
            {
                return;
            }

            if (cameraColor == null)
            {
                cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

            if (cameraColor == null)
            {
                return;
            }

            CommandBuffer command = CommandBufferPool.Get("Camera Frame Overlay");
            Blitter.BlitCameraTexture(command, cameraColor, gameCopy);
            frame.screenMaterial.SetTexture("_FrameGameTex", gameCopy.rt);
            RTHandle depth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            if (depth != null)
            {
                CoreUtils.SetRenderTarget(command, cameraColor, depth, ClearFlag.None, Color.clear);
            }
            else
            {
                CoreUtils.SetRenderTarget(command, cameraColor, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
            }

            if (frame.parts != null)
            {
                for (int i = 0; i < frame.parts.Length; i++)
                {
                    Renderer part = frame.parts[i];
                    Material partMaterial = frame.partMaterials != null && i < frame.partMaterials.Length ? frame.partMaterials[i] : null;
                    if (part == null || partMaterial == null || !part.enabled || !part.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    command.DrawRenderer(part, partMaterial);
                }
            }

            if (frame.screen.enabled && frame.screen.gameObject.activeInHierarchy)
            {
                command.DrawRenderer(frame.screen, frame.screenMaterial);
            }

            context.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command);
        }
    }
}
