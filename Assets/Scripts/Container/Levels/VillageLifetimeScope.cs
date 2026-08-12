using Landings.Fields;
using Training;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Levels
{
    public sealed class VillageLifetimeScope : LifetimeScope
    {
        [SerializeField] private FarmField[] farmFields;
        [SerializeField] private TrainingSessionController trainingSessionController;

        protected override void Configure(IContainerBuilder builder)
        {
            if (trainingSessionController != null)
            {
                builder.RegisterComponent(trainingSessionController).AsSelf();
            }

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
