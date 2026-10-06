using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A normal-only texture, independent of _CameraDepthTexture as required by HW02.
// Objects' native DepthNormals passes preserve their own vertex deformation.
public class NormalFeature : ScriptableRendererFeature
{
    public LayerMask normalsLayerMask = ~0;
    public RenderTexture NormalsTexture;
    public RenderPassEvent _NormalsEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 1);
    public Material normalsMaterial;
    public bool showInSceneView = true;
    [Tooltip("Rendering-layer bits whose geometry keeps its luminous color instead of dark ink.")]
    public uint unoutlinedRenderingLayers = 128;
    NormalsPass pass;
    public override void Create() { pass?.Dispose(); pass = new NormalsPass(this); }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        var type = data.cameraData.cameraType;
        if (NormalsTexture == null || normalsMaterial == null || (type != CameraType.Game && !(showInSceneView && type == CameraType.SceneView))) return;
        pass.renderPassEvent = _NormalsEvent; renderer.EnqueuePass(pass);
    }
    protected override void Dispose(bool disposing) { pass?.Dispose(); }
    sealed class NormalsPass : ScriptableRenderPass
    {
        readonly NormalFeature owner;
        readonly ProfilingSampler profiler = new ProfilingSampler("MIO separate normal buffer");
        readonly List<ShaderTagId> tags = new List<ShaderTagId> { new ShaderTagId("DepthNormalsOnly"), new ShaderTagId("DepthNormals") };
        RTHandle rawNormals, rawDepth, luminousMask;
        static readonly int LuminousId = Shader.PropertyToID("_MIOLuminousMask");
        static readonly int BufferId = Shader.PropertyToID("_MIONormalBuffer");
        static readonly int SizeId = Shader.PropertyToID("_MIONormalBuffer_TexelSize");
        public NormalsPass(NormalFeature owner) { this.owner = owner; }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1; descriptor.depthBufferBits = 0;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat; descriptor.sRGB = false;
            RenderingUtils.ReAllocateIfNeeded(ref rawNormals, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_MIOSignedWorldNormals");
            RenderingUtils.ReAllocateIfNeeded(ref luminousMask, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_MIOLuminousMask");
            descriptor.graphicsFormat = GraphicsFormat.None; descriptor.depthStencilFormat = GraphicsFormat.D32_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref rawDepth, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_MIONormalDepthAttachment");
            var target = owner.NormalsTexture;
            int width = data.cameraData.cameraTargetDescriptor.width, height = data.cameraData.cameraTargetDescriptor.height;
            if (target.width != width || target.height != height || target.antiAliasing != 1)
            { target.Release(); target.width = width; target.height = height; target.antiAliasing = 1; }
            target.filterMode = FilterMode.Point; target.wrapMode = TextureWrapMode.Clamp;
            if (!target.IsCreated()) target.Create();
            ConfigureTarget(rawNormals, rawDepth); ConfigureClear(ClearFlag.All, Color.clear);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var drawing = CreateDrawingSettings(tags, ref data, data.cameraData.defaultOpaqueSortFlags);
            // Do not override surface materials: their normals pass includes vertex animation.
            var filtering = new FilteringSettings(RenderQueueRange.opaque, owner.normalsLayerMask);
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profiler))
            {
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                context.DrawRenderers(data.cullResults, ref drawing, ref filtering);
                // Render luminous curves with the same animated vertex pass and shared depth.
                // This is only an outline-eligibility mask, not another depth/normal encoding.
                CoreUtils.SetRenderTarget(cmd, luminousMask, rawDepth, ClearFlag.Color, Color.clear);
                context.ExecuteCommandBuffer(cmd); cmd.Clear();
                var luminous = new FilteringSettings(RenderQueueRange.opaque, owner.normalsLayerMask, owner.unoutlinedRenderingLayers);
                if (owner.unoutlinedRenderingLayers != 0) context.DrawRenderers(data.cullResults, ref drawing, ref luminous);
                cmd.SetGlobalTexture(LuminousId, luminousMask);
                CoreUtils.SetRenderTarget(cmd, new RenderTargetIdentifier(owner.NormalsTexture));
                cmd.SetViewport(new Rect(0, 0, owner.NormalsTexture.width, owner.NormalsTexture.height));
                Blitter.BlitTexture(cmd, rawNormals, new Vector4(1, 1, 0, 0), owner.normalsMaterial, 1);
                cmd.SetGlobalTexture(BufferId, owner.NormalsTexture);
                cmd.SetGlobalVector(SizeId, new Vector4(1f / owner.NormalsTexture.width, 1f / owner.NormalsTexture.height, owner.NormalsTexture.width, owner.NormalsTexture.height));
            }
            context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
        }
        public void Dispose() { rawNormals?.Release(); rawDepth?.Release(); luminousMask?.Release(); rawNormals = null; rawDepth = null; luminousMask = null; }
    }
}
