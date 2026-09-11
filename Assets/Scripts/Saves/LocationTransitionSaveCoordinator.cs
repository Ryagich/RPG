using Locations;
using UnityEngine;

namespace Saves
{
    /// <summary>Builds a destination checkpoint when a location transition has been confirmed.</summary>
    public sealed class LocationTransitionSaveCoordinator
    {
        private readonly LocationTransitionService locationTransitions;
        private readonly GameSaveController saveController;

        public LocationTransitionSaveCoordinator(
            LocationTransitionService locationTransitions,
            GameSaveController saveController)
        {
            this.locationTransitions = locationTransitions;
            this.saveController = saveController;
        }

        public bool SaveConfirmedTransition(VillageLocationTransitionRequest transition)
        {
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
