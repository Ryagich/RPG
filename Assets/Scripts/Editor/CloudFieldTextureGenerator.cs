using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace EditorTools
{
    internal static class CloudFieldTextureGenerator
    {
        public const string SkyShaderName = "RPG/Skybox/Low Poly Clouds";
        private const string GeneratorPath = "Assets/Shaders/Clouds/Editor/CloudFieldGenerator.compute";

        public static bool IsSupported => SystemInfo.supportsComputeShaders
            && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore)
            && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Sample)
            && SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Linear);

        public static RenderTexture Generate(CloudFieldSettings settings)
        {
            RequireEditMode();
            var generator = AssetDatabase.LoadAssetAtPath<ComputeShader>(GeneratorPath);
            if (generator == null || !IsSupported)
                throw new InvalidOperationException("Cloud generation requires compute and filterable RGBAHalf random-write textures.");

            var texture = new RenderTexture(settings.Resolution, settings.Resolution, 0)
            {
                name = "Generated Cloud Field",
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                if (!texture.Create())
                    throw new InvalidOperationException("Could not allocate a cloud field texture.");
                string kernelName = settings.Shape switch
                {
                    CloudBakeShape.Faceted => "GenerateFaceted",
                    CloudBakeShape.Rounded => "GenerateRounded",
                    CloudBakeShape.RealisticCirrus => "GenerateCirrus",
                    CloudBakeShape.RealisticCumulus => "GenerateCumulus",
                    _ => throw new ArgumentOutOfRangeException(nameof(settings), "Unknown cloud shape.")
                };
                int kernel = generator.FindKernel(kernelName);
                generator.SetInt("_Resolution", settings.Resolution);
                generator.SetFloat("_Period", settings.Period);
                generator.SetFloat("_Detail", settings.Detail);
                generator.SetFloat("_Iterations", settings.Iterations);
                generator.SetFloat("_Lacunarity", settings.Lacunarity);
                generator.SetFloat("_Persistence", settings.Persistence);
                generator.SetTexture(kernel, "_Result", texture);
                generator.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out _);
                generator.Dispatch(kernel, Mathf.CeilToInt(settings.Resolution / (float)x),
                    Mathf.CeilToInt(settings.Resolution / (float)y), 1);
                texture.GenerateMips();
                return texture;
            }
            catch
            {
                Release(texture);
                throw;
            }
        }

        public static void Assign(Material material, Texture near, Vector4 nearParameters,
            Texture distant, Vector4 distantParameters)
        {
            material.SetTexture("_CloudField", near);
            material.SetTexture("_UpperCloudField", distant);
            material.SetVector("_CloudFieldParams", nearParameters);
            material.SetVector("_UpperCloudFieldParams", distantParameters);
        }

        public static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Cloud preview and baking are available only in Edit Mode.");
        }

        public static void Release(RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
