using UnityEngine;
using UnityEngine.SceneManagement;

namespace Loading
{
    public sealed class SceneLoadingService
    {
        private static string targetSceneName;
        private static bool waitForInputBeforeActivation;

        private readonly LoadSceneConfig config;

        public SceneLoadingService(LoadSceneConfig config)
        {
            this.config = config;
        }

        public string TargetSceneName => targetSceneName;
        public bool WaitForInputBeforeActivation => waitForInputBeforeActivation;
        public bool HasPendingRequest => HasPendingLoadRequest;

        public static string PendingTargetSceneName => targetSceneName;
        public static bool PendingWaitForInputBeforeActivation => waitForInputBeforeActivation;
        public static bool HasPendingLoadRequest => !string.IsNullOrWhiteSpace(targetSceneName);

        public void Load(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("Target scene name is empty.");
                return;
            }

            if (config == null)
            {
                Debug.LogError("LoadSceneConfig is not assigned.");
                return;
            }

            var activeSceneName = SceneManager.GetActiveScene().name;
            PrepareLoad(
                targetSceneName,
                activeSceneName == config.MenuSceneName && targetSceneName != config.MenuSceneName);

            Time.timeScale = 1f;
            SceneManager.LoadScene(config.LoadSceneName);
        }

        public void PrepareDirectLoad(string targetSceneName, bool waitForInputBeforeActivation)
        {
            PrepareLoad(targetSceneName, waitForInputBeforeActivation);
        }

        public static void PrepareLoad(string targetSceneName, bool waitForInputBeforeActivation)
        {
            SceneLoadingService.targetSceneName = targetSceneName;
            SceneLoadingService.waitForInputBeforeActivation = waitForInputBeforeActivation;
        }

        public void ClearRequest()
        {
            ClearPendingRequest();
        }

        public static void ClearPendingRequest()
        {
            targetSceneName = null;
            waitForInputBeforeActivation = false;
        }
    }
}
