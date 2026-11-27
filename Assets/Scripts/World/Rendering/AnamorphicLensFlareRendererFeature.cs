using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace World.Rendering
{
    public sealed class AnamorphicLensFlareRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, HideInInspector] private Shader shader;
        private Material material;
        private LensPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new LensPass(material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (material == null || !camera.postProcessEnabled || camera.renderType == CameraRenderType.Overlay
                || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;
            var settings = VolumeManager.instance.stack.GetComponent<AnamorphicLensFlare>();
            if (settings != null && settings.IsActive())
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        private sealed class LensPass : ScriptableRenderPass
        {
            private readonly Material material;
            private static readonly int Filter = Shader.PropertyToID("_Filter");
            private static readonly int Direction = Shader.PropertyToID("_Direction");
            private static readonly int Strength = Shader.PropertyToID("_Strength");
            private static readonly int Ghosts = Shader.PropertyToID("_Ghosts");
            private static readonly int Tint = Shader.PropertyToID("_Tint");
            private static readonly int StreakTexture = Shader.PropertyToID("_StreakTexture");
            private static readonly int SeedTexture = Shader.PropertyToID("_SeedTexture");
            private static readonly int Falloff = Shader.PropertyToID("_Falloff");
            private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");

            public LensPass(Material material)
            {
                this.material = material;
                requiresIntermediateTexture = true;
            }

            private sealed class CompositeData
            {
                public TextureHandle source, seed, streak;
                public Material material;
                public MaterialPropertyBlock properties;
            }

            private sealed class AccumulateData
            {
                public TextureHandle source;
                public Material material;
                public MaterialPropertyBlock properties;
            }

            private void Accumulate(RenderGraph graph, TextureHandle source, TextureHandle target,
                MaterialPropertyBlock properties, string name)
            {
                using var builder = graph.AddRasterRenderPass<AccumulateData>(name, out var data);
                data.source = source;
                data.material = material;
                data.properties = properties;
                builder.UseTexture(source);
                // Each independent direction adds to one quarter-resolution target.
                builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (AccumulateData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(BlitTexture, data.source);
                    data.properties.SetVector(BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 3, MeshTopology.Triangles, 3, 1, data.properties);
                });
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var settings = VolumeManager.instance.stack.GetComponent<AnamorphicLensFlare>();
                if (resources.isActiveTargetBackBuffer || settings == null || !settings.IsActive())
                    return;

                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.clearBuffer = false;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.name = "Anamorphic Composite";
                var destination = graph.CreateTexture(descriptor);
                float aspect = (float)descriptor.width / Mathf.Max(1, descriptor.height);
                descriptor.width = Mathf.Max(1, descriptor.width / 4);
                descriptor.height = Mathf.Max(1, descriptor.height / 4);
                descriptor.name = "Anamorphic Bright Sources";
                var seed = graph.CreateTexture(descriptor);
                var filter = new MaterialPropertyBlock();
                filter.SetVector(Filter, new Vector4(settings.threshold.value, settings.clamp.value, 0f, 0f));
                Vector4 falloff = new Vector4(settings.edgeFalloff.value, settings.radialFalloff.value, 0f, 0f);
                filter.SetVector(Falloff, falloff);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, seed, material, 0, filter),
                    passName: "Anamorphic Bright Sources");

                var streak = graph.defaultResources.blackTextureXR;
                if (settings.HasBloom())
                {
                    descriptor.name = "Anamorphic Bloom Sum";
                    descriptor.clearBuffer = true;
                    descriptor.clearColor = Color.clear;
                    streak = graph.CreateTexture(descriptor);
                    descriptor.clearBuffer = false;
                    int count = settings.directionCount.value;
                    for (int directionIndex = 0; directionIndex < count; directionIndex++)
                    {
                        Vector2 configuration = settings.GetDirection(directionIndex);
                        if (configuration.x <= 0f) continue;
                        float angle = settings.rotation.value * Mathf.Deg2Rad + Mathf.PI * directionIndex / count;
                        Vector2 direction = new Vector2(Mathf.Cos(angle) / aspect, Mathf.Sin(angle));
                        var input = seed;
                        // Start every axis from the same bright sources, avoiding an isotropic blur.
                        for (int i = 0; i < 5; i++)
                        {
                            var blur = new MaterialPropertyBlock();
                            Vector2 step = direction * (settings.stretch.value * Mathf.Clamp(configuration.y, 0.01f, 4f) / 64f) * (1 << i);
                            float weight = i == 4 ? configuration.x / count : 1f;
                            blur.SetVector(Direction, new Vector4(step.x, step.y, weight, 0f));
                            string name = "Anamorphic Direction " + (directionIndex + 1) + " Blur " + i;
                            if (i == 4)
                                Accumulate(graph, input, streak, blur, name);
                            else
                            {
                                descriptor.name = name;
                                var next = graph.CreateTexture(descriptor);
                                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(input, next, material, 1, blur), passName: name);
                                input = next;
                            }
                        }
                    }
                }

                using var builder = graph.AddRasterRenderPass<CompositeData>("Anamorphic Bloom and Flares", out var data);
                data.source = source;
                data.seed = seed;
                data.streak = streak;
                data.material = material;
                data.properties = new MaterialPropertyBlock();
                data.properties.SetVector(Strength, new Vector4(settings.intensity.value * settings.bloomIntensity.value,
                    settings.intensity.value * settings.flareIntensity.value, aspect, settings.chromaticDispersion.value));
                data.properties.SetVector(Ghosts, new Vector4(settings.ghostCount.value, settings.ghostSpacing.value, 0f, 0f));
                data.properties.SetColor(Tint, settings.tint.value);
                data.properties.SetVector(Falloff, falloff);
                builder.UseTexture(source);
                builder.UseTexture(seed);
                builder.UseTexture(streak);
                builder.SetRenderAttachment(destination, 0);
                builder.SetRenderFunc(static (CompositeData data, RasterGraphContext context) =>
                {
                    data.properties.SetTexture(BlitTexture, data.source);
                    data.properties.SetTexture(SeedTexture, data.seed);
                    data.properties.SetTexture(StreakTexture, data.streak);
                    data.properties.SetVector(BlitScaleBias, new Vector4(1f, 1f, 0f, 0f));
                    context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 2, MeshTopology.Triangles, 3, 1, data.properties);
                });
                resources.cameraColor = destination;
            }
        }
    }
}

