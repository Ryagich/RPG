using Inventory.Item;
using UnityEngine;

namespace Inventory
{
    [DisallowMultipleComponent]
    public class CharacterBodyPartVisual : MonoBehaviour
    {
        [SerializeField] private BodyPart bodyPart;
        [SerializeField] private string visualName;
        [SerializeField] private CharacterBodyPartVisualGender gender = CharacterBodyPartVisualGender.Male;

        public BodyPart BodyPart => bodyPart;
        public string Name => visualName;
        public CharacterBodyPartVisualGender Gender => gender;
        public bool IsAvailableFor(CharacterGender targetGender) =>
            gender == CharacterBodyPartVisualGender.Shared || (CharacterGender)gender == targetGender;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (bodyPart == BodyPart.Hair)
            {
                gender = CharacterBodyPartVisualGender.Shared;
            }

            if (string.IsNullOrWhiteSpace(visualName))
            {
                visualName = gameObject.name;
            }
        }
#endif
    }
}
