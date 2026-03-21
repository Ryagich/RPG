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
           
            builder.RegisterEntryPoint<InventoryPage>().AsSelf();
            builder.RegisterEntryPoint<PagesController>();
        }
    }
}