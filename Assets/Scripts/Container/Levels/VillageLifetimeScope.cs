using Landings.Fields;
using Forest.Bandits;
using Training;
using Mill;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Container.Levels
{
    public sealed class VillageLifetimeScope : LifetimeScope
    {
        [SerializeField] private FarmField[] farmFields;
        [SerializeField] private TrainingSessionController trainingSessionController;
        [SerializeField] private ForestTollEncounterController forestTollEncounterController;
        [SerializeField] private MillScenarioController millScenarioController;

        protected override void Configure(IContainerBuilder builder)
        {
            if (trainingSessionController != null)
            {
                builder.RegisterComponent(trainingSessionController).AsSelf();
            }

            if (forestTollEncounterController != null)
            {
                builder.RegisterComponent(forestTollEncounterController).AsSelf();
            }

            if (millScenarioController != null)
            {
                builder.RegisterComponent(millScenarioController).AsSelf();
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
