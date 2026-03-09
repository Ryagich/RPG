using Input;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Project
{
    public class ProjectLifetimeScope : LifetimeScope
    {
        [field: SerializeField] public InputConfig InputConfig { get; private set; }

        protected override void Configure(IContainerBuilder builder)
        {
            // === Общие зависимости ===
            builder.RegisterInstance(InputConfig).AsSelf();
     
            // builder.RegisterEntryPoint<Bootloader>().AsSelf();
        }
    }
}