using System;
using System.Threading.Tasks;
using GameAudio;
using Localization;
using Locations;
using Saves;
using UnityEngine;
using VContainer.Unity;

namespace Container.Game
{
    /// <summary>
    /// Starts world selection only after PluginYG has loaded its data. This preserves the normal
    /// location lifecycle while allowing a saved location to be the first active location.
    /// </summary>
    public sealed class GameWorldBootstrapper : IStartable
    {
        private readonly GameLifetimeScope scope;
        private readonly BootCompletion bootCompletion;
        private readonly GameSaveController saveController;
        private readonly LocationTransitionContext transitionContext;
        private readonly LocationTransitionService locationTransitions;
        private readonly IAudioService audioService;
        private readonly GameSceneSessionConfiguration sceneSessionConfiguration;
        private readonly TaskCompletionSource<bool> worldInitialized = new();

        public Task WorldInitialized => worldInitialized.Task;

        public GameWorldBootstrapper(
            GameLifetimeScope scope,
            BootCompletion bootCompletion,
            GameSaveController saveController,
            LocationTransitionContext transitionContext,
            LocationTransitionService locationTransitions,
            IAudioService audioService,
            GameSceneSessionConfiguration sceneSessionConfiguration)
        {
            this.scope = scope;
            this.bootCompletion = bootCompletion;
            this.saveController = saveController;
            this.transitionContext = transitionContext;
            this.locationTransitions = locationTransitions;
            this.audioService = audioService;
            this.sceneSessionConfiguration = sceneSessionConfiguration;
        }

        public async void Start()
        {
            await bootCompletion.WaitAsync();
            UnityEngine.Pose? savedPlayerPose = null;
            if (sceneSessionConfiguration.IsGameplayScene)
            {
                await saveController.Ready;
                saveController.RestoreLocationTransition(transitionContext);
                savedPlayerPose = saveController.TryGetSavedPlayerPose(out UnityEngine.Pose pose)
                    ? pose
                    : null;
            }
            else
            {
                transitionContext.Clear();
            }

            scope.InitializeWorld(locationTransitions, audioService, savedPlayerPose);
            worldInitialized.TrySetResult(true);
        }
    }
}
