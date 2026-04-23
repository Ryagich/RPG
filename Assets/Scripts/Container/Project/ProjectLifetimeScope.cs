using CameraScripts;
using Colors;
using Gravity;
using Input;
using Interactable;
using Inventory;
using Localization;
using Movement;
using UI;
using UI.Configs;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Project
{
    public class ProjectLifetimeScope : LifetimeScope
    {
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

            builder.RegisterEntryPoint<Bootloader>().AsSelf();
        }
    }
}
