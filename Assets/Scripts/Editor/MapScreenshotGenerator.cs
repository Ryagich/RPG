using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EditorTools
{
    [InitializeOnLoad]
    public static class MapScreenshotGeneratorRequestWatcher
    {
        private const string RequestPath = "Temp/MapScreenshot.request";
        private const string DonePath = "Temp/MapScreenshot.done";
        private const string ErrorPath = "Temp/MapScreenshot.error";

        private static double nextPollTime;
        private static bool isGenerating;

        static MapScreenshotGeneratorRequestWatcher()
        {
            EditorApplication.update += PollForRequest;
        }

        private static void PollForRequest()
        {
            if (isGenerating || EditorApplication.timeSinceStartup < nextPollTime || !File.Exists(RequestPath))
            {
                return;
            }

            nextPollTime = EditorApplication.timeSinceStartup + 1d;
            isGenerating = true;
            File.Delete(RequestPath);
            if (File.Exists(DonePath))
            {
                File.Delete(DonePath);
            }

            if (File.Exists(ErrorPath))
            {
                File.Delete(ErrorPath);
            }

            EditorApplication.delayCall += ExecuteRequestedGeneration;
        }

        private static void ExecuteRequestedGeneration()
        {
            try
            {
                MapScreenshotGenerator.GenerateSampleSceneMap();
                File.WriteAllText(DonePath, "ok");
            }
            catch (System.Exception exception)
            {
                File.WriteAllText(ErrorPath, exception.ToString());
                Debug.LogException(exception);
            }
            finally
            {
                isGenerating = false;
            }
        }
    }

    public static class MapScreenshotGenerator
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string OutputPath = "Assets/Screenshots/SampleSceneMap.png";
        private const int MaxResolution = 2048;
        private const float BoundsPadding = 8f;
        private const float CameraHeightPadding = 25f;

        public static void GenerateSampleSceneMap()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != SampleScenePath)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            }

            if (!scene.IsValid())
            {
                throw new IOException($"Failed to open scene: {SampleScenePath}");
            }

            Bounds sceneBounds = CollectSceneBounds();
            if (sceneBounds.size == Vector3.zero)
            {
                throw new IOException("Could not calculate scene bounds for map screenshot.");
            }

            CreateDirectoryIfNeeded(OutputPath);

            var hiddenObjects = new List<GameObject>();
            try
            {
                hiddenObjects = HidePlayerObjects();
                CaptureTopDown(sceneBounds);
                AssetDatabase.Refresh();
                Debug.Log($"Map screenshot saved to {OutputPath}");
            }
            finally
            {
                RestoreHiddenObjects(hiddenObjects);
            }
        }

        private static Bounds CollectSceneBounds()
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var hasBounds = false;
            Bounds sceneBounds = default;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                GameObject gameObject = renderer.gameObject;
                if (!gameObject.scene.IsValid() || gameObject.scene.path != SampleScenePath)
                {
                    continue;
                }

                if (gameObject.CompareTag("EditorOnly") || gameObject.GetComponentInParent<Canvas>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    sceneBounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                sceneBounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            sceneBounds.Expand(new Vector3(BoundsPadding * 2f, 0f, BoundsPadding * 2f));
            return sceneBounds;
        }

        private static List<GameObject> HidePlayerObjects()
        {
            var hiddenObjects = new List<GameObject>();
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Transform transform in allTransforms)
            {
                if (transform == null)
                {
                    continue;
                }

                GameObject gameObject = transform.gameObject;
                if (!gameObject.scene.IsValid() || gameObject.scene.path != SampleScenePath)
                {
                    continue;
                }

                if (!gameObject.activeSelf)
                {
                    continue;
                }

                if (gameObject.CompareTag("Player") || gameObject.name.Contains("Player"))
                {
                    gameObject.SetActive(false);
                    hiddenObjects.Add(gameObject);
                }
            }

            return hiddenObjects;
        }

        private static void RestoreHiddenObjects(IEnumerable<GameObject> hiddenObjects)
        {
            foreach (GameObject gameObject in hiddenObjects.Where(gameObject => gameObject != null))
            {
                gameObject.SetActive(true);
            }
        }

        private static void CaptureTopDown(Bounds sceneBounds)
        {
            float width = Mathf.Max(1f, sceneBounds.size.x);
            float depth = Mathf.Max(1f, sceneBounds.size.z);
            int textureWidth;
            int textureHeight;

            if (width >= depth)
            {
                textureWidth = MaxResolution;
                textureHeight = Mathf.Max(1, Mathf.RoundToInt(MaxResolution * (depth / width)));
            }
            else
            {
                textureHeight = MaxResolution;
                textureWidth = Mathf.Max(1, Mathf.RoundToInt(MaxResolution * (width / depth)));
            }

            var cameraGameObject = new GameObject("Temp Map Screenshot Camera");
            try
            {
                Camera camera = cameraGameObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.cullingMask = ~0;
                camera.transform.position = new Vector3(
                    sceneBounds.center.x,
                    sceneBounds.max.y + CameraHeightPadding,
                    sceneBounds.center.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.aspect = textureWidth / (float)textureHeight;
                camera.orthographicSize = Mathf.Max(sceneBounds.extents.z, sceneBounds.extents.x / camera.aspect);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = CameraHeightPadding + sceneBounds.size.y + 500f;

                var renderTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.ARGB32);
                try
                {
                    camera.targetTexture = renderTexture;
                    RenderTexture activeRenderTexture = RenderTexture.active;
                    RenderTexture.active = renderTexture;

                    camera.Render();

                    var texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
                        texture.Apply();
                        File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                        RenderTexture.active = activeRenderTexture;
                    }
                }
                finally
                {
                    camera.targetTexture = null;
                    Object.DestroyImmediate(renderTexture);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGameObject);
            }
        }

        private static void CreateDirectoryIfNeeded(string filePath)
        {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
