using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// HW02: process camera color in a temporary target, then write it BACK.
// URP 14 RTHandle/Blitter API, with a procedural Fullscreen Shader Graph.
public class FullScreenFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class FullScreenPassSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material material;
        [Min(0)] public int materialPass = 0;
        public bool showInSceneView = true;
        public bool requiresDepth = true;
        [Tooltip("Let MIOStyleSwitcher on this camera choose this pass's material at runtime.")]
        public bool useCameraStyle = false;
    }
    public FullScreenPassSettings settings = new FullScreenPassSettings();
    FullScreenPass pass;
    public override void Create() { pass?.Dispose(); pass = new FullScreenPass(settings); }
    bool CanRender(CameraType type) => type == CameraType.Game || (settings.showInSceneView && type == CameraType.SceneView);
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
    {
        if (settings.material == null || !CanRender(data.cameraData.cameraType)) return;
        var material = settings.material;
        if (settings.useCameraStyle && data.cameraData.camera.TryGetComponent<MIOStyleSwitcher>(out var style)
            && style.isActiveAndEnabled && style.ActiveMaterial != null)
            material = style.ActiveMaterial;
        pass.SetSource(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle, material);
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (settings.material == null || !CanRender(data.cameraData.cameraType)) return;
        pass.renderPassEvent = settings.renderPassEvent; renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) { pass?.Dispose(); }
    sealed class FullScreenPass : ScriptableRenderPass
    {
        readonly FullScreenPassSettings settings;
        readonly ProfilingSampler profiler = new ProfilingSampler("MIO full-screen composite");
        RTHandle source, depth, temporary;
        Material renderMaterial;
        public FullScreenPass(FullScreenPassSettings settings)
        {
            this.settings = settings;
            ConfigureInput(settings.requiresDepth ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
        }
        public void SetSource(RTHandle source, RTHandle depth, Material material)
        { this.source = source; this.depth = depth; renderMaterial = material; }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
            RenderingUtils.ReAllocateIfNeeded(ref temporary, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_MIOPostProcessTemporary");
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (source == null || renderMaterial == null) return;
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profiler))
            {
                Blitter.BlitCameraTexture(cmd, source, temporary, renderMaterial, settings.materialPass);
                // Missing operation in the starter: return the result to camera color.
                Blitter.BlitCameraTexture(cmd, temporary, source);
                // Blitter binds color alone. A background pass must restore camera depth
                // for the opaque draw that follows, including its MSAA depth attachment.
                if (settings.renderPassEvent <= RenderPassEvent.BeforeRenderingOpaques)
                    CoreUtils.SetRenderTarget(cmd, source, depth);
            }
            context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
        }
        public void Dispose() { temporary?.Release(); temporary = null; source = null; depth = null; renderMaterial = null; }
    }
}
