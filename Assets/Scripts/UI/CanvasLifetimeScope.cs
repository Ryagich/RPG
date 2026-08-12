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
            builder.RegisterEntryPoint<QuestNotificationService>(Lifetime.Singleton).AsSelf();

            builder.Register<MainPage>(Lifetime.Singleton);
            builder.Register<PausePage>(Lifetime.Singleton);
            builder.Register<PauseSettingsPage>(Lifetime.Singleton);
            builder.Register<SwitchLocationPage>(Lifetime.Singleton).AsSelf().As<System.IDisposable>();
            builder.RegisterEntryPoint<DeathPage>().AsSelf();
            builder.Register<DialoguePage>(Lifetime.Singleton);
            builder.RegisterEntryPoint<MapPage>().AsSelf();
            builder.RegisterEntryPoint<QuestPage>().AsSelf();
            builder.RegisterEntryPoint<LessonPage>().AsSelf();
            
            builder.RegisterEntryPoint<TradePage>().AsSelf();
            builder.RegisterEntryPoint<InventoryPage>().AsSelf();
            builder.RegisterEntryPoint<LootingPage>().AsSelf();
            builder.RegisterEntryPoint<PagesController>();
        }
    }
}
