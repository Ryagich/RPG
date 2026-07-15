using UI.Pages;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace UI
{
    public class CanvasLifetimeScope : LifetimeScope
    {
        [field: SerializeField] public Canvas Canvas { get; private set; }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(Canvas).As<Canvas>();

            builder.Register<MainPage>(Lifetime.Singleton);
            builder.Register<PausePage>(Lifetime.Singleton);
            builder.Register<PauseSettingsPage>(Lifetime.Singleton);
            builder.RegisterEntryPoint<DeathPage>().AsSelf();
            builder.Register<DialoguePage>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MapPage>().AsSelf();
            
            builder.RegisterEntryPoint<TradePage>().AsSelf();
            builder.RegisterEntryPoint<InventoryPage>().AsSelf();
            builder.RegisterEntryPoint<LootingPage>().AsSelf();
            builder.RegisterEntryPoint<PagesController>();
        }
    }
}
