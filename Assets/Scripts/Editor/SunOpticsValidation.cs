using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using World.Rendering;

[InitializeOnLoad]
internal static class SunOpticsValidation
{
    private const string Output = "Library/SunOpticsValidation";
    static SunOpticsValidation() { EditorApplication.delayCall += Run; }
    private static void Run()
    {
        if (!File.Exists(Output + "/request")) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Run; return; }
        File.Delete(Output + "/request");
        try
        {
            Directory.CreateDirectory(Output);
            var report = new System.Text.StringBuilder();
            var shader = Shader.Find("Hidden/RPG/AnamorphicLensFlare");
            Require(shader != null && shader.isSupported, "Optics shader unavailable");
            var sky = Shader.Find("RPG/Skybox/Low Poly Clouds");
            Require(sky != null && sky.isSupported, "Sky shader unavailable");
            var a = new Material(sky); var b = new Material(sky);
            try
            {
                var type = typeof(World.DayNightCycle).Assembly.GetType("World.CloudSkyboxParameters");
                var apply = type.GetMethod("Apply"); var changed = type.GetMethod("HaveChanges");
                a.SetFloat("_SunDispersion", 0.75f);
                apply.Invoke(null, new object[] { a, b, null });
                Require(b.GetFloat("_SunDispersion") == 0.75f, "Material dispersion not copied");
                apply.Invoke(null, new object[] { a, b, (float?)1.25f });
                Require(!(bool)changed.Invoke(null, new object[] { a, b, (float?)1.25f }), "Override causes continuous preview updates");
                apply.Invoke(null, new object[] { a, b, null });
                Require(b.GetFloat("_SunDispersion") == 0.75f, "Clearing override does not restore material");
                report.AppendLine("PASS dispersion copy, override, stable preview, override release");
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
            var component = ScriptableObject.CreateInstance<AnamorphicLensFlare>();
            try
            {
                Require(!component.IsActive(), "Default volume should be inactive");
                Require(component.directionCount.value == 6, "Expected six directions by default");
                component.direction1.value = new Vector2(2f, 3f);
                component.direction1.Interp(Vector2.one, new Vector2(3f, 3f), 0.5f);
                Require(component.direction1.value == new Vector2(2f, 2f), "Direction Volume blend failed");
                for (int i = 0; i < 8; i++) Require(component.GetDirection(i).x > 0f, "Direction default missing");
                component.intensity.value = 1f;
                Require(component.IsActive(), "Enabled volume should render");
                component.bloomIntensity.value = component.flareIntensity.value = 0f;
                Require(!component.IsActive(), "Zero contributions should skip rendering");
                report.AppendLine("PASS volume disabled, enabled, zero contributions");
            }
            finally { UnityEngine.Object.DestroyImmediate(component); }

            var mat = new Material(shader);
            var input = new Texture2D(256, 128, TextureFormat.RGBAFloat, false, true);
            var pixels = new Color[256 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++)
                pixels[y * 256 + x] = (x - 184) * (x - 184) + (y - 64) * (y - 64) < 36 ? new Color(16, 12, 8, 1) : Color.black;
            input.SetPixels(pixels); input.Apply();
            var seed = RenderTexture.GetTemporary(256, 128, 0, RenderTextureFormat.ARGBHalf);
            var streak = RenderTexture.GetTemporary(256, 128, 0, RenderTextureFormat.ARGBHalf);
            var final = RenderTexture.GetTemporary(256, 128, 0, RenderTextureFormat.ARGBHalf);
            var old = RenderTexture.active;
            try
            {
                mat.SetVector("_Filter", new Vector4(2, 32, 0, 0));
                Draw(input, seed, mat, 0);
                mat.SetVector("_Direction", new Vector4(0.015f, 0, 1, 0));
                Draw(seed, streak, mat, 1);
                mat.SetTexture("_SeedTexture", seed); mat.SetTexture("_StreakTexture", streak);
                mat.SetVector("_Strength", new Vector4(1, 1, 2, 0.005f));
                mat.SetVector("_Ghosts", new Vector4(4, 0.45f, 0, 0)); mat.SetColor("_Tint", Color.white);
                Draw(input, final, mat, 2);
                RenderTexture.active = final;
                var read = new Texture2D(256, 128, TextureFormat.RGBAFloat, false, true);
                read.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); read.Apply();
                var result = read.GetPixels();
                Require(result.All(c => !float.IsNaN(c.r) && !float.IsInfinity(c.r)), "Nonfinite render");
                Require(result[64 * 256 + 176].r > 0.01f, "No anamorphic spread");
                Require(result[64 * 256 + 71].r > 0.01f, "No mirrored flare");
                File.WriteAllBytes(Output + "/synthetic-optics.png", read.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(read);
                report.AppendLine("PASS HDR prefilter, horizontal spread, mirrored flare, finite pixels");
                mat.SetVector("_Strength", new Vector4(0, 0, 2, 0));
                Draw(input, final, mat, 2);
                foreach (var current in new[] { shader, sky })
                    Require(!ShaderUtil.GetShaderMessages(current).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), "Shader compilation errors: " + current.name);
                TestFalloff(mat, input, seed, streak, final, report);
                report.AppendLine("PASS both shaders compiled");
            }
            finally
            {
                RenderTexture.active = old;
                RenderTexture.ReleaseTemporary(seed); RenderTexture.ReleaseTemporary(streak); RenderTexture.ReleaseTemporary(final);
                UnityEngine.Object.DestroyImmediate(input); UnityEngine.Object.DestroyImmediate(mat);
            }
            TestPipeline(report);
            File.WriteAllText(Output + "/render-report.txt", report.ToString());
            var settings = new ScriptCompilationSettings
            {
                target = EditorUserBuildSettings.activeBuildTarget,
                group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                options = ScriptCompilationOptions.None
            };
            var build = PlayerBuildInterface.CompilePlayerScripts(settings, Output + "/PlayerScripts");
            Require(build.assemblies != null && build.assemblies.Count > 0, "Player scripts build failed");
            report.AppendLine("PASS player script build: " + build.assemblies.Count + " assemblies");
            File.WriteAllText(Output + "/result.txt", report.ToString());
        }
        catch (Exception exception) { File.WriteAllText(Output + "/result.txt", exception.ToString()); Debug.LogException(exception); }
    }
    private static Color[] Read(RenderTexture target)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
        try
        {
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            return texture.GetPixels();
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void TestFalloff(Material mat, Texture2D input, RenderTexture seed, RenderTexture streak,
        RenderTexture target, System.Text.StringBuilder report)
    {
        var white = Enumerable.Repeat(new Color(4, 4, 4, 1), input.width * input.height).ToArray();
        input.SetPixels(white); input.Apply();
        mat.SetVector("_Falloff", new Vector4(0.4f, 0, 0, 0));
        Draw(input, seed, mat, 0);
        var sources = Read(seed);
        Require(sources[64 * 256 + 128].r > 1f && sources[64 * 256].r < 0.01f, "Bright sources lack edge falloff");
        using (var cmd = new CommandBuffer())
        {
            cmd.SetRenderTarget(streak); cmd.ClearRenderTarget(false, true, Color.white);
            cmd.SetRenderTarget(seed); cmd.ClearRenderTarget(false, true, Color.white);
            Graphics.ExecuteCommandBuffer(cmd);
        }
        input.SetPixels(Enumerable.Repeat(Color.black, white.Length).ToArray()); input.Apply();
        mat.SetVector("_Ghosts", new Vector4(1, 0, 0, 0));
        foreach (var strength in new[] { new Vector4(1, 0, 2, 0), new Vector4(0, 1, 2, 0) })
        {
            mat.SetVector("_Strength", strength);
            mat.SetVector("_Falloff", new Vector4(0.4f, 0, 0, 0)); Draw(input, target, mat, 2);
            var rectangle = Read(target);
            Require(rectangle[64 * 256 + 128].r > 0.99f, "Falloff darkens effect center");
            Require(rectangle[64 * 256].r < 0.001f, "Effect fails to fade at screen edge");
            Require(rectangle[64 * 256 + 8].r < rectangle[64 * 256 + 24].r, "Nonmonotonic edge falloff");
            mat.SetVector("_Falloff", new Vector4(0.4f, 1, 0, 0)); Draw(input, target, mat, 2);
            var radial = Read(target);
            Require(radial[107 * 256 + 215].r < rectangle[107 * 256 + 215].r * 0.2f, "Radial falloff does not attenuate corners");
        }
        mat.SetVector("_Strength", new Vector4(0, 0, 2, 0)); Draw(input, target, mat, 2);
        Require(Read(target).All(c => c.r == 0f), "Disabled effects modify original pixels");
        report.AppendLine("PASS source falloff, separate bloom/flare edge fade, monotonic transition, radial corners, zero-strength identity");
    }

    private static void TestPipeline(System.Text.StringBuilder report)
    {
        var cameraObject = new GameObject("Sun Optics Validation Camera") { hideFlags = HideFlags.HideAndDontSave };
        var volumeObject = new GameObject("Sun Optics Validation Volume") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var target = RenderTexture.GetTemporary(256, 128, 24, RenderTextureFormat.ARGBHalf);
        try
        {
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(4, 4, 4, 1); camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true; cameraData.volumeLayerMask = 1 << 31;
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000; volume.sharedProfile = profile;
            var effect = profile.Add<AnamorphicLensFlare>(true);
            effect.intensity.value = 0f;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(camera, request);
            var baseline = Read(target);
            foreach (int count in new[] { 1, 6, 8 })
            {
                effect.intensity.value = 1f; effect.directionCount.value = count;
                effect.bloomIntensity.value = 1f; effect.flareIntensity.value = 0f;
                RenderPipeline.SubmitRenderRequest(camera, request);
                var pixels = Read(target);
                Require(pixels.All(c => !float.IsNaN(c.r) && !float.IsInfinity(c.r)), "Invalid pipeline pixels");
                Require(pixels[64 * 256 + 128].r > baseline[64 * 256 + 128].r + 0.05f, "Bloom missing from RenderGraph at direction count " + count);
                Require(Mathf.Abs(pixels[64 * 256].r - baseline[64 * 256].r) < 0.02f, "RenderGraph bloom changes screen edge");
            }
            effect.bloomIntensity.value = 0f; effect.flareIntensity.value = 1f;
            RenderPipeline.SubmitRenderRequest(camera, request);
            Require(Read(target)[64 * 256 + 128].r > baseline[64 * 256 + 128].r + 0.05f, "Flare-only pipeline missing");
            effect.intensity.value = 0f;
            RenderPipeline.SubmitRenderRequest(camera, request);
            Require(Mathf.Abs(Read(target)[64 * 256 + 128].r - baseline[64 * 256 + 128].r) < 0.02f, "Effect fails to disable");
            report.AppendLine("PASS actual URP RenderGraph: 1/6/8 directions, flare-only, edge preservation, disable/restart");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(volumeObject);
            foreach (var component in profile.components) UnityEngine.Object.DestroyImmediate(component);
            UnityEngine.Object.DestroyImmediate(profile); RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Draw(Texture source, RenderTexture target, Material material, int pass)
    {
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_BlitTexture", source);
        properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
        properties.SetVector("_BlitTexture_TexelSize", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));
        using var command = new CommandBuffer();
        command.SetRenderTarget(target);
        command.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, properties);
        Graphics.ExecuteCommandBuffer(command);
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}

