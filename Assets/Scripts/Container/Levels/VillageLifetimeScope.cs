using Landings.Fields;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Levels
{
    public sealed class VillageLifetimeScope : LifetimeScope
    {
        [SerializeField] private FarmField[] farmFields;

        protected override void Configure(IContainerBuilder builder)
        {
            if (farmFields == null)
            {
                return;
            }

            builder.RegisterBuildCallback(container =>
            {
                foreach (var farmField in farmFields)
                {
                    if (farmField != null)
                    {
                        container.Inject(farmField);
                    }
                }
            });
        }
    }
}
