using Dialogue;
using Combat;
using Factions;
using GameModes;
using Input;
using MessagePipe;
using Messages;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Inventory.Looting;
using Inventory;
using GameAudio;
using Locations;
using NPC;
using Quests;
using Quests.MapTargets;
using TargetLock;
using UI.Inventory;
using Training;
using Mill;
using Saves;

namespace Container.Game
{
    public class GameLifetimeScope : LifetimeScope
    {
        private const string GlobalSoundsRootName = "Global Sounds";

        [field: SerializeField] public PlayerLifetimeScope PlayerPrefab { get; private set; } = null!;

        private PlayerLifetimeScope playerScope;
        private Transform globalSoundsRoot;
        private VillageLocationSelector locationSelector;
        private Camera gameCamera;
        private GameSceneSessionConfiguration sceneSessionConfiguration = new(isGameplayScene: true);
        private bool worldInitialized;

        public void SetLocationSelector(VillageLocationSelector selector)
        {
            locationSelector = selector;
        }

        public void SetGameCamera(Camera camera)
        {
            gameCamera = camera;
        }

        public void SetSceneSessionConfiguration(GameSceneSessionConfiguration configuration)
        {
            if (Container != null)
            {
                Debug.LogError("Game scene session configuration must be assigned before GameLifetimeScope is built.", this);
                return;
            }

            sceneSessionConfiguration = configuration ?? new GameSceneSessionConfiguration(isGameplayScene: true);
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(sceneSessionConfiguration);

            // Faction standing is mutable session state. Development scenes must begin from
            // authored relations instead of inheriting the project-level relations restored
            // from a player's save; gameplay scenes deliberately keep that shared instance.
            if (!sceneSessionConfiguration.IsGameplayScene)
            {
                builder.Register<RuntimeFactionRelations>(Lifetime.Singleton)
                    .As<IFactionRelations>()
                    .AsSelf();
            }

            if (gameCamera == null)
            {
                Debug.LogError("Game camera is not assigned to GameLifetimeScope.", this);
            }
            else
            {
                builder.RegisterInstance(gameCamera).AsSelf();
            }

            // === MessagePipe ===
            var options = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<PlayerMoveMessage>(options);
            builder.RegisterMessageBroker<InteractableInputMessage>(options);
            builder.RegisterMessageBroker<InventoryInputMessage>(options);
            builder.RegisterMessageBroker<MapInputMessage>(options);
            builder.RegisterMessageBroker<QuestLogInputMessage>(options);
            builder.RegisterMessageBroker<PauseInputMessage>(options);
            builder.RegisterMessageBroker<TargetLockInputMessage>(options);
            builder.RegisterMessageBroker<MouseDown>(options);
            builder.RegisterMessageBroker<MouseUp>(options);
            builder.RegisterMessageBroker<DodgeInputMessage>(options);
            builder.RegisterMessageBroker<RollInputMessage>(options);
            builder.RegisterMessageBroker<LessonSkipInputMessage>(options);
            builder.RegisterMessageBroker<LessonEvasionInputMessage>(options);
            builder.RegisterMessageBroker<LessonAttackInputMessage>(options);
            builder.RegisterMessageBroker<PlayerEvasionCompletedMessage>(options);
            builder.RegisterMessageBroker<NpcAttackStartedMessage>(options);
            builder.RegisterMessageBroker<WeaponSheathedMessage>(options);
            builder.RegisterMessageBroker<ShowStatsInputMessage>(options);
            builder.RegisterMessageBroker<FastSlotInputMessage>(options);
            builder.RegisterMessageBroker<WeaponSlotInputMessage>(options);
            builder.RegisterMessageBroker<ChangeGameModeRequest>(options);
            builder.RegisterMessageBroker<GameModeChangedMessage>(options);
            builder.RegisterMessageBroker<DialogueExitRequestedMessage>(options);
            builder.RegisterMessageBroker<DialogueGameplayEventRaisedMessage>(options);
            builder.RegisterMessageBroker<PlayerDiedMessage>(options);
            builder.RegisterMessageBroker<CharacterDamagedMessage>(options);
            builder.RegisterMessageBroker<InteractableMessage>(options);
            builder.RegisterMessageBroker<InteractableEndMessage>(options);
            builder.RegisterMessageBroker<PlayerRelocatedMessage>(options);
            builder.RegisterMessageBroker<ItemHolderFoundMessage>(options);
            builder.RegisterMessageBroker<ItemHolderLostMessage>(options);
            builder.RegisterMessageBroker<PlaySoundMessage>(options);

            builder.Register<TargetLockTargetRegistry>(Lifetime.Singleton)
                   .As<ITargetLockTargetRegistry>()
                   .AsSelf();
            builder.Register<NpcCombatRegistry>(Lifetime.Singleton)
                   .As<INpcCombatRegistry>()
                   .AsSelf();
            builder.Register<NonLethalCombatSessionRegistry>(Lifetime.Singleton)
                   .As<INonLethalCombatSessionRegistry>()
                   .AsSelf();
            builder.Register<QuestMapTargetRegistry>(Lifetime.Singleton)
                   .As<IQuestMapTargetRegistry>()
                   .AsSelf();
            builder.Register<InventoryInteractionContext>(Lifetime.Singleton).AsSelf();
            builder.Register<LessonPresentationContext>(Lifetime.Singleton).AsSelf();
            builder.Register<QuestSelectionLock>(Lifetime.Singleton).AsSelf();
            builder.Register<QuestObjectiveOverrideContext>(Lifetime.Singleton).AsSelf();

            if (locationSelector != null)
            {
                builder.RegisterInstance(locationSelector);
            }

            builder.Register(
                resolver => new LocationTransitionService(
                    locationSelector,
                    resolver.Resolve<LocationTransitionContext>()),
                Lifetime.Singleton).AsSelf();
             
            // === InputHandler ===
            builder.Register<InputHandler>(Lifetime.Singleton).AsSelf().As<IStartable>();

            builder.RegisterEntryPoint<GameWorldBootstrapper>().AsSelf();
            if (sceneSessionConfiguration.IsGameplayScene)
            {
                builder.RegisterEntryPoint<MillQuestProgressionCoordinator>().AsSelf();
                builder.RegisterEntryPoint<MillPropertyClaimQuestCoordinator>().AsSelf();
                builder.RegisterEntryPoint<PlantWorldGameplaySession>().AsSelf();
            }

            builder.Register<LootingContext>(Lifetime.Singleton).AsSelf();
            builder.Register<DialogueContext>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<DialogueExitController>().AsSelf();
            builder.RegisterEntryPoint<DialogueSaveCoordinator>().AsSelf();
            builder.Register<LocationTransitionSaveCoordinator>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<DroppedItemPersistenceService>()
                   .AsSelf()
                   .As<IWorldItemDropObserver>()
                   .As<ITickable>();
            builder.Register<Player.PlayerDeathState>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<GameModesController>().AsSelf();
            builder.RegisterEntryPoint<SoundMessagePlayer>().AsSelf();
            builder.RegisterEntryPoint<AmbientController>().AsSelf();
        }

