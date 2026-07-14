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
        [field: SerializeField] public InputActionReference Pause { get; private set; }
        [field: SerializeField] public InputActionReference Map { get; private set; }
        [field: SerializeField] public InputActionReference TargetLock { get; private set; }
        [field: SerializeField] public InputActionReference TargetLockNext { get; private set; }
        [field: SerializeField] public InputActionReference TargetLockPrevious { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot1 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot2 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot3 { get; private set; }
        [field: SerializeField] public InputActionReference FastSlot4 { get; private set; }
        [field: SerializeField] public InputActionReference WeaponSlot1 { get; private set; }
        [field: SerializeField] public InputActionReference WeaponSlot2 { get; private set; }

        private void OnEnable()
        {
            var actionMap = Movement?.action?.actionMap;
            if (actionMap == null)
            {
                return;
            }

            Interactable ??= CreateReference(actionMap, "Interactable");
            Inventory ??= CreateReference(actionMap, "Inventory");
            LeftClick ??= CreateReference(actionMap, "Left Mouse");
            RightClick ??= CreateReference(actionMap, "Right Mouse");
            Run ??= CreateReference(actionMap, "Run");
            ShowStats ??= CreateReference(actionMap, "ShowStats");
            Pause ??= CreateReference(actionMap, "Pause");
            Map ??= CreateReference(actionMap, "Map");
            TargetLock ??= CreateReference(actionMap, "TargetLock");
            TargetLockNext ??= CreateReference(actionMap, "TargetLockNext");
            TargetLockPrevious ??= CreateReference(actionMap, "TargetLockPrevious");
            FastSlot1 ??= CreateReference(actionMap, "FastSlot1");
            FastSlot2 ??= CreateReference(actionMap, "FastSlot2");
            FastSlot3 ??= CreateReference(actionMap, "FastSlot3");
            FastSlot4 ??= CreateReference(actionMap, "FastSlot4");
            WeaponSlot1 ??= CreateReference(actionMap, "WeaponSlot1");
            WeaponSlot2 ??= CreateReference(actionMap, "WeaponSlot2");
        }

        private static InputActionReference CreateReference(InputActionMap actionMap, string actionName)
        {
            var action = actionMap.FindAction(actionName, false);
            return action == null ? null : InputActionReference.Create(action);
        }
    }
}
