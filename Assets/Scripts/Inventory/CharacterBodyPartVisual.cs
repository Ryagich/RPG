using Inventory.Item;
using UnityEngine;

namespace Inventory
{
    [DisallowMultipleComponent]
    public class CharacterBodyPartVisual : MonoBehaviour
    {
        [SerializeField] private BodyPart bodyPart;
        [SerializeField] private string visualName;

        public BodyPart BodyPart => bodyPart;
        public string Name => visualName;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(visualName))
            {
                visualName = gameObject.name;
            }
        }
#endif
    }
}
