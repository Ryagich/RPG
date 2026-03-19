using Container.Project;
using Localization;
using UnityEngine;
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
            var gameLifetimeScope = projectScope.CreateChildFromPrefab(gameScopePrefab);
        }
    }
}