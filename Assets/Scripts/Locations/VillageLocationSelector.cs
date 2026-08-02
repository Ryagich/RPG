using System;
using System.Collections.Generic;
using System.Linq;
using Container;
using UnityEngine;

namespace Locations
{
    /// <summary>Scene authoring data for the locations available in this scene.</summary>
    public sealed class VillageLocationSelector : MonoBehaviour
    {
        [SerializeField] private List<VillageLocationDefinition> locations = new();
        [Header("First game session")]
        [SerializeField] private string defaultLocationId;
        [SerializeField] private Transform defaultPlayerTransform;

        public IReadOnlyList<VillageLocationDefinition> Locations => locations;
        public string DefaultLocationId => defaultLocationId;
        public Transform DefaultPlayerTransform => defaultPlayerTransform;

        public VillageLocationDefinition FindLocation(string locationId) =>
            locations.FirstOrDefault(location => location != null && location.Id == locationId);
    }

    /// <summary>
    /// Project-lifetime state passed from a confirmed transition to the next GameLifetimeScope.
    /// </summary>
    public sealed class LocationTransitionContext
    {
        private string targetLocationId;
        private string targetTransitionId;

        public bool HasPendingTransition => !string.IsNullOrWhiteSpace(targetLocationId)
                                            || !string.IsNullOrWhiteSpace(targetTransitionId);

        public string TargetLocationId => targetLocationId;
        public string TargetTransitionId => targetTransitionId;

        public void SetPendingTransition(string locationId, string transitionId)
        {
            targetLocationId = locationId;
            targetTransitionId = transitionId;
        }

        public void Clear()
        {
            targetLocationId = null;
            targetTransitionId = null;
        }
    }

    /// <summary>Runtime logic for selecting locations and processing transitions.</summary>
    public sealed class LocationTransitionService
    {
        private readonly VillageLocationSelector selector;
        private readonly LocationTransitionContext transitionContext;
        private VillageLocationDefinition currentLocation;
        private VillageLocationTransition currentEntrance;
        private bool requiresTransitionEntrance;
        private bool initialized;

        public event Action<VillageLocationTransitionRequest> TransitionRequested;

        public LocationTransitionService(
            VillageLocationSelector selector,
            LocationTransitionContext transitionContext)
        {
            this.selector = selector;
            this.transitionContext = transitionContext;
        }

        public VillageLocationDefinition CurrentLocation => currentLocation;

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            if (selector == null)
            {
                return;
            }

            requiresTransitionEntrance = transitionContext.HasPendingTransition;
            var selectedId = string.IsNullOrWhiteSpace(transitionContext.TargetLocationId)
                ? selector.DefaultLocationId
                : transitionContext.TargetLocationId;
            currentLocation = selector.FindLocation(selectedId);
            currentEntrance = currentLocation?.FindTransition(transitionContext.TargetTransitionId);
            if (requiresTransitionEntrance && (currentEntrance == null || !currentEntrance.CanEnter))
            {
                Debug.LogError($"Transition '{transitionContext.TargetTransitionId}' in location '{selectedId}' is not a valid entrance: assign a Player Spawn Transform.", selector);
            }

            if (currentLocation == null)
            {
                Debug.LogError($"Village location '{selectedId}' was not found. Assign a default location ID.", selector);
                return;
            }

            var requiredObjects = new HashSet<GameObject>(currentLocation.RequiredObjects.Where(item => item != null));
            foreach (var requiredObject in requiredObjects)
            {
                requiredObject.SetActive(true);
            }

            ConfigureTransitionTriggers(currentLocation);
            DeactivateObjectsExclusiveToInactiveLocations(requiredObjects);
            transitionContext.Clear();
        }

        public bool TryGetPlayerSpawn(out Pose pose)
        {
            Initialize();
            if (selector == null)
            {
                pose = default;
                return false;
            }

            if (requiresTransitionEntrance)
            {
                if (currentEntrance == null || !currentEntrance.CanEnter)
                {
                    Debug.LogError("Player spawn was requested from an invalid location transition.", selector);
                    pose = default;
                    return false;
                }

                pose = new Pose(currentEntrance.PlayerSpawnTransform.position, currentEntrance.PlayerSpawnTransform.rotation);
                return true;
            }

            if (selector.DefaultPlayerTransform == null)
            {
                Debug.LogError("Default Player Transform is not assigned for the first game session.", selector);
                pose = default;
                return false;
            }

            pose = new Pose(selector.DefaultPlayerTransform.position, selector.DefaultPlayerTransform.rotation);
            return true;
        }

        public bool TryRequestTransition(string sourceLocationId, string sourceTransitionId)
        {
            if (selector == null)
            {
                Debug.LogWarning("Location transition was requested in a scene without a VillageLocationSelector.");
                return false;
            }

            var sourceLocation = selector.FindLocation(sourceLocationId);
            var transition = sourceLocation?.FindTransition(sourceTransitionId);
            if (transition == null || !transition.CanExit)
            {
                Debug.LogWarning($"Transition '{sourceTransitionId}' in location '{sourceLocationId}' cannot be used as an exit because it has no trigger zone.", selector);
                return false;
            }

            var destinationLocation = selector.FindLocation(transition.TargetLocationId);
            var destinationTransition = destinationLocation?.FindTransition(transition.TargetTransitionId);
            if (!transition.HasDestination || destinationTransition == null || !destinationTransition.CanEnter)
            {
                Debug.LogWarning($"Transition '{sourceTransitionId}' in location '{sourceLocationId}' has no valid destination entrance.", selector);
                return false;
            }

            if (TransitionRequested == null)
            {
                Debug.LogError("Location transition UI is not registered in the active player scope.", selector);
                return false;
            }

            TransitionRequested?.Invoke(new VillageLocationTransitionRequest(
                sourceLocation.Id,
                transition.Id,
                transition.TargetLocationId,
                transition.TargetTransitionId));
            return true;
        }

