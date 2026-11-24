using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

public class VHSRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public Material retroVhsMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    private VHSRenderPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass?.Dispose();
        m_ScriptablePass = new VHSRenderPass(settings);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
        m_ScriptablePass = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isPreviewCamera || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        if (settings.material == null)
        {
            Debug.LogWarning("VHSRendererFeature: Material is missing.");
            return;
        }
        renderer.EnqueuePass(m_ScriptablePass);
    }

    private class VHSRenderPass : ScriptableRenderPass
    {
        private static readonly int s_ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int s_ScanlineCountId = Shader.PropertyToID("_ScanlineCount");
        private static readonly int s_ChromaticBleedId = Shader.PropertyToID("_ChromaticBleed");
        private static readonly int s_EdgeChromaticId = Shader.PropertyToID("_EdgeChromatic");
        private static readonly int s_TapeWobbleId = Shader.PropertyToID("_TapeWobble");
        private static readonly int s_TrackingStrengthId = Shader.PropertyToID("_TrackingStrength");
        private static readonly int s_GrainIntensityId = Shader.PropertyToID("_GrainIntensity");
        private static readonly int s_VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int s_FisheyeBendId = Shader.PropertyToID("_FisheyeBend");
        private static readonly int s_BlackBordersId = Shader.PropertyToID("_BlackBorders");
        private static readonly int s_ShadowTintId = Shader.PropertyToID("_ShadowTint");

        private Material m_Material;
        private Material m_RetroBaseMaterial;
        private Material m_RetroRuntimeMaterial;
        private int m_TempRTid;
        private static Material s_CopyMaterial;

        public VHSRenderPass(Settings settings)
        {
            m_Material = settings.material;
            m_RetroBaseMaterial = settings.retroVhsMaterial;
            renderPassEvent = settings.renderPassEvent;
            m_TempRTid = Shader.PropertyToID("_VHSTempRT");

            if (s_CopyMaterial == null)
            {
#if UNITY_6000_0_OR_NEWER
                s_CopyMaterial = Blitter.GetBlitMaterial(TextureDimension.Tex2D, false);
#endif
                if (s_CopyMaterial == null)
                {
                    Shader blitShader = Shader.Find("Hidden/Universal/CoreBlit");
                    if (blitShader == null) blitShader = Shader.Find("Hidden/Universal Render Pipeline/Blit");
                    if (blitShader == null) blitShader = Shader.Find("Hidden/BlitCopy");
                    if (blitShader != null)
                    {
                        s_CopyMaterial = new Material(blitShader);
                        s_CopyMaterial.hideFlags = HideFlags.HideAndDontSave;
                    }
                    else
                    {
                        Debug.LogError("VHSRendererFeature: Could not find a valid blit shader for copying.");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (m_RetroRuntimeMaterial != null)
            {
                CoreUtils.Destroy(m_RetroRuntimeMaterial);
                m_RetroRuntimeMaterial = null;
            }
        }

        private Material ResolveActiveMaterial()
        {
            var retroVol = VolumeManager.instance.stack.GetComponent<RetroVHSVolumeComponent>();
            if (retroVol == null || !retroVol.IsActive())
                return m_Material;

            if (m_RetroRuntimeMaterial == null)
            {
                if (m_RetroBaseMaterial != null)
                {
                    m_RetroRuntimeMaterial = new Material(m_RetroBaseMaterial);
                }
                else
                {
                    Shader s = Shader.Find("Custom/RetroVHS_URP");
                    if (s == null) return m_Material;
                    m_RetroRuntimeMaterial = new Material(s);
                }
                m_RetroRuntimeMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            m_RetroRuntimeMaterial.SetFloat(s_ScanlineIntensityId, retroVol.scanlineIntensity.value);
            m_RetroRuntimeMaterial.SetFloat(s_ScanlineCountId, retroVol.scanlineCount.value);
            m_RetroRuntimeMaterial.SetFloat(s_ChromaticBleedId, retroVol.chromaticBleed.value);
            m_RetroRuntimeMaterial.SetFloat(s_EdgeChromaticId, retroVol.edgeChromatic.value);
            m_RetroRuntimeMaterial.SetFloat(s_TapeWobbleId, retroVol.tapeWobble.value);
            m_RetroRuntimeMaterial.SetFloat(s_TrackingStrengthId, retroVol.trackingStrength.value);
            m_RetroRuntimeMaterial.SetFloat(s_GrainIntensityId, retroVol.grainIntensity.value);
            m_RetroRuntimeMaterial.SetFloat(s_VignetteIntensityId, retroVol.vignetteIntensity.value);
            m_RetroRuntimeMaterial.SetFloat(s_FisheyeBendId, retroVol.fisheyeBend.value);
            m_RetroRuntimeMaterial.SetFloat(s_BlackBordersId, retroVol.blackBorders.value ? 1f : 0f);
            m_RetroRuntimeMaterial.SetColor(s_ShadowTintId, retroVol.shadowTint.value);
            return m_RetroRuntimeMaterial;
        }

#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            Material activeMaterial = ResolveActiveMaterial();
            if (activeMaterial == null) return;
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle cameraColor = resourceData.activeColorTexture;

            TextureDesc descriptor = cameraColor.GetDescriptor(renderGraph);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.name = "_VHSTempRT";
            descriptor.clearBuffer = false;
            descriptor.useDynamicScale = true;

            TextureHandle tempTex = renderGraph.CreateTexture(descriptor);

            if (s_CopyMaterial == null)
            {
                s_CopyMaterial = Blitter.GetBlitMaterial(TextureDimension.Tex2D, false);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("VHS Copy", out var passData))
            {
                passData.source = cameraColor;
                passData.copyMaterial = s_CopyMaterial;
                builder.UseTexture(cameraColor, AccessFlags.Read);
                builder.SetRenderAttachment(tempTex, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    if (data.copyMaterial != null)
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, Vector2.one, data.copyMaterial, 0);
                    }
                    else
                    {
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
                    }
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("VHS Effect", out var passData))
            {
                passData.source = tempTex;
                passData.copyMaterial = null;
                passData.material = activeMaterial;
                builder.UseTexture(tempTex, AccessFlags.Read);
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, Vector2.one, data.material, 0);
                });
            }
        }

        class PassData
        {
            public TextureHandle source;
            public Material material;
            public Material copyMaterial;
        }
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Material activeMaterial = ResolveActiveMaterial();
            if (activeMaterial == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("VHS Effect");

            RenderTargetIdentifier cameraColorTarget =
                renderingData.cameraData.renderer.cameraColorTarget;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(m_TempRTid, descriptor);

            cmd.SetGlobalTexture("_BlitTexture", cameraColorTarget);
            cmd.Blit(cameraColorTarget, m_TempRTid);

            cmd.SetGlobalTexture("_BlitTexture", m_TempRTid);
            cmd.Blit(m_TempRTid, cameraColorTarget, activeMaterial);

            cmd.ReleaseTemporaryRT(m_TempRTid);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
    }
}
