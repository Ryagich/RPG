using Dialogue;
using Inventory.Slot;
using Stats;
using TMPro;
using UI.Map;
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
        [field: SerializeField] public RightPlayerInventory RightPlayerInventory { get; private set; }
        [field: SerializeField] public SlotsViewContainer CenterSection { get; private set; }
        [field: SerializeField] public RectTransform LeftSection { get; private set; }
        [field: SerializeField] public LeftAnotherInventory LeftAnotherInventory { get; private set; }
        [field: SerializeField] public InventoryView InventoryView { get; private set; }
        [field: SerializeField] public InfoAboutInventory InfoAboutInventory { get; private set; }
        [field: SerializeField] public InfoAboutPlayer InfoAboutPlayer { get; private set; }
        [field: SerializeField] public DialogueContainer DialogueContainer { get; private set; }
        [field: SerializeField] public PauseMenu PauseMenu { get; private set; }
        
        //Trade
        [field: SerializeField] public GameObject LeftAnotherInventoryInTrade { get; private set; }
        [field: SerializeField] public GameObject RightPlayerInventoryInTrade { get; private set; }
        [field: SerializeField] public ScrollRect SellInventory { get; private set; }
        [field: SerializeField] public InventoryView InventoryInTrading { get; private set; }
        [field: SerializeField] public SellInfo SellInfo { get; private set; }
        [field: SerializeField] public Button TradingExitButton { get; private set; }
        
        // Dialog
        [field: SerializeField] public PhraseContainer PhraseContainer { get; private set; }
        [field: SerializeField] public Button AnswerButton { get; private set; }
        [field: SerializeField] public NotificationInDialog NotificationInDialog { get; private set; }
        
        // Game
        [field: SerializeField] public StatsHolder StatsHolder { get; private set; }
        
        // Popups
        [field: SerializeField] public RectTransform PopupRect { get; private set; }
        [field: SerializeField] public PopupContent PopupContent { get; private set; }
        [field: SerializeField] public PopupTitle PopupItemName { get; private set; }
        [field: SerializeField] public TMP_Text PopupWeight { get; private set; }
        [field: SerializeField] public Button PopupButton { get; private set; }
        [field: SerializeField, Min(0f)] public float PopupHoverOpenDelaySeconds { get; private set; } = 0.5f;
        [field: SerializeField] public StatHolderForUsable StatHolderForUsable { get; private set; }
        [field: SerializeField] public StatHolder StatHolderForClothes { get; private set; }
        [field: SerializeField] public Image BloodScreen { get; private set; }

        //Map
        [field: SerializeField] public ScrollRect QuestionsScrollView { get; private set; }
        [field: SerializeField] public ScrollRect MapScroll { get; private set; }
        [field: SerializeField] public Title Title { get; private set; }
        [field: SerializeField] public QuestShortInfo QuestShortInfo { get; private set; }
        [field: SerializeField] public RectTransform QuestPopup { get; private set; }
        [field: SerializeField] public CharacterIcon CharacterIcon { get; private set; }
        [field: SerializeField] public Image MapIcon { get; private set; }
    }
}
