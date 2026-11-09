using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PSXRetro
{
    public sealed class PSXRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            public bool applyInSceneView = true;
            public Shader postProcessShader;
        }

        public Settings settings = new Settings();

        private Material _material;
        private PSXRenderPass _pass;

        public override void Create()
        {
            if (settings.postProcessShader == null)
            {
                settings.postProcessShader = Shader.Find("Hidden/PSX/PSX_PostProcess");
            }

            if (settings.postProcessShader != null)
            {
                _material = CoreUtils.CreateEngineMaterial(settings.postProcessShader);
            }

            _pass = new PSXRenderPass(_material, settings)
            {
                renderPassEvent = settings.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                if (settings.postProcessShader == null)
                    settings.postProcessShader = Shader.Find("Hidden/PSX/PSX_PostProcess");

                if (settings.postProcessShader != null)
                    _material = CoreUtils.CreateEngineMaterial(settings.postProcessShader);
            }

            if (_material == null || _pass == null) return;

            CameraType camType = renderingData.cameraData.cameraType;
            bool isGameCamera = (camType == CameraType.Game);
            bool isSceneCamera = (camType == CameraType.SceneView && settings.applyInSceneView);

            if (isGameCamera || isSceneCamera)
            {
                _pass.Setup(_material);
                renderer.EnqueuePass(_pass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            base.Dispose(disposing);
        }

        private class PSXRenderPass : ScriptableRenderPass
        {
            private const string PassName = "PSXPostProcessPass";
            private Material _material;
            private Settings _settings;

            public PSXRenderPass(Material material, Settings settings)
            {
                _material = material;
                _settings = settings;
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material)
            {
                _material = material;
            }

            private void UpdatePassUniforms(Material mat, Camera cam)
            {
                if (mat == null) return;

                if (PSXSystem.Instance != null)
                {
                    PSXSystem.Instance.UpdateShaderGlobals();
                    Vector2 res = PSXSystem.Instance.GetAspectAwareResolution(cam);
                    mat.SetVector("_PSX_ScreenResolution", new Vector4(res.x, res.y, 1f / res.x, 1f / res.y));
                }
                else
                {
                    float aspect = (cam != null && cam.aspect > 0.001f) ? cam.aspect : (16f / 9f);
                    int height = 240;
                    int width = Mathf.RoundToInt(height * aspect);
                    mat.SetVector("_PSX_ScreenResolution", new Vector4(width, height, 1f / width, 1f / height));
                    mat.SetFloat("_PSX_ColorPrecision", 32f);
                    mat.SetFloat("_PSX_DitherStrength", 1.0f);
                    mat.SetFloat("_PSX_FogEnabled", 0f);
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                TextureHandle src = resourceData.activeColorTexture;
                TextureDesc dstDesc = renderGraph.GetTextureDesc(src);
                dstDesc.name = PassName;
                TextureHandle dst = renderGraph.CreateTexture(dstDesc);

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PassName, out PassData passData))
                {
                    passData.src = src;
                    passData.material = _material;
                    passData.camera = cameraData.camera;

                    builder.UseTexture(passData.src, AccessFlags.Read);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        UpdatePassUniforms(data.material, data.camera);
                        Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                resourceData.cameraColor = dst;
            }

            private class PassData
            {
                public TextureHandle src;
                public Material material;
                public Camera camera;
            }
        }
    }
}
