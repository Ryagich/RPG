using UnityEngine;

namespace UI.Map
{
    public class CharacterIcon : MonoBehaviour
    {
        [field: SerializeField] public RectTransform Direction { get; private set; }
        [field: SerializeField] public float DirectionAngleOffset { get; private set; } = -90f;
    }
}
