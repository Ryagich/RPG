using UnityEngine;

namespace Interactable
{
    [CreateAssetMenu(fileName = "InteractableConfig", menuName = "configs/Interactable/InteractableConfig")]
    public class InteractableConfig : ScriptableObject
    {
        [field: SerializeField] public LayerMask InteractiveLayers { get; private set; }
        [field: SerializeField] public float TimeBetweenInteractions { get; private set; } = 0.1f;
    }
}