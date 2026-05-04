using UnityEngine;
using UnityEngine.Localization;

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
    }
}
