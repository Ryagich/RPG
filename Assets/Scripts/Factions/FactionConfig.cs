using UnityEngine;
using UnityEngine.Localization;
using NPC;

namespace Factions
{
    [CreateAssetMenu(fileName = "FactionConfig", menuName = "configs/Factions/Faction")]
    public sealed class FactionConfig : ScriptableObject
    {
        [field: SerializeField] public LocalizedString Name { get; private set; } = new("Tables", "Null String");
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: Header("Combat AI")]
        [field: Tooltip("Baseline combat preferences for faction NPCs. A specific NPC can override this in its lifetime scope.")]
        [field: SerializeField] public NpcCombatProfile CombatProfile { get; private set; }
    }
}
