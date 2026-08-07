using Character;
using Inventory.Inventories;
using Factions;
using Money;

namespace Inventory.Looting
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class LootingContext
    {
        public IInventory CurrentTargetInventory { get; private set; }
        public CharacterInfo CurrentTargetCharacterInfo { get; private set; }
        public FactionConfig CurrentTargetFaction { get; private set; }
        public MoneyStorage CurrentTargetMoneyStorage { get; private set; }

        public void SetTarget(
            IInventory inventory,
            CharacterInfo characterInfo = null,
            MoneyStorage moneyStorage = null,
            FactionConfig faction = null)
        {
            CurrentTargetInventory = inventory;
            CurrentTargetCharacterInfo = characterInfo;
            CurrentTargetMoneyStorage = moneyStorage;
            CurrentTargetFaction = faction;
        }

        public void Clear()
        {
            CurrentTargetInventory = null;
            CurrentTargetCharacterInfo = null;
            CurrentTargetMoneyStorage = null;
            CurrentTargetFaction = null;
        }
    }
}
