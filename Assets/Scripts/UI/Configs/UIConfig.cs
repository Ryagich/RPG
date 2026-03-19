using TMPro;
using UI.Inventory;
using UI.UIElements;
using UnityEngine;

namespace UI.Configs
{
    [CreateAssetMenu(fileName = "UI Config", menuName = "configs/UI/UIConfig")]
    public class UIConfig : ScriptableObject
    {
        [field: SerializeField] public RectTransform ContentPref { get; private set; }
        [field: SerializeField] public TMP_Text InteractableText { get; private set; }
        [field: SerializeField] public RectTransform Tile { get; private set; }
        [field: SerializeField] public RectTransform RightSection { get; private set; }
        [field: SerializeField] public InventoryView InventoryView { get; private set; }
        [field: SerializeField] public InfoAboutInventory InfoAboutInventory { get; private set; }
        [field: SerializeField] public InfoAboutPlayer InfoAboutPlayer { get; private set; }
    }
}