using Landings.Fields;
using Landings.Plants;
using Forest.Bandits;
using Training;
using Mill;
using Tavern;
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
        [SerializeField] private ElinaQuestLocationPresence[] elinaQuestLocationPresences;

        protected override void Configure(IContainerBuilder builder)
        {
            if (trainingSessionController != null)
            {
                builder.RegisterComponent(trainingSessionController).AsSelf();
            }

            if (forestTollEncounterController != null)
            {
                // The encounter is driven by the Unity lifecycle, not resolved by another
                // service. Inject it explicitly while this scope is built, before Start can
                // subscribe to its dialogue events.
                builder.RegisterBuildCallback(container => container.Inject(forestTollEncounterController));
            }

            if (millScenarioController != null)
            {
                builder.RegisterComponent(millScenarioController).AsSelf();
            }

            builder.RegisterBuildCallback(container =>
            {
                foreach (ElinaQuestLocationPresence presence in elinaQuestLocationPresences ?? System.Array.Empty<ElinaQuestLocationPresence>())
                {
                    if (presence != null)
                    {
                        // Both authored appearances need injection, but neither is a service
                        // that consumers resolve by type. Registering both as self conflicts
                        // in VContainer because a component registration is singleton.
                        container.Inject(presence);
                    }
                }
            });

            builder.RegisterBuildCallback(container =>
            {
                foreach (var farmField in farmFields ?? System.Array.Empty<FarmField>())
                {
                    if (farmField != null)
                    {
                        container.Inject(farmField);
                    }
                }

                // Fruit trees are scene visualizers, not DI services. Inject only currently
                // active trees: inactive locations may already be removed from this scene.
                foreach (var fruitTree in FindObjectsByType<AppleTreeFruitGrower>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None))
                {
                    container.Inject(fruitTree);
                }
            });
        }
    }
}
