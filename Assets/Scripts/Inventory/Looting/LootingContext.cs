using Character;
using Inventory.Inventories;
using Money;

namespace Inventory.Looting
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LootingContext
    {
        public IInventory CurrentTargetInventory { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public MoneyStorage CurrentTargetMoneyStorage { get; private set; }

        public void SetTarget(IInventory inventory, CharacterInfo characterInfo = null, MoneyStorage moneyStorage = null)
        {
            CurrentTargetInventory = inventory;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentTargetMoneyStorage = moneyStorage;
        }

        public void Clear()
        {
            CurrentTargetInventory = null;
            CurrentTargetCharacterInfo = null;
            CurrentTargetMoneyStorage = null;
        }
    }
}