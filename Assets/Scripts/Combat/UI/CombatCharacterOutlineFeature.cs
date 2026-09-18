using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class CombatCharacterOutlineFeature : ScriptableRendererFeature
{
    public const uint RenderingLayer = 1u << 31;

    [SerializeField] private Shader _shader;
    [SerializeField, Range(1, 4)] private int _width = 2;
    private Material _material;
    private OutlinePass _pass;

    public override void Create()
    {
        CoreUtils.Destroy(_material);
        if (_shader == null) return;
        _material = CoreUtils.CreateEngineMaterial(_shader);
        _pass = new OutlinePass(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || renderingData.cameraData.isPreviewCamera) return;
        _material.SetFloat("_OutlineWidth", _width);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

    private sealed class OutlinePass : ScriptableRenderPass
    {
        private readonly Material _material;
        private readonly List<ShaderTagId> _tags = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("Universal2D"),
        };

        private sealed class MaskData
        {
            public RendererListHandle Renderers;
        }

        private sealed class CompositeData
        {
            public TextureHandle Mask;
            public Material Material;
        }

        public OutlinePass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var lights = frameData.Get<UniversalLightData>();
            var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
            descriptor.name = "Combat Character Silhouettes";
            descriptor.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            descriptor.clearBuffer = true;
            descriptor.clearColor = Color.clear;
            descriptor.filterMode = FilterMode.Point;
            TextureHandle mask = graph.CreateTexture(descriptor);

            var drawing = RenderingUtils.CreateDrawingSettings(
                _tags, rendering, camera, lights, SortingCriteria.CommonTransparent);
            drawing.overrideMaterial = _material;
            drawing.overrideMaterialPassIndex = 0;
            var filtering = new FilteringSettings(RenderQueueRange.transparent, -1, RenderingLayer);
            var rendererList = graph.CreateRendererList(
                new RendererListParams(rendering.cullResults, drawing, filtering));

            using (var builder = graph.AddRasterRenderPass<MaskData>("Combat Character Silhouettes", out var data))
            {
                data.Renderers = rendererList;
                builder.UseRendererList(rendererList);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(mask, 0);
                builder.SetRenderFunc(static (MaskData pass, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(pass.Renderers));
            }

            using (var builder = graph.AddRasterRenderPass<CompositeData>("Combat Character Outlines", out var data))
            {
                data.Mask = mask;
                data.Material = _material;
                builder.UseTexture(mask);
                builder.UseTexture(resources.cameraDepthTexture);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (CompositeData pass, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, pass.Mask, new Vector4(1, 1, 0, 0), pass.Material, 1));
            }
        }
    }
}
