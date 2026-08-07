using UnityEngine;
using UnityEngine.Localization;

namespace Character
{
    [CreateAssetMenu(fileName = "Character Info", menuName = "configs/Character/Character Info")]
    public class CharacterInfo : ScriptableObject
    {
        [field: SerializeField] public LocalizedString Name { get; private set; }
        [field: SerializeField] public Sprite Photo { get; private set; }
    }
}