        private Transform CreateGlobalSoundsRoot()
        {
            var soundsRoot = new GameObject(GlobalSoundsRootName).transform;
            soundsRoot.SetParent(transform, false);
            return soundsRoot;
        }

        internal void InitializeWorld(
            LocationTransitionService locationTransitions,
            IAudioService audioService,
            Pose? savedPlayerPose)
        {
            if (worldInitialized)
            {
                return;
            }

            worldInitialized = true;
            globalSoundsRoot = CreateGlobalSoundsRoot();
            audioService.SetWorldSoundParent(globalSoundsRoot);
            playerScope = CreateChildFromPrefab(PlayerPrefab, _ => { });
            if (sceneSessionConfiguration.IsGameplayScene)
            {
                locationTransitions.Initialize();
                if (locationTransitions.TryGetPlayerSpawn(out var spawnPose))
                {
                    PlacePlayerAtSpawn(playerScope, spawnPose);
                }

                if (savedPlayerPose.HasValue)
                {
                    PlacePlayerAtSpawn(playerScope, savedPlayerPose.Value);
                }
            }

            audioService.SetListenerTransform(playerScope.transform);
        }

        private static void PlacePlayerAtSpawn(PlayerLifetimeScope player, Pose spawnPose)
        {
            var controller = player.Container.Resolve<CharacterController>();
            controller.enabled = false;

            player.transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
            Physics.SyncTransforms();

            controller.enabled = true;
        }
    }
}
