using UnityEngine;

namespace Container.Project
{
    public static class ProjectBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Object.FindAnyObjectByType<ProjectLifetimeScope>() != null)
            {
                return;
            }

            var prefab = Resources.Load<ProjectLifetimeScope>("Project/ProjectLifetimeScope");

            if (prefab == null)
            {
                Debug.LogError("ProjectLifetimeScope prefab not found in Resources!");
                return;
            }

            var scope = Object.Instantiate(prefab);
            scope.name = prefab.name;
        }
    }
}
