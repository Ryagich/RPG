using Container.Project;
using Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace Container.Game
{
    public sealed class GameSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameLifetimeScope gameScopePrefab;

        private async void Awake()
        {
            await BootSignal.WaitAsync();

            var projectScope = LifetimeScope.Find<ProjectLifetimeScope>();
            if (projectScope == null)
            {
                Debug.LogError("ProjectLifetimeScope not found.");
                return;
            }

            var wasActive = gameScopePrefab.gameObject.activeSelf;
            gameScopePrefab.gameObject.SetActive(false);

            var gameLifetimeScope = Instantiate(gameScopePrefab);
            gameLifetimeScope.parentReference.Object = projectScope;
            SceneManager.MoveGameObjectToScene(gameLifetimeScope.gameObject, gameObject.scene);

            gameScopePrefab.gameObject.SetActive(wasActive);
            gameLifetimeScope.gameObject.SetActive(wasActive);
        }
    }
}
