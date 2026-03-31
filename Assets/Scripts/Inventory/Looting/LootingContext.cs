using Character;
using Inventory.Inventories;

namespace Inventory.Looting
{
    public class LootingContext
    {
        public IInventory CurrentTargetInventory { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }

        public void SetTarget(IInventory inventory, CharacterInfo characterInfo = null)
        {
            CurrentTargetInventory = inventory;
            CurrentTargetCharacterInfo = characterInfo;
        }

        public void Clear()
        {
            CurrentTargetInventory = null;
            CurrentTargetCharacterInfo = null;
        }
    }
}