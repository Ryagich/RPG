using Stats;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Interactable
{
    [RequireComponent(typeof(Interactable))]
    public class DamageInteractable : MonoBehaviour
    {
        [SerializeField] private float damagePerInteraction = 10f;

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

        private void OnValidate()
        {
            damagePerInteraction = Mathf.Max(0f, damagePerInteraction);
        }

        private void OnInteracted(LifetimeScope playerScope)
        {
            Debug.Log($"Damage Interacted");
            if (playerScope == null || !playerScope.Container.TryResolve<StatsController>(out var statsController))
            {
                Debug.Log($"Damage Cant");
                return;
            }
            Debug.Log($"Damage");

            statsController.Hp.AddValue(-damagePerInteraction);
        }
    }
}
