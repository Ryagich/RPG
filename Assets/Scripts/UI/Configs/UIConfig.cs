using Dialogue;
using Inventory.Slot;
using TMPro;
using UI.Inventory;
using UI.UIElements;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "UI Config", menuName = "configs/UI/UIConfig")]
    public class UIConfig : ScriptableObject
    {
        [field: SerializeField] public RectTransform ContentPref { get; private set; }
        [field: SerializeField] public TMP_Text InteractableText { get; private set; }
        [field: SerializeField] public InventoryTilePointerHandler Tile { get; private set; }
        [field: SerializeField] public RectTransform RightSection { get; private set; }
        [field: SerializeField] public SlotsViewContainer CenterSection { get; private set; }
        [field: SerializeField] public RectTransform LeftSection { get; private set; }
        [field: SerializeField] public InventoryView InventoryView { get; private set; }
        [field: SerializeField] public InfoAboutInventory InfoAboutInventory { get; private set; }
        [field: SerializeField] public InfoAboutPlayer InfoAboutPlayer { get; private set; }
        [field: SerializeField] public DialogueContainer DialogueContainer { get; private set; }
        
        //Trade
        [field: SerializeField] public ScrollRect SellInventory { get; private set; }
        [field: SerializeField] public ScrollRect InventoryInTrading { get; private set; }
        [field: SerializeField] public SellInfo SellInfo { get; private set; }
    }
}