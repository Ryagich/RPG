using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Inventory
{
    [CreateAssetMenu(fileName = "CharacterVisualConfig", menuName = "configs/Character/Visual Config")]
    [MovedFrom(true, sourceNamespace: "Inventory", sourceAssembly: "Assembly-CSharp", sourceClassName: "CharacterDefaultVisualConfig")]
    public sealed class CharacterVisualConfig : ScriptableObject
    {
        [field: SerializeField] public CharacterGender Gender { get; private set; } = CharacterGender.Male;
        [field: SerializeField] public List<DefaultBodyPartVisual> DefaultVisuals { get; private set; } = new();
    }
}