        public void ConfirmTransition(VillageLocationTransitionRequest request)
        {
            transitionContext.SetPendingTransition(request.TargetLocationId, request.TargetTransitionId);
        }

        public bool TryGetTransition(string locationId, string transitionId, out VillageLocationTransition transition)
        {
            if (selector == null)
            {
                transition = null;
                return false;
            }

            transition = selector.FindLocation(locationId)?.FindTransition(transitionId);
            return transition != null;
        }

        private void ConfigureTransitionTriggers(VillageLocationDefinition location)
        {
            foreach (var transition in location.Transitions)
            {
                if (transition.TriggerZone == null)
                {
                    continue;
                }

                var trigger = transition.TriggerZone.GetComponent<VillageLocationTransitionTrigger>();
                if (trigger == null)
                {
                    trigger = transition.TriggerZone.AddComponent<VillageLocationTransitionTrigger>();
                }

                trigger.Configure(this, location.Id, transition.Id);
            }
        }

        private void DeactivateObjectsExclusiveToInactiveLocations(HashSet<GameObject> activeLocationObjects)
        {
            foreach (var location in selector.Locations)
            {
                if (location == null || location == currentLocation)
                {
                    continue;
                }

                foreach (var locationObject in location.RequiredObjects)
                {
                    // Active ownership wins both for a direct reference and for a hierarchy
                    // overlap: disabling an inactive parent would otherwise disable a required
                    // child of the active location.
                    if (locationObject == null || IsCoveredByActiveLocation(locationObject, activeLocationObjects))
                    {
                        continue;
                    }

                    locationObject.SetActive(false);
                }
            }
        }

        private static bool IsCoveredByActiveLocation(
            GameObject locationObject,
            IEnumerable<GameObject> activeLocationObjects)
        {
            foreach (var activeObject in activeLocationObjects)
            {
                if (activeObject == null)
                {
                    continue;
                }

                if (activeObject == locationObject
                    || activeObject.transform.IsChildOf(locationObject.transform)
                    || locationObject.transform.IsChildOf(activeObject.transform))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class VillageLocationDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private List<GameObject> requiredObjects = new();
        [SerializeField] private List<VillageLocationTransition> transitions = new();

        public string Id => id;
        public IReadOnlyList<GameObject> RequiredObjects => requiredObjects;
        public IReadOnlyList<VillageLocationTransition> Transitions => transitions;
        public VillageLocationTransition FindTransition(string transitionId) =>
            transitions.FirstOrDefault(transition => transition != null && transition.Id == transitionId);
    }

    [Serializable]
    public sealed class VillageLocationTransition
    {
        [SerializeField] private string id;
        [SerializeField] private Transform playerSpawnTransform;
        [SerializeField] private GameObject triggerZone;
        [Header("Destination")]
        [SerializeField] private string targetLocationId;
        [SerializeField] private string targetTransitionId;

        public string Id => id;
        public Transform PlayerSpawnTransform => playerSpawnTransform;
        public GameObject TriggerZone => triggerZone;
        public string TargetLocationId => targetLocationId;
        public string TargetTransitionId => targetTransitionId;
        public bool CanEnter => playerSpawnTransform != null;
        public bool CanExit => triggerZone != null;
        public bool HasDestination => !string.IsNullOrWhiteSpace(targetLocationId)
                                      && !string.IsNullOrWhiteSpace(targetTransitionId);
    }

    public readonly struct VillageLocationTransitionRequest
    {
        public VillageLocationTransitionRequest(string sourceLocationId, string sourceTransitionId, string targetLocationId, string targetTransitionId)
        {
            SourceLocationId = sourceLocationId;
            SourceTransitionId = sourceTransitionId;
            TargetLocationId = targetLocationId;
            TargetTransitionId = targetTransitionId;
        }

        public string SourceLocationId { get; }
        public string SourceTransitionId { get; }
        public string TargetLocationId { get; }
        public string TargetTransitionId { get; }
    }

    [RequireComponent(typeof(Collider))]
    public sealed class VillageLocationTransitionTrigger : MonoBehaviour
    {
        private LocationTransitionService transitionService;
        private string locationId;
        private string transitionId;
        private bool requested;

        public void Configure(LocationTransitionService service, string sourceLocationId, string sourceTransitionId)
        {
            transitionService = service;
            locationId = sourceLocationId;
            transitionId = sourceTransitionId;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (requested || transitionService == null || other.GetComponentInParent<PlayerLifetimeScope>() == null)
            {
                return;
            }

            requested = transitionService.TryRequestTransition(locationId, transitionId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerLifetimeScope>() != null)
            {
                requested = false;
            }
        }
    }
}
