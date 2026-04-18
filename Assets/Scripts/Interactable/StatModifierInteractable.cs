using Stats;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Interactable
{
    [RequireComponent(typeof(Interactable))]
    public class StatModifierInteractable : MonoBehaviour
    {
        [SerializeField] private StatType statType = StatType.Hp;
        [SerializeField] private float valuePerInteraction = 10f;

        private Interactable interactable;

        private void Awake()
        {
            interactable = GetComponent<Interactable>();
            interactable.InteractionMode = InteractionMode.Automatic;
        }

        private void OnEnable()
        {
            interactable.Interacted += OnInteracted;
        }

        private void OnDisable()
        {
            if (interactable != null)
            {
                interactable.Interacted -= OnInteracted;
            }
        }

        private void OnInteracted(LifetimeScope playerScope)
        {
            if (playerScope == null || !playerScope.Container.TryResolve<StatsController>(out var statsController))
            {
                return;
            }

            statsController.AddValue(statType, valuePerInteraction);
        }
    }
}
