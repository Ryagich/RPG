using UnityEngine;

namespace Container.Project
{
    public static class ProjectBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // защита от двойного создания
            if (Object.FindObjectOfType<ProjectLifetimeScope>() != null)
                return;
            var prefab = Resources.Load<ProjectLifetimeScope>("Project/ProjectLifetimeScope");

            if (prefab == null)
            {
                Debug.LogError("ProjectLifetimeScope prefab not found in Resources!");
                return;
            }

            Object.Instantiate(prefab);
        }
    }
}