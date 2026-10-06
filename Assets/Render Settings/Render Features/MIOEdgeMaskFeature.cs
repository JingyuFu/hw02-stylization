using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Detect once at a fixed footprint, then widen this mask in the outline graph.
// Changing line width consequently cannot introduce a new detected crease.
public class MIOEdgeMaskFeature : ScriptableRendererFeature
{
    public Material outlineMaterial;
    public Shader maskShader;
    public RenderPassEvent passEvent = (RenderPassEvent)449;
    EdgePass pass;
    public override void Create() { pass?.Dispose(); pass = new EdgePass(this); }
    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
    { if (Supported(data.cameraData.cameraType)) pass.source = renderer.cameraColorTargetHandle; }
    static bool Supported(CameraType type) => type == CameraType.Game || type == CameraType.SceneView;
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (!Supported(data.cameraData.cameraType) || outlineMaterial == null || maskShader == null) return;
        pass.renderPassEvent = passEvent; renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) { pass?.Dispose(); }
    sealed class EdgePass : ScriptableRenderPass
    {
        readonly MIOEdgeMaskFeature owner;
        readonly ProfilingSampler profiler = new ProfilingSampler("MIO fixed edge detection");
        static readonly string[] Properties = { "_DepthThreshold", "_NormalThreshold", "_WobblePixels", "_WobbleSpeed", "_SketchFPS", "_PreviewTime" };
        public RTHandle source;
        RTHandle mask;
        Material material;
        public EdgePass(MIOEdgeMaskFeature owner) { this.owner = owner; ConfigureInput(ScriptableRenderPassInput.Depth); }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var d = data.cameraData.cameraTargetDescriptor;
            d.depthBufferBits = 0; d.msaaSamples = 1;
            d.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref mask, d, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_MIOFixedEdgeMask");
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (material == null) material = CoreUtils.CreateEngineMaterial(owner.maskShader);
            foreach (var name in Properties)
                material.SetFloat(name, owner.outlineMaterial.GetFloat(name));
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profiler))
            {
                Blitter.BlitCameraTexture(cmd, source, mask, material, 0);
                cmd.SetGlobalTexture("_MIOFixedEdgeMask", mask);
            }
            context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
        }
        public void Dispose() { mask?.Release(); mask = null; CoreUtils.Destroy(material); material = null; source = null; }
    }
}
