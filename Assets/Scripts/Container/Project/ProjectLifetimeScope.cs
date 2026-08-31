using CameraScripts;
using Colors;
using Factions;
using Gravity;
using Input;
using Interactable;
using Inventory;
using Inventory.Storage;
using Localization;
using Locations;
using Loading;
using Movement;
using NPC;
using Player;
using TargetLock;
using UI;
using UI.Configs;
using UI.Map;
using UnityEngine;
using VContainer;
using VContainer.Unity;

using Combat;
using Dialogue;
using GameAudio;
using Training;

namespace Container.Project
{
    public class ProjectLifetimeScope : LifetimeScope
    {
        public static ProjectLifetimeScope Instance { get; private set; }

        [field: SerializeField] public InputConfig InputConfig { get; private set; }
        [field: SerializeField] public CameraConfig CameraConfig { get; private set; }
        [field: SerializeField] public PlayerMovementConfig PlayerMovementConfig { get; private set; }
        [field: SerializeField] public GravityConfig GravityConfig { get; private set; }
        [field: SerializeField] public UIConfig UIConfig { get; private set; }
        [field: SerializeField] public StatsConfig StatsConfig { get; private set; }
        [field: SerializeField] public LocalizationConfig LocalizationConfig { get; private set; }
        [field: SerializeField] public InteractableConfig InteractableConfig { get; private set; }
        [field: SerializeField] public InventoryConfig InventoryConfig { get; private set; }
        [field: SerializeField] public ColorsConfig ColorsConfig { get; private set; }
        [field: SerializeField] public StatIconsConfig StatIconsConfig { get; private set; }
        [field: SerializeField] public MapConfig MapConfig { get; private set; }
        [field: SerializeField] public TargetLockConfig TargetLockConfig { get; private set; }
        [field: SerializeField] public NpcVisionConfig NpcVisionConfig { get; private set; }
        [field: SerializeField] public NpcItemPickupConfig NpcItemPickupConfig { get; private set; }
        [field: SerializeField] public NpcCombatConfig NpcCombatConfig { get; private set; }
        [field: SerializeField] public LoadSceneConfig LoadSceneConfig { get; private set; }
        [field: SerializeField] public DeathConfig DeathConfig { get; private set; }
        [field: SerializeField] public HitReactionConfig HitReactionConfig { get; private set; }
        [field: SerializeField] public FactionRelationsConfig FactionRelationsConfig { get; private set; }
        [field: SerializeField] public AudioConfig AudioConfig { get; private set; }
        [field: SerializeField] public FootstepConfig FootstepConfig { get; private set; }
        [field: SerializeField] public AnimationEventSoundConfig AnimationEventSoundConfig { get; private set; }
        [field: SerializeField] public LessonConfig LessonConfig { get; private set; }

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnDestroy();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // === Общие зависимости ===
            builder.RegisterInstance(InputConfig).AsSelf();
            builder.RegisterInstance(CameraConfig).AsSelf();
            builder.RegisterInstance(PlayerMovementConfig).AsSelf();
            builder.RegisterInstance(GravityConfig).AsSelf();
            builder.RegisterInstance(UIConfig).AsSelf();
            builder.RegisterInstance(StatsConfig).AsSelf();
            builder.RegisterInstance(LocalizationConfig).AsSelf();
            builder.RegisterInstance(InteractableConfig).AsSelf();
            builder.RegisterInstance(InventoryConfig).AsSelf();
            builder.RegisterInstance(ColorsConfig).AsSelf();
            builder.RegisterInstance(StatIconsConfig).AsSelf();
            builder.RegisterInstance(MapConfig).AsSelf();
            builder.RegisterInstance(TargetLockConfig).AsSelf();
            builder.RegisterInstance(NpcVisionConfig).AsSelf();
            builder.RegisterInstance(NpcItemPickupConfig).AsSelf();
            builder.RegisterInstance(NpcCombatConfig).AsSelf();
            builder.RegisterInstance(LoadSceneConfig).AsSelf();
            builder.RegisterInstance(DeathConfig).AsSelf();
            builder.RegisterInstance(HitReactionConfig != null ? HitReactionConfig : HitReactionConfig.CreateDefault()).AsSelf();
            builder.RegisterInstance(FactionRelationsConfig).AsSelf();
            builder.RegisterInstance(AudioConfig).AsSelf();
            builder.RegisterInstance(FootstepConfig).AsSelf();
            builder.RegisterInstance(AnimationEventSoundConfig).AsSelf();
            builder.RegisterInstance(LessonConfig).AsSelf();
            builder.RegisterEntryPoint<AudioService>(Lifetime.Singleton).As<IAudioService>().AsSelf();
            builder.Register<SceneLoadingService>(Lifetime.Singleton).AsSelf();
            builder.Register<BootCompletion>(Lifetime.Singleton).AsSelf();
            builder.Register<LocationTransitionContext>(Lifetime.Singleton).AsSelf();
            builder.Register<DialogueRuntimeFlagRegistry>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<ItemStorage>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<InputBindingOverridesBootstrap>();

            builder.RegisterEntryPoint<Bootloader>().AsSelf();
        }
    }
}
