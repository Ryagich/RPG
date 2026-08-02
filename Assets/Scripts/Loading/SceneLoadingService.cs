using UnityEngine;
using UnityEngine.SceneManagement;

namespace Loading
{
    public sealed class SceneLoadingService
    {
        private string targetSceneName;
        private bool waitForInputBeforeActivation;

        private readonly LoadSceneConfig config;

        public SceneLoadingService(LoadSceneConfig config)
        {
            this.config = config;
        }

        public string TargetSceneName => targetSceneName;
        public bool WaitForInputBeforeActivation => waitForInputBeforeActivation;
        public bool HasPendingRequest => !string.IsNullOrWhiteSpace(targetSceneName);

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
            PrepareDirectLoad(
                targetSceneName,
                activeSceneName == config.MenuSceneName && targetSceneName != config.MenuSceneName);

            Time.timeScale = 1f;
            SceneManager.LoadScene(config.LoadSceneName);
        }

        public void PrepareDirectLoad(string targetSceneName, bool waitForInputBeforeActivation)
        {
            this.targetSceneName = targetSceneName;
            this.waitForInputBeforeActivation = waitForInputBeforeActivation;
        }

        public void ClearRequest()
        {
            targetSceneName = null;
            waitForInputBeforeActivation = false;
        }
    }
}
