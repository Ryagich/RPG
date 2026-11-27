using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Tests.EditMode
{
    public sealed class GrassDeformationEditModeTests
    {
        private ComputeShader compute;
        private RenderTexture input;
        private RenderTexture output;
        private int kernel;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders
                || !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore))
                Assert.Ignore("This test requires compute and RGBAHalf load/store support.");
            var asset = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/Grass/GrassDeformation.compute");
            Assert.That(asset, Is.Not.Null);
            compute = Object.Instantiate(asset);
            kernel = compute.FindKernel("UpdateField");
            input = CreateTexture();
            output = CreateTexture();
            compute.SetInt("_Resolution", 8);
            compute.SetInts("_HistoryOffset", 0, 0);
            compute.SetInt("_ClearHistory", 1);
            compute.SetInt("_SourceCount", 0);
            compute.SetVector("_FieldOrigin", new Vector4(0, 0, 1, 0));
            compute.SetFloat("_Decay", 1);
            compute.SetFloat("_PressResponse", 1);
            compute.SetFloat("_HeightTolerance", 0.3f);
            compute.SetVectorArray("_SourcePrevious", new Vector4[64]);
            compute.SetVectorArray("_SourceCurrent", new Vector4[64]);
            compute.SetTexture(kernel, "_PreviousField", input);
            compute.SetTexture(kernel, "_NextField", output);
        }

        [TearDown]
        public void TearDown()
        {
            if (input != null) { input.Release(); Object.DestroyImmediate(input); }
            if (output != null) { output.Release(); Object.DestroyImmediate(output); }
            if (compute != null) Object.DestroyImmediate(compute);
        }

        [Test]
        public void ClearDiscardsOldHistoryIncludingHeight()
        {
            Seed(3, 3, new Color(0.5f, -0.25f, 1, 4));
            Dispatch();
            foreach (var pixel in Read()) Assert.That(pixel, Is.EqualTo(Color.clear));
        }

        [Test]
        public void RecoveryDecaysPressureAndPreservesContactHeight()
        {
            Seed(3, 3, new Color(0.5f, -0.25f, 1, 4));
            compute.SetInt("_ClearHistory", 0);
            compute.SetFloat("_Decay", 0.5f);
            Dispatch();
            var pixel = Read()[3 + 3 * 8];
            Assert.That(pixel.b, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(pixel.r, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(pixel.a / pixel.b, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void MovingOriginKeepsFootprintAtSameWorldPositionAndClearsNewEdge()
        {
            Seed(3, 3, new Color(0.5f, 0, 1, 4));
            compute.SetInt("_ClearHistory", 0);
            compute.SetInts("_HistoryOffset", 1, -1);
            Dispatch();
            var pixels = Read();
            Assert.That(pixels[2 + 4 * 8].b, Is.EqualTo(1f));
            Assert.That(pixels[3 + 3 * 8].b, Is.Zero);
            for (int y = 0; y < 8; y++) Assert.That(pixels[7 + y * 8], Is.EqualTo(Color.clear));
        }

        [Test]
        public void OppositeDirectionsKeepPressureWhenHorizontalVectorsCancel()
        {
            Sources(new Vector4(2.5f, 0, 3.5f, 2), new Vector4(4.5f, 0, 3.5f, 2));
            Dispatch();
            var pixel = Read()[3 + 3 * 8];
            Assert.That(pixel.b, Is.GreaterThan(0.4f));
            Assert.That(Mathf.Abs(pixel.r) + Mathf.Abs(pixel.g), Is.LessThan(0.001f));
        }

        [Test]
        public void RecedingContactCannotEraseOrReverseItsStrongerFootprint()
        {
            Seed(3, 3, new Color(0.9f, 0, 0.9f, 3.6f));
            compute.SetInt("_ClearHistory", 0);
            compute.SetFloat("_Decay", 0.99f);
            // The source has passed this texel. Its weak edge now points in the
            // opposite direction, but the stored footprint must recover on its own.
            Sources(new Vector4(4.5f, 4, 3.5f, 2));
            Dispatch();
            var pixel = Read()[3 + 3 * 8];
            Assert.That(pixel.b, Is.EqualTo(0.891f).Within(0.002f));
            Assert.That(pixel.r, Is.EqualTo(0.891f).Within(0.002f));
            Assert.That(pixel.a / pixel.b, Is.EqualTo(4f).Within(0.01f));
        }

        [Test]
        public void TrailRecoversOverManyFramesAfterSourceMovesAway()
        {
            float decay = Mathf.Pow(0.5f, 1f / 60f);
            Seed(3, 3, new Color(0.9f, 0, 0.9f, 3.6f));
            compute.SetInt("_ClearHistory", 0);
            compute.SetFloat("_Decay", decay);
            for (int frame = 0; frame < 60; frame++)
            {
                // During the first half-second a weak contact still overlaps
                // this texel; the second half-second has no contact at all.
                if (frame < 30) Sources(new Vector4(4.5f, 4, 3.5f, 2));
                else compute.SetInt("_SourceCount", 0);
                Dispatch();
                if (frame < 59)
                {
                    (input, output) = (output, input);
                    compute.SetTexture(kernel, "_PreviousField", input);
                    compute.SetTexture(kernel, "_NextField", output);
                }
            }
            var pixel = Read()[3 + 3 * 8];
            Assert.That(pixel.b, Is.EqualTo(0.45f).Within(0.02f));
            Assert.That(pixel.r, Is.EqualTo(0.45f).Within(0.02f));
            Assert.That(pixel.a / pixel.b, Is.EqualTo(4f).Within(0.04f));
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        [TestCase(8f)]
        public void RecoveryHalfLifeControlsRemainingTrailAcrossFrames(float halfLife)
        {
            Seed(3, 3, new Color(0.9f, 0, 0.9f, 3.6f));
            compute.SetInt("_ClearHistory", 0);
            compute.SetFloat("_Decay", Mathf.Pow(0.5f, 1f / (60f * halfLife)));
            for (int frame = 0; frame < 120; frame++)
            {
                Dispatch();
                if (frame < 119)
                {
                    (input, output) = (output, input);
                    compute.SetTexture(kernel, "_PreviousField", input);
                    compute.SetTexture(kernel, "_NextField", output);
                }
            }
            float expected = 0.9f * Mathf.Pow(0.5f, 2f / halfLife);
            Assert.That(Read()[3 + 3 * 8].b, Is.EqualTo(expected).Within(0.04f));
        }

        [Test]
        public void SweptFootprintCoversFastMovementWithoutGaps()
        {
            var starts = new Vector4[64];
            var ends = new Vector4[64];
            starts[0] = new Vector4(1.5f, 4, 3.5f, 1);
            ends[0] = new Vector4(6.5f, 4, 3.5f, 1);
            compute.SetInt("_SourceCount", 1);
            compute.SetVectorArray("_SourcePrevious", starts);
            compute.SetVectorArray("_SourceCurrent", ends);
            Dispatch();
            var pixels = Read();
            for (int x = 1; x <= 6; x++)
            {
                Assert.That(pixels[x + 3 * 8].b, Is.GreaterThan(0.99f));
                Assert.That(pixels[x + 3 * 8].a / pixels[x + 3 * 8].b, Is.EqualTo(4f).Within(0.01f));
            }
        }

        [Test]
        public void WeakerContactOnDifferentFloorDoesNotCorruptStoredHeight()
        {
            Seed(3, 3, new Color(0.5f, 0, 1, 4));
            compute.SetInt("_ClearHistory", 0);
            Sources(new Vector4(4.5f, 0, 3.5f, 2));
            Dispatch();
            var pixel = Read()[3 + 3 * 8];
            Assert.That(pixel.b, Is.EqualTo(1f));
            Assert.That(pixel.a / pixel.b, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void VanishingTraceReachesExactZero()
        {
            Seed(3, 3, new Color(0.0005f, 0, 0.001f, 0.004f));
            compute.SetInt("_ClearHistory", 0);
            compute.SetFloat("_Decay", 0.5f);
            Dispatch();
            Assert.That(Read()[3 + 3 * 8], Is.EqualTo(Color.clear));
        }

        [Test]
        public void ScrollingBeyondCoverageCannotWrapOldFootprints()
        {
            Seed(3, 3, new Color(0.5f, 0, 1, 4));
            compute.SetInt("_ClearHistory", 0);
            compute.SetInts("_HistoryOffset", 16, 0);
            Dispatch();
            foreach (var pixel in Read()) Assert.That(pixel, Is.EqualTo(Color.clear));
        }

        private void Sources(params Vector4[] positions)
        {
            var starts = new Vector4[64];
            var ends = new Vector4[64];
            for (int i = 0; i < positions.Length; i++)
            {
                starts[i] = positions[i];
                ends[i] = new Vector4(positions[i].x, positions[i].y, positions[i].z, 1);
            }
            compute.SetVectorArray("_SourcePrevious", starts);
            compute.SetVectorArray("_SourceCurrent", ends);
            compute.SetInt("_SourceCount", positions.Length);
        }

        private static RenderTexture CreateTexture()
        {
            var texture = new RenderTexture(8, 8, 0)
            {
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Assert.That(texture.Create(), Is.True);
            return texture;
        }

        private void Seed(int x, int y, Color value)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
            try
            {
                var pixels = new Color[64];
                pixels[x + y * 8] = value;
                texture.SetPixels(pixels);
                texture.Apply();
                Graphics.Blit(texture, input);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private void Dispatch() => compute.Dispatch(kernel, 1, 1, 1);

        private Color[] Read()
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(8, 8, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = output;
                texture.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                texture.Apply();
                return texture.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(texture);
            }
        }
    }
}
