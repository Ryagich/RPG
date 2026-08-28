using UnityEngine;
using UnityEngine.Localization;
using Inventory.Item;

namespace Localization
{
    [CreateAssetMenu(fileName = "LocalizationConfig", menuName = "configs/Localization/Localization Config")]
    public class LocalizationConfig : ScriptableObject
    {
        [field: SerializeField] public LocalizedString InventoryCurrentWeight { get; private set; }
        [field: SerializeField] public LocalizedString kg { get; private set; }
        [field: SerializeField] public LocalizedString max { get; private set; }
        [field: SerializeField] public LocalizedString MoneyReceived { get; private set; }
        [field: SerializeField] public LocalizedString MoneyLost { get; private set; }
        [field: SerializeField] public LocalizedString ItemReceived { get; private set; }
        [field: SerializeField] public LocalizedString ItemLost { get; private set; }
        [field: SerializeField] public LocalizedString QuestNew { get; private set; }
        [field: SerializeField] public LocalizedString QuestUpdate { get; private set; }
        [field: SerializeField] public LocalizedString QuestCompleted { get; private set; }
        [field: SerializeField] public LocalizedString QuestFailed { get; private set; }
        [field: SerializeField] public LocalizedString QuestCanceled { get; private set; }
        [field: SerializeField] public LocalizedString WorldMapTitle { get; private set; }
        [field: SerializeField] public LocalizedString QuestsTitle { get; private set; }
        [field: SerializeField] public LocalizedString ActiveQuestsOnly { get; private set; }
        [field: SerializeField] public LocalizedString AllQuests { get; private set; }
        [field: SerializeField] public LocalizedString DialogueFarewell { get; private set; }

        [field: Header("Item Types")]
        [field: SerializeField] public LocalizedString ItemTypeNone { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeUsable { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeBackpack { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeWeapon { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeBody { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeHelm { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeFace { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeHands { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeLegs { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeHips { get; private set; }
        [field: SerializeField] public LocalizedString ItemTypeArms { get; private set; }

        [field: Header("Item Popup Actions")]
        [field: SerializeField] public LocalizedString PopupActionUse { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionDrop { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionDropHalf { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionMove { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionMoveHalf { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionMoveToInventory { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionPutUpForSale { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionPutHalfUpForSale { get; private set; }
        [field: SerializeField] public LocalizedString PopupActionRemoveFromSale { get; private set; }

        public string GetItemTypeDisplayName(ItemType itemType)
        {
            LocalizedString localizedString = itemType switch
            {
                ItemType.None => ItemTypeNone,
                ItemType.Usable => ItemTypeUsable,
                ItemType.Backpack => ItemTypeBackpack,
                ItemType.Weapon => ItemTypeWeapon,
                ItemType.Body => ItemTypeBody,
                ItemType.Helm => ItemTypeHelm,
                ItemType.Face => ItemTypeFace,
                ItemType.Hands => ItemTypeHands,
                ItemType.Legs => ItemTypeLegs,
                ItemType.Hips => ItemTypeHips,
                ItemType.Arms => ItemTypeArms,
                _ => null
            };

            string localizedName = localizedString.GetLocalizedStringCached();
            return string.IsNullOrEmpty(localizedName) ? itemType.ToString() : localizedName;
        }
    }
}
