using Container.Project;
using Localization;
using Locations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Container.Game
{
    public sealed class GameSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameLifetimeScope gameScopePrefab;
        [SerializeField] private VillageLocationSelector locationSelector;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private LifetimeScope[] levelScopes;

        private async void Awake()
        {
            var projectScope = ProjectLifetimeScope.Instance;
            if (projectScope == null)
            {
                Debug.LogError("ProjectLifetimeScope not found.");
                return;
            }

            var bootCompletion = projectScope.Container.Resolve<BootCompletion>();
            await bootCompletion.WaitAsync();

            if (gameScopePrefab == null)
            {
                Debug.LogError("GameLifetimeScope prefab is not assigned.", this);
                return;
            }

            if (gameCamera == null)
            {
                Debug.LogError("Game camera is not assigned.", this);
                return;
            }

            var gameLifetimeScope = Instantiate(gameScopePrefab);
            gameLifetimeScope.gameObject.SetActive(false);
            gameLifetimeScope.parentReference.Object = projectScope;
            SceneManager.MoveGameObjectToScene(gameLifetimeScope.gameObject, gameObject.scene);
            gameLifetimeScope.SetLocationSelector(locationSelector);
            gameLifetimeScope.SetGameCamera(gameCamera);

            gameLifetimeScope.gameObject.SetActive(true);
            gameLifetimeScope.Build();

            BuildLevelScopes(gameLifetimeScope);
        }

        private void BuildLevelScopes(GameLifetimeScope gameLifetimeScope)
        {
            if (levelScopes == null)
            {
                return;
            }

            foreach (var scope in levelScopes)
            {
                if (scope == null
                    || scope == gameLifetimeScope
                    || !scope.gameObject.activeInHierarchy
                    || scope.Container != null
                    || scope.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                scope.parentReference.Object = gameLifetimeScope;
                scope.Build();
            }
        }
    }
}
