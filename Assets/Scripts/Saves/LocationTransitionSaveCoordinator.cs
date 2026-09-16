using Locations;
using Container.Game;
using UnityEngine;

namespace Saves
{
    /// <summary>Builds a destination checkpoint when a location transition has been confirmed.</summary>
    public sealed class LocationTransitionSaveCoordinator
    {
        private readonly LocationTransitionService locationTransitions;
        private readonly GameSaveController saveController;
        private readonly GameSceneSessionConfiguration sceneSessionConfiguration;

        public LocationTransitionSaveCoordinator(
            LocationTransitionService locationTransitions,
            GameSaveController saveController,
            GameSceneSessionConfiguration sceneSessionConfiguration)
        {
            this.locationTransitions = locationTransitions;
            this.saveController = saveController;
            this.sceneSessionConfiguration = sceneSessionConfiguration;
        }

        public bool SaveConfirmedTransition(VillageLocationTransitionRequest transition)
        {
            if (!sceneSessionConfiguration.IsGameplayScene)
            {
                return true;
            }

            if (!locationTransitions.TryGetEntrancePose(
                    transition.TargetLocationId,
                    transition.TargetTransitionId,
                    out Pose destinationPose))
            {
                Debug.LogError($"Cannot save transition to '{transition.TargetLocationId}/{transition.TargetTransitionId}': destination entrance has no spawn pose.");
                return false;
            }

            return saveController.SaveFullAtPosition(
                transition.TargetLocationId,
                transition.TargetTransitionId,
                destinationPose);
        }
    }
}
