using Character;
using Interactable;
using Inventory.Inventories;

namespace Dialogue
{
    public class DialogueContext
    {
        public Interactable.Interactable CurrentTarget { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public IInventory CurrentTargetInventory { get; private set; }

        public void SetTarget(Interactable.Interactable target, CharacterInfo characterInfo = null, IInventory inventory = null)
        {
            CurrentTarget = target;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentTargetInventory = inventory;
        }

        public void Clear()
        {
            CurrentTarget = null;
            CurrentTargetCharacterInfo = null;
            CurrentTargetInventory = null;
        }
    }
}