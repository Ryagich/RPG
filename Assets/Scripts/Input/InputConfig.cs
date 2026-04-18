using UnityEngine;
using UnityEngine.InputSystem;

namespace Input
{
    [CreateAssetMenu(fileName = "InputConfig", menuName = "configs/Input/InputConfig")]
    public class InputConfig : ScriptableObject
    {
        [field: SerializeField] public InputActionReference Movement { get; private set; } = null!;
        [field: SerializeField] public InputActionReference Interactable { get; private set; }
        [field: SerializeField] public InputActionReference Inventory { get; private set; }
        [field: SerializeField] public InputActionReference LeftClick { get; private set; }
        [field: SerializeField] public InputActionReference RightClick { get; private set; }
        [field: SerializeField] public InputActionReference Run { get; private set; }
        [field: SerializeField] public InputActionReference ShowStats { get; private set; }
    }
}
