using GameModes;
using Loading;
using Locations;
using MessagePipe;
using Messages;
using UI.Configs;
using UI.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace UI.Pages
{
    public sealed class SwitchLocationPage : BasePage, System.IDisposable
    {
        private readonly UIConfig uiConfig;
        private readonly RectTransform canvasRect;
        private readonly IObjectResolver resolver;
        private readonly IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher;
        private readonly IPublisher<PlayerRelocatedMessage> playerRelocatedPublisher;
        private readonly SceneLoadingService sceneLoadingService;
        private readonly LocationTransitionService locationTransitions;
        private readonly Transform playerTransform;
        private readonly CharacterController playerController;

        private SwitchMenuHolder switchMenu;
        private VillageLocationTransitionRequest pendingRequest;
        private bool hasPendingRequest;
        private bool isResolving;

        public override PageType Type { get; } = PageType.SwitchLocation;

        public SwitchLocationPage(
            UIConfig uiConfig,
            Canvas canvas,
            IObjectResolver resolver,
            IPublisher<ChangeGameModeRequest> changeGameModeRequestPublisher,
            IPublisher<PlayerRelocatedMessage> playerRelocatedPublisher,
            SceneLoadingService sceneLoadingService,
            LocationTransitionService locationTransitions,
            Transform playerTransform,
            CharacterController playerController)
        {
            this.uiConfig = uiConfig;
            this.resolver = resolver;
            this.changeGameModeRequestPublisher = changeGameModeRequestPublisher;
            this.playerRelocatedPublisher = playerRelocatedPublisher;
            this.sceneLoadingService = sceneLoadingService;
            this.locationTransitions = locationTransitions;
            this.playerTransform = playerTransform;
            this.playerController = playerController;
            canvasRect = canvas.GetComponent<RectTransform>();

            locationTransitions.TransitionRequested += Open;
        }

        public override void Draw()
        {
            if (!hasPendingRequest)
            {
                ReturnToGame();
                return;
            }

            if (uiConfig.SwitchMenu == null)
            {
                Debug.LogError("Switch Menu is not assigned in UIConfig.");
                ReturnToGame();
                return;
            }

            switchMenu = resolver.Instantiate(uiConfig.SwitchMenu, canvasRect);
            switchMenu.name = $"{uiConfig.SwitchMenu.name} | {Type}";

            if (switchMenu.YesButton == null || switchMenu.NoButton == null)
            {
                Debug.LogError("Switch Menu Holder requires both Yes Button and No Button references.", switchMenu);
                ReturnToGame();
                return;
            }

            switchMenu.YesButton.onClick.AddListener(ConfirmTransition);
            switchMenu.NoButton.onClick.AddListener(CancelTransition);
        }

        public override void Hide()
        {
            if (switchMenu != null)
            {
                if (switchMenu.YesButton != null)
                {
                    switchMenu.YesButton.onClick.RemoveListener(ConfirmTransition);
                }

                if (switchMenu.NoButton != null)
                {
                    switchMenu.NoButton.onClick.RemoveListener(CancelTransition);
                }

                Object.Destroy(switchMenu.gameObject);
                switchMenu = null;
            }

            if (!isResolving)
            {
                ClearPendingRequest();
            }
        }

        public void Dispose()
        {
            locationTransitions.TransitionRequested -= Open;
        }

        private void Open(VillageLocationTransitionRequest request)
        {
            if (isResolving)
            {
                return;
            }

            pendingRequest = request;
            hasPendingRequest = true;
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.SwitchLocation));
        }

        private void ConfirmTransition()
        {
            if (!hasPendingRequest || isResolving)
            {
                return;
            }

            isResolving = true;
            locationTransitions.ConfirmTransition(pendingRequest);
            pendingRequest = default;
            hasPendingRequest = false;
            sceneLoadingService.Load(SceneManager.GetActiveScene().name);
        }

        private void CancelTransition()
        {
            if (!hasPendingRequest || isResolving)
            {
                return;
            }

            isResolving = true;
            if (!locationTransitions.TryGetTransition(pendingRequest.SourceLocationId, pendingRequest.SourceTransitionId, out var transition))
            {
                Debug.LogError($"Source transition '{pendingRequest.SourceTransitionId}' was not found while cancelling a location transition.");
                ReturnToGame();
                return;
            }

            if (transition.PlayerSpawnTransform != null)
            {
                PlacePlayer(playerTransform, playerController, transition.PlayerSpawnTransform.position, transition.PlayerSpawnTransform.rotation);
                playerRelocatedPublisher.Publish(new PlayerRelocatedMessage());
            }
            else
            {
                TryMovePlayerAwayFromTransition(playerTransform, playerController, transition.TriggerZone);
                playerRelocatedPublisher.Publish(new PlayerRelocatedMessage());
            }

            ReturnToGame();
        }

        private static void TryMovePlayerAwayFromTransition(
            Transform player,
            CharacterController playerController,
            GameObject triggerZone)
        {
            if (triggerZone == null)
            {
                Debug.LogError("Cannot move player away: the source transition has no trigger zone.");
                return;
            }

            var zoneCenter = GetZoneCenter(triggerZone);
            var awayDirection = player.position - zoneCenter;
            awayDirection.y = 0f;
            if (awayDirection.sqrMagnitude < 0.0001f)
            {
                awayDirection = -triggerZone.transform.forward;
                awayDirection.y = 0f;
            }

            if (awayDirection.sqrMagnitude < 0.0001f)
            {
                Debug.LogError("Cannot determine a safe direction away from the transition zone.", triggerZone);
                return;
            }

            awayDirection.Normalize();
            var centeredAtPlayerHeight = new Vector3(zoneCenter.x, player.position.y, zoneCenter.z);
            var candidates = new[]
            {
                player.position + awayDirection,
                centeredAtPlayerHeight + awayDirection,
                player.position + awayDirection * 0.5f,
                centeredAtPlayerHeight + awayDirection * 0.5f
            };

            foreach (var candidate in candidates)
            {
                if (!IsPlacementClear(player, playerController, candidate, triggerZone))
                {
                    continue;
                }

                PlacePlayer(player, playerController, candidate, Quaternion.LookRotation(awayDirection, Vector3.up));
                return;
            }

            Debug.LogError("No safe position was found for moving the player away from the transition zone.", triggerZone);
        }

        private static Vector3 GetZoneCenter(GameObject triggerZone)
        {
            var zoneCollider = triggerZone.GetComponent<Collider>();
            return zoneCollider == null ? triggerZone.transform.position : zoneCollider.bounds.center;
        }

        private static bool IsPlacementClear(
            Transform player,
            CharacterController playerController,
            Vector3 position,
            GameObject triggerZone)
        {
            if (playerController == null)
            {
                return true;
            }

            var scale = playerController.transform.lossyScale;
            var radius = playerController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var height = Mathf.Max(playerController.height * Mathf.Abs(scale.y), radius * 2f);
            var centerOffset = playerController.transform.TransformPoint(playerController.center) - player.position;
            var capsuleCenter = position + centerOffset;
            var halfLine = Mathf.Max(0f, height * 0.5f - radius);
            var pointA = capsuleCenter + Vector3.up * halfLine;
            var pointB = capsuleCenter - Vector3.up * halfLine;
            var collisions = Physics.OverlapCapsule(
                pointA,
                pointB,
                radius,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var collision in collisions)
            {
                if (collision.transform.IsChildOf(player) || collision.transform.IsChildOf(triggerZone.transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void PlacePlayer(
            Transform player,
            CharacterController playerController,
            Vector3 position,
            Quaternion rotation)
        {
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            player.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();

            if (playerController != null)
            {
                playerController.enabled = true;
            }
        }

        private void ReturnToGame()
        {
            ClearPendingRequest();
            changeGameModeRequestPublisher.Publish(new ChangeGameModeRequest(GameMode.Game));
        }

        private void ClearPendingRequest()
        {
            pendingRequest = default;
            hasPendingRequest = false;
            isResolving = false;
        }
    }
}
