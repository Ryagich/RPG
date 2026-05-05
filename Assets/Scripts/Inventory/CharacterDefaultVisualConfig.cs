using System.Collections.Generic;
using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(fileName = "CharacterDefaultVisualConfig", menuName = "configs/Character/DefaultVisualConfig")]
    public class CharacterDefaultVisualConfig : ScriptableObject
    {
        [field: SerializeField] public List<DefaultBodyPartVisual> DefaultVisuals { get; private set; } = new();
    }
}
