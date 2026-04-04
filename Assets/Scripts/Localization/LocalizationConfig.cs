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
    }
}