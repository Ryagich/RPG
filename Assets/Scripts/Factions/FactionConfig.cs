using UnityEngine;
using UnityEngine.Localization;

namespace Factions
{
    [CreateAssetMenu(fileName = "FactionConfig", menuName = "configs/Factions/Faction")]
    public sealed class FactionConfig : ScriptableObject
    {
        [field: SerializeField] public LocalizedString Name { get; private set; } = new("Tables", "Null String");
        [field: SerializeField] public Sprite Icon { get; private set; }
    }
}
