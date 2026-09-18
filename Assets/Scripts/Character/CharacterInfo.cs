using UnityEngine;
using UnityEngine.Localization;

using NaughtyAttributes;

namespace Character
{
    [CreateAssetMenu(fileName = "Character Info", menuName = "configs/Character/Character Info")]
    public class CharacterInfo : ScriptableObject
    {
        [Header("Persistence")]
        [SerializeField] private bool isUniqueCharacter;
        [SerializeField, ShowIf(nameof(IsUniqueCharacter))] private string characterId;

        [field: SerializeField] public LocalizedString Name { get; private set; }
        [field: SerializeField] public Sprite Photo { get; private set; }

        public bool IsUniqueCharacter => isUniqueCharacter;
        public string CharacterId => characterId;

        private void OnValidate()
        {
            if (isUniqueCharacter && string.IsNullOrWhiteSpace(characterId))
            {
                characterId = $"character_{System.Guid.NewGuid():N}";
            }
        }
    }
}
