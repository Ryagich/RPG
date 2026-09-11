using System;
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

        public GameWorldBootstrapper(
            GameLifetimeScope scope,
            BootCompletion bootCompletion,
            GameSaveController saveController,
            LocationTransitionContext transitionContext,
            LocationTransitionService locationTransitions,
            IAudioService audioService)
        {
            this.scope = scope;
            this.bootCompletion = bootCompletion;
            this.saveController = saveController;
            this.transitionContext = transitionContext;
            this.locationTransitions = locationTransitions;
            this.audioService = audioService;
        }

        public async void Start()
        {
            await bootCompletion.WaitAsync();
            await saveController.Ready;
            saveController.RestoreLocationTransition(transitionContext);
            UnityEngine.Pose? savedPlayerPose = saveController.TryGetSavedPlayerPose(out UnityEngine.Pose pose)
                ? pose
                : null;
            scope.InitializeWorld(locationTransitions, audioService, savedPlayerPose);
        }
    }
}
